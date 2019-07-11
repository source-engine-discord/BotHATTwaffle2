using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using BotHATTwaffle2.Handlers;
using BotHATTwaffle2.Models.LiteDB;
using BotHATTwaffle2.Services.Calendar;
using BotHATTwaffle2.Services.SRCDS;
using BotHATTwaffle2.Util;
using Discord;
using FluentScheduler;

namespace BotHATTwaffle2.Services.Playtesting
{
    public class PlaytestService
    {
        private const ConsoleColor LOG_COLOR = ConsoleColor.DarkYellow;
        private static AnnouncementMessage _announcementMessage;
        private readonly GoogleCalendar _calendar;
        private readonly DataService _dataService;
        private readonly int _failedRetryCount = 60;
        private readonly LogHandler _log;
        private readonly ReservationService _reservationService;
        private int _failedToFetch;
        private DateTime _lastSeenEditTime;
        private AnnounceMessage _oldMessage;
        public bool PlaytestStartAlert = true;
        private readonly RconService _rconService;
        private readonly LogReceiverService _logReceiverService;

        //Playtest Command Functions
        private static PlaytestCommandInfo _playtestCommandInfo;
        private IUserMessage PlaytestAnnouncementMessage { get; set; }

        public PlaytestService(DataService data, GoogleCalendar calendar, LogHandler log, Random random,
            ReservationService reservationService, RconService rconService, LogReceiverService logReceiverService)
        {
            _dataService = data;
            _log = log;
            _calendar = calendar;
            _reservationService = reservationService;
            _logReceiverService = logReceiverService;

            PlaytestAnnouncementMessage = null;
            _oldMessage = null;
            _rconService = rconService;
            _announcementMessage = new AnnouncementMessage(_calendar, _dataService, random, _log);

            _logReceiverService.SetPlayTestService(this);
        }

        public PlaytestCommandInfo GetPlaytestCommandInfo() => _playtestCommandInfo;

        public bool PlaytestCommandPreCheck()
        {
            //Make sure we have a valid event, if not, abort.
            if (!_calendar.GetTestEventNoUpdate().IsValid)
            {
                return false;
            }

            //Reload the last used playtest if the current event is null
            if (_playtestCommandInfo == null)
                _playtestCommandInfo = DatabaseUtil.GetPlaytestCommandInfo();

            return true;
        }

        public async Task<PlaytestCommandInfo> PlaytestcommandGenericAction(bool replyInContext, string command, string message = null)
        {
            if (!replyInContext)
                await _dataService.TestingChannel.SendMessageAsync(embed: new EmbedBuilder()
                    .WithAuthor(message)
                    .WithColor(new Color(55, 55, 165))
                    .Build());

            await _rconService.RconCommand(_playtestCommandInfo.ServerAddress, command);
            return _playtestCommandInfo;
        }

