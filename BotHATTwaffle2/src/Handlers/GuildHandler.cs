using System;
using System.Threading.Tasks;
using BotHATTwaffle2.Services;
using Discord;
using Discord.WebSocket;

namespace BotHATTwaffle2.Handlers
{
    internal class GuildHandler
    {
        private const ConsoleColor LOG_COLOR = ConsoleColor.DarkMagenta;
        private readonly DiscordSocketClient _client;
        private readonly DataService _dataService;
        private readonly LogHandler _log;
        private readonly ScheduleHandler _schedule;
        private static bool attemptingReconnect = false;

        public GuildHandler(DataService data, DiscordSocketClient client, LogHandler log, ScheduleHandler schedule)
        {
            Console.WriteLine("Setting up GuildHandler...");

            _log = log;
            _dataService = data;
            _client = client;
            _schedule = schedule;

            _client.GuildAvailable += GuildAvailableEventHandler;
            _client.GuildUnavailable += GuildUnavailableEventHandler;
            _client.Ready += ReadyEventHandler;
            _client.Disconnected += ClientDisconnetedHandler;
        }

        private async Task GuildAvailableEventHandler(SocketGuild guild)
        {
            await _log.LogMessage($"Guild Available: {guild.Name}", false, color: LOG_COLOR);
            await _dataService.DeserializeConfig();

            _schedule.AddRequiredJobs();
        }

        /// <summary>
        /// Run when the client has disconnected from Discord.
        /// When here, we will wait around for a while and see if we are re-connected.
        /// If not, after a while we will just give up on connecting and close.
        /// </summary>
        /// <param name="ex"></param>
        /// <returns></returns>
        private async Task ClientDisconnetedHandler(Exception ex)
        {
            //Discord.net will send multiple disconnected events when a network issue is happening.
            //If we are already waiting on a reconnect, don't start another one.
            if (attemptingReconnect)
                return;

            //Start a non-blocking background task with a basic countdown timer
            _ = Task.Run(async() =>
            {
                //Let the program know we are already running
                attemptingReconnect = true;
                int attempt = 0;

                while (attempt < 240 && _client.ConnectionState != ConnectionState.Connected)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine($"Client is disconnected!\n" +
                                      $"We will wait around for {1200 - (attempt * 5)} seconds before giving up.");
                    attempt++;
                    await Task.Delay(5000);
                }

                //Consider the connection dead, close the program. Let the watchdog program handle it.
                if(_client.ConnectionState != ConnectionState.Connected)
                    Environment.Exit(0);

                //If we got here, that means connected again. We can be setup for the next run.
                attemptingReconnect = true;
            });
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