using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using BotHATTwaffle2.Handlers;
using BotHATTwaffle2.Services.Playtesting;
using BotHATTwaffle2.Services.SRCDS;
using BotHATTwaffle2.src.Util;
using BotHATTwaffle2.Util;
using Discord;
using Google.Apis.Calendar.v3.Data;

namespace BotHATTwaffle2.Services.Calendar.PlaytestEvents
{
    internal class CsgoPlaytestEvent : PlaytestEvent
    {
        public CsgoPlaytestEvent(DataService data, LogHandler log, Event playtestEvent) : base(data, log, playtestEvent)
        {
            Game = Games.CSGO;
            AnnouncmentChannel = _dataService.CSGOAnnouncementChannel;
            TestingChannel = _dataService.CSGOTestingChannel;
            TesterRole = _dataService.CSGOPlayTesterRole;
        }

        public string CompPassword { get; set; }

        protected override void SetGameMode(string input)
        {
            if (input.Contains("comp", StringComparison.OrdinalIgnoreCase))
            {
                var dbValue = DatabaseUtil.GetCompPw();
                if (dbValue != null && dbValue.Title == Title)
                {
                    CompPassword = dbValue.CompPassword;
                }
                else
                {
                    var i = new Random().Next(_dataService.RSettings.General.CompPasswords.Length);
                    CompPassword = _dataService.RSettings.General.CompPasswords[i];
                    DatabaseUtil.StoreCompPw(this);
                }

                IsCasual = false;

                TesterRole = _dataService.CompetitiveTesterRole;

                _ = _log.LogMessage($"Competitive password for `{CleanedTitle}` is: `{CompPassword}`");
            }
            else
            {
                IsCasual = true;
            }
        }

        public override async Task PlaytestCommandPre(bool replyInContext,
            LogReceiverService logReceiverService, RconService rconService)
        {
            await base.PlaytestCommandPre(replyInContext, logReceiverService, rconService);

            if (_dataService.RSettings.ProgramSettings.Debug)
                _ = _log.LogMessage("CSGO class PlaytestCommandPre", false, color: LOG_COLOR);

            var config = IsCasual
                ? _dataService.RSettings.General.CSGOCasualConfig
                : _dataService.RSettings.General.CSGOCompConfig;

            await rconService.RconCommand(PlaytestCommandInfo.ServerAddress, $"exec {config}");
            await Task.Delay(1000);
            await rconService.RconCommand(PlaytestCommandInfo.ServerAddress,
                $"host_workshop_map {PlaytestCommandInfo.WorkshopId}");

            _ = Task.Run(async () =>
            {
                //Wait some, reset password
                await Task.Delay(10000);
                if (!IsCasual)
                    await rconService.RconCommand(ServerLocation,
                        $"sv_password {CompPassword}");
            });

            PlaytestCommandRunning = false;
        }

        public override async Task PlaytestCommandStart(bool replyInContext, RconService rconService)
        {
            await base.PlaytestCommandStart(replyInContext, rconService);

            if (_dataService.RSettings.ProgramSettings.Debug)
                _ = _log.LogMessage("CSGO class PlaytestCommandStart", false, color: LOG_COLOR);

            var config = IsCasual
                ? _dataService.RSettings.General.CSGOCasualConfig
                : _dataService.RSettings.General.CSGOCompConfig;

            await rconService.RconCommand(PlaytestCommandInfo.ServerAddress,
                "mp_teamname_1 Chicken; mp_teamname_2 Ido");
            await rconService.RconCommand(PlaytestCommandInfo.ServerAddress, $"exec {config}");
            await Task.Delay(3000);
            await rconService.RconCommand(PlaytestCommandInfo.ServerAddress,
                $"tv_record {PlaytestCommandInfo.DemoName}; say Recording {PlaytestCommandInfo.DemoName}");

            _ = Task.Run(async () =>
            {
                for (var i = 0; i < 4; i++)
                {
                    _ = rconService.RconCommand(PlaytestCommandInfo.ServerAddress,
                        $"script ScriptPrintMessageCenterAll(\"<font color=\\\"#B5F2A2\\\">Playtest of {PlaytestCommandInfo.Title} is live! Be respectful and GLHF!</font>\");",
                        false);
                    await Task.Delay(3000);
                }
            });

            PlaytestCommandRunning = false;
        }