        public async Task<PlaytestCommandInfo> PlaytestCommandPost(bool replyInContext)
        {
            //No context to send these messages to - default them
            if (!replyInContext)
                await _dataService.TestingChannel.SendMessageAsync(embed: new EmbedBuilder()
                    .WithAuthor($"Post playtest of {_playtestCommandInfo.Title}")
                    .WithColor(new Color(55, 55, 165))
                    .WithDescription($"\nOn **{_playtestCommandInfo.ServerAddress}**" +
                                     $"\nWorkshop ID **{_playtestCommandInfo.WorkshopId}**" +
                                     $"\nDemo Name **{_playtestCommandInfo.DemoName}**").Build());

            //Test over - stop asking for player counts.
            JobManager.RemoveJob("[QueryPlayerCount]");

            //Fire and forget all of this.
            _ = Task.Run(async () =>
            {
                await _rconService.RconCommand(_playtestCommandInfo.ServerAddress,
                $"host_workshop_map {_playtestCommandInfo.WorkshopId}");
                await Task.Delay(15000); //Wait for map to change
                await _rconService.RconCommand(_playtestCommandInfo.ServerAddress,
                    $"sv_cheats 1; bot_stop 1;exec {_dataService.RSettings.General.PostgameConfig};sv_voiceenable 0;" +
                    "say Please join the level testing voice channel for feedback!;" +
                    "say Please join the level testing voice channel for feedback!;" +
                    "say Please join the level testing voice channel for feedback!;" +
                    "say Please join the level testing voice channel for feedback!;" +
                    "say Please join the level testing voice channel for feedback!");

                DownloadHandler.DownloadPlaytestDemo(_playtestCommandInfo);

                const string demoUrl = "http://demos.tophattwaffle.com";

                var embed = new EmbedBuilder()
                    .WithAuthor($"Download playtest demo for {_playtestCommandInfo.Title}", _dataService.Guild.IconUrl,
                        demoUrl)
                    .WithThumbnailUrl(_playtestCommandInfo.ThumbNailImage)
                    .WithColor(new Color(243, 128, 72))
                    .WithDescription(
                        $"[Download Demo Here]({demoUrl}) | [Map Images]({_playtestCommandInfo.ImageAlbum}) | [Playtesting Information](https://www.tophattwaffle.com/playtesting/)");

                //Stop getting more feedback
                _logReceiverService.DisableFeedback();

                //Make sure the playtest file exists before trying to send it.
                if (File.Exists(_logReceiverService.GetFilePath()))
                {
                    Directory.CreateDirectory($"{_dataService.RSettings.ProgramSettings.PlaytestDemoPath}\\{_playtestCommandInfo.StartDateTime:yyyy}" +
                                              $"\\{_playtestCommandInfo.StartDateTime:MM} - {_playtestCommandInfo.StartDateTime:MMMM}" +
                                              $"\\{_playtestCommandInfo.DemoName}");

                    File.Copy(_logReceiverService.GetFilePath(),
                            $"{_dataService.RSettings.ProgramSettings.PlaytestDemoPath}\\{_playtestCommandInfo.StartDateTime:yyyy}" +
                            $"\\{_playtestCommandInfo.StartDateTime:MM} - {_playtestCommandInfo.StartDateTime:MMMM}" +
                            $"\\{_playtestCommandInfo.DemoName}\\{_playtestCommandInfo.DemoName}.txt"
                        ,true);
                    await _dataService.TestingChannel.SendFileAsync(_logReceiverService.GetFilePath(),
                        _playtestCommandInfo.CreatorMentions,
                        embed: embed.Build());
                }
                else
                {
                    await _dataService.TestingChannel.SendMessageAsync(_playtestCommandInfo.CreatorMentions,
                        embed: embed.Build());
                }

                await Task.Delay(30000);
                var patreonUsers = _dataService.PatreonsRole.Members.ToArray();
                GeneralUtil.Shuffle(patreonUsers);
                string thanks = "";
                foreach (var patreonsRoleMember in patreonUsers)
                {
                    thanks += $"{patreonsRoleMember.Username}, ";
                }
                await _rconService.RconCommand(_playtestCommandInfo.ServerAddress, $"say Thanks to these supporters: {thanks.TrimEnd(new[] { ',', ' ' })}");
                await Task.Delay(2000);
                await _rconService.RconCommand(_playtestCommandInfo.ServerAddress, @"Say Become a supporter at www.patreon.com/tophattwaffle");

            });

            return _playtestCommandInfo;
        }

        public async Task<PlaytestCommandInfo> PlaytestCommandStart(bool replyInContext)
        {
            //No context to send these messages to - default them
            if (!replyInContext)
                await _dataService.TestingChannel.SendMessageAsync(embed: new EmbedBuilder()
                    .WithAuthor($"Start playtest of {_playtestCommandInfo.Title}")
                    .WithColor(new Color(55, 55, 165))
                    .WithDescription($"\nOn **{_playtestCommandInfo.ServerAddress}**" +
                                     $"\nWith config of **{_playtestCommandInfo.Mode}**" +
                                     $"\nWorkshop ID **{_playtestCommandInfo.WorkshopId}**" +
                                     $"\nDemo Name **{_playtestCommandInfo.DemoName}**").Build());

            var config = _calendar.GetTestEventNoUpdate().IsCasual
                ? _dataService.RSettings.General.CasualConfig
                : _dataService.RSettings.General.CompConfig;

            await _rconService.RconCommand(_playtestCommandInfo.ServerAddress, $"exec {config}");
            await Task.Delay(3000);
            await _rconService.RconCommand(_playtestCommandInfo.ServerAddress,
                $"tv_record {_playtestCommandInfo.DemoName}; say Recording {_playtestCommandInfo.DemoName}");
            await Task.Delay(1000);
            await _rconService.RconCommand(_playtestCommandInfo.ServerAddress,
                $"say Playtest of {_playtestCommandInfo.Title} is live! Be respectful and GLHF!");
            await Task.Delay(1000);
            await _rconService.RconCommand(_playtestCommandInfo.ServerAddress,
                $"say Playtest of {_playtestCommandInfo.Title} is live! Be respectful and GLHF!");
            await Task.Delay(1000);
            await _rconService.RconCommand(_playtestCommandInfo.ServerAddress,
                $"say Playtest of {_playtestCommandInfo.Title} is live! Be respectful and GLHF!");

            await Task.Delay(3000);
            var patreonUsers = _dataService.PatreonsRole.Members.ToArray();
            GeneralUtil.Shuffle(patreonUsers);
            string thanks = "";
            foreach (var patreonsRoleMember in patreonUsers)
            {
                thanks += $"{patreonsRoleMember.Username}, ";
            }
            await _rconService.RconCommand(_playtestCommandInfo.ServerAddress, $"say Thanks to these supporters: {thanks.TrimEnd(new[] { ',', ' ' })}");
            await Task.Delay(2000);
            await _rconService.RconCommand(_playtestCommandInfo.ServerAddress, @"Say Become a supporter at www.patreon.com/tophattwaffle");

            return _playtestCommandInfo;
        }

