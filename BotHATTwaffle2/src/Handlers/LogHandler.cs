using System;
using System.Threading.Tasks;
using BotHATTwaffle2.Services;
using BotHATTwaffle2.Util;
using Discord;
using Discord.WebSocket;

namespace BotHATTwaffle2.Handlers
{
    public class LogHandler
    {
        private readonly DiscordSocketClient _client;
        private readonly DataService _dataService;

        public LogHandler(DataService data, DiscordSocketClient client)
        {
            Console.WriteLine("Setting up LogHandler...");

            _dataService = data;
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
            string alertUser = null;
            var date = DateTime.Now.ToString("HH:mm:ss.fff - dddd, MMMM dd yyyy");
            if (alert)
                alertUser = _dataService.AlertUser.Mention;

            if (msg.Length > 1950)
                msg = msg.Substring(0, 1950);

            if (console)
            {
                Console.ForegroundColor = color;
                Console.WriteLine(date + "\n" + msg.Replace("```", "") + "\n");
                Console.ResetColor();
            }

            if (channel)
                await _dataService.LogChannel.SendMessageAsync(alertUser, embed: new EmbedBuilder()
                    .WithDescription(msg)
                    .WithColor(GeneralUtil.ColorFromConsoleColor(color))
                    .WithFooter(date)
                    .Build());
        }
    }
}