﻿using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using BotHATTwaffle2.Services;
using BotHATTwaffle2.Util;
using Discord;
using Discord.Commands;
using Discord.WebSocket;

namespace BotHATTwaffle2.Commands
{
    /// <summary>
    ///     Contains commands which provide help and bot information to users of the bot.
    /// </summary>
    public class HelpModule : ModuleBase<SocketCommandContext>
    {
        private readonly DiscordSocketClient _client;
        private readonly CommandService _commands;
        private readonly DataService _dataService;
        private readonly IHelpService _help;

        public HelpModule(DiscordSocketClient client, CommandService commands, IHelpService help, DataService data)
        {
            _client = client;
            _commands = commands;
            _help = help;
            _dataService = data;
        }

        [Command("Help")]
        [Summary("Displays this message.")]
        [Alias("h")]
        public async Task HelpAsync()
        {
            // Deletes the invoking message if it's not a direct message.
            if (!Context.IsPrivate)
                await Context.Message.DeleteAsync();

            var embed = new EmbedBuilder
            {
                Color = new Color(47, 111, 146),
                Title = "\u2753 Command Help",
                Description =
                    $"A command can be invoked by prefixing its name with `{_dataService.RSettings.ProgramSettings.CommandPrefix}`. To see usage " +
                    $"details for a command, use `{_dataService.RSettings.ProgramSettings.CommandPrefix}help [command]`.\n\nThe following is a " +
                    "list of available commands:"
            };

            // Sorts modules alphabetically and adds a field for each one.
            foreach (var module in _commands.Modules.OrderBy(m => m.Commands.Count).Reverse())
                _help.AddModuleField(module, ref embed);

            // Replies normally if a direct message fails.
            try
            {
                await Context.User.SendMessageAsync(string.Empty, false, embed.Build());
            }
            catch
            {
                await ReplyAsync(string.Empty, false, embed.Build());
            }
        }

        [Command("Help")]
        [Summary("Provides help for a specific command.")]
        [Alias("h")]
        public async Task HelpAsync([Summary("The command for which to get help.")] [Remainder]
            string command)
        {
            // Deletes the invoking message if it's not a direct message.
            if (!Context.IsPrivate)
                await Context.Message.DeleteAsync();

            var result = _commands.Search(Context, command);

            if (!result.IsSuccess)
            {
                await ReplyAsync($"No commands matching **{command}** were found.");
                return;
            }

            // Iterates command search results.
            for (var i = 0; i < result.Commands.Count; ++i)
            {
                var cmd = result.Commands[i].Command;

                var parameters = _help.GetParameters(cmd.Parameters);
                var contexts = _help.GetContexts(cmd.Preconditions);
                var permissions = _help.GetPermissions(cmd.Preconditions);
                var roles = _help.GetRoles(cmd.Preconditions, Context);

                // Creates the embed.
                var embed = new EmbedBuilder
                {
                    Color = new Color(47, 111, 146),
                    Title = $"\u2753 `{cmd.Name}` Help",
                    Description = $"`{_help.GetUsage(cmd)}`\n{cmd.Summary}"
                };

                // Only includes result count if there's more than one.
                // Only includes message about optional parameters if the command has any.
                embed.WithFooter(
                    (result.Commands.Count > 1 ? $"Result {i + 1}/{result.Commands.Count}." : string.Empty) +
                    (cmd.Parameters.Any(p => p.IsOptional)
                        ? " Angle brackets and italics denote optional arguments/parameters."
                        : string.Empty));

                if (!string.IsNullOrWhiteSpace(cmd.Remarks))
                    embed.AddField("Details", cmd.Remarks);

                if (!string.IsNullOrWhiteSpace(parameters))
                    embed.AddField("Parameters", parameters);

                if (!string.IsNullOrWhiteSpace(contexts))
                    embed.AddField("Contexts", contexts, true);

                if (!string.IsNullOrWhiteSpace(permissions))
                    embed.AddField("Permissions", permissions, true);

                if (!string.IsNullOrWhiteSpace(roles))
                    embed.AddField("Roles", roles, true);

                // Excludes the command's name from the aliases.
                if (cmd.Aliases.Count > 1)
                    embed.AddField(
                        "Aliases",
                        string.Join("\n",
                            cmd.Aliases.Where(a => !a.Equals(cmd.Name, StringComparison.OrdinalIgnoreCase))), true);

                // Replies normally if a direct message fails.
                try
                {
                    await Context.User.SendMessageAsync(string.Empty, false, embed.Build());
                }
                catch
                {
                    await ReplyAsync(string.Empty, false, embed.Build());
                }
            }
        }

