using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BotHATTwaffle2.Services.Calendar.PlaytestEvents;
using BotHATTwaffle2.Services.SRCDS;
using Discord;
using Discord.WebSocket;
using FluentScheduler;

namespace BotHATTwaffle2.Services.Playtesting
{
    public class VoiceFeedbackSession : IDisposable
    {
        private readonly DiscordSocketClient _client;
        private readonly DataService _dataService;
        private readonly PlaytestEvent _playtestEvent;
        private readonly RconService _rconService;
        private readonly List<ulong> _userQueue = new List<ulong>();
        private bool _abortTimer;
        private SocketGuildUser _activeUser;
        private IUserMessage _display;
        private bool _disposed;
        private TimeSpan _duration;
        private SocketGuildUser _onDeckUser;
        private bool _paused;
        private bool _running;
        private string _status = "Idle 🛑";
        private bool _tick = true;
        private TimeSpan _timeLeft;
        private bool _timerRunning;

        public VoiceFeedbackSession(DataService dataService, DiscordSocketClient client, PlaytestEvent playtestEvent,
            RconService rconService)
        {
            _dataService = dataService;
            _client = client;
            _playtestEvent = playtestEvent;
            _rconService = rconService;

            _client.UserVoiceStateUpdated += UserVoiceStateUpdated;

            _duration = TimeSpan.FromMinutes(_dataService.RSettings.General.FeedbackDuration);

            //Mute everyone on start
            foreach (var user in _dataService.LevelTestVoiceChannel.Users) _ = ProcessMute(true, user);

            _ = _rconService.RconCommand(_playtestEvent.ServerLocation,
                "say Feedback Queue Started!;say Feedback Queue Started!;say Feedback Queue Started!");

            _ = _client.SetStatusAsync(UserStatus.AFK);
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

                //Holy rate limit batman
                await Task.Delay(500);
            }

            _disposed = true;
            _client.UserVoiceStateUpdated -= UserVoiceStateUpdated;
        }

        /// <summary>
        ///     Sets the duration for feedback
        /// </summary>
        /// <param name="duration"></param>
        public void SetDuration(int duration)
        {
            var rawDuration = duration > 0 ? duration : _dataService.RSettings.General.FeedbackDuration;
            _duration = TimeSpan.FromMinutes(duration);
            _ = UpdateCurrentQueueMessage();
        }

        /// <summary>
        ///     Modifies a user's server voice mute state. Ignores mod staff and the map creators.
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
        ///     The event that handles users when the join after a playtest session has started
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
            if (joinedState.VoiceChannel != null &&
                joinedState.VoiceChannel.Id == _dataService.LevelTestVoiceChannel.Id)
            {
                //Don't re-mute a muted user
                if (guildUser.IsMuted)
                    return Task.CompletedTask;

                _ = ProcessMute(true, guildUser);
                return Task.CompletedTask;
            }

            //If a user leaves the channel, remove from the queue.
            if (lefState.VoiceChannel != null && lefState.VoiceChannel.Id == _dataService.LevelTestVoiceChannel.Id)
                _ = RemoveUser(user.Id);

