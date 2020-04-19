using System;
using System.Linq;
using System.Threading.Tasks;
using BotHATTwaffle2.Handlers;
using BotHATTwaffle2.Models.LiteDB;
using BotHATTwaffle2.Services;
using BotHATTwaffle2.Services.FaceIt;
using BotHATTwaffle2.Util;
using Discord;
using Discord.Commands;

namespace BotHATTwaffle2.Commands
{
    [Group("FaceIt")]
    public class FaceItModule : ModuleBase<SocketCommandContext>
    {
        private readonly DataService _dataService;
        private readonly LogHandler _log;

        public FaceItModule(DataService dataService, LogHandler log)
        {
            _dataService = dataService;
            _log = log;
        }

        [Command("GetDemos", RunMode = RunMode.Async)]
        [RequireUserPermission(GuildPermission.MoveMembers)]
        [Summary("Invokes a fetch of games from all FACEIT hubs")]
        [Remarks("Example: `>FaceIt GetDemos 11/20/2019 12/20/2019`")]
        public async Task GetDemosAsync(DateTime startTime, DateTime endTime)
        {
            var embed = new EmbedBuilder()
                .WithColor(55, 55, 165)
                .WithAuthor("Getting FACEIT Demos");

            var message = await ReplyAsync(embed: embed.Build());

            var faceItApi = new FaceItApi(_dataService, _log);
            var result = await faceItApi.GetDemos(startTime, endTime);

            embed.WithAuthor("Retrieved FACEIT Demos");
            embed.WithDescription(result);
            embed.WithColor(55, 165, 55);

            await message.ModifyAsync(x => x.Embed = embed.Build());
        }

        [Group("Hubs")]
        public class FaceItHubsModule : ModuleBase<SocketCommandContext>
        {
            [Command("Add")]
            [RequireUserPermission(GuildPermission.MoveMembers)]
            [Summary("Add a new FACEIT Hub.")]
            [Remarks("Adds a hub to the database")]
            public async Task AddAsync(string hubName, string hubType, string hubGuid, int endpoint)
            {
                var embed = new EmbedBuilder()
                    .WithAuthor("Added new FACEIT Hub tags")
                    .WithColor(55, 165, 55);

                if (endpoint != 0 && endpoint != 1)
                {
                    embed.WithAuthor("Endpoint can only be 0 or 1");
                    embed.WithDescription("`0` = Hub\n`1` = Championships");
                    embed.WithColor(165, 55, 55);
                    await ReplyAsync(embed: embed.Build());
                    return;
                }

                var result = DatabaseUtil.InsertHub(new FaceItHub
                {
                    HubName = hubName,
                    HubGUID = hubGuid,
                    HubType = hubType,
                    Endpoint = endpoint
                });

                if (!result)
                {
                    embed.WithAuthor("Failure adding FACEIT Hub tags");
                    embed.WithColor(165, 55, 55);
                }

                await ReplyAsync(embed: embed.Build());
            }

            [Command("Delete")]
            [RequireUserPermission(GuildPermission.MoveMembers)]
            [Summary("Delete a FACEIT Hub.")]
            public async Task DeleteAsync(int id)
            {
                var embed = new EmbedBuilder()
                    .WithColor(55, 165, 55)
                    .WithAuthor($"Deleted FACEIT Hub #{id}");

                if (!DatabaseUtil.DeleteHub(id))
                {
                    embed.WithColor(165, 55, 55);
                    embed.WithAuthor($"Failure deleting FACEIT Hub #{id}");
                    embed.WithDescription("Are you sure that hub exists?");
                }

                await ReplyAsync(embed: embed.Build());
            }

            [Command("Show")]
            [RequireUserPermission(GuildPermission.MoveMembers)]
            [Summary("Show all current FACEIT Hubs")]
            public async Task ShowAsync()
            {
                var embed = new EmbedBuilder()
                    .WithColor(55, 55, 165);

                var result = DatabaseUtil.GetHubs();
                embed.WithAuthor("Current FACEIT Hubs");

                var counter = 0;
                foreach (var r in result)
                {
                    if (counter >= 24)
                        break;

                    embed.AddField($"[{r.Id}] `{r.HubName}`",
                        $"Type: `{r.HubType}`" +
                        $"\nEndpoint: `{r.Endpoint}`" +
                        $"\nGUID: `{r.HubGUID}`");
                    // Handle embed field limit
                    counter++;
                }

                await ReplyAsync(embed: embed.Build());
            }
        }

        [Group("Tags")]
        public class FaceItTagsModule : ModuleBase<SocketCommandContext>
        {
            [Command("Add")]
            [RequireUserPermission(GuildPermission.MoveMembers)]
            [Summary("Add a new FACEIT Hub tag.")]
            [Remarks("Dates should **NOT** overlap. Make sure the ending date is 23:59 as well.")]
            public async Task AddAsync(string type, string tagName, DateTime startTime, DateTime endTime)
            {
                var embed = new EmbedBuilder()
                    .WithAuthor("Added new FACEIT Hub tags")
                    .WithColor(55, 165, 55);

                var result = DatabaseUtil.InsertHubTag(new FaceItHubTag
                {
                    TagName = tagName,
                    Type = type,
                    StartDate = DateTime.SpecifyKind(startTime, DateTimeKind.Utc),
                    EndDate = DateTime.SpecifyKind(endTime, DateTimeKind.Utc)
                });

                if (!result)
                {
                    embed.WithAuthor("Failure adding FACEIT Hub tags");
                    embed.WithColor(165, 55, 55);
                }

                await ReplyAsync(embed: embed.Build());
            }

            [Command("Delete")]
            [RequireUserPermission(GuildPermission.MoveMembers)]
            [Summary("Delete a FACEIT Hub tag.")]
            public async Task DeleteAsync(int id)
            {
                var embed = new EmbedBuilder()
                    .WithColor(55, 165, 55)
                    .WithAuthor($"Deleted FACEIT Hub tag #{id}");

                if (!DatabaseUtil.DeleteHubTag(id))
                {
                    embed.WithColor(165, 55, 55);
                    embed.WithAuthor($"Failure deleting FACEIT Hub tag #{id}");
                    embed.WithDescription("Are you sure that tag exists?");
                }

                await ReplyAsync(embed: embed.Build());
            }

            [Command("Show")]
            [RequireUserPermission(GuildPermission.MoveMembers)]
            [Summary("Show all current FACEIT Hub tags sorted by date.")]
            public async Task ShowAsync()
            {
                var embed = new EmbedBuilder()
                    .WithColor(55, 55, 165);

                // Get all items, sort by date, and reverse so it is newest first
                var result = DatabaseUtil.GetHubTags().OrderByDescending(x => x.EndDate);
                embed.WithAuthor("Current FACEIT Hub tags - sorted most recent first");
                var counter = 0;

                foreach (var r in result)
                {
                    counter++;
                    embed.AddField(
                        $"[{r.Id}] `{r.StartDate.ToUniversalTime():MM/dd/yyyy HH:mm:ss} - {r.EndDate.ToUniversalTime():MM/dd/yyyy HH:mm:ss}`",
                        $"Type: `{r.Type}`" +
                        $"\nTag: `{r.TagName}`");
                    // Handle embed field limit
                    if (counter >= 24)
                        break;
                }

                await ReplyAsync(embed: embed.Build());
            }
        }
    }
}