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

        public VoiceChannelHandler(DataService dataService, DiscordSocketClient client, PlaytestService playtestService)
        {
            Console.WriteLine("Setting up VoiceChannelHandler...");
            _dataService = dataService;
            _client = client;
            _playtestService = playtestService;

            _client.UserVoiceStateUpdated += UserVoiceStateUpdated;
        }

        private Task UserVoiceStateUpdated(SocketUser user, SocketVoiceState lefState, SocketVoiceState joinedState)
        {
            //If leftState and joinedState are the same, the event was fired from a modified user event.
            if (lefState.VoiceChannel == joinedState.VoiceChannel)
                return Task.CompletedTask;

            var guildUser = _dataService.GetSocketGuildUser(user.Id);

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