            return Task.CompletedTask;
        }

        private void RemoveUserJobs(ulong userId)
        {
            JobManager.RemoveJob($"[FBQ_UserTimeOut_{userId}]");
        }

        private void AddUserJobs(SocketGuildUser user)
        {
            JobManager.AddJob(async () => await UserTimeOut(user), s => s
                .WithName($"[FBQ_UserTimeOut_{user.Id}]").ToRunOnceAt(DateTime.Now.Add(_timeLeft)));
        }

        private async Task UserTimeOut(SocketGuildUser user)
        {
            await RemoveUser(user.Id);
        }

        /// <summary>
        ///     Pauses the currently active feedback session
        /// </summary>
        public void PauseFeedback()
        {
            _status = "Paused ⏸";
            _paused = true;

            if (_userQueue.Count > 0) RemoveUserJobs(_userQueue[0]);
            _ = _client.SetStatusAsync(UserStatus.AFK);
            _ = UpdateCurrentQueueMessage();
        }

        /// <summary>
        ///     Starts user feedback
        /// </summary>
        /// <returns>True if started, false otherwise</returns>
        public async Task<bool> StartFeedback()
        {
            if (_running && !_paused) return false;

            _status = "Running 🏁";
            _running = true;
            _ = _client.SetStatusAsync(UserStatus.DoNotDisturb);
            await UpdateCurrentQueueMessage();

            if (_paused)
            {
                _paused = false;

                if (_userQueue.Count > 0)
                {
                    RemoveUserJobs(_userQueue[0]);
                    var user = _dataService.GetSocketGuildUser(_userQueue[0]);
                    AddUserJobs(user);
                    await UpdateTimer();
                }

                return true;
            }

            await StartNextUserFeedback();
            return true;
        }

        /// <summary>
        ///     Processes the next user in the queue for feedback.
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

            //Countdown before next user
            for (var i = 5; i > 0; i--)
            {
                _ = _rconService.RconCommand(_playtestEvent.ServerLocation,
                    $"script ScriptPrintMessageCenterAll(\"{user.Username}'s turn starts in: {i}\\nStart with your in-game name.\");",
                    false);
                await Task.Delay(1000);
            }


            //Alert users
            var msg = await _dataService.CSGOTestingChannel.SendMessageAsync(
                $"{user.Mention} may begin their voice feedback.\nType `>done` when you're finished.");

            _activeUser = user;

            _timeLeft = _duration;

            AddUserJobs(user);

            if (_userQueue.Count > 1) _onDeckUser = _dataService.GetSocketGuildUser(_userQueue[1]);

            if (!_timerRunning)
            {
                _abortTimer = false;
                _ = UpdateTimer();
            }

            await Task.Delay(10000);
            await msg.DeleteAsync();
        }

        private async Task UpdateTimer()
        {
            //Prevent recursive refire of the timer when a user is abruptly removed.
            if (_abortTimer)
            {
                _abortTimer = false;
                _timerRunning = false;
                return;
            }

            if (!_running)
            {
                _timerRunning = false;
                _ = _client.SetGameAsync("Waiting...");
                return;
            }

            if (_paused) return;

            _timerRunning = true;

            //Prevent pounding the discord API like crazy.
            if (_tick)
                _ = _client.SetGameAsync($"Time left: {_timeLeft:mm\\:ss}");

            _tick = !_tick;

            var message = $"script ScriptPrintMessageCenterAll(\"{_activeUser.Username}'s " +
                          $"Time Left: {_timeLeft:mm\\:ss} ⏰" +
                          "\\nType >done in Discord when finished" +
                          " | Type >q in Discord to enter the queue";

            if (_userQueue.Count > 1)
            {
                if (_userQueue[1] != _onDeckUser.Id)
                    _onDeckUser = _dataService.GetSocketGuildUser(_userQueue[1]);

                message += $"\\n{_onDeckUser.Username} is next";
            }

            //Need to append the closer characters for the script
            message += "\");";

            _ = _rconService.RconCommand(_playtestEvent.ServerLocation, message, false);

            await Task.Delay(2000);
            _timeLeft = _timeLeft.Subtract(TimeSpan.FromSeconds(2));

            _ = UpdateTimer();
        }

        /// <summary>
        ///     Removes a user from the feedback queue
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
            var processNext = _userQueue.IndexOf(userId) == 0;

            _userQueue.Remove(userId);

            var user = _dataService.GetSocketGuildUser(userId);

            await ProcessMute(true, user);

            if (processNext)
            {
                //Prevent timer from running on old user.
                _abortTimer = true;
                await StartNextUserFeedback();
            }

            await UpdateCurrentQueueMessage();
            return true;
        }

        /// <summary>
        ///     Adds a user to the bottom of the feedback queue
        /// </summary>
        /// <param name="user">User to add</param>
        /// <returns></returns>
        public async Task<bool> AddUserToQueue(SocketUser user)
        {
            if (_userQueue.Contains(user.Id) || _dataService.LevelTestVoiceChannel.Users.All(x => x.Id != user.Id))
                return false;

            _userQueue.Add(user.Id);

            if (_userQueue.Count > 1)
                _onDeckUser = _dataService.GetSocketGuildUser(_userQueue[1]);

            await UpdateCurrentQueueMessage();
            return true;
        }

        /// <summary>
        ///     Pushes a user to the top of the queue
        /// </summary>
        /// <param name="user">User to put at the top of the queue</param>
        /// <returns></returns>
        public async Task AddUserToTopQueue(SocketUser user)
        {
            //If user already exists, remove them and re-add them in the new stop.
            if (_userQueue.Contains(user.Id))
                _userQueue.Remove(user.Id);

            if (_userQueue.Count > 0)
                _userQueue.Insert(1, user.Id);
            else
                _userQueue.Add(user.Id);

            await UpdateCurrentQueueMessage();
        }

        /// <summary>
        ///     Updates the display of users who are giving feedback
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
                    .WithColor(165, 55, 55)
                    .WithFooter("Type >q to enter the queue");
                if (_display == null)
                {
                    _display = await _dataService.CSGOTestingChannel.SendMessageAsync(embed: emptyEmbed.Build());
                }
                else
                {
                    await _display.DeleteAsync();
                    _display = await _dataService.CSGOTestingChannel.SendMessageAsync(embed: emptyEmbed.Build());
                }

                return;
            }

            var embed = new EmbedBuilder()
                .WithAuthor($"Feedback Queue - {_duration:mm\\:ss}min - {_status}")
                .WithColor(_running ? new Color(55, 165, 55) : new Color(222, 130, 50))
                .AddField("On Deck", _dataService.GetSocketUser(_userQueue[0]).Mention)
                .WithFooter("Type >q to enter the queue");

            var message = "";
            for (var i = 1; i < _userQueue.Count; i++)
            {
                var user = _dataService.GetSocketUser(_userQueue[i]);
                message += $"**{i}** - {user.Mention}\n";
            }

            message = message.Trim();
            if (_userQueue.Count > 1) embed.AddField("Next Up", message);

            if (_display == null)
            {
                _display = await _dataService.CSGOTestingChannel.SendMessageAsync(embed: embed.Build());
            }
            else
            {
                await _display.DeleteAsync();
                _display = await _dataService.CSGOTestingChannel.SendMessageAsync(embed: embed.Build());
            }
        }
    }
}