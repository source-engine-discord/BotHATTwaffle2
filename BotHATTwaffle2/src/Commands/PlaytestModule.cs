using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using BotHATTwaffle2.Handlers;
using BotHATTwaffle2.Services;
using BotHATTwaffle2.src.Handlers;
using Discord;
using Discord.Commands;
using Discord.WebSocket;

namespace BotHATTwaffle2.Commands
{
    public class PlaytestModule : ModuleBase<SocketCommandContext>
    {
        private readonly DiscordSocketClient _client;
        private readonly DataService _dataService;

        public PlaytestModule(DiscordSocketClient client, DataService dataService)
        {
            _client = client;
            _dataService = dataService;
        }

        [Command("servers")]
        [RequireContext(ContextType.Guild)]
        [Summary("Displays all playtest servers.")]
        public async Task ServersAsync()
        {
            var foundServers = DatabaseHandler.GetAllTestServers();
            var embed = new EmbedBuilder()
                .WithAuthor("Source Engine Discord CS:GO Test Servers")
                .WithFooter($"Total of {foundServers.Count()} servers.")
                .WithThumbnailUrl(_dataService.Guild.IconUrl)
                .WithColor(new Color(255,135,57));

            foreach (var server in foundServers)
            {
                embed.AddField(server.Address, server.Description, true);
            }

            await ReplyAsync(embed:embed.Build());
        }

        [Command("playtester")]
        [RequireContext(ContextType.Guild)]
        [Summary("Join or leave playtest notifications.")]
        [Remarks("Toggles your subscription to playtest notifications.")]
        public async Task PlaytesterAsync()
        {
            if (((SocketGuildUser)Context.User).Roles.Contains(_dataService.PlayTesterRole))
            {
                await ReplyAsync($"Sorry to see you go from playtest notifications {Context.User.Mention}!");
                await ((SocketGuildUser)Context.User).RemoveRoleAsync(_dataService.PlayTesterRole);
            }
            else
            {
                await ReplyAsync($"Thanks for subscribing to playtest notifications {Context.User.Mention}!");
                await ((SocketGuildUser)Context.User).AddRoleAsync(_dataService.PlayTesterRole);
            }
        }
    }
}
