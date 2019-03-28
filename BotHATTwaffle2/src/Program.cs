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
                .AddSingleton<CommandHandler>()
                .AddSingleton<UserJoinHandler>()
                .AddSingleton<DataService>()
                .AddSingleton<Random>()
                .AddSingleton<IHelpService, HelpService>()
                .BuildServiceProvider();

            // Event subscriptions
            _client.Log += LogEventHandler;
            _client.Ready += ReadyEventHandler;
            _client.GuildAvailable += GuildAvailableEventHandler;

            _data = _services.GetRequiredService<DataService>();
            await _services.GetRequiredService<CommandHandler>().InstallCommandsAsync();
            _services.GetRequiredService<UserJoinHandler>();

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

        private Task LogEventHandler(LogMessage msg)
        {
            Console.WriteLine(msg.ToString());
            return Task.CompletedTask;
        }

        /// <summary>
        /// Raised when the guild (server) becomes available.
        /// <para>
        /// Calls for the configuration to be read from the file.
        /// </para>
        /// </summary>
        /// <remarks>
        /// The configuration is called to be read here because some configuration fields are parsed into objects. Some of this
        /// parsing requires the guild to be available so that names and roles can be retrieved.
        /// Because this bot is intended to be used on only one server, this should only get raised once.
        /// </remarks>
        /// <param name="guild">The guild that has become available.</param>
        /// <returns>No object or value is returned by this method when it completes.</returns>
        private async Task GuildAvailableEventHandler(SocketGuild guild)
        {
            await _data.DeserialiseConfig();
        }

        private Task ReadyEventHandler()
        {
            return Task.CompletedTask;
        }
       
    }
}
