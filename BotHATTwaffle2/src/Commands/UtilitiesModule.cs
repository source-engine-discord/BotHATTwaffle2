using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using BotHATTwaffle2.Handlers;
using BotHATTwaffle2.Models.LiteDB;
using BotHATTwaffle2.Services;
using BotHATTwaffle2.Util;
using Discord;
using Discord.Commands;
using Discord.WebSocket;

namespace BotHATTwaffle2.Commands
{
    public class UtilitiesModule : ModuleBase<SocketCommandContext>
    {
        private readonly DataService _dataService;
        private readonly LogHandler _log;

        public UtilitiesModule(LogHandler log, DataService dataService)
        {
            _log = log;
            _dataService = dataService;
        }

        /// <summary>
        ///     Toggles the invoking user's roles.
        /// </summary>
        /// <remarks>
        ///     Yields a list of all toggleable roles when invoked without parameters. The roles which can be used with this
        ///     command
        ///     are specified in the <c>roleMeWhiteListCSV</c> config field.
        /// </remarks>
        /// <param name="roles">A case-insensitive space-delimited list of roles to toggle.</param>
        /// <returns>No object or value is returned by this method when it completes.</returns>
        [Command("RoleMe")]
        [Summary("Toggles the invoking user's roles.")]
        [Remarks(
            "Toggleable roles typically display possession of a skill, such as 3D modelling or level design. To send multiple " +
            "roles in one invocation, separate the names with a space. Invoking without any parameters displays a list of " +
            "all toggleable roles.")]
        public async Task RolemeAsync(
            [Summary("A case-insensitive, space-delimited list of roles to toggle.")] [Remainder]
            string roles = null)
        {
            if (string.IsNullOrWhiteSpace(roles))
            {
                string d1 = null;
                string d2 = null;
                var tick = true;
                foreach (var role in _dataService.RSettings.Lists.Roles)
                {
                    var r = role;
                    if (!Context.IsPrivate)
                        r = _dataService.Guild.Roles
                            .FirstOrDefault(x => x.Name.Equals(role, StringComparison.OrdinalIgnoreCase))?.Mention;

                    if (tick)
                    {
                        d1 += $"{r}\n";
                        tick = false;
                    }
                    else
                    {
                        d2 += $"{r}\n";
                        tick = true;
                    }
                }

                await ReplyAsync(embed: new EmbedBuilder()
                    .WithAuthor("Toggleable Roles")
                    .WithColor(new Color(255, 135, 57))
                    .WithDescription(
                        "*`Example: >roleme Level Designer Programmer` will give you both `Level Designer` and `Programmer` roles.*")
                    .AddField("\u200E", d1.Trim(), true)
                    .AddField("\u200E", d2.Trim(), true)
                    .Build());
                return;
            }


            roles = roles.Replace("@", "");
            var roleNames = new List<string>();

            foreach (var role in _dataService.RSettings.Lists.Roles)
            {
                var match = Regex.Match(roles, $@"\b{role}\b", RegexOptions.IgnoreCase);

                if (!match.Success) continue;

                // Finds and removes all occurrences of the whitelisted role in the input string.
                while (match.Success)
                {
                    roles = roles.Remove(match.Index, match.Length);
                    match = Regex.Match(roles, $@"\b{role}\b", RegexOptions.IgnoreCase);
                }

                roleNames.Add(role);
            }

            // Splits the remaining roles not found in the whitelist. Filters out empty elements.
            var rolesInvalid = roles.Split(' ').Where(r => !string.IsNullOrWhiteSpace(r)).ToImmutableArray();

            // Finds all SocketRoles from roleNames.
            var rolesValid =
                _dataService.Guild.Roles.Where(r =>
                    roleNames.Contains(r.Name, StringComparer.InvariantCultureIgnoreCase));

            var user = _dataService.GetSocketGuildUser(Context.User.Id);
            var rolesAdded = new List<SocketRole>();
            var rolesRemoved = new List<SocketRole>();

            // Updates roles.
            foreach (var role in rolesValid)
                if (user.Roles.Any(x => x.Id == role.Id))
                {
                    await user.RemoveRoleAsync(role);
                    rolesRemoved.Add(role);
                }
                else
                {
                    await user.AddRoleAsync(role);
                    rolesAdded.Add(role);
                }

            // Builds the response.
            var logMessage = new StringBuilder();

            var embed = new EmbedBuilder();
            embed.WithTitle("`roleme` Results")
                .WithDescription($"Results of toggled roles for {Context.User.Mention}:")
                .WithColor(55, 55, 165);

            if (rolesAdded.Any())
            {
                var name = $"Added ({rolesAdded.Count})";

                if (Context.IsPrivate)
                    embed.AddField(name, string.Join("\n", rolesAdded.Select(r => r.Name)), true);
                else
                    embed.AddField(name, string.Join("\n", rolesAdded.Select(r => r.Mention)), true);

                logMessage.AppendLine($"{name}\n    " + string.Join("\n    ", rolesAdded.Select(r => r.Name)));
            }

            if (rolesRemoved.Any())
            {
                var name = $"Removed ({rolesRemoved.Count})";
                if (Context.IsPrivate)
                    embed.AddField(name, string.Join("\n", rolesRemoved.Select(r => r.Name)), true);
                else
                    embed.AddField(name, string.Join("\n", rolesRemoved.Select(r => r.Mention)), true);
                logMessage.AppendLine($"{name}\n    " + string.Join("\n    ", rolesRemoved.Select(r => r.Name)));
            }

            if (rolesInvalid.Any())
            {
                var name = $"Failed ({rolesInvalid.Count()})";

                embed.AddField(name, string.Join("\n", rolesInvalid), true);
                embed.WithFooter("Failures occur when roles don't exist or toggling them is disallowed.");
                logMessage.Append($"{name}\n    " + string.Join("\n    ", rolesInvalid));
            }

            await ReplyAsync(string.Empty, false, embed.Build());
            await _log.LogMessage($"`{Context.User}` `{Context.User.Id}` used `RoleMe`:\n" +
                                  $"**Added:**\n{string.Join(" | ", rolesAdded.Select(r => r.Name))}" +
                                  $"**Removed:**\n{string.Join(" | ", rolesRemoved.Select(r => r.Name))}" +
                                  $"**Invalid:**\n{string.Join(" | ", rolesInvalid)}");
        }

