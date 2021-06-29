using System;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Threading.Tasks;
using BotHATTwaffle2.Commands;
using BotHATTwaffle2.Models.LiteDB;
using BotHATTwaffle2.Services;
using BotHATTwaffle2.Services.Steam;
using BotHATTwaffle2.TypeReader;
using Discord;
using Discord.Commands;
using Discord.WebSocket;
using Newtonsoft.Json.Linq;

namespace BotHATTwaffle2.Handlers
{
    public class CommandHandler
    {
        private readonly DiscordSocketClient _client;
        private readonly CommandService _commands;
        private readonly DataService _dataService;
        private readonly LogHandler _log;
        private readonly char _prefix;
        private readonly IServiceProvider _service;
        private readonly ToolsService _toolsService;
        private readonly string[] _prefixStrings;

        public CommandHandler(
            DiscordSocketClient client,
            CommandService commands,
            IServiceProvider service,
            DataService data,
            LogHandler log, ToolsService toolsService)
        {
            Console.WriteLine("Setting up CommandHandler...");
            _commands = commands;
            _client = client;
            _service = service;
            _dataService = data;
            _log = log;
            _toolsService = toolsService;
            _prefix = _dataService.RSettings.ProgramSettings.CommandPrefix[0];
            _prefixStrings = new[] {"okay ido, " , "<:botido:592644736029032448> " };
        }

        /// <summary>
        ///     Perform setup to enable command support.
        /// </summary>
        public async Task InstallCommandsAsync()
        {
            // Use our event handlers for the following events.
            _client.MessageReceived += MessageReceivedEventHandler;
            _commands.CommandExecuted += CommandExecutedEventHandler;

            // Register custom type readers.
            _commands.AddTypeReader(typeof(TimeSpan), new BetterTimeSpanReader());

            // Here we discover all of the command modules in the entry
            // assembly and load them. Starting from Discord.NET 2.0, a
            // service provider is required to be passed into the
            // module registration method to inject the
            // required dependencies.
            //
            // If you do not use Dependency Injection, pass null.
            // See Dependency Injection guide for more information.
            await _commands.AddModulesAsync(Assembly.GetEntryAssembly(), _service);
        }

        /// <summary>
        ///     Attempts to execute a command when a message is received.
        /// </summary>
        /// <param name="messageParam">The message sent to the client.</param>
        private async Task MessageReceivedEventHandler(SocketMessage messageParam)
        {
            // Don't process the command if it was a system message.
            if (!(messageParam is SocketUserMessage message))
                return;

            _dataService.MessageCount++;

            // Ignore users who are inside interactive sessions.
            if (_dataService.IgnoreListenList.Contains(message.Author.Id))
                return;

            //Don't let people run any commands other than Verify in this channel
            if (message.Channel.Id == _dataService.VerificationChannel.Id &&
                !message.Content.Equals(">verify", StringComparison.OrdinalIgnoreCase))
                return;

            //Don't let users in the Void do anything with the bot.
            if (message.Channel.Id == _dataService.VoidChannel.Id
                && ((SocketGuildUser) message.Author).Roles.All(x => x.Id != _dataService.AdminRole.Id && x.Id != _dataService.ModeratorRole.Id))
                return;

            // Create a number to track where the prefix ends and the command begins.
            var argPos = 0;

           
            // Determine if the message is a command based on the prefix and make sure no bots trigger commands.
            if (!(message.HasCharPrefix(_prefix, ref argPos) ||
                  message.HasMentionPrefix(_client.CurrentUser, ref argPos) ||
                  message.HasStringPrefix(_prefixStrings[0], ref argPos, StringComparison.OrdinalIgnoreCase) ||
                  message.HasStringPrefix(_prefixStrings[1], ref argPos,
                      StringComparison.OrdinalIgnoreCase)) ||
                message.Author.IsBot)
            {
                // Fire and forget listening on the message.
                _ = Task.Run(() =>
                {
                    Listen(messageParam);
                });
                return;
            }

            // Create a WebSocket-based command context based on the message.
            var context = new SocketCommandContext(_client, message);

            // Execute the command with the command context we just
            // created, along with the service provider for precondition checks.
            await _commands.ExecuteAsync(context, argPos, _service);
        }

