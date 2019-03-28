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
    class UserJoinHandler
    {
        private readonly DataService _data;
        private readonly DiscordSocketClient _client;

        public UserJoinHandler(DataService data, DiscordSocketClient client)
        {
            _data = data;
            _client = client;

            _client.UserJoined += UserJoinedEventHandler;
        }

        private async Task UserJoinedEventHandler(SocketGuildUser user)
        {
            string message = _data.RootSettings.general.welcomeMessage;

            //Replace placeholders
            message = message.Replace("[USER]", user.Mention)
                .Replace("[WELCOME]", _data.WelcomeChannel.Mention);

            await _data.GeneralChannel.SendMessageAsync(message);
        }
    }
}