        public async Task<PlaytestCommandInfo> PlaytestCommandPre(bool replyInContext)
        {
            var config = _calendar.GetTestEventNoUpdate().IsCasual
                ? _dataService.RSettings.General.CasualConfig
                : _dataService.RSettings.General.CompConfig;

            //Store test information for later use. Will be written to the DB.
            var gameMode = _calendar.GetTestEventNoUpdate().IsCasual ? "casual" : "comp";
            string mentions = null;
            _calendar.GetTestEventNoUpdate().Creators.ForEach(x => mentions += $"{x.Mention} ");
            _playtestCommandInfo = new PlaytestCommandInfo
            {
                Id = 1, //Only storing 1 of these in the DB at a time, so hard code to 1.
                Mode = gameMode,
                DemoName = $"{_calendar.GetTestEventNoUpdate().StartDateTime:MM_dd_yyyy}" +
                           $"_{_calendar.GetTestEventNoUpdate().Title.Substring(0, _calendar.GetTestEventNoUpdate().Title.IndexOf(' '))}" +
                           $"_{gameMode}",
                WorkshopId =
                    GeneralUtil.GetWorkshopIdFromFqdn(_calendar.GetTestEventNoUpdate().WorkshopLink.ToString()),
                ServerAddress = _calendar.GetTestEventNoUpdate().ServerLocation,
                Title = _calendar.GetTestEventNoUpdate().Title,
                ThumbNailImage = _calendar.GetTestEventNoUpdate().CanUseGallery
                    ? _calendar.GetTestEventNoUpdate().GalleryImages[0]
                    : _dataService.RSettings.General.FallbackTestImageUrl,
                ImageAlbum = _calendar.GetTestEventNoUpdate().ImageGallery.ToString(),
                CreatorMentions = mentions,
                StartDateTime = _calendar.GetTestEventNoUpdate().StartDateTime.Value
            };

            //Start receiver if it isn't already
            _logReceiverService.StartLogReceiver(_playtestCommandInfo.ServerAddress);

            //Start feedback capture
            _logReceiverService.EnableFeedback(_playtestCommandInfo.DemoName);

            //Write to the DB so we can restore this info next boot
            DatabaseUtil.StorePlaytestCommandInfo(_playtestCommandInfo);
            
            await _rconService.RconCommand(_playtestCommandInfo.ServerAddress, $"exec {config}");
            await Task.Delay(1000);
            await _rconService.RconCommand(_playtestCommandInfo.ServerAddress,
                $"host_workshop_map {_playtestCommandInfo.WorkshopId}");

            //No context to send these messages to - default them
            if (!replyInContext)
                await _dataService.TestingChannel.SendMessageAsync(embed: new EmbedBuilder()
                    .WithAuthor($"Pre-start playtest of {_playtestCommandInfo.Title}")
                    .WithColor(new Color(55, 55, 165))
                    .WithDescription($"\nOn **{_playtestCommandInfo.ServerAddress}**" +
                                     $"\nWith config of **{_playtestCommandInfo.Mode}**" +
                                     $"\nWorkshop ID **{_playtestCommandInfo.WorkshopId}**").Build());

            _ = Task.Run(async () =>
            {
                //Wait some, reset password
                await Task.Delay(5000);
                await _rconService.RconCommand(_calendar.GetTestEventNoUpdate().ServerLocation,
                    $"sv_password {_calendar.GetTestEventNoUpdate().CompPassword}");
            });
            
            return _playtestCommandInfo;
        }

        /// <summary>
        ///     Starts the chain of events to post a new announcement message.
        ///     If a valid existing message can be used, it will be used instead.
        /// </summary>
        /// <returns></returns>
        public async Task PostOrUpdateAnnouncement()
        {
            //Get event, required for posting new / updating
            //Abort if the test isn't valid
            //Clean up old message if required
            //Check old message, required for fresh boot with empty collection in db
            if (!_calendar.GetTestEvent().IsValid)
            {
                if (_dataService.RSettings.ProgramSettings.Debug)
                    _ = _log.LogMessage("No test was found!", false, color: LOG_COLOR);

                if (PlaytestAnnouncementMessage != null)
                {
                    if (_dataService.RSettings.ProgramSettings.Debug)
                        _ = _log.LogMessage("Attempting to deleted outdated announcement", false, color: LOG_COLOR);
                    try
                    {
                        await _dataService.AnnouncementChannel.DeleteMessageAsync(PlaytestAnnouncementMessage);
                    }
                    catch
                    {
                        _ = _log.LogMessage(
                            "Failed to delete outdated playtest message. It may have been deleted manually",
                            false, color: LOG_COLOR);
                    }
                }

                PlaytestAnnouncementMessage = null;

                return;
            }


            if (_dataService.RSettings.ProgramSettings.Debug)
                _ = _log.LogMessage("Posting or updating playtest announcement", false, color: LOG_COLOR);


            if (PlaytestAnnouncementMessage == null)
                await PostNewAnnouncement();
            else
                await UpdateAnnouncementMessage();
        }

