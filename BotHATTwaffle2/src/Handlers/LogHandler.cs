using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using BotHATTwaffle2.Services;
using Discord;
using Discord.WebSocket;

namespace BotHATTwaffle2.src.Handlers
{
    public class LogHandler
    {
        private readonly DataService _data;
        private readonly DiscordSocketClient _client;

        public LogHandler(DataService data, DiscordSocketClient client)
        {
            Console.WriteLine("Setting up LogHandler...");

            _data = data;
            _client = client;

            _client.Log += LogEventHandler; 
        }

        private Task LogEventHandler(LogMessage msg)
        {
            Console.WriteLine(msg.ToString());
            return Task.CompletedTask;
        }

        public async Task LogMessage(string msg, bool channel = true, bool console = true)
        {
            if(channel)
                await _data.LogChannel.SendMessageAsync(msg);

            if(console)
                Console.WriteLine(msg);
        }
    } 
}