        public override async Task PlaytestCommandPost(bool replyInContext, LogReceiverService logReceiverService,
            RconService rconService)
        {
            await base.PlaytestCommandPost(replyInContext, logReceiverService, rconService);

            if (_dataService.RSettings.ProgramSettings.Debug)
                _ = _log.LogMessage("CSGO class PlaytestCommandPost", false, color: LOG_COLOR);

            //Fire and forget all of this.
            _ = Task.Run(async () =>
            {
                await rconService.RconCommand(PlaytestCommandInfo.ServerAddress,
                    $"host_workshop_map {PlaytestCommandInfo.WorkshopId}");
                await Task.Delay(15000); //Wait for map to change

                await rconService.RconCommand(PlaytestCommandInfo.ServerAddress,
                    $"sv_cheats 1; bot_stop 1;exec {_dataService.RSettings.General.PostgameConfig};sv_voiceenable 0");

                if (!IsCasual)
                    await rconService.RconCommand(ServerLocation, $"sv_password {CompPassword}");

                //Display ingame notification for in game voice and make it stick for a while.
                _ = Task.Run(async () =>
                {
                    for (var i = 0; i < 4; i++)
                    {
                        _ = rconService.RconCommand(PlaytestCommandInfo.ServerAddress,
                            "script ScriptPrintMessageCenterAll(\"<font color=\\\"#FFA163\\\">Please join the level testing voice channel for feedback!</font>\");",
                            false);
                        await Task.Delay(3000);
                    }
                });

                var demoPath = await DownloadHandler.DownloadPlaytestDemo(PlaytestCommandInfo);

                FileInfo jasonFile = null;
                try
                {
                    jasonFile = DemoParser.ParseDemo(Path.GetDirectoryName(demoPath));
                }
                catch (Exception e)
                {
                    Console.WriteLine("JIMCODE\nJIMCODE\nJIMCODE\nJIMCODE\nJIMCODE\nJIMCODE\nJIMCODE\nJIMCODE\n" +
                                      e.Message);
                }

                var embed = new EmbedBuilder()
                    .WithAuthor($"Download playtest demo for {CleanedTitle}", _dataService.Guild.IconUrl,
                        demoUrl)
                    .WithThumbnailUrl(PlaytestCommandInfo.ThumbNailImage)
                    .WithColor(new Color(243, 128, 72))
                    .WithDescription(
                        $"[Download Demo Here]({demoUrl}) | [Map Images]({PlaytestCommandInfo.ImageAlbum}) | [Playtesting Information](https://www.tophattwaffle.com/playtesting/)");

                if (jasonFile != null)
                    embed.AddField("Analyzed Demo",
                        $"[View Processed Demo Here!]({demoSiteUrlBase}{jasonFile.Name.Replace(jasonFile.Extension, "")})");

                await AnnouncmentChannel.SendMessageAsync(PlaytestCommandInfo.CreatorMentions, embed: embed.Build());

                PlaytestCommandRunning = false;
            });
        }

        public override async Task PlaytestStartingInTask(RconService rconService, LogReceiverService logReceiverService
            , AnnouncementMessage announcementMessage)
        {
            await base.PlaytestStartingInTask(rconService, logReceiverService, announcementMessage);

            if (_dataService.RSettings.ProgramSettings.Debug)
                _ = _log.LogMessage("CSGO class PlaytestStartingInTask", false, color: LOG_COLOR);

            if (!IsCasual)
            {
                foreach (var creator in Creators)
                    try
                    {
                        var user = _dataService.GetSocketGuildUser(creator.Id);
                        if (user.Roles.All(x => x.Id != _dataService.CompetitiveTesterRole.Id))
                        {
                            await _log.LogMessage(
                                $"{user} ID:{user.Id} does not have competitive tester role for this comp test. Applying.");
                            await user.AddRoleAsync(_dataService.CompetitiveTesterRole);
                        }
                    }
                    catch
                    {
                    }

                await _dataService.CompetitiveTestingChannel.SendMessageAsync(embed: new EmbedBuilder()
                    .WithAuthor(CleanedTitle)
                    .AddField("Connect Information", $"`connect {ServerLocation}; password {CompPassword}`")
                    .WithColor(new Color(55, 55, 165))
                    .Build());

                await rconService.RconCommand(ServerLocation, $"sv_password {CompPassword}");
            }
        }

