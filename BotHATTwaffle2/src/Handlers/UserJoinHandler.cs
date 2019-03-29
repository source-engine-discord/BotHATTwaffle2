using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using BotHATTwaffle2.Services;
using Discord.Commands;
using Discord.WebSocket;

namespace BotHATTwaffle2.src.Handlers
{
    class UserHandler
    {
        private readonly DataService _data;
        private readonly DiscordSocketClient _client;
        private readonly LogHandler _log;

        public UserHandler(DataService data, DiscordSocketClient client, LogHandler log)
        {
            Console.WriteLine("Setting up UserHandler...");

            _data = data;
            _client = client;
            _log = log;

            _client.UserJoined += UserJoinedEventHandler;
            _client.UserLeft += UserLeftEventHandler;
        }

        private async Task UserJoinedEventHandler(SocketGuildUser user)
        {
            string message = _data.RootSettings.general.welcomeMessage;

            //Replace placeholders
            message = message.Replace("[USER]", user.Mention)
                .Replace("[WELCOME]", _data.WelcomeChannel.Mention);

            await _data.GeneralChannel.SendMessageAsync(message);
        }

        private async Task UserLeftEventHandler(SocketGuildUser user)
        {
            await _log.LogMessage($"{user.Username} has left the guild!");
        }
    }
}
