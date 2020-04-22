using System;
using System.Threading.Tasks;
using BotHATTwaffle2.Handlers;
using BotHATTwaffle2.Services;
using BotHATTwaffle2.Services.Calendar;
using BotHATTwaffle2.Services.Playtesting;
using BotHATTwaffle2.Services.SRCDS;
using BotHATTwaffle2.Services.YouTube;
using BotHATTwaffle2.Util;
using Discord;
using Discord.Addons.Interactive;
using Discord.Commands;
using Discord.WebSocket;
using Microsoft.Extensions.DependencyInjection;

namespace BotHATTwaffle2
{
    internal class Program
    {
        private static DiscordSocketClient _client;
        private static CommandService _commands;
        private static DataService _dataService;
        private static LogHandler _log;
        private static IServiceProvider _services;

        public static async Task Main(string[] args)
        {
            Console.Title = "Bot Ido";

            //Always download users to make sure we can always get them
            var config = new DiscordSocketConfig
            {
                AlwaysDownloadUsers = true
            };

            // Dependency injection. All objects use constructor injection.
            _client = new DiscordSocketClient(config);
            _commands = new CommandService();
            _services = new ServiceCollection()
                .AddSingleton(_client)
                .AddSingleton(_commands)
                .AddSingleton<GuildHandler>()
                .AddSingleton<CommandHandler>()
                .AddSingleton<LogHandler>()
                .AddSingleton<UserHandler>()
                .AddSingleton<ScheduleHandler>()
                .AddSingleton<DataService>()
                .AddSingleton<Random>()
                .AddSingleton<GoogleCalendar>()
                .AddSingleton<YouTube>()
                .AddSingleton<ReservationService>()
                .AddSingleton<PlaytestService>()
                .AddSingleton<RconService>()
                .AddSingleton<LogReceiverService>()
                .AddSingleton<SrcdsLogService>()
                .AddSingleton<VoiceChannelHandler>()
                .AddSingleton<IHelpService, HelpService>()
                .AddSingleton(s => new InteractiveService(_client, TimeSpan.FromMinutes(10)))
                .BuildServiceProvider();

            _dataService = _services.GetRequiredService<DataService>();
            _log = _services.GetRequiredService<LogHandler>();
            _services.GetRequiredService<GuildHandler>();
            _services.GetRequiredService<ScheduleHandler>();
            await _services.GetRequiredService<CommandHandler>().InstallCommandsAsync();
            _services.GetRequiredService<UserHandler>();
            _services.GetRequiredService<VoiceChannelHandler>();
            _services.GetRequiredService<SrcdsLogService>();

            //Google APIs
            _services.GetRequiredService<GoogleCalendar>();
            _services.GetRequiredService<YouTube>();

            // Remember to keep token private or to read it from an 
            // external source! In this case, we are reading the token 
            // from an environment variable. If you do not know how to set-up
            // environment variables, you may find more information on the 
            // Internet or by using other methods such as reading from 
            // a configuration.

            await _client.LoginAsync(TokenType.Bot,
                _services.GetRequiredService<DataService>().RSettings.ProgramSettings.BotToken);
            _dataService.SetLogHandler(_services.GetRequiredService<LogHandler>());

            //Set handlers for static classes
            DatabaseUtil.SetHandlers(_services.GetRequiredService<LogHandler>(),
                _services.GetRequiredService<DataService>());
            DownloadHandler.SetHandlers(_services.GetRequiredService<LogHandler>(),
                _services.GetRequiredService<DataService>());
            GeneralUtil.SetHandlers(_services.GetRequiredService<LogHandler>(),
                _services.GetRequiredService<DataService>(), _services.GetRequiredService<Random>());
            DemoParser.SetHandlers(_services.GetRequiredService<LogHandler>(),
                _services.GetRequiredService<DataService>());
            HeatmapGenerator.SetHandlers(_services.GetRequiredService<LogHandler>(),
                _services.GetRequiredService<DataService>());

            await _client.StartAsync();

            // Block this task until the program is closed.
            await Task.Delay(-1);
        }
    }
}