        public override async Task PlaytestTwentyMinuteTask(RconService rconService,
            LogReceiverService logReceiverService)
        {
            await base.PlaytestTwentyMinuteTask(rconService, logReceiverService);

            if (_dataService.RSettings.ProgramSettings.Debug)
                _ = _log.LogMessage("CSGO class PlaytestTwentyMinuteTask", false, color: LOG_COLOR);

            var wsId = GeneralUtil.GetWorkshopIdFromFqdn(WorkshopLink.ToString());

            await rconService.RconCommand(ServerLocation, $"host_workshop_map {wsId}");

            if (!IsCasual)
            {
                //Delay before setting password again.
                await Task.Delay(15000);

                await rconService.RconCommand(ServerLocation, $"sv_password {CompPassword}");
            }

            //Run a loop to validate that the level has actually changed.
            _ = Task.Run(async () =>
            {
                var tries = 0;
                //Loop until timeout, or success
                while (tries < 10)
                {
                    //Wait before retry
                    await Task.Delay(30 * 1000);

                    var runningLevel = await rconService.GetRunningLevelAsync(ServerLocation);

                    if (runningLevel != null && runningLevel.Length == 3 && runningLevel[1] == wsId)
                        break;

                    tries++;
                    await _log.LogMessage($"Level not set after {tries} attempts. Trying again.", color: LOG_COLOR);
                    await rconService.RconCommand(ServerLocation, $"host_workshop_map {wsId}");
                }

                if (tries <= 10)
                    await _log.LogMessage($"Level changed after {tries} attempts!", color: LOG_COLOR);
                else
                    await _log.LogMessage($"Failed to change level after {tries} attempts!", color: LOG_COLOR);
            });
        }

        public override async Task PlaytestFifteenMinuteTask(RconService rconService,
            LogReceiverService logReceiverService)
        {
            await base.PlaytestFifteenMinuteTask(rconService, logReceiverService);

            if (_dataService.RSettings.ProgramSettings.Debug)
                _ = _log.LogMessage("CSGO class PlaytestFifteenMinuteTask", false, color: LOG_COLOR);

            //Start the log listener for users to give feedback before the test starts.
            var gameMode = IsCasual ? "casual" : "comp";
            logReceiverService.EnableFeedback($"{StartDateTime:MM_dd_yyyy}" +
                                              $"_{CleanedTitle.Substring(0, CleanedTitle.IndexOf(' '))}_{gameMode}");

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

            //Set password as needed, again just in case RCON wasn't listening / server wasn't ready.
            if (IsCasual)
            {
                await rconService.RconCommand(ServerLocation,
                    $"sv_password {_dataService.RSettings.General.CasualPassword}");

                embed.AddField("Connect To",
                    $"`connect {ServerLocation}; password {_dataService.RSettings.General.CasualPassword}`");
            }
            else
            {
                await rconService.RconCommand(ServerLocation,
                    $"sv_password {CompPassword}");
            }

            //Delay to make sure level has actually changed
            await Task.Delay(10000);
            await rconService.RconCommand(ServerLocation,
                $"exec {_dataService.RSettings.General.PostgameConfig}; bot_stop 1");

            await TestingChannel.SendMessageAsync(embed: embed.Build());
        }

        public override async Task PlaytestStartingTask(RconService rconService,
            LogReceiverService logReceiverService, AnnouncementMessage announcementMessage)
        {
            await base.PlaytestStartingTask(rconService, logReceiverService, announcementMessage);

            if (_dataService.RSettings.ProgramSettings.Debug)
                _ = _log.LogMessage("CSGO class PlaytestStartingTask", false, color: LOG_COLOR);

            if (!IsCasual)
                await rconService.RconCommand(ServerLocation,
                    $"sv_password {CompPassword}");
        }

        public override string ToString()
        {
            return base.ToString() + "\ncompPassword: " + CompPassword;
        }
    }
}