        /// <summary>
        /// Builds an embed that shows upcoming playtest events and events from the queue
        /// </summary>
        /// <param name="getSchedule">Get events from Queue</param>
        /// <param name="getCalendar">Get events from Calendar</param>
        /// <returns>Built embed with events</returns>
        public async Task<EmbedBuilder> GetUpcomingEvents(bool getSchedule, bool getCalendar)
        {
            string author = "Current ";
            var embed = new EmbedBuilder().WithColor(new Color(55,55,165))
                .WithFooter($"Current CT Time: {DateTime.Now}")
                .WithDescription("[View Testing Calendar](http://playtesting.tophattwaffle.com) " +
                                 "| [View Testing Requirements](https://www.tophattwaffle.com/playtesting) " +
                                 "| View Queue with `>Schedule`");

            if (getSchedule)
            {
                author += "Playtest Requests";
                var testQueue = DatabaseUtil.GetAllPlaytestRequests().ToList();
                //No tests found - do nothing
                if (testQueue.Count == 0)
                {
                    embed.AddField("No playtest requests found!", "Submit your own with: `>request`");
                }
                else
                    for (var i = 0; i < testQueue.Count; i++)
                    {
                        //Don't have more than 24
                        if (embed.Fields.Count >= 24)
                            break;

                        var info = "Creator(s): ";
                        foreach (var creator in testQueue[i].CreatorsDiscord)
                        {
                            var user = _dataService.GetSocketGuildUser(creator);
                            if (user != null)
                                info += $"`{user.Username}`, ";
                            else
                                info += $"Could not get user `{creator}`, ";
                        }

                        info = info.Trim(',', ' ');
                        info += $"\nRequested Time: `{testQueue[i].TestDate}`\n" +
                                $"[Map Images]({testQueue[i].ImgurAlbum}) - " +
                                $"[Workshop Link]({testQueue[i].WorkshopURL})\n";

                        embed.AddField($"[{i}] - {testQueue[i].MapName} - {testQueue[i].TestType}", info, true);
                    }
            }

            if (getCalendar)
            {
                //If we added requests, toss "and" in there.
                author += getSchedule ? " and " : "";

                author += "Scheduled Playtests";
                var testEvents = await _calendar.GetNextMonthAsync(DateTime.Now);
                if (testEvents.Items.Count  == 0)
                {
                    embed.AddField("No scheduled playtests found!", "Submit yours with: `>request`");
                }
                else
                {
                    foreach (var item in testEvents.Items)
                    {
                        if (embed.Fields.Count >= 24)
                            break;

                        embed.AddField(item.Summary, $"`Scheduled`\nStart Time: `{item.Start.DateTime}`\nEnd Time: `{item.End.DateTime}`", true);
                    }
                }
            }

            if (embed.Fields.Count >= 24)
                embed.AddField("Max Fields Added","Somehow there are more items than Discord embeds allow. Some items omitted.");

            embed.WithAuthor(author);
            return embed;
        }

        /// <summary>
        ///     Attempts to update the existing announcement message.
        ///     If failure to update after
        ///     <value>_failedRetryCount</value>
        ///     (default 60) tries, the message is
        ///     assumed to be lost, and will be recreated. This may result in double announcement messages that require
        ///     manual cleanup.
        /// </summary>
        /// <returns></returns>
        private async Task UpdateAnnouncementMessage()
        {
            try
            {
                //Compare the current event edit time with the last know.
                //The current event edit time will be different from last known if the event has changed.
                var eventEditTime = _calendar.GetTestEventNoUpdate().EventEditTime;
                if (eventEditTime != null && eventEditTime.Value.Equals(_lastSeenEditTime))
                {
                    await PlaytestAnnouncementMessage.ModifyAsync(x =>
                    {
                        x.Embed = _announcementMessage.CreatePlaytestEmbed(
                            _calendar.GetTestEventNoUpdate().IsCasual);
                    });
                    _failedToFetch = 0;
                }
                else
                {
                    //Being in this else means we know the message is different, remake it.
                    await _dataService.AnnouncementChannel.DeleteMessageAsync(PlaytestAnnouncementMessage);
                    await PostNewAnnouncement();
                }

                var lastEditTime = _calendar.GetTestEventNoUpdate().LastEditTime;
                if (lastEditTime != null)
                    _lastSeenEditTime = lastEditTime.Value;
            }
            catch
            {
                //Have we failed enough to rebuild?
                if (_failedToFetch >= _failedRetryCount)
                {
                    _ = _log.LogMessage($"Tried to update announcement messages {_failedToFetch}, but failed." +
                                        "\nCreated a new message next time.", false, color: LOG_COLOR);
                    PlaytestAnnouncementMessage = null;
                }
                else
                {
                    //Have not failed enough, lets keep trying.
                    _failedToFetch++;
                    if (_dataService.RSettings.ProgramSettings.Debug)
                        _ = _log.LogMessage($"Failed to update playtest announcement {_failedToFetch} times", false,
                            color: LOG_COLOR);
                }
            }
        }