        [Command("Link")]
        [Summary("Links your Steam ID to your Discord ID")]
        [Remarks("To use certain commands ingame, you need to link your SteamID to your Discord account. This " +
                 "allows us to know what Discord account to target when a command is fired ingame." +
                 "\n\n Basic usage is:\n`>link [command] [id]`" +
                 "\n`>link add STEAM_1:1:1101` Links that SteamID to your account." +
                 "\n`>link get` shows you the current SteamID linked to your account." +
                 "\n`>link delete` removes your current SteamID link." +
                 "\n\nModerators may use\n`>link delete [User/SteamID]` to force remove a link." +
                 "\n`>link get [User/SteamID]` to see a users current link.")]
        public async Task LinkAccountAsync(string command, [Optional] [Remainder] string id)
        {
            var guildUser = _dataService.GetSocketGuildUser(Context.User.Id);
            switch (command.ToLower())
            {
                case "add":
                    await AddUser();
                    break;

                case "delete":
                    if (id == null)
                        await DeleteContextUser();
                    else if (guildUser.Roles.Any(x =>
                                 x.Id == _dataService.ModeratorRole.Id || x.Id == _dataService.AdminRole.Id) &&
                             id != null)
                        await DeleteTargetUser();
                    else
                        await ReplyAsync(embed: new EmbedBuilder()
                            .WithAuthor("You don't have the right permissions for that.")
                            .WithColor(165, 55, 55).Build());
                    break;

                case "get":
                    if (id == null)
                        await GetLinked();
                    else if (guildUser.Roles.Any(x =>
                                 x.Id == _dataService.ModeratorRole.Id || x.Id == _dataService.AdminRole.Id) &&
                             id != null)
                        await GetTargetLinked();
                    else
                        await ReplyAsync(embed: new EmbedBuilder()
                            .WithAuthor("You don't have the right permissions for that.")
                            .WithColor(165, 55, 55).Build());
                    break;

                default:
                    await ReplyAsync(embed: new EmbedBuilder()
                        .WithAuthor("Unknown command")
                        .WithDescription("See `>help link`")
                        .WithColor(165, 55, 55).Build());
                    break;
            }

            async Task DeleteTargetUser()
            {
                UserSteamID returnedUser = null;
                var steamIdRegex = new Regex(@"(STEAM_[\d]:[\d]:\d+)");
                var target = id;
                if (steamIdRegex.IsMatch(id))
                {
                    returnedUser = DatabaseUtil.GetUserSteamID(steamId: steamIdRegex.Match(id).Value);
                }
                else
                {
                    var targetUser = _dataService.GetSocketGuildUser(id);
                    returnedUser = DatabaseUtil.GetUserSteamID(targetUser.Id);
                    target = targetUser.ToString();
                }

                if (returnedUser == null)
                {
                    await DisplayNoLinkFound(target);
                    return;
                }

                var deleteResult = DatabaseUtil.DeleteUserSteamID(returnedUser);
                if (deleteResult)
                {
                    await ReplyAsync(embed: new EmbedBuilder()
                        .WithAuthor("Link deleted!")
                        .WithDescription(
                            $"`{_dataService.GetSocketGuildUser(returnedUser.UserId)}` is **no longer** linked to `{returnedUser.SteamID}`")
                        .WithColor(165, 55, 55).Build());
                    await ReplyAsync();
                }
                else
                {
                    await ReplyAsync(embed: new EmbedBuilder()
                        .WithAuthor("Failed to delete link")
                        .WithColor(165, 55, 55).Build());
                }
            }

            async Task DeleteContextUser()
            {
                var returnedUser = DatabaseUtil.GetUserSteamID(Context.User.Id);
                if (returnedUser == null)
                {
                    await DisplayNoLinkFound(Context.User.ToString());
                    return;
                }

                var deleteResult = DatabaseUtil.DeleteUserSteamID(returnedUser);
                if (deleteResult)
                    await ReplyAsync(embed: new EmbedBuilder()
                        .WithAuthor("Link deleted!")
                        .WithDescription($"`{Context.User}` is **no longer** linked to `{returnedUser.SteamID}`")
                        .WithColor(165, 55, 55).Build());
                else
                    await ReplyAsync(embed: new EmbedBuilder()
                        .WithAuthor("Failed to delete link")
                        .WithColor(165, 55, 55).Build());
            }

            async Task AddUser()
            {
                if (id == null)
                {
                    await ReplyAsync(embed: new EmbedBuilder()
                        .WithAuthor("ID cannot be empty")
                        .WithColor(165, 55, 55).Build());
                    return;
                }

                var steamIdRegex = new Regex(@"(STEAM_[\d]:[\d]:\d+)");

                if (!steamIdRegex.IsMatch(id))
                {
                    await ReplyAsync(embed: new EmbedBuilder()
                        .WithAuthor("Invalid SteamID format")
                        .WithDescription("Example: `STEAM_1:1:11383969`")
                        .WithColor(165, 55, 55).Build());
                    return;
                }

                var parsedID = steamIdRegex.Match(id).Value;

                var result = DatabaseUtil.AddUserSteamID(new UserSteamID
                {
                    UserId = Context.User.Id,
                    SteamID = parsedID
                });

                if (result)
                    await ReplyAsync(embed: new EmbedBuilder()
                        .WithAuthor("Link added!")
                        .WithDescription($"`{Context.User}` is linked to `{parsedID}`")
                        .WithColor(55, 165, 55).Build());
                else
                    await ReplyAsync(embed: new EmbedBuilder()
                        .WithAuthor("Unable to add link")
                        .WithColor(165, 55, 55).Build());
            }

            async Task GetLinked()
            {
                var userSteam = DatabaseUtil.GetUserSteamID(Context.User.Id);

                if (userSteam == null)
                {
                    await DisplayNoLinkFound(Context.User.ToString());
                    return;
                }

                await ReplyAsync(embed: new EmbedBuilder()
                    .WithAuthor("Link found")
                    .WithDescription($"`{Context.User}` is linked to `{userSteam.SteamID}`")
                    .WithColor(55, 55, 165).Build());
            }

            async Task GetTargetLinked()
            {
                UserSteamID returnedUser = null;
                var steamIdRegex = new Regex(@"(STEAM_[\d]:[\d]:\d+)");
                var target = id;
                if (steamIdRegex.IsMatch(id))
                {
                    returnedUser = DatabaseUtil.GetUserSteamID(steamId: steamIdRegex.Match(id).Value);
                }
                else
                {
                    var targetUser = _dataService.GetSocketGuildUser(id);
                    returnedUser = DatabaseUtil.GetUserSteamID(targetUser.Id);
                    target = targetUser.ToString();
                }

                if (returnedUser == null)
                {
                    await DisplayNoLinkFound(target);
                    return;
                }

                await ReplyAsync(embed: new EmbedBuilder()
                    .WithAuthor("Link found")
                    .WithDescription(
                        $"`{_dataService.GetSocketGuildUser(returnedUser.UserId)}` is linked to `{returnedUser.SteamID}`")
                    .WithColor(55, 55, 165).Build());
            }

            async Task DisplayNoLinkFound(string target)
            {
                await ReplyAsync(embed: new EmbedBuilder()
                    .WithAuthor($"No link found for {target}!")
                    .WithColor(165, 55, 55).Build());
            }
        }

        [Command("Convert")]
        [Summary("Converts different units. Example: `>convert 420cm`")]
        [Alias("c")]
        public async Task ConvertAsync([Remainder] string conversion)
        {
            var converted = UnitConverter.AutoConversion(conversion);
            if (converted.Count > 0)
            {
                string formatted = null;
                var counter = 0;
                foreach (var c in converted)
                {
                    formatted += $"`{c.Key.ToLower()}` = `{c.Value}` | ";
                    counter++;

                    if (counter > 5)
                        break;
                }

                await ReplyAsync(formatted.TrimEnd('|', ' '));
            }
        }
    }
}