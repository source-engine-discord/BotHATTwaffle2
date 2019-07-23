using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BotHATTwaffle2.Models.LiteDB;
using BotHATTwaffle2.Services.Calendar;
using BotHATTwaffle2.Services.SRCDS;
using Discord;
using Discord.WebSocket;
using FluentScheduler;
using Google.Apis.YouTube.v3;

namespace BotHATTwaffle2.Services.Playtesting
{
    public class VoiceFeedbackSession : IDisposable
    {
        private readonly DataService _dataService;
        private readonly DiscordSocketClient _client;
        private readonly List<ulong> _userQueue = new List<ulong>();
        private readonly RconService _rconService;
        private readonly PlaytestEvent _playtestEvent;
        private IUserMessage _display;
        private bool _running = false;
        private bool _timerRunning = false;
        private string _status = "Idle 🛑";
        private bool _disposed = false;
        private int _duration;
        private TimeSpan _timeLeft = new TimeSpan();

        public VoiceFeedbackSession(DataService dataService, DiscordSocketClient client, PlaytestEvent playtestEvent,
            RconService rconService)
        {
            _dataService = dataService;
            _client = client;
            _playtestEvent = playtestEvent;
            _rconService = rconService;

            _client.UserVoiceStateUpdated += UserVoiceStateUpdated;

            _duration = _dataService.RSettings.General.FeedbackDuration;

            //Mute everyone on start
            foreach (var user in _dataService.LevelTestVoiceChannel.Users)
            {
                _ = ProcessMute(true, user);
            }

            _ = _client.SetStatusAsync(UserStatus.AFK);
        }

        /// <summary>
        /// Sets the duration for feedback
        /// </summary>
        /// <param name="duration"></param>
        public void SetDuration(int duration)
        {
            _duration = duration > 0 ? duration : _dataService.RSettings.General.FeedbackDuration;
            _ = UpdateCurrentQueueMessage();
        }

