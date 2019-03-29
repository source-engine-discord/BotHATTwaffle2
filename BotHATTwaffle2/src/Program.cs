using System;
using System.Threading.Tasks;
using BotHATTwaffle2.Handlers;
using BotHATTwaffle2.src.Handlers;
using BotHATTwaffle2.Services;
using Discord;
using Discord.Commands;
using Discord.WebSocket;
using Microsoft.Extensions.DependencyInjection;


namespace BotHATTwaffle2
{
    class Program
    {
        private CommandService _commands;
        private DiscordSocketClient _client;
        private IServiceProvider _services;
        private DataService _data;

        public static void Main(string[] args)
            => new Program().MainAsync().GetAwaiter().GetResult();

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
                .AddSingleton<DataService>()
                .AddSingleton<Random>()
                .AddSingleton<IHelpService, HelpService>()
                .BuildServiceProvider();

            // Event subscriptions
            _client.Ready += ReadyEventHandler;

            _services.GetRequiredService<LogHandler>();
            _services.GetRequiredService<GuildHandler>();
            _data = _services.GetRequiredService<DataService>();
            await _services.GetRequiredService<CommandHandler>().InstallCommandsAsync();
            _services.GetRequiredService<UserHandler>();

            // Remember to keep token private or to read it from an 
            // external source! In this case, we are reading the token 
            // from an environment variable. If you do not know how to set-up
            // environment variables, you may find more information on the 
            // Internet or by using other methods such as reading from 
            // a configuration.
            await _client.LoginAsync(TokenType.Bot, _services.GetRequiredService<DataService>().RootSettings.program_settings.botToken);
            await _client.StartAsync();

            // Block this task until the program is closed.
            await Task.Delay(-1);
        }

        private Task ReadyEventHandler()
        {
            Console.WriteLine("Guild ready!");
            return Task.CompletedTask;
        }
       
    }
}