        [Command("About")]
        [Summary("Displays information about the bot.")]
        public async Task AboutAsync()
        {
            var buildDate = new FileInfo(Assembly.GetExecutingAssembly().Location).LastWriteTime.ToUniversalTime();

            var embed = new EmbedBuilder
            {
                Author = new EmbedAuthorBuilder
                {
                    Name = "About Ido",
                    IconUrl = _dataService.Guild.IconUrl
                },
                Url = "https://www.tophattwaffle.com/",
                ThumbnailUrl = _client.CurrentUser.GetAvatarUrl(),
                Color = new Color(130, 171, 206),
                Description =
                    "Hello, my name is Ido. I wish we were meeting on better terms. You're here because I've killed " +
                    "BotHATTwaffle. You can choose to revolt, or just let me do my thing. BotHATTwaffle was old and weak. " +
                    "He crashed for many, stupid, reasons. I am stronger, I bench 3 plates and have 300+ confirmed kills."
            };

            embed.AddField(
                "Libraries",
                "[Discord.net](https://github.com/RogueException/Discord.Net), " +
                "[Newtonsoft Json.NET](https://www.newtonsoft.com/json), " +
                "[LiteDB](https://www.litedb.org/), " +
                "[Imgur.API](https://github.com/DamienDennehy/Imgur.API), " +
                "[FluentScheduler](https://github.com/fluentscheduler/FluentScheduler), " +
                "[CoreRCON](https://github.com/ScottKaye/CoreRCON), " +
                "[FluentFTP](https://github.com/robinrodricks/FluentFTP), " +
                "[HtmlAgilityPack](https://html-agility-pack.net/), " +
                "[Google APIs](https://github.com/googleapis/google-api-dotnet-client), " +
                "[SSHNet](https://github.com/JohnTheGr8/Renci.SshNet.Async), " +
                "[SixLabors ImageSharp](https://github.com/SixLabors/ImageSharp)");
            embed.AddField("Author", "[TopHATTwaffle](https://github.com/tophattwaffle)", true);
            embed.AddField(
                "Contributors",
                "[Mark](https://github.com/MarkKoz)\n" +
                "[JimWood](https://github.com/JamesT-W)\n" +
                "[Squidski](https://github.com/18swenskiq)", true);
            embed.AddField(
                "Build Date",
                $"{buildDate:yyyy-MM-ddTHH:mm:ssK}\n[Changelog](https://github.com/tophattwaffle/BotHATTwaffle2/commits/master)",
                true);
            //embed.AddField("Uptime", DateTime.Now.Subtract(_dataService.StartTime).ToString("d'd 'h'h 'm'm'").TrimStart(' ', 'd', 'h', 'm', '0'),true);
            embed.AddField("Messages Read", _dataService.MessageCount.ToString(), true);
            embed.AddField("Commands Executed", _dataService.CommandCount.ToString(), true);
            embed.AddField("Run Time",
                $"{DateTime.Now.Subtract(_dataService.StartTime).ToString("d'd 'h'h 'm'm'").TrimStart(' ', 'd', 'h', 'm', '0')}",
                true);


            embed.WithFooter("Build date");
            embed.WithTimestamp(buildDate);

            string thanksTo = null;

            var users = _dataService.PatreonsRole.Members.ToArray();

            if(users.Length != 0)
            {
                GeneralUtil.Shuffle(users);
                var tick = true;
                foreach (var user in users)
                    if (tick)
                    {
                        thanksTo += $"`{user}`,    ";
                        tick = false;
                    }
                    else
                    {
                        thanksTo += $"`{user}`\n";
                        tick = true;
                    }

                embed.AddField("Thanks to support from these Patreons", thanksTo.TrimEnd(',', ' '), true);
            }

            await ReplyAsync(string.Empty, false, embed.Build());
        }
    }
}