        /// <summary>
        ///     Posts a new playtest announcement
        /// </summary>
        /// <returns></returns>
        private async Task PostNewAnnouncement()
        {
            if (_dataService.RSettings.ProgramSettings.Debug)
                _ = _log.LogMessage("Posting new announcement", false, color: LOG_COLOR);

            //Stop asking server for player counts
            _dataService.IncludePlayerCount = false;
            _dataService.PlayerCount = "0";

            //We posted a new announcement, meaning we can allow reservations again.
            _reservationService.AllowReservations();

            try
            {
                //Make the announcement and store to a variable
                PlaytestAnnouncementMessage = await _dataService.AnnouncementChannel.SendMessageAsync(
                    embed: _announcementMessage.CreatePlaytestEmbed(_calendar.GetTestEventNoUpdate().IsCasual));

                //Hand off the message and time to be stored in the DB for use on restarts
                var eventEditTime = _calendar.GetTestEventNoUpdate().EventEditTime;
                if (eventEditTime != null)
                    DatabaseUtil.StoreAnnouncement(PlaytestAnnouncementMessage,
                        eventEditTime.Value);

                var lastEditTime = _calendar.GetTestEventNoUpdate().LastEditTime;
                if (lastEditTime != null)
                    _lastSeenEditTime = lastEditTime.Value;

                SchedulePlaytestAnnouncements();
            }
            catch
            {
                _ = _log.LogMessage("Attempted to post new announcement, but failed", false, color: LOG_COLOR);
            }
        }

        /// <summary>
        ///     Attempts to get a previously created announcement message based on values that were stored in the DB.
        ///     If the located message does not match the current event it will be deleted.
        ///     If nothing can be located, it does nothing.
        /// </summary>
        /// <returns></returns>
        public async Task TryAttachPreviousAnnounceMessage()
        {
            var testEvent = _calendar.GetTestEvent();

            //Get the last known message
            _oldMessage = DatabaseUtil.GetAnnouncementMessage();

            //No message found in the DB, do nothing. Likely to happen when DB is new.
            if (_oldMessage == null)
            {
                if (_dataService.RSettings.ProgramSettings.Debug)
                    _ = _log.LogMessage("No message found in DB to reattach to", false, color: LOG_COLOR);

                return;
            }

            //Make sure a test is valid
            if (!testEvent.IsValid)
            {
                if (_dataService.RSettings.ProgramSettings.Debug)
                    _ = _log.LogMessage("No valid test found to post", false, color: LOG_COLOR);

                return;
            }

            _ = _log.LogMessage("Attempting to get old announcement message\n" +
                                $"{_oldMessage.AnnouncementId} that was created at {_oldMessage.AnnouncementDateTime}",
                false, color: LOG_COLOR);


            var eventEditTime = _calendar.GetTestEventNoUpdate().EventEditTime;
            if (eventEditTime != null && eventEditTime.Value.Equals(_oldMessage.AnnouncementDateTime))
            {
                try
                {
                    PlaytestAnnouncementMessage =
                        await _dataService.AnnouncementChannel.GetMessageAsync(_oldMessage.AnnouncementId) as
                            IUserMessage;

                    if (PlaytestAnnouncementMessage != null)
                        _ = _log.LogMessage($"Retrieved old announcement! ID: {PlaytestAnnouncementMessage.Id}", false,
                            color: LOG_COLOR);

                    var lastEditTime = _calendar.GetTestEventNoUpdate().LastEditTime;
                    if (lastEditTime != null)
                        _lastSeenEditTime = lastEditTime.Value;
                }
                catch
                {
                    _ = _log.LogMessage("Unable to retrieve old announcement message!", false, color: LOG_COLOR);
                }
            }
            else
            {
                _ = _log.LogMessage("Messages do not match, deleting old message", false, color: LOG_COLOR);
                try
                {
                    await _dataService.AnnouncementChannel.DeleteMessageAsync(_oldMessage.AnnouncementId);
                    PlaytestAnnouncementMessage = null;
                }
                catch
                {
                    _ = _log.LogMessage("Could not delete old message - it was likely deleted manually",
                        false, color: LOG_COLOR);
                }
            }
        }

        public void ClearScheduledAnnouncements()
        {
            JobManager.RemoveJob("[Playtest1Hour]");
            JobManager.RemoveJob("[Playtest15Minute]");
            JobManager.RemoveJob("[Playtest20Minute]");
            JobManager.RemoveJob("[PlaytestStarting]");
            JobManager.RemoveJob("[QueryPlayerCount]");

            //Stop getting server listen messages.
            _logReceiverService.StopLogReceiver();
        }

