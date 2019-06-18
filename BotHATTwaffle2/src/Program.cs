using System;
using System.Threading.Tasks;
using BotHATTwaffle2.Handlers;
using BotHATTwaffle2.Services;
using BotHATTwaffle2.Services.Calendar;
using BotHATTwaffle2.Services.Playtesting;
using BotHATTwaffle2.src.Handlers;
using Discord;
using Discord.Commands;
using Discord.WebSocket;
using Microsoft.Extensions.DependencyInjection;

namespace BotHATTwaffle2
{
    internal class Program
    {
        private const ConsoleColor LogColor = ConsoleColor.Red;
        private DiscordSocketClient _client;
        private CommandService _commands;
        private DataService _data;
        private LogHandler _log;
        private IServiceProvider _services;

        public static void Main(string[] args)
        {
            new Program().MainAsync().GetAwaiter().GetResult();
        }

        public async Task MainAsync()
        {
            Console.Title = "BotHATTwaffle 2 - Return of the Bot";

            // Dependency injection. All objects use constructor injection.
            _client = new DiscordSocketClient();
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
                .AddSingleton<PlaytestService>()
                .AddSingleton<IHelpService, HelpService>()
                .BuildServiceProvider();

            _data = _services.GetRequiredService<DataService>();
            _log = _services.GetRequiredService<LogHandler>();
            _services.GetRequiredService<GuildHandler>();
            _services.GetRequiredService<ScheduleHandler>();
            await _services.GetRequiredService<CommandHandler>().InstallCommandsAsync();
            _services.GetRequiredService<UserHandler>();
            _services.GetRequiredService<GoogleCalendar>();

            // Remember to keep token private or to read it from an 
            // external source! In this case, we are reading the token 
            // from an environment variable. If you do not know how to set-up
            // environment variables, you may find more information on the 
            // Internet or by using other methods such as reading from 
            // a configuration.

            await _client.LoginAsync(TokenType.Bot,
                _services.GetRequiredService<DataService>().RSettings.ProgramSettings.BotToken);
            _data.SetLogHandler(_services.GetRequiredService<LogHandler>());
            DatabaseHandler.SetHandlers(_services.GetRequiredService<LogHandler>(), _services.GetRequiredService<DataService>());
            await _client.StartAsync();

            // Block this task until the program is closed.
            await Task.Delay(-1);
        }
    }
}