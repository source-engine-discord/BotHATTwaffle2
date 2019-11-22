using System;
using System.Linq;
using System.Runtime.InteropServices;
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
        [RequireUserPermission(GuildPermission.BanMembers)]
        [Summary("Invokes a fetch of games from all FACEIT hubs")]
        [Remarks("Example: `>FaceIt GetDemos 11/20/2019 12/20/2019`")]
        public async Task GetDemosAsync(DateTime startTime, DateTime endTime)
        {
            var embed = new EmbedBuilder()
                .WithColor(55, 55, 165)
                .WithAuthor("Getting FACEIT Demos!");

            var message = await ReplyAsync(embed: embed.Build());

            var faceItAPI = new FaceItApi(_dataService, _log);
            var result = await faceItAPI.GetDemos(startTime, endTime);

            embed.WithAuthor("Complete!");
            embed.WithDescription(result);
            embed.WithColor(55, 165, 55);

            await message.ModifyAsync(x => x.Embed = embed.Build());
        }

        [Group("Tags")]
        class FaceItTagsModule : ModuleBase<SocketCommandContext> {
            [Command("Add")]
            [RequireUserPermission(GuildPermission.BanMembers)]
            [Summary("Add a new FACEIT hub tag.")]
            [Remarks("Dates should **NOT** overlap. Make sure the ending date is 23:59 as well.")]
            public async Task AddAsync(string type, string tagName, DateTime startTime, DateTime endTime) {
                var embed = new EmbedBuilder()
                    .WithAuthor("Added new hub tags!")
                    .WithColor(55, 55, 165);

                var result = DatabaseUtil.StoreHubTypes(new FaceItHubSeason
                {
                    TagName = tagName,
                    Type = type,
                    StartDate = startTime,
                    EndDate = endTime
                });

                if (!result)
                {
                    embed.WithAuthor("Failure adding hub tags!");
                    embed.WithColor(165, 55, 55);
                }

                await ReplyAsync(embed: embed.Build());
            }

            [Command("Delete")]
            [RequireUserPermission(GuildPermission.BanMembers)]
            [Summary("Delete a FACEIT hub tag.")]
            public async Task DeleteAsync(int id) {
                var embed = new EmbedBuilder()
                    .WithColor(55, 55, 165);

                var wasDeleted = false;
                if (DatabaseUtil.DeleteHubType(id))
                {
                    embed.WithColor(165, 55, 55);
                    embed.WithDescription($"Deleted Hub tag with ID {id}");
                    wasDeleted = true;
                }
                else
                {
                    embed.WithDescription($"Failed deleting hub tag with ID `{id}`. Are you sure it exists?");
                }

                embed.WithAuthor($"Result of tag deletion {wasDeleted}");

                await ReplyAsync(embed: embed.Build());
            }

            [Command("Show")]
            [RequireUserPermission(GuildPermission.BanMembers)]
            [Summary("Show all current FACEIT hub tags sorted by date.")]
            public async Task ShowAsync() {
                var embed = new EmbedBuilder()
                    .WithColor(55, 55, 165);

                // Get all items, sort by date, and reverse so it is newest first
                var result = DatabaseUtil.GetHubTypes().OrderByDescending(x => x.EndDate);
                embed.WithAuthor("Current FACEIT Hub Tags - Sorted most recent first");
                var counter = 0;

                foreach (var r in result)
                {
                    counter++;
                    embed.AddField($"[{r.Id}] `{r.StartDate:MM/dd/yyyy HH:mm:ss} - {r.EndDate:MM/dd/yyyy HH:mm:ss}`",
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