        /// <summary>
        ///     Provides feedback and/or logs any errors or exceptions resulting from an executed command.
        /// </summary>
        /// <param name="info">
        ///     The information for the executed command.
        ///     May be missing if failure occurs during parsing or precondition stages.
        /// </param>
        /// <param name="context">The context of the executed command.</param>
        /// <param name="result">The result of the execution of the command.</param>
        private async Task CommandExecutedEventHandler(
            Optional<CommandInfo> info,
            ICommandContext context,
            IResult result)
        {
            //Remove the user from the ignore list when the command finished
            if(_dataService.IgnoreListenList.Contains(context.User.Id))
                _dataService.IgnoreListenList.Remove(context.User.Id);

            if (result.Error is null)
            {
                _dataService.CommandCount++;
                return; // Ignores successful executions and unknown commands.
            }
            var alert = false;
            var logMessage =
                $"Command: {context.Message}\n" +
                $"Invoking User: {context.Message.Author}\n" +
                $"Channel: {context.Message.Channel}\n" +
                $"Error Reason: {result.ErrorReason}";

            switch (result.Error)
            {
                case CommandError.UnknownCommand:
                    //Let's check if the requested command was "tools" command.
                    string toolRequest = null;
                    foreach (string s in _prefixStrings)
                    {
                        if(context.Message.Content.StartsWith(s, StringComparison.OrdinalIgnoreCase))
                        {
                            toolRequest = context.Message.Content.Substring(s.Length);
                        }
                    }

                    if(toolRequest == null && context.Message.Content.StartsWith(_prefix))
                        toolRequest = context.Message.Content.Substring(1);

                    if (toolRequest == null && context.Message.Content.StartsWith(_client.CurrentUser.Mention))
                        toolRequest = context.Message.Content.Substring(_client.CurrentUser.Mention.Length);

                    if (toolRequest != null)
                    {
                        var tool = _toolsService.GetTool(toolRequest.Trim());

                        if (tool != null)
                            await HandleTools(context, tool);
                    }

                    return;
                case CommandError.BadArgCount:
                    var determiner = result.ErrorReason == "The input text has too many parameters." ? "many" : "few";
                    var commandName = info.IsSpecified ? info.Value.Name : "";

                    await context.Channel.SendMessageAsync(
                        $"You provided too {determiner} parameters! Please consult `{_prefix}help {commandName}`");

                    break;
                case CommandError.UnmetPrecondition:
                    var reason = result.ErrorReason;

                    //Give user a more accurate representation of what roles they need.
                    switch (result.ErrorReason)
                    {
                        case "User requires channel permission UseExternalEmojis.":
                            reason = $"This command requires you to have the `{_dataService.ActiveRole.Name}` role.";
                            break;
                        case "User requires guild permission KickMembers.":
                            reason = $"This command requires you to have the `{_dataService.ModeratorRole.Name}` role.";
                            break;
                        case "User requires guild permission Administrator.":
                            reason = "This command requires you be a server Administrator.";
                            break;
                    }

                    await context.Channel.SendMessageAsync(reason);
                    break;
                case CommandError.ParseFailed:
                case CommandError.ObjectNotFound:
                    await context.Channel.SendMessageAsync(result.ErrorReason);
                    break;
                case CommandError.Exception:
                    alert = true;
                    await context.Channel.SendMessageAsync(
                        $"Something bad happened, I told {_dataService.AlertUser.Username}.");

                    var e = ((ExecuteResult) result).Exception;
                    logMessage +=
                        $"\nException: {e.GetType()}\nMethod: {e.TargetSite.Name}\n StackTrace: {e.StackTrace}";
                    break;
                default:
                    await context.Channel.SendMessageAsync(
                        $"Something bad happened, I told {_dataService.AlertUser.Username}.");
                    break;
            }

            await _log.LogMessage(logMessage, alert: alert);
        }

        private async Task HandleTools(ICommandContext context, Tool tool)
        {
            var embed = new EmbedBuilder
            {
                Author = new EmbedAuthorBuilder
                {
                    Name = tool.AuthorName,
                    IconUrl = _dataService.Guild.IconUrl,
                    Url = tool.Url
                },
                Title = "Click Here",
                Url = tool.Url,
                ThumbnailUrl = tool.ThumbnailUrl,
                Color = tool.GetColor(),
                Description = tool.Description
            };

            await context.Message.Channel.SendMessageAsync(embed: embed.Build());
        }


