using System;
using System.Threading.Tasks;
using BotHATTwaffle2.Services;
using Discord.WebSocket;

namespace BotHATTwaffle2.Handlers
{
    internal class GuildHandler
    {
        private const ConsoleColor LOG_COLOR = ConsoleColor.DarkGreen;
        private readonly DiscordSocketClient _client;
        private readonly DataService _data;
        private readonly LogHandler _log;
        private readonly ScheduleHandler _schedule;

        public GuildHandler(DataService data, DiscordSocketClient client, LogHandler log, ScheduleHandler schedule)
        {
            Console.WriteLine("Setting up GuildHandler...");

            _log = log;
            _data = data;
            _client = client;
            _schedule = schedule;

            _client.GuildAvailable += GuildAvailableEventHandler;
            _client.GuildUnavailable += GuildUnavailableEventHandler;
            _client.Ready += ReadyEventHandler;
        }

        private async Task GuildAvailableEventHandler(SocketGuild guild)
        {
            await _log.LogMessage($"Guild Available: {guild.Name}", false, color: LOG_COLOR);
            await _data.DeserializeConfig();

            _schedule.AddRequiredJobs();
        }


        private async Task GuildUnavailableEventHandler(SocketGuild guild)
        {
            await _log.LogMessage($"GUILD UNAVAILABLE: {guild.Name}", false, color: ConsoleColor.Red);

            _schedule.RemoveAllJobs();
        }

        private Task ReadyEventHandler()
        {
            _ = _log.LogMessage("Guild ready!", false, color: LOG_COLOR);
            return Task.CompletedTask;
        }
    }
}