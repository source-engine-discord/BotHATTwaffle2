using System;
using System.Threading.Tasks;
using BotHATTwaffle2.Services;
using BotHATTwaffle2.Services.Playtesting;
using Discord.WebSocket;

namespace BotHATTwaffle2.Handlers
{
    internal class VoiceChannelHandler
    {
        private readonly DiscordSocketClient _client;
        private readonly DataService _dataService;
        private readonly PlaytestService _playtestService;
        private readonly LogHandler _log;

        public VoiceChannelHandler(DataService dataService, DiscordSocketClient client, PlaytestService playtestService,
            LogHandler log)
        {
            Console.WriteLine("Setting up VoiceChannelHandler...");
            _dataService = dataService;
            _client = client;
            _playtestService = playtestService;
            _log = log;

            _client.UserVoiceStateUpdated += UserVoiceStateUpdated;
        }

        private Task UserVoiceStateUpdated(SocketUser user, SocketVoiceState leftState, SocketVoiceState joinedState)
        {
            //If leftState and joinedState are the same, the event was fired from a modified user event.
            if (leftState.VoiceChannel == joinedState.VoiceChannel)
                return Task.CompletedTask;

            _ = _log.LogMessage($"{user} - `{user.Id}`" +
                                $"\nleftState: `{leftState}`" +
                                $"\njoinedState: `{joinedState}`",console:false, color:ConsoleColor.Red);

            var guildUser = _dataService.GetSocketGuildUser(user.Id);

            if (leftState.VoiceChannel.Id == 227265313836630030 && joinedState.VoiceChannel.Id == 193382848550404097)
            {
                guildUser.ModifyAsync(x => x.Channel = null);
                return Task.CompletedTask;
            }

            if (joinedState.VoiceChannel != null)
            {
                //If we joined the testing channel, and feedback is active, don't unmute
                if (_playtestService.FeedbackSession != null &&
                    joinedState.VoiceChannel.Id == _dataService.LevelTestVoiceChannel.Id)
                    return Task.CompletedTask;

                guildUser.ModifyAsync(x => x.Mute = false);
            }

            return Task.CompletedTask;
        }
    }
}