        /// <summary>
        ///     Checks contents of non-command messages for miscellaneous functionality.
        /// </summary>
        /// <remarks>
        ///     Mostly used for shit posting, but also does useful things like nag users to use more up to date tools, or
        ///     automatically answer some simple questions.
        /// </remarks>
        /// <param name="message">The message to check.</param>
        private async void Listen(SocketMessage message)
        {
            // Don't want to listen to what a bot tells us to do
            if (message.Author.IsBot) return;

            //Handle Pastebin Message
            /*
            Disabled since Discord added large message embeds
            if (message.Attachments.Count == 1)
            {
                var file = message.Attachments.FirstOrDefault();

                //Should never be null, but better safe than sorry.
                if (file == null)
                    return;

                if (file.Filename.Equals("message.txt", StringComparison.OrdinalIgnoreCase) ||
                    file.Filename.EndsWith(".log", StringComparison.OrdinalIgnoreCase))
                    await LargeMessage(file);
            }*/
            //Check for blacklisting on each message. But don't check mod/admin message
            if(((SocketGuildUser)message.Author).Roles.All(x => x.Id != _dataService.AdminRole.Id && x.Id != _dataService.ModeratorRole.Id))
                if (new BlacklistHandler(_dataService.Blacklist, message, _dataService).CheckBlacklist())
                {
                    await message.DeleteAsync();
                    return;
                }

            // Embed Steam workshop links
            if (message.Content.Contains("://steamcommunity.com/sharedfiles/filedetails/",
                    StringComparison.OrdinalIgnoreCase) || message.Content.Contains(
                    "://steamcommunity.com/workshop/filedetails/", StringComparison.OrdinalIgnoreCase))
            {
                var workshop = new Workshop(_dataService, _log);
                await workshop.SendWorkshopEmbed(message);
                return;
            }

            // Listen for specific user questions, then answer them if we can
            // Listen for carve messages
            if (_dataService.RSettings.AutoReplies.Carve.Any(s => message.Content.ToLower().Contains(s)))
            {
                await Carve();
                return;
            }

            // Listen for packing questions
            if (_dataService.RSettings.AutoReplies.Packing.Any(s => message.Content.ToLower().Contains(s)))
            {
                await Packing();
                return;
            }

            // Tell users that pakrat is bad
            if (_dataService.RSettings.AutoReplies.Pakrat.Any(s => message.Content.ToLower().Contains(s)))
            {
                await Pakrat();
                return;
            }

            // Recommend WallWorm over propper
            if (_dataService.RSettings.AutoReplies.Propper.Any(s => message.Content.ToLower().Contains(s)))
                await Propper();

            /// <summary>
            /// Shames users for asking about carve.
            /// </summary>
            /// <param name="message"></param>
            /// <returns></returns>
            async Task Carve()
            {
                var carveEmbed = new EmbedBuilder()
                    .WithAuthor($"Hey there {message.Author.Username}!", message.Author.GetAvatarUrl())
                    .WithTitle("DO NOT USE CARVE!")
                    .WithThumbnailUrl("https://i.ytimg.com/vi/xh9Kr2iO4XI/maxresdefault.jpg")
                    .WithDescription(
                        "You were asking about carve. We don't use carve here. Not only does it create bad brushwork, but it " +
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
                    .WithTitle("Click here to learn how to use VIDE!")
                    .WithUrl("https://www.tophattwaffle.com/packing-custom-content-using-vide-in-steampipe/")
                    .WithThumbnailUrl("https://www.tophattwaffle.com/wp-content/uploads/2013/11/vide.png")
                    .WithDescription(
                        "I noticed you may be looking for information on how to pack custom content into your level. " +
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
                    .WithTitle("Click here to learn how to use VIDE!")
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
                    .WithTitle("Click here to go to the WallWorm site!")
                    .WithUrl("https://dev.wallworm.com/")
                    .WithThumbnailUrl("https://www.tophattwaffle.com/wp-content/uploads/2017/12/worm_logo.png")
                    .WithDescription(
                        "I saw you were asking about propper. While Propper still works, it's advised to learn " +
                        "a better modeling solution. The preferred method for Source Engine is using 3dsmax with WallWorm Model Tools" +
                        " If you don't want to learn 3dsmax and WWMT, you can learn to configure propper at the link below.: " +
                        "\n\nhttps://www.tophattwaffle.com/configuring-propper-for-steampipe/")
                    .WithColor(new Color(243, 128, 72));

                await message.Channel.SendMessageAsync(embed: wallWormEmbed.Build());
            }

            /*
            async Task LargeMessage(Attachment file)
            {
                //Limit size
                if (file.Size > 5000000)
                    return;

                using (var client = new WebClient())
                {
                    try
                    {
                        var content = client.DownloadString(file.Url);

                        var webResult = client.UploadString(new Uri("https://hastebin.com/documents"), content);

                        var jResult = JObject.Parse(webResult);

                        await message.Channel.SendMessageAsync(
                            "The message was pretty long, for convenience I've uploaded it online:\n" +
                            @"https://hastebin.com/raw/" + jResult.PropertyValues().FirstOrDefault());
                    }
                    catch (Exception e)
                    {
                        await _log.LogMessage("Tried to send to hastebin, but failed for some reason.\n" + e, false);
                    }
                }
            }
            */
        }
    }
}