using System;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using BotHATTwaffle2.Handlers;
using BotHATTwaffle2.Services;
using BotHATTwaffle2.Services.SRCDS;
using BotHATTwaffle2.src.Services.FaceIt;
using Discord;
using Discord.Addons.Interactive;
using Discord.Commands;
using Discord.WebSocket;

namespace BotHATTwaffle2.src.Commands
{
    public class FaceItModule : InteractiveBase
    {
        private const ConsoleColor LOG_COLOR = ConsoleColor.DarkRed;
        private readonly DiscordSocketClient _client;
        private readonly DataService _dataService;
        private readonly InteractiveService _interactive;
        private readonly LogHandler _log;
        private readonly LogReceiverService _logReceiverService;
        private readonly RconService _rconService;
        private readonly ScheduleHandler _scheduleHandler;

        public FaceItModule(
            DiscordSocketClient client, DataService dataService,
            RconService rconService, InteractiveService interactive, LogHandler log,
            ScheduleHandler scheduleHandler, LogReceiverService logReceiverService
        ) {
            _client = client;
            _dataService = dataService;
            _interactive = interactive;
            _log = log;
            _rconService = rconService;
            _scheduleHandler = scheduleHandler;
            _logReceiverService = logReceiverService;
        }

        [Command("FaceitHubDemos")]
        [Alias("FaceitHubDemo", "fhd")]
        // [RequireUserPermission(GuildPermission.Administrator)]
        [Summary("Kick off Faceit Hub demo parsing and heatmap creation.")]
        [Remarks("Parse Faceit Hub matches within the specified dates + create heatmaps for the combined data" +
                 "```>FaceitHubDemos\nDate From:\nDate To:\nGamemode:\nHub Regions:```")]
        public async Task FaceitHubAsync(
            [Summary("A pre-built faceit hub demos request for parsing a group of demos.")] [Optional] [Remainder]
            string faceitHubInformation
        ) {
            _dataService.IgnoreListenList.Add(Context.User);

            var faceitHubDemosBuilder =
                new FaceItHubDemosBuilder(Context, _interactive, _dataService, _log);

            if (!string.IsNullOrWhiteSpace(faceitHubInformation))
            {
                //If we are here from a full dump, split it to handle
                var split = faceitHubInformation.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);

                if (split.Length != 4)
                {
                    await ReplyAsync("Invalid bulk faceit hub demos request submission. Consult the help documents.");
                    return;
                }

                await faceitHubDemosBuilder.BuildFaceItHubDemosBulk(split);
            }
            else
            {
                await ReplyAsync("Use >help to find out how to format this command.");
            }

            _dataService.IgnoreListenList.Remove(Context.User);
        }
    }
}
