using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BotHATTwaffle2.Models.LiteDB;
using BotHATTwaffle2.Services.Calendar;
using BotHATTwaffle2.Services.SRCDS;
using Discord;
using Discord.WebSocket;

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
        private string _status = "Idle 🛑";

        public VoiceFeedbackSession(DataService dataService, DiscordSocketClient client, PlaytestEvent playtestEvent,
            RconService rconService)
        {
            _dataService = dataService;
            _client = client;
            _playtestEvent = playtestEvent;
            _rconService = rconService;

            _client.UserVoiceStateUpdated += UserVoiceStateUpdated;

            //Mute everyone on start
            foreach (var user in _dataService.LevelTestVoiceChannel.Users)
            {
                //Skip mods
                if (user.Roles.Any(x => x.Id == _dataService.ModeratorRole.Id || x.Id == _dataService.AdminRole.Id))
                    continue;

                //Skip creators
                if (_playtestEvent.Creators.Any(x => x.Id == user.Id))
                    continue;

                user.ModifyAsync(x => x.Mute = true);
            }

            _ = _client.SetStatusAsync(UserStatus.AFK);
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

            //Skip creators
            if (_playtestEvent.Creators.Any(x => x.Id == user.Id))
                return Task.CompletedTask;

            var guildUser = _dataService.GetSocketGuildUser(user.Id);

            //Skip mods
            if (guildUser.Roles.Any(x => x.Id == _dataService.ModeratorRole.Id || x.Id == _dataService.AdminRole.Id))
                return Task.CompletedTask;

            //User joined
            if (joinedState.VoiceChannel != null && joinedState.VoiceChannel.Id == _dataService.LevelTestVoiceChannel.Id)
            {
                //Don't re-mute a muted user
                if (guildUser.IsMuted)
                    return Task.CompletedTask;

                guildUser.ModifyAsync(x => x.Mute = true);
            }

            return Task.CompletedTask;
        }

        /// <summary>
        /// Pauses the currently active feedback session
        /// </summary>
        public void PauseFeedback()
        {
            _status = "Idle 🛑";
            _running = false;
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

            try
            {
                await user.ModifyAsync(x => x.Mute = false);
            }
            catch
            {
                //Unable to remove the user because they don't exist in the
                await RemoveUser(_userQueue[0]);
                return;
            }

            //Alert users
            var msg = await _dataService.TestingChannel.SendMessageAsync($"{user.Mention} may begin their voice feedback.");
            await _rconService.RconCommand(_playtestEvent.ServerLocation,$"say {user.Username} may now begin their feedback!;" +
                                                   $"say Please let the creator know your in-game name!");

            _ = Task.Run(async () =>
            {
                await Task.Delay(10000);
                await msg.DeleteAsync();
            });

            _ = Task.Run(async () =>
            {
                await Task.Delay((30 * 1000) - (15 * 1000));

                //Abort if we aren't the same user.
                if (_userQueue[0] != user.Id || !_running)
                    return;

                await _rconService.RconCommand(_playtestEvent.ServerLocation, $"say {user.Username} - 15 seconds left. Wrap it up!");
                var msg2 = await _dataService.TestingChannel.SendMessageAsync($"{user.Username} - 15 seconds left. Wrap it up!");
                await Task.Delay(10000);
                await msg2.DeleteAsync();
            });

            await Task.Delay(_dataService.RSettings.General.FeedbackDuration * 60 * 1000);
            
            //Check if the current queue[0] user id matches the user id that sated. In case they end prematurely.
            if (_userQueue[0] == user.Id && _running)
            {
                await RemoveUser(_userQueue[0]);
            }
        }

        /// <summary>
        /// Removes a user from the feedback queue
        /// </summary>
        /// <param name="userId">ID of user to remove</param>
        /// <returns>True if removed, false otherwise</returns>
        public async Task<bool> RemoveUser(ulong userId)
        {
            if (!_userQueue.Contains(userId))
                return false;

            //check if the user we are removing is the current active user.
            //If so, we want to start the next user in the queue right away.
            bool processNext = _userQueue.IndexOf(userId) == 0;

            _userQueue.Remove(userId);

            var user = _dataService.GetSocketGuildUser(userId);

            try
            {
                await user.ModifyAsync(x => x.Mute = true);
            }
            catch
            {
                //Do nothing.
            }

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
            if (_userQueue.Contains(user.Id))
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
            if (_userQueue.Count == 0)
            {
                var emptyEmbed = new EmbedBuilder()
                    .WithAuthor("No users left in queue...")
                    .WithColor(165,55,55);
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
                .WithAuthor($"Feedback Queue - {_status}")
                .WithColor(_running ? new Color(55, 165, 55) : new Color(222, 130, 50))
                .AddField("On Deck", _dataService.GetSocketUser(_userQueue[0]).Mention);

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

            _client.UserVoiceStateUpdated -= UserVoiceStateUpdated;
        }
    }
}
