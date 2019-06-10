using System;
using System.Threading.Tasks;
using BotHATTwaffle2.Services;
using Discord;
using Discord.WebSocket;

namespace BotHATTwaffle2.src.Handlers
{
    public class LogHandler
    {
        private readonly DiscordSocketClient _client;
        private readonly DataService _data;

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

        public async Task LogMessage(string msg, bool channel = true, bool console = true, bool alert = false,
            ConsoleColor color = ConsoleColor.White)
        {
            if (alert)
                msg = _data.AlertUser.Mention + "\n" + msg;

            if (msg.Length > 1950)
                msg = msg.Substring(0, 1950);

            if (channel)
                await _data.LogChannel.SendMessageAsync(msg);


            if (console)
            {
                Console.ForegroundColor = color;
                Console.WriteLine(msg + "\n");
                Console.ResetColor();
            }
        }
    }
}