        public void SchedulePlaytestAnnouncements()
        {
            //Clear old jobs, if any.
            ClearScheduledAnnouncements();

            if (PlaytestAnnouncementMessage == null || !_calendar.GetTestEventNoUpdate().TestValid())
                return;

            var startDateTime = _calendar.GetTestEventNoUpdate().StartDateTime;
            _ = _log.LogMessage($"Playtest scheduled for: {startDateTime}", false, color: LOG_COLOR);

            if (startDateTime != null && DateTime.Compare(DateTime.Now.AddMinutes(60), startDateTime.Value) < 0)
            {
                //Subtract 60.2 minutes. If .2 isn't added the announcement states wrong time.
                JobManager.AddJob(async () => await PlaytestStartingInTask(), s => s
                    .WithName("[Playtest1Hour]").ToRunOnceAt(startDateTime.Value.AddMinutes(-60.2)));

                _ = _log.LogMessage("1 hour playtest announcement scheduled for:" +
                                    $"\n{JobManager.GetSchedule("[Playtest1Hour]").NextRun}", false,
                    color: LOG_COLOR);
            }

            if (startDateTime != null && DateTime.Compare(DateTime.Now.AddMinutes(15), startDateTime.Value) < 0)
            {
                JobManager.AddJob(async () => await PlaytestFifteenMinuteTask(), s => s
                    .WithName("[Playtest15Minute]").ToRunOnceAt(startDateTime.Value.AddMinutes(-15)));

                _ = _log.LogMessage("15 minute playtest announcement scheduled for:" +
                                    $"\n{JobManager.GetSchedule("[Playtest15Minute]").NextRun}", false,
                    color: LOG_COLOR);
            }

            if (startDateTime != null && DateTime.Compare(DateTime.Now.AddMinutes(20), startDateTime.Value) < 0)
            {
                JobManager.AddJob(async () => await PlaytestTwentyMinuteTask(), s => s
                    .WithName("[Playtest20Minute]").ToRunOnceAt(startDateTime.Value.AddMinutes(-20)));

                _ = _log.LogMessage("20 minute playtest announcement scheduled for:" +
                                    $"\n{JobManager.GetSchedule("[Playtest20Minute]").NextRun}", false,
                    color: LOG_COLOR);
            }

            if (startDateTime != null && DateTime.Compare(DateTime.Now, startDateTime.Value) < 0)
            {
                JobManager.AddJob(async () => await PlaytestStartingTask(), s => s
                    .WithName("[PlaytestStarting]").ToRunOnceAt(startDateTime.Value));

                _ = _log.LogMessage("Starting playtest announcement scheduled for:" +
                                    $"\n{JobManager.GetSchedule("[PlaytestStarting]").NextRun}", false,
                    color: LOG_COLOR);
            }
        }

        /// <summary>
        ///     Posts a new announcement message and alerts playtester role
        /// </summary>
        /// <returns></returns>
        public async Task PlaytestStartingInTask()
        {
            _ = _log.LogMessage("Running playtesting starting in X minutes task...", true, color: LOG_COLOR);

            //Ensure server is awake and RCON connection is established. Run other things while waking server
            _ = _rconService.WakeRconServer(_calendar.GetTestEventNoUpdate().ServerLocation);
            
            //Setup the log receiver for this test.
            _ = Task.Run(async () =>
            {
                _logReceiverService.StopLogReceiver();

                //Log receiver takes time to stop before it can be restarted.
                await Task.Delay(2000);
                _logReceiverService.StartLogReceiver(_calendar.GetTestEventNoUpdate().ServerLocation);
            });
            
            //Disable reservations on servers
            await _reservationService.DisableReservations();

            //Start asking the server for player counts.
            _dataService.IncludePlayerCount = true;

            //Start asking for player counts
            JobManager.AddJob(
                async () => await _rconService.GetPlayCountFromServer(
                    GeneralUtil.GetServerCode(_calendar.GetTestEventNoUpdate().ServerLocation)),
                s => s.WithName("[QueryPlayerCount]").ToRunEvery(60).Seconds());

            //Figure out how long until the event starts
            var countdown = _calendar.GetTestEventNoUpdate().StartDateTime.GetValueOrDefault().Subtract(DateTime.Now);
            var countdownString =
                countdown.ToString("d'D 'h' Hour 'm' Minutes'").TrimStart(' ', 'D', 'H', 'o', 'u', 'r', '0')
                    .Replace(" 0 Minutes", "");

            var mentionRole = _dataService.PlayTesterRole;
            var unsubInfo = "";

            //Handle comp or casual
            if (_calendar.GetTestEvent().IsCasual)
            {
                await _rconService.RconCommand(_calendar.GetTestEventNoUpdate().ServerLocation,
                    $"sv_cheats 0; sv_password {_dataService.RSettings.General.CasualPassword}");
                unsubInfo = "\nType `>playtester` to stop getting these notifications.";
            }
            else
            {
                foreach (var creator in _calendar.GetTestEventNoUpdate().Creators)
                {
                    try
                    {
                        var user = _dataService.GetSocketGuildUser(creator.Id);
                        if (user.Roles.All(x => x.Id != _dataService.CompetitiveTesterRole.Id))
                        {
                            await _log.LogMessage($"{user} ID:{user.Id} does not have competitive tester role for this comp test. Applying.");
                            await user.AddRoleAsync(_dataService.CompetitiveTesterRole);
                        }
                    }
                    catch
                    {}
                }

                mentionRole = _dataService.CompetitiveTesterRole;

                await _dataService.CompetitiveTestingChannel.SendMessageAsync(embed: new EmbedBuilder()
                    .WithAuthor(_calendar.GetTestEventNoUpdate().Title)
                    .AddField("Connect Information",
                        $"`connect {_calendar.GetTestEventNoUpdate().ServerLocation}; password {_calendar.GetTestEventNoUpdate().CompPassword}`")
                    .WithColor(new Color(55, 55, 165))
                    .Build());

                await _rconService.RconCommand(_calendar.GetTestEventNoUpdate().ServerLocation,
                    $"sv_cheats 0; sv_password {_calendar.GetTestEventNoUpdate().CompPassword}");
            }

            //Skip the alert.
            if (!PlaytestStartAlert)
            {
                PlaytestStartAlert = true;
                return;
            }

            await mentionRole.ModifyAsync(x => { x.Mentionable = true; });
            await _dataService.TestingChannel.SendMessageAsync($"Heads up {mentionRole.Mention}! " +
                                                               $"There is a playtest starting in {countdownString}." +
                                                               $"{unsubInfo}",
                embed: _announcementMessage.CreatePlaytestEmbed(_calendar.GetTestEventNoUpdate().IsCasual,
                    true, PlaytestAnnouncementMessage.Id));
            await mentionRole.ModifyAsync(x => { x.Mentionable = false; });

            //DM users about their test
            foreach (var creator in _calendar.GetTestEventNoUpdate().Creators)
                try
                {
                    await creator.SendMessageAsync(
                        $"Don't forget that you have a playtest for __**{_calendar.GetTestEventNoUpdate().Title}**__ in __**{countdownString}**__");
                }
                catch
                {
                    //Could not DM creator about their test.
                }
        }