        /// <summary>
        /// Modifies a user's server voice mute state. Ignores mod staff and the map creators.
        /// </summary>
        /// <param name="mute">Set mute to True or False</param>
        /// <param name="user">User to modify</param>
        /// <returns>True if successful, false otherwise</returns>
        private async Task<bool> ProcessMute(bool mute, SocketGuildUser user)
        {
            //Skip mods
            if (user.Roles.Any(x => x.Id == _dataService.ModeratorRole.Id || x.Id == _dataService.AdminRole.Id))
                return true;

            //Skip creators
            if (_playtestEvent.Creators.Any(x => x.Id == user.Id))
                return true;

            try
            {
                await user.ModifyAsync(x => x.Mute = mute);
                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// The event that handles users when the join after a playtest session has started
        /// </summary>
        /// <param name="user">User who joined voice</param>
        /// <param name="lefState">Information on what channel they left</param>
        /// <param name="joinedState">What channel they joined</param>
        /// <returns></returns>
        private Task UserVoiceStateUpdated(SocketUser user, SocketVoiceState lefState, SocketVoiceState joinedState)
        {
            //If leftState and joinedState are the same, the event was fired from a modified event.
            if (lefState.VoiceChannel == joinedState.VoiceChannel)
                return Task.CompletedTask;

            var guildUser = _dataService.GetSocketGuildUser(user.Id);

            //User joined
            if (joinedState.VoiceChannel != null && joinedState.VoiceChannel.Id == _dataService.LevelTestVoiceChannel.Id)
            {
                //Don't re-mute a muted user
                if (guildUser.IsMuted)
                    return Task.CompletedTask;
                
                _ = ProcessMute(true, guildUser);
                return Task.CompletedTask;
            }

            //If a user leaves the channel, remove from the queue.
            if (lefState.VoiceChannel != null && lefState.VoiceChannel.Id == _dataService.LevelTestVoiceChannel.Id)
            {
                _ = RemoveUser(user.Id);
            }

            return Task.CompletedTask;
        }

        private void RemoveUserJobs(ulong userId)
        {
            JobManager.RemoveJob($"[FBQ_UserTimeOut_{userId}]");
            JobManager.RemoveJob($"[FBQ_Warning_{userId}]");
        }

        private void AddUserJobs(SocketGuildUser user)
        {
            JobManager.AddJob(async () => await UserTimeOut(user), s => s
                .WithName($"[FBQ_UserTimeOut_{user.Id}]").ToRunOnceIn(_duration * 60).Seconds());

            JobManager.AddJob(async () => await UserWarning(user), s => s
                .WithName($"[FBQ_Warning_{user.Id}]").ToRunOnceIn(_duration * 60 - 20).Seconds());
        }

        private async Task UserTimeOut(SocketGuildUser user)
        {
            await RemoveUser(user.Id);
        }

        /// <summary>
        /// The message code for warning a user that their turn is ending.
        /// </summary>
        /// <param name="user">User to warn</param>
        /// <returns></returns>
        private async Task UserWarning(SocketGuildUser user)
        {
            string message = $"{user.Mention} - 20 seconds left. Wrap it up!";
            string rconMessage = $"{user} - 20 seconds left. Wrap it up!";

            if (_userQueue.Count > 1)
            {
                message += $"\n{_dataService.GetSocketGuildUser(_userQueue[1]).Mention} you are up next!";
                rconMessage += $";say {_dataService.GetSocketGuildUser(_userQueue[1])} you are up next!";
            }

            _ = _rconService.RconCommand(_playtestEvent.ServerLocation, $"say {rconMessage}");
            var msg = await _dataService.TestingChannel.SendMessageAsync(message);
            await Task.Delay(10000);
            await msg.DeleteAsync();
        }

        /// <summary>
        /// Pauses the currently active feedback session
        /// </summary>
        public void PauseFeedback()
        {
            _status = "Idle 🛑";
            _running = false;

            if (_userQueue.Count > 0)
            {
                var user = _dataService.GetSocketGuildUser(_userQueue[0]);
                RemoveUserJobs(_userQueue[0]);
                _ = ProcessMute(true, user);
            }
            _ = _client.SetStatusAsync(UserStatus.AFK);
        }
        /// <summary>
        /// Starts user feedback
        /// </summary>
        /// <returns>True if started, false otherwise</returns>
        public async Task<bool> StartFeedback()
        {
            if (_running)
            {
                return false;
            }

            _status = "Running 🏁";
            _running = true;
            _ = _client.SetStatusAsync(UserStatus.DoNotDisturb);
            await UpdateCurrentQueueMessage();
            await StartNextUserFeedback();
            return true;
        }

        /// <summary>
        /// Processes the next user in the queue for feedback.
        /// </summary>
        /// <returns></returns>
        private async Task StartNextUserFeedback()
        {
            //No longer running, don't continue
            if (!_running)
                return;
           
            //If the list is empty, there are no users
            if (_userQueue.Count == 0)
            {
                PauseFeedback();
                return;
            }
            
            var user = _dataService.GetSocketGuildUser(_userQueue[0]);

            //Make sure user is in voice channel
            if (_dataService.LevelTestVoiceChannel.Users.All(x => x.Id != user.Id))
            {
                await RemoveUser(user.Id);
                return;
            }

            if (!await ProcessMute(false, user))
            {
                //Failure to remove mute for some reason. Just remove from stack
                await RemoveUser(user.Id);
                return;
            }
            
            //Alert users
            var msg = await _dataService.TestingChannel.SendMessageAsync($"{user.Mention} may begin their voice feedback.\nType `>done` in Discord when you're finished.");

            AddUserJobs(user);

            _timeLeft = TimeSpan.FromMinutes(_duration);

            if(!_timerRunning)
                _ = UpdateTimer();

            _ = _rconService.RconCommand(_playtestEvent.ServerLocation,$"say {user.Username} may now begin their feedback!;" +
                                                   $"say Please let the creator know your in-game name!;" +
                                                   $"say Type >done in Discord when you're finished.");
            await Task.Delay(10000);
            await msg.DeleteAsync();
        }

        private async Task UpdateTimer()
        {
            if (!_running)
            {
                _timerRunning = false;
                _ = _client.SetGameAsync($"Waiting...");
                return;
            }

            _timerRunning = true;
            _ = _client.SetGameAsync($"Time left: {_timeLeft:mm\\:ss}");
            await Task.Delay(5000);
            _timeLeft = _timeLeft.Subtract(TimeSpan.FromSeconds(5));
            _ = UpdateTimer();
        }

        /// <summary>
        /// Removes a user from the feedback queue
        /// </summary>
        /// <param name="userId">ID of user to remove</param>
        /// <returns>True if removed, false otherwise</returns>
        public async Task<bool> RemoveUser(ulong userId)
        {
            RemoveUserJobs(userId);

            if (!_userQueue.Contains(userId))
                return false;

            //check if the user we are removing is the current active user.
            //If so, we want to start the next user in the queue right away.
            bool processNext = _userQueue.IndexOf(userId) == 0;

            _userQueue.Remove(userId);

            var user = _dataService.GetSocketGuildUser(userId);

            await ProcessMute(true, user);

            if(processNext)
                await StartNextUserFeedback();

            await UpdateCurrentQueueMessage();
            return true;
        }

        /// <summary>
        /// Adds a user to the bottom of the feedback queue
        /// </summary>
        /// <param name="user">User to add</param>
        /// <returns></returns>
        public async Task<bool> AddUserToQueue(SocketUser user)
        {
            if (_userQueue.Contains(user.Id) || _dataService.LevelTestVoiceChannel.Users.All(x=>x.Id != user.Id))
                return false;

            _userQueue.Add(user.Id);
            await UpdateCurrentQueueMessage();
            return true;
        }

        /// <summary>
        /// Pushes a user to the top of the queue
        /// </summary>
        /// <param name="user">User to put at the top of the queue</param>
        /// <returns></returns>
        public async Task AddUserToTopQueue(SocketUser user)
        {
            //If user already exists, remove them and re-add them in the new stop.
            if (_userQueue.Contains(user.Id))
                _userQueue.Remove(user.Id);

            if(_userQueue.Count > 0)
                _userQueue.Insert(1,user.Id);
            else
                _userQueue.Add(user.Id);

            await UpdateCurrentQueueMessage();
        }

        /// <summary>
        /// Updates the display of users who are giving feedback
        /// </summary>
        /// <returns></returns>
        private async Task UpdateCurrentQueueMessage()
        {
            //If we disposed, don't update again.
            if (_disposed)
                return;

            if (_userQueue.Count == 0)
            {
                var emptyEmbed = new EmbedBuilder()
                    .WithAuthor("No users in queue...")
                    .WithColor(165,55,55)
                    .WithFooter("Type >q to enter the queue");
                if (_display == null)
                {
                    _display = await _dataService.TestingChannel.SendMessageAsync(embed: emptyEmbed.Build());
                }
                else
                {
                    await _display.DeleteAsync();
                    _display = await _dataService.TestingChannel.SendMessageAsync(embed: emptyEmbed.Build());
                }
                return;
            }
            
            var embed = new EmbedBuilder()
                .WithAuthor($"Feedback Queue - {_duration}min - {_status}")
                .WithColor(_running ? new Color(55, 165, 55) : new Color(222, 130, 50))
                .AddField("On Deck", _dataService.GetSocketUser(_userQueue[0]).Mention)
                .WithFooter("Type >q to enter the queue");

            string message = "";
            for (int i = 1; i < _userQueue.Count; i++)
            {
                var user = _dataService.GetSocketUser(_userQueue[i]);
                message += $"**{i}** - {user.Mention}\n";
            }
            message = message.Trim();
            if (_userQueue.Count > 1)
            {
                embed.AddField("Next Up",message);
            }

            if (_display == null)
            {
                _display = await _dataService.TestingChannel.SendMessageAsync(embed: embed.Build());
            }
            else
            {
                await _display.DeleteAsync();
                _display = await _dataService.TestingChannel.SendMessageAsync(embed: embed.Build());
            }
        }

        public async void Dispose()
        {
            _ = _client.SetStatusAsync(UserStatus.Online);
            _running = false;
            
            foreach (var user in _dataService.LevelTestVoiceChannel.Users)
            {
                //Skip mods
                if (user.Roles.Any(x => x.Id == _dataService.ModeratorRole.Id || x.Id == _dataService.AdminRole.Id))
                    continue;

                try
                {
                    await user.ModifyAsync(x => x.Mute = false);
                }
                catch
                {
                    //Do nothing, somehow we failed to unmute.
                }
            }
            _disposed = true;
            _client.UserVoiceStateUpdated -= UserVoiceStateUpdated;
        }
    }
}
