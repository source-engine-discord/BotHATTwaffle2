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

        private async Task LogEventHandler(LogMessage msg)
        {
            if (msg.Exception == null)
                Console.WriteLine(msg.ToString());
            else
                await LogMessage(msg.ToString(prependTimestamp: false), alert: false, color: ConsoleColor.Red);
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

            if(channel && _dataService.LogChannel == null)
            {
                Console.WriteLine($"Attempted to log:\n[{msg}]\nto the log channel, but log channel is not yet set.");
                return;
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