        /// <summary>
        ///     Server setup tasks for 20 minutes before a test
        /// </summary>
        /// <returns></returns>
        private async Task PlaytestTwentyMinuteTask()
        {
            //Ensure server is awake and RCON connection is established. Run other things while waking server
            _ = _rconService.WakeRconServer(_calendar.GetTestEventNoUpdate().ServerLocation);

            //Ensure server is awake and RCON connection is established.
            await _rconService.WakeRconServer(_calendar.GetTestEventNoUpdate().ServerLocation);

            _ = _log.LogMessage("Running playtesting starting in 20 minutes task...", true, color: LOG_COLOR);
           _logReceiverService.StartLogReceiver(_calendar.GetTestEventNoUpdate().ServerLocation);
            await _rconService.RconCommand(GeneralUtil.GetServerCode(_calendar.GetTestEventNoUpdate().ServerLocation),
               $"host_workshop_map {GeneralUtil.GetWorkshopIdFromFqdn(_calendar.GetTestEventNoUpdate().WorkshopLink.ToString())}");

           //Setup the mirror server for comp
           if (!_calendar.GetTestEvent().IsCasual)
           {
               //Setup a casual server for people who aren't in the comp test group
               await _rconService.RconCommand(
                   GeneralUtil.GetServerCode(_calendar.GetTestEventNoUpdate().CompCasualServer),
                   $"host_workshop_map {GeneralUtil.GetWorkshopIdFromFqdn(_calendar.GetTestEventNoUpdate().WorkshopLink.ToString())}");

               //Delay before setting password again.
               await Task.Delay(5000);

               await _rconService.RconCommand(_calendar.GetTestEventNoUpdate().ServerLocation,
                   $"sv_password {_calendar.GetTestEventNoUpdate().CompPassword}");
            }
        }

