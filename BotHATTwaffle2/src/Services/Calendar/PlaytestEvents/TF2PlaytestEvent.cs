using System;
using System.Threading.Tasks;
using BotHATTwaffle2.Handlers;
using BotHATTwaffle2.Models.LiteDB;
using BotHATTwaffle2.Services.Playtesting;
using BotHATTwaffle2.Services.SRCDS;
using BotHATTwaffle2.Util;
using Discord;
using Google.Apis.Calendar.v3.Data;

namespace BotHATTwaffle2.Services.Calendar.PlaytestEvents
{
    class Tf2PlaytestEvent : PlaytestEvent
    {
        public Tf2PlaytestEvent(DataService data, LogHandler log, Event playtestEvent) : base(data, log, playtestEvent)
        {
            Game = Games.TF2;
            AnnouncmentChannel = _dataService.TF2AnnouncementChannel;
            TestingChannel = _dataService.TF2TestingChannel;
            TesterRole = _dataService.TF2PlayTesterRole;
        }

        protected override void SetGameMode(string input)
        {
            //TF2 is always for filthy casuals
            IsCasual = true;
        }

        public override async Task PlaytestCommandPre(bool replyInContext,
            LogReceiverService logReceiverService, RconService rconService)
        {
            if (_dataService.RSettings.ProgramSettings.Debug)
                _ = _log.LogMessage("TF2 class PlaytestCommandPre", false, color: LOG_COLOR);

            //Generic setup
            await base.PlaytestCommandPre(replyInContext, logReceiverService, rconService);

            await rconService.RconCommand(PlaytestCommandInfo.ServerAddress, $"exec {_dataService.RSettings.General.TF2Config}");
            await Task.Delay(1000);
            await rconService.RconCommand(ServerLocation, $"changelevel workshop/{PlaytestCommandInfo.WorkshopId}");

            PlaytestCommandRunning = false;
        }

        public override async Task PlaytestCommandStart(bool replyInContext, RconService rconService)
        {
            if (_dataService.RSettings.ProgramSettings.Debug)
                _ = _log.LogMessage("TF2 class PlaytestCommandStart", false, color: LOG_COLOR);

            await base.PlaytestCommandStart(replyInContext, rconService);

            await rconService.RconCommand(PlaytestCommandInfo.ServerAddress, $"exec {_dataService.RSettings.General.TF2Config}");
            await Task.Delay(3000);
            await rconService.RconCommand(PlaytestCommandInfo.ServerAddress,
                $"tv_record {PlaytestCommandInfo.DemoName}; say Recording {PlaytestCommandInfo.DemoName}");

            _ = Task.Run(async () =>
            {
                for (int i = 0; i < 4; i++)
                {
                    _ = rconService.RconCommand(PlaytestCommandInfo.ServerAddress,
                        $"say Playtest of {PlaytestCommandInfo.Title} is live! Be respectful and GLHF!",
                        false);
                    await Task.Delay(3000);
                }
            });

            PlaytestCommandRunning = false;
        }

        public override async Task PlaytestCommandPost(bool replyInContext, LogReceiverService logReceiverService, RconService rconService)
        {
            if (_dataService.RSettings.ProgramSettings.Debug)
                _ = _log.LogMessage("TF2 class PlaytestCommandPost", false, color: LOG_COLOR);

            await base.PlaytestCommandPost(replyInContext, logReceiverService, rconService);
            
            await rconService.RconCommand(ServerLocation, $"changelevel workshop/{PlaytestCommandInfo.WorkshopId}");
            await Task.Delay(15000); //Wait for map to change
            await rconService.RconCommand(PlaytestCommandInfo.ServerAddress,
                $"sv_cheats 1; exec {_dataService.RSettings.General.PostgameConfig};sv_voiceenable 0");

            await rconService.RconCommand(PlaytestCommandInfo.ServerAddress,"mp_tournament 1");
            await rconService.RconCommand(PlaytestCommandInfo.ServerAddress, "mp_tournament_restart");

            await DownloadHandler.DownloadPlaytestDemo(PlaytestCommandInfo);

            //TF2 Embed
            var embed = new EmbedBuilder()
                .WithAuthor($"Download playtest demo for {CleanedTitle}", _dataService.Guild.IconUrl,
                    demoUrl)
                .WithThumbnailUrl(PlaytestCommandInfo.ThumbNailImage)
                .WithColor(new Color(243, 128, 72))
                .WithDescription(
                    $"[Download Demo Here]({demoUrl}) | [Map Images]({PlaytestCommandInfo.ImageAlbum}) | [Playtesting Information](https://www.tophattwaffle.com/playtesting/)");

            //Stop getting more feedback
            logReceiverService.DisableFeedback();

            await AnnouncmentChannel.SendMessageAsync(PlaytestCommandInfo.CreatorMentions, embed: embed.Build());

            PlaytestCommandRunning = false;
        }

        public override async Task PlaytestStartingInTask(RconService rconService, LogReceiverService logReceiverService
            , AnnouncementMessage announcementMessage)
        {
            await base.PlaytestStartingInTask(rconService, logReceiverService, announcementMessage);

            if (_dataService.RSettings.ProgramSettings.Debug)
                _ = _log.LogMessage("TF2 class PlaytestStartingInTask", false, color: LOG_COLOR);
        }

        public override async Task PlaytestTwentyMinuteTask(RconService rconService, LogReceiverService logReceiverService)
        {
            await base.PlaytestTwentyMinuteTask(rconService, logReceiverService);

            if (_dataService.RSettings.ProgramSettings.Debug)
                _ = _log.LogMessage("TF2 class PlaytestTwentyMinuteTask", false, color: LOG_COLOR);

            var wsId = GeneralUtil.GetWorkshopIdFromFqdn(WorkshopLink.ToString());

            await rconService.RconCommand(ServerLocation, $"changelevel workshop/{wsId}");
        }

        public override async Task PlaytestFifteenMinuteTask(RconService rconService,
            LogReceiverService logReceiverService)
        {

            await base.PlaytestFifteenMinuteTask(rconService, logReceiverService);

            if (_dataService.RSettings.ProgramSettings.Debug)
                _ = _log.LogMessage("TF2 class PlaytestFifteenMinuteTask", false, color: LOG_COLOR);

            var embed = new EmbedBuilder()
                .WithAuthor($"Setting up test server for {CleanedTitle}")
                .WithTitle("Workshop Link")
                .WithUrl(WorkshopLink.ToString())
                .WithThumbnailUrl(CanUseGallery
                    ? GalleryImages[0]
                    : _dataService.RSettings.General.FallbackTestImageUrl)
                .WithDescription(
                    $"{DatabaseUtil.GetTestServer(ServerLocation).Description}" +
                    $"\n{Description}")
                .WithColor(new Color(51, 100, 173));
            embed.AddField("Connect To",
                $"`connect {ServerLocation}; password {_dataService.RSettings.General.CasualPassword}`");

            await rconService.RconCommand(ServerLocation,
                $"exec {_dataService.RSettings.General.PostgameConfig}");

            await TestingChannel.SendMessageAsync(embed: embed.Build());
        }

        public override async Task PlaytestStartingTask(RconService rconService,
            LogReceiverService logReceiverService, AnnouncementMessage announcementMessage)
        {
            await base.PlaytestStartingTask(rconService, logReceiverService, announcementMessage);

            if (_dataService.RSettings.ProgramSettings.Debug)
                _ = _log.LogMessage("TF2 class PlaytestStartingTask", false, color: LOG_COLOR);
        }
    }
}