using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using BotHATTwaffle2.Services;
using Discord;
using Discord.Commands;
using Discord.WebSocket;

namespace BotHATTwaffle2.Commands
{
    public class UtilitiesModule : ModuleBase<SocketCommandContext>
    {
        private readonly DiscordSocketClient _client;
        private readonly DataService _dataService;

        public UtilitiesModule(DiscordSocketClient client, DataService dataService)
        {
            _client = client;
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
                bool tick = true;
                foreach (var role in _dataService.RSettings.Lists.Roles)
                {
                    if (tick)
                    {
                        d1 += $"{role}\n";
                        tick = false;
                    }
                    else
                    {
                        d2 += $"{role}\n";
                        tick = true;
                    }
                }

                await ReplyAsync(embed: new EmbedBuilder()
                .WithAuthor("Toggleable Roles")
                .WithColor(new Color(255, 135, 57))
                .WithDescription("*`Example: >roleme Level Designer Programmer` will give you both `Level Designer` and `Programmer` roles.*")
                .AddField("\u200E",d1.Trim(), true)
                .AddField("\u200E", d2.Trim(),true)
                .Build());
                return;
            }

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
                _dataService.Guild.Roles.Where(r => roleNames.Contains(r.Name, StringComparer.InvariantCultureIgnoreCase));

            var user = _dataService.GetSocketGuildUser(Context.User.Id);
            var rolesAdded = new List<SocketRole>();
            var rolesRemoved = new List<SocketRole>();

            // Updates roles.
            foreach (var role in rolesValid)
                if (user.Roles.Contains(role))
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
            .WithColor(55,55,165);

            if (rolesAdded.Any())
            {
                var name = $"Added ({rolesAdded.Count})";

                if(Context.IsPrivate)
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
        }
    }
}