        /// <summary>
        ///     Server setup tasks for 15 minutes before a test
        /// </summary>
        /// <returns></returns>
        private async Task PlaytestFifteenMinuteTask()
        {
            _ = _log.LogMessage("Running playtesting starting in 15 minutes task...", true, color: LOG_COLOR);

            //Ensure server is awake and RCON connection is established. Run other things while waking server
            _ = _rconService.WakeRconServer(_calendar.GetTestEventNoUpdate().ServerLocation);

            //Disable reservations on servers
            await _reservationService.DisableReservations();

            _logReceiverService.StartLogReceiver(_calendar.GetTestEventNoUpdate().ServerLocation);

            //Start the log listener for users to give feedback before the test starts.
            var gameMode = _calendar.GetTestEventNoUpdate().IsCasual ? "casual" : "comp";
            _logReceiverService.EnableFeedback($"{_calendar.GetTestEventNoUpdate().StartDateTime:MM_dd_yyyy}" +
                                               $"_{_calendar.GetTestEventNoUpdate().Title.Substring(0, _calendar.GetTestEventNoUpdate().Title.IndexOf(' '))}" +
                                               $"_{gameMode}");

            var embed = new EmbedBuilder()
                .WithAuthor($"Settings up test server for {_calendar.GetTestEventNoUpdate().Title}")
                .WithTitle("Workshop Link")
                .WithUrl(_calendar.GetTestEventNoUpdate().WorkshopLink.ToString())
                .WithThumbnailUrl(_calendar.GetTestEventNoUpdate().CanUseGallery
                    ? _calendar.GetTestEventNoUpdate().GalleryImages[0]
                    : _dataService.RSettings.General.FallbackTestImageUrl)
                .WithDescription(
                    $"{DatabaseUtil.GetTestServer(_calendar.GetTestEventNoUpdate().ServerLocation).Description}" +
                    $"\n{_calendar.GetTestEventNoUpdate().Description}")
                .WithColor(new Color(51, 100, 173));

            //Set password as needed, again just in case RCON wasn't listening / server wasn't ready.
            if (_calendar.GetTestEvent().IsCasual)
            {
                await _rconService.RconCommand(_calendar.GetTestEventNoUpdate().ServerLocation,
                    $"sv_password {_dataService.RSettings.General.CasualPassword}");

                embed.AddField("Connect To",
                    $"`connect {_calendar.GetTestEventNoUpdate().ServerLocation}; password {_dataService.RSettings.General.CasualPassword}`");
            }
            else
            {
                await _rconService.RconCommand(_calendar.GetTestEventNoUpdate().ServerLocation,
                    $"sv_password {_calendar.GetTestEventNoUpdate().CompPassword}");

                //Delay to make sure level has actually changed
                await Task.Delay(10000);
                await _rconService.RconCommand(
                    GeneralUtil.GetServerCode(_calendar.GetTestEventNoUpdate().CompCasualServer),
                    $"exec {_dataService.RSettings.General.PostgameConfig}; sv_password {_dataService.RSettings.General.CasualPassword}; bot_stop 1");
            }
            
            //Delay to make sure level has actually changed
            await Task.Delay(10000);
            await _rconService.RconCommand(GeneralUtil.GetServerCode(_calendar.GetTestEventNoUpdate().ServerLocation),
                $"exec {_dataService.RSettings.General.PostgameConfig}; bot_stop 1");

            await _dataService.TestingChannel.SendMessageAsync(embed: embed.Build());
        }

        /// <summary>
        ///     Announcement for playtest starting
        /// </summary>
        /// <returns></returns>
        private async Task PlaytestStartingTask()
        {
            _ = _log.LogMessage("Running playtesting starting now task...", false, color: LOG_COLOR);

            //Disable reservations on servers
            await _reservationService.DisableReservations();

            var mentionRole = _dataService.PlayTesterRole;
            var unsubInfo = "";
            //Handle comp or casual
            if (_calendar.GetTestEvent().IsCasual)
            {
                await _rconService.RconCommand(_calendar.GetTestEventNoUpdate().ServerLocation,
                    $"sv_password {_dataService.RSettings.General.CasualPassword}");
                unsubInfo = "\nType `>playtester` to stop getting these notifications.";
            }
            else
            {
                await _rconService.RconCommand(_calendar.GetTestEventNoUpdate().ServerLocation,
                    $"sv_password {_calendar.GetTestEventNoUpdate().CompPassword}");
                mentionRole = _dataService.CompetitiveTesterRole;
            }

            //Skip the alert.
            if (!PlaytestStartAlert)
            {
                PlaytestStartAlert = true;
                return;
            }

            await mentionRole.ModifyAsync(x => { x.Mentionable = true; });

            await _dataService.TestingChannel.SendMessageAsync($"Heads up {mentionRole.Mention}! " +
                                                               $"There is a playtest starting __now__! {unsubInfo}",
                embed: _announcementMessage.CreatePlaytestEmbed(_calendar.GetTestEventNoUpdate().IsCasual,
                    true, PlaytestAnnouncementMessage.Id));

            await mentionRole.ModifyAsync(x => { x.Mentionable = false; });
        }

        public async Task CallNormalTesters(int neededPlayers)
        {
            await _dataService.PlayTesterRole.ModifyAsync(x => { x.Mentionable = true; });

            await _dataService.TestingChannel.SendMessageAsync(
                $"Currently looking for {neededPlayers} players. {_dataService.PlayTesterRole.Mention}",
                embed: _announcementMessage.CreatePlaytestEmbed(_calendar.GetTestEventNoUpdate().IsCasual,
                    true, PlaytestAnnouncementMessage.Id));

            await _dataService.PlayTesterRole.ModifyAsync(x => { x.Mentionable = false; });
        }

        /// <summary>
        /// Gets the current running level, and workshop ID from a test server.
        /// If array.length == 3 it is a workshop map, with the ID in [1] and map name in [2]
        /// Otherwise it is a stock level with the name in [0]
        /// </summary>
        /// <param name="server">Server to query</param>
        /// <returns>An array populated with the result.</returns>
        public async Task<string[]> GetRunningLevelAsync(string server)
        {
            var reply = await _rconService.RconCommand(server, "host_map");
            reply = reply.Substring(14, reply.IndexOf(".bsp", StringComparison.Ordinal) - 14);
            return reply.Split('/');
        }
    }
}