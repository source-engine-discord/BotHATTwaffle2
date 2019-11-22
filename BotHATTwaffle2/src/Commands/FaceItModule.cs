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
    public class FaceItModule : ModuleBase<SocketCommandContext>
    {
        private readonly DataService _dataService;
        private readonly LogHandler _log;

        public FaceItModule(DataService dataService, LogHandler log)
        {
            _dataService = dataService;
            _log = log;
        }

        [Command("GetHubFiles", RunMode = RunMode.Async)]
        [RequireUserPermission(GuildPermission.BanMembers)]
        [Summary("Invokes a fetch of games from all FaceIT hubs")]
        [Remarks("`>GetHubFiles [startTime] [endTime]`" +
                 "\nExample: `>GetHubFiles \"11/20/2019\" \"12/20/2019\"`")]
        public async Task GetFaceItHubRangeAsync([Optional] string startTime, [Optional] string endTime)
        {
            var embed = new EmbedBuilder()
                .WithColor(55, 55, 165)
                .WithAuthor("Getting Faceit Demos!");

            var message = await ReplyAsync(embed:embed.Build());
            
            if (!DateTime.TryParse(startTime, out var startDateTime)) await ReplyAsync("Failed");

            if (!DateTime.TryParse(endTime, out var endDateTime)) await ReplyAsync("Failed");

            var faceItAPI = new FaceItApi(_dataService, _log);
            var result = await faceItAPI.GetDemos(startDateTime, endDateTime);

            embed.WithAuthor("Complete!");
            embed.WithDescription(result);
            embed.WithColor(55, 165, 55);

            await message.ModifyAsync(x => x.Embed = embed.Build());
        }

        [Command("HubTags")]
        [RequireUserPermission(GuildPermission.BanMembers)]
        [Summary("Manages tags for FaceIt hub demo handing")]
        [Remarks("Dates should **NOT** overlap. Make sure the ending date it 23:59 as well." +
                 "\n`>HubTags show` will display all current tags, sorted by date." +
                 "\n`>HubTags delete [index]` will remove a hub tag from the list. Use `show` to see indexes." +
                 "\n`>HubTags add [Traditional/Wingman] [tagName] \"[startDate]\" \"[endDate]\"" +
                 "\nExample: `>HubTags Traditional MCHs9 \"11/20/2019 00:00\" \"12/20/2019 23:59\"" +
                 "\nQuotes are required around the dates")]
        public async Task FaceItHubTagsAsync(string command, [Optional] string type, [Optional] string tagName,
            [Optional] string startTime, [Optional] string endTime)
        {
            var embed = new EmbedBuilder()
                .WithColor(55, 55, 165);

            switch (command.ToLower())
            {
                case "show":
                    Show();
                    break;
                case "add":
                    Add();
                    break;
                case "delete":
                    Delete();
                    break;
                default:
                    embed.WithAuthor("Unknown Command!");
                    embed.WithColor(165, 55, 55);
                    break;
            }

            await ReplyAsync(embed: embed.Build());

            //Handles showing the current entries
            void Show()
            {
                //Get all items, sort by date, and reverse so it is newest first
                var result = DatabaseUtil.GetHubTypes().OrderBy(x => x.EndDate).Reverse();
                embed.WithAuthor("Current Faceit Hub Tags - Sorted most recent first");
                var counter = 0;

                foreach (var r in result)
                {
                    counter++;
                    embed.AddField($"[{r.Id}] `{r.StartDate:MM/dd/yyyy HH:mm:ss} - {r.EndDate:MM/dd/yyyy HH:mm:ss}`",
                        $"Type: `{r.Type}`" +
                        $"\nTag: `{r.TagName}`");
                    //Handle embed field limit
                    if (counter >= 24)
                        break;
                }
            }

            //Handles adding a new entry
            void Add()
            {
                if (type == null || tagName == null || startTime == null || endTime == null)
                {
                    embed.WithAuthor("All parameters are required when adding a new hub tag!");
                    embed.WithColor(165, 55, 55);
                    return;
                }

                if (!DateTime.TryParse(startTime, out var startDateTime))
                {
                    embed.WithAuthor("Failure parsing startDateTime");
                    embed.WithColor(165, 55, 55);
                }

                if (!DateTime.TryParse(endTime, out var endDateTime))
                {
                    embed.WithAuthor("Failure parsing endDateTime");
                    embed.WithColor(165, 55, 55);
                }

                var result = DatabaseUtil.StoreHubTypes(new FaceItHubSeason
                {
                    TagName = tagName,
                    Type = type,
                    StartDate = startDateTime,
                    EndDate = endDateTime
                });

                if (result)
                {
                    embed.WithAuthor("Added new hub tags!");
                    embed.WithColor(55, 55, 165);
                    return;
                }

                embed.WithAuthor("Failure adding hub tags!");
                embed.WithColor(165, 55, 55);
            }

            //Handles deletion of a entry
            void Delete()
            {
                var wasDeleted = false;
                if (type != null && int.TryParse(type, out var id))
                {
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
                }
                else
                {
                    embed.WithDescription("Unable to parse int from command. See >help HubTags");
                }

                embed.WithAuthor($"Result of tag deletion {wasDeleted}");
            }
        }
    }
}