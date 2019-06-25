using System;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using BotHATTwaffle2.Services;
using BotHATTwaffle2.Services.Steam;
using BotHATTwaffle2.TypeReader;
using Discord;
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
        private readonly Workshop _workshop = new Workshop();

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

            //Register custom type readers
            _commands.AddTypeReader(typeof(TimeSpan), new BetterTimeSpanReader());

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
                {
                    //Fire and forget listening on the message.
                    Listen(messageParam);
                    return;
                }

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

            if (result.Error is null || result.Error == CommandError.UnknownCommand)
                return; // Ignores successful executions and unknown commands.

            var logMessage =
                $"Command: {message}\nInvoking User: {context.Message.Author}\nChannel: {context.Message.Channel}\nError Reason: {result.ErrorReason}";
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
            // Don't want to listen to what a bot tells us to do
            if (message.Author.IsBot) return;

            // Embed Steam workshop links
            if ((message.Content.Contains("://steamcommunity.com/sharedfiles/filedetails/?id=")) || (message.Content.Contains("://steamcommunity.com/workshop/filedetails/")))
            {
                // The two empty strings here are for image album and test type (for when the bot sends the "playtest submitted" message)
                await _workshop.SendWorkshopEmbed(message, _data);
                return;
            }

            // Listen for specific user questions, then answer them if we can
            // Listen for carve messages
            if (_data.RSettings.AutoReplies.Carve.Any(s => message.Content.ToLower().Contains(s)))
            {
                await Carve();
                return;
            }

            // Listen for packing questions
            if (_data.RSettings.AutoReplies.Packing.Any(s => message.Content.ToLower().Contains(s)))
            {
                await Packing();
                return;
            }

            // Tell users that pakrat is bad
            if (_data.RSettings.AutoReplies.Pakrat.Any(s => message.Content.ToLower().Contains(s)))
            {
                await Pakrat();
                return;
            }

            // Recommend WallWorm over propper
            if (_data.RSettings.AutoReplies.Propper.Any(s => message.Content.ToLower().Contains(s)))
            {
                await Propper();
                return;
            }

            // Methods for building the embeds that the if statements caught above

            /// <summary>
            /// Shames users for asking about carve.
            /// </summary>
            /// <param name="message"></param>
            /// <returns></returns>
            async Task Carve()
            {
                var carveEmbed = new EmbedBuilder()
                    .WithAuthor($"Hey there {message.Author.Username}!", message.Author.GetAvatarUrl())
                    .WithTitle($"DO NOT USE CARVE!")
                    .WithThumbnailUrl("https://i.ytimg.com/vi/xh9Kr2iO4XI/maxresdefault.jpg")
                    .WithDescription("You were asking about carve. We don't use carve here. Not only does it create bad brushwork, but it " +
                                     "can also cause Hammer to stop responding and crash. If you're here trying to defend using carve, just stop - you are wrong.")
                    .WithColor(new Color(243, 128, 72));

                await message.Channel.SendMessageAsync(embed: carveEmbed.Build());
            }

            /// <summary>
            /// Tells users how to pack custom content.
            /// </summary>
            /// <param name="message"></param>
            /// <returns></returns>
            async Task Packing()
            {
                var packingEmbed = new EmbedBuilder()
                    .WithAuthor($"Hey there {message.Author.Username}!", message.Author.GetAvatarUrl())
                    .WithTitle($"Click here to learn how to use VIDE!")
                    .WithUrl("https://www.tophattwaffle.com/packing-custom-content-using-vide-in-steampipe/")
                    .WithThumbnailUrl("https://www.tophattwaffle.com/wp-content/uploads/2013/11/vide.png")
                    .WithDescription("I noticed you may be looking for information on how to pack custom content into your level. " +
                                     "This is easily done using VIDE. Click the link above to download VIDE and learn how to use it.")
                    .WithColor(new Color(243, 128, 72));

                await message.Channel.SendMessageAsync(embed: packingEmbed.Build());
            }

            /// <summary>
            /// Nags users to not use pakrat.
            /// </summary>
            /// <param name="message"></param>
            /// <returns></returns>
            async Task Pakrat()
            {
                var pakratEmbed = new EmbedBuilder()
                    .WithAuthor($"Hey there {message.Author.Username}!", message.Author.GetAvatarUrl())
                    .WithTitle($"Click here to learn how to use VIDE!")
                    .WithUrl("https://www.tophattwaffle.com/packing-custom-content-using-vide-in-steampipe/")
                    .WithThumbnailUrl("https://www.tophattwaffle.com/wp-content/uploads/2013/11/vide.png")
                    .WithDescription("I was minding my own business when I heard you mention something about PakRat. " +
                                     "Don't know if you know this, but PakRat is super old and has been know to cause issues in newer games. " +
                                     "There is a newer program that handles packing better called VIDE. You should check that out instead.")
                    .WithColor(new Color(243, 128, 72));

                await message.Channel.SendMessageAsync(embed: pakratEmbed.Build());
            }

            /// <summary>
            /// Suggests WWMT over Propper
            /// </summary>
            /// <param name="message"></param>
            /// <returns></returns>
            async Task Propper()
            {
                var wallWormEmbed = new EmbedBuilder()
                    .WithAuthor($"Hey there {message.Author.Username}!", message.Author.GetAvatarUrl())
                    .WithTitle($"Click here to go to the WallWorm site!")
                    .WithUrl("https://dev.wallworm.com/")
                    .WithThumbnailUrl("https://www.tophattwaffle.com/wp-content/uploads/2017/12/worm_logo.png")
                    .WithDescription("I saw you were asking about propper. While Propper still works, it's advised to learn " +
                                     "a better modeling solution. The preferred method for Source Engine is using 3dsmax with WallWorm Model Tools" +
                                     " If you don't want to learn 3dsmax and WWMT, you can learn to configure propper at the link below.: " +
                                     "\n\nhttps://www.tophattwaffle.com/configuring-propper-for-steampipe/")
                    .WithColor(new Color(243, 128, 72));

                await message.Channel.SendMessageAsync(embed: wallWormEmbed.Build());
            }
        }
    }
}