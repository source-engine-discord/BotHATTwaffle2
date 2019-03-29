using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using BotHATTwaffle2.Services;
using Discord.WebSocket;

namespace BotHATTwaffle2.src.Handlers
{
    class GuildHandler
    {
        private readonly DataService _data;
        private readonly DiscordSocketClient _client;

        public GuildHandler(DataService data, DiscordSocketClient client)
        {
            Console.WriteLine("Setting up GuildHandler...");

            _data = data;
            _client = client;

            _client.GuildAvailable += GuildAvailableEventHandler;

            //Not used yet
            //_client.GuildUnavailable += GuildUnavailableEventHandler;
        }

        private async Task GuildAvailableEventHandler(SocketGuild guild)
        {
            Console.WriteLine($"Guild Available: {guild.Name}");
            await _data.DeserialiseConfig();
        }

        /*
        private async Task GuildUnavailableEventHandler(SocketGuild guild)
        {
            //Not used yet
        }
        */
    }
}
