using System;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using BotHATTwaffle2.Services;
using BotHATTwaffle2.src.Handlers;
using Discord.Commands;
using Discord.WebSocket;

namespace BotHATTwaffle2.Handlers
{
    public class CommandHandler
    {
        private readonly DiscordSocketClient _client;
        private readonly CommandService _commands;
        private readonly DataService _data;
        private readonly LogHandler _log;
        private readonly IServiceProvider _service;

        public CommandHandler(DiscordSocketClient client, CommandService commands, IServiceProvider service,
            DataService data, LogHandler log)
        {
            Console.WriteLine("Setting up CommandHandler...");
            _commands = commands;
            _client = client;
            _service = service;
            _data = data;
            _log = log;
        }

        public async Task InstallCommandsAsync()
        {
            // Hook the MessageReceived event into our command handler
            _client.MessageReceived += HandleCommandAsync;

            // Here we discover all of the command modules in the entry 
            // assembly and load them. Starting from Discord.NET 2.0, a
            // service provider is required to be passed into the
            // module registration method to inject the 
            // required dependencies.
            //
            // If you do not use Dependency Injection, pass null.
            // See Dependency Injection guide for more information.
            await _commands.AddModulesAsync(Assembly.GetEntryAssembly(),
                _service);
        }

        private async Task HandleCommandAsync(SocketMessage messageParam)
        {
            // Don't process the command if it was a system message
            var message = messageParam as SocketUserMessage;
            if (message == null) return;

            // Create a number to track where the prefix ends and the command begins
            var argPos = 0;

            // Determine if the message is a command based on the prefix and make sure no bots trigger commands
            if (!(message.HasCharPrefix(_data.RSettings.ProgramSettings.CommandPrefix[0], ref argPos) ||
                  message.HasMentionPrefix(_client.CurrentUser, ref argPos)) ||
                message.Author.IsBot)
                return;

            // Create a WebSocket-based command context based on the message
            var context = new SocketCommandContext(_client, message);

            // Execute the command with the command context we just
            // created, along with the service provider for precondition checks.

            // Keep in mind that result does not indicate a return value
            // rather an object stating if the command executed successfully.
            var result = await _commands.ExecuteAsync(
                context,
                argPos,
                _service);

            //Fire and forget listening on the message.
            Listen(messageParam);

            if (result.Error is null || result.Error == CommandError.UnknownCommand)
                return; // Ignores successful executions and unknown commands.

            var logMessage =
                $"Invoking User: {context.Message.Author}\nChannel: {context.Message.Channel}\nError Reason: {result.ErrorReason}";
            var alert = false;

            switch (result.Error)
            {
                case CommandError.BadArgCount:
                    var determiner = result.ErrorReason == "The input text has too many parameters." ? "many" : "few";

                    // Retrieves the command's name from the message by finding the first word after the prefix. The string will
                    // be empty if somehow no match is found.
                    var commandName =
                        Regex.Match(context.Message.Content,
                                _data.RSettings.ProgramSettings.CommandPrefix[0] + @"(\w+)", RegexOptions.IgnoreCase)
                            .Groups[1].Value;

                    await context.Channel.SendMessageAsync(
                        $"You provided too {determiner} parameters! Please consult `{_data.RSettings.ProgramSettings.CommandPrefix[0]}help {commandName}`");

                    break;
                case CommandError.ParseFailed:
                case CommandError.UnmetPrecondition:
                case CommandError.ObjectNotFound:
                    await context.Channel.SendMessageAsync(result.ErrorReason);
                    break;
                case CommandError.Exception:
                    alert = true;
                    await context.Channel.SendMessageAsync(
                        $"Something bad happened, I told {_data.AlertUser.Username}.");

                    var e = ((ExecuteResult) result).Exception;
                    logMessage +=
                        $"\nException: {e.GetType()}\nMethod: {e.TargetSite.Name}\n StackTrace: {e.StackTrace}";
                    break;
                default:
                    await context.Channel.SendMessageAsync(
                        $"Something bad happened, I told {_data.AlertUser.Username}.");
                    break;
            }

            await _log.LogMessage(logMessage, alert: alert);
        }

        /// <summary>
        /// This is used to scan each message for less important things.
        /// Mostly used for shit posting, but also does useful things like nag users
        /// to use more up to date tools, or automatically answer some simple questions.
        /// </summary>
        /// <param name="message">Message that got us here</param>
        /// <returns></returns>
        internal async void Listen(SocketMessage message)
        {
            //Add code here for eavesdropping
        }
    }
}