using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using BotHATTwaffle2.Handlers;
using BotHATTwaffle2.Models.LiteDB;
using BotHATTwaffle2.Services.Calendar;
using BotHATTwaffle2.Services.Calendar.PlaytestEvents;
using BotHATTwaffle2.Services.SRCDS;
using BotHATTwaffle2.Util;
using Discord;
using Discord.WebSocket;
using FluentScheduler;

namespace BotHATTwaffle2.Services.Playtesting
{
    public class PlaytestService
    {
        private const ConsoleColor LOG_COLOR = ConsoleColor.Cyan;
        private static AnnouncementMessage _announcementMessage;

        private static readonly Dictionary<PlaytestEvent.Games, DateTime> _knownTests =
            new Dictionary<PlaytestEvent.Games, DateTime>();

        private readonly GoogleCalendar _calendar;
        private readonly DiscordSocketClient _client;
        private readonly DataService _dataService;
        private readonly int _failedRetryCount = 60;
        private readonly LogHandler _log;
        private readonly LogReceiverService _logReceiverService;
        private readonly RconService _rconService;
        private readonly ReservationService _reservationService;
        private int _failedToFetch;
        public bool PlaytestStartAlert = true;

        public PlaytestService(DataService data, GoogleCalendar calendar, LogHandler log, Random random,
            ReservationService reservationService, RconService rconService, LogReceiverService logReceiverService,
            DiscordSocketClient client)
        {
            _dataService = data;
            _log = log;
            _calendar = calendar;
            _reservationService = reservationService;
            _logReceiverService = logReceiverService;
            _client = client;

            _rconService = rconService;
            _announcementMessage = new AnnouncementMessage(_calendar, _dataService, random, _log);


            _logReceiverService.SetPlayTestService(this);
        }

        public VoiceFeedbackSession FeedbackSession { get; private set; }

        public void ResetCommandRunningFlag()
        {
            _calendar.GetNextPlaytestEvent().PlaytestCommandRunning = false;
        }

        public bool PlaytestCommandPreCheck()
        {
            return _calendar.GetNextPlaytestEvent().PlaytestCommandPreCheck();
        }

        /// <summary>
        ///     Creates a new feedback session for a playtest
        /// </summary>
        /// <returns>True if created, false otherwise</returns>
        public bool CreateVoiceFeedbackSession()
        {
            var testEvent = _calendar.GetNextPlaytestEvent();

            if (FeedbackSession != null || !testEvent.IsValid || testEvent.Game != PlaytestEvent.Games.CSGO)
                return false;

            FeedbackSession = new VoiceFeedbackSession(_dataService, _client, testEvent, _rconService);
            return true;
        }

        /// <summary>
        ///     Ends the active feedback session
        /// </summary>
        /// <returns>True if successful, false otherwise</returns>
        public bool EndVoiceFeedbackSession()
        {
            try
            {
                if (FeedbackSession == null)
                    return false;

                FeedbackSession.Dispose();
                FeedbackSession = null;
            }
            catch (Exception e)
            {
                _ = _log.LogMessage($"Something happened when ending a feedback session\n{e}");
            }

            return true;
        }


        public async Task<PlaytestCommandInfo> PlaytestcommandGenericAction(bool replyInContext, string command,
            string message = null)
        {
            var testEvent = _calendar.GetNextPlaytestEvent();
            await testEvent.PlaytestcommandGenericAction(replyInContext, command, _rconService, message);
            return testEvent.PlaytestCommandInfo;
        }

        public async Task<PlaytestCommandInfo> PlaytestCommandPost(bool replyInContext)
        {
            //Test over - stop asking for player counts.
            JobManager.RemoveJob("[QueryPlayerCount]");

            var testEvent = _calendar.GetNextPlaytestEvent();

            await testEvent.PlaytestCommandPost(replyInContext, _logReceiverService, _rconService);

            //Delay setting previous test event to prevent playtest channel from getting out of order.
            _ = Task.Run(async () =>
            {
                await Task.Delay(5 * 60 * 1000);
                _calendar.SetPreviousPlaytestEvent(testEvent);
            });

            return testEvent.PlaytestCommandInfo;
        }

        public async Task<PlaytestCommandInfo> PlaytestCommandStart(bool replyInContext)
        {
            var testEvent = _calendar.GetNextPlaytestEvent();

            await testEvent.PlaytestCommandStart(replyInContext, _rconService);

            return testEvent.PlaytestCommandInfo;
        }

        public async Task<PlaytestCommandInfo> PlaytestCommandPre(bool replyInContext)
        {
            var testEvent = _calendar.GetNextPlaytestEvent();

            await testEvent.PlaytestCommandPre(replyInContext, _logReceiverService, _rconService);

            return testEvent.PlaytestCommandInfo;
        }

        /// <summary>
        ///     Starts the chain of events to post a new announcement message.
        ///     If a valid existing message can be used, it will be used instead.
        /// </summary>
        /// <returns></returns>
        public async Task PostOrUpdateAnnouncement(string game)
        {
            //Get event, required for posting new / updating
            //Abort if the test isn't valid
            //Clean up old message if required
            //Check old message, required for fresh boot with empty collection in db
            var testEvent = _calendar.GetNextPlaytestEvent(game);

            if (testEvent == null)
            {
                if (_dataService.RSettings.ProgramSettings.Debug)
                    _ = _log.LogMessage($"Failure running PostOrUpdateAnnouncement for {game}!\nNo test found.", false,
                        color: LOG_COLOR);
                return;
            }

            if (!testEvent.IsValid)
            {
                if (_dataService.RSettings.ProgramSettings.Debug)
                    _ = _log.LogMessage($"No valid test for {game} was found!", false, color: LOG_COLOR);

                if (testEvent.AnnouncementMessage != null)
                {
                    if (_dataService.RSettings.ProgramSettings.Debug)
                        _ = _log.LogMessage($"Attempting to deleted outdated announcement for {game}", false,
                            color: LOG_COLOR);
                    try
                    {
                        await testEvent.AnnouncmentChannel.DeleteMessageAsync(testEvent.AnnouncementMessage);
                    }
                    catch
                    {
                        _ = _log.LogMessage(
                            $"Failed to delete outdated playtest message for {game}. It may have been deleted manually",
                            false, color: LOG_COLOR);
                    }
                }

                testEvent.SetAnnouncementMessage(null);

                return;
            }

            if (_dataService.RSettings.ProgramSettings.Debug)
                _ = _log.LogMessage("Posting or updating playtest announcement", false, color: LOG_COLOR);

            if (testEvent.AnnouncementMessage == null)
                await PostNewAnnouncement(testEvent);
            else
                await UpdateAnnouncementMessage(testEvent);
        }

        /// <summary>
        ///     Builds an embed that shows upcoming playtest events and events from the queue
        /// </summary>
        /// <param name="getSchedule">Get events from Queue</param>
        /// <param name="getCalendar">Get events from Calendar</param>
        /// <returns>Built embed with events</returns>
        public async Task<EmbedBuilder> GetUpcomingEvents(bool getSchedule, bool getCalendar)
        {
            var author = "Current ";
            var embed = new EmbedBuilder().WithColor(new Color(55, 55, 165))
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
                    embed.AddField("No playtest requests found!", "Submit your own with: `>request`");
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
                                info += $"`{user}`, ";
                            else
                                info += $"Could not get user `{creator}`, ";
                        }

                        info = info.Trim(',', ' ');
                        info += $"\nGame: `{testQueue[i].Game}`" +
                                $"\nRequested Time: `{testQueue[i].TestDate:ddd, MMM d, HH:mm}`" +
                                $"\n[Map Images]({testQueue[i].ImgurAlbum}) - " +
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
                if (testEvents.Items.Count == 0)
                    embed.AddField("No scheduled playtests found!", "Submit yours with: `>request`");
                else
                    foreach (var item in testEvents.Items)
                    {
                        if (embed.Fields.Count >= 24)
                            break;

                        //Get the moderator for each test
                        var strippedHtml = item.Description.Replace("<br>", "\n").Replace("&nbsp;", "");
                        strippedHtml = Regex.Replace(strippedHtml, "<.*?>", string.Empty);
                        var description = strippedHtml.Trim().Split('\n')
                            .Select(line => line.Substring(line.IndexOf(':') + 1).Trim()).ToImmutableArray();
                        var mod = _dataService.GetSocketUser(description.ElementAtOrDefault(4));

                        embed.AddField($"{item.Summary} - {description.ElementAtOrDefault(3)}",
                            $"`Scheduled`\nStart Time: `{item.Start.DateTime:ddd, MMM d, HH:mm}`\nEnd Time: `{item.End.DateTime:ddd, MMM d, HH:mm}`\nModerator: {mod.Mention}",
                            true);
                    }
            }

            if (embed.Fields.Count >= 24)
                embed.AddField("Max Fields Added",
                    "Somehow there are more items than Discord embeds allow. Some items omitted.");

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
        private async Task UpdateAnnouncementMessage(PlaytestEvent playtestEvent)
        {
            if (_dataService.RSettings.ProgramSettings.Debug)
                _ = _log.LogMessage($"Updating playtest announcement for {playtestEvent.Title}", false,
                    color: LOG_COLOR);

            try
            {
                //Compare the current title and the last known title.
                if (_knownTests.ContainsKey(playtestEvent.Game) &&
                    playtestEvent.EventEditTime.Value.Equals(_knownTests[playtestEvent.Game]))
                {
                    await playtestEvent.AnnouncementMessage.ModifyAsync(x =>
                    {
                        x.Embed = _announcementMessage.CreatePlaytestEmbed(playtestEvent);
                    });
                    _failedToFetch = 0;
                }
                else
                {
                    //Being in this else means we know the message is different, remake it.
                    await playtestEvent.AnnouncmentChannel.DeleteMessageAsync(playtestEvent.AnnouncementMessage);
                    await PostNewAnnouncement(playtestEvent);
                }
            }
            catch
            {
                //Have we failed enough to rebuild?
                if (_failedToFetch >= _failedRetryCount)
                {
                    _ = _log.LogMessage($"Tried to update announcement message {_failedToFetch} times, but failed." +
                                        "\nCreated a new message next time.", false, color: LOG_COLOR);
                    playtestEvent.SetAnnouncementMessage(null);
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
        ///     WTF is this method name. God you suck.
        /// </summary>
        /// <returns></returns>
        private void AllowReservationsStopCount()
        {
            //Stop asking server for player counts
            _dataService.SetIncludePlayerCount(false);
            _dataService.SetPlayerCount("0");
            //We posted a new announcement, meaning we can allow reservations again.
            _reservationService.AllowReservations();
        }

        /// <summary>
        ///     Posts a new playtest announcement
        /// </summary>
        /// <returns></returns>
        private async Task PostNewAnnouncement(PlaytestEvent testEvent)
        {
            if (_dataService.RSettings.ProgramSettings.Debug)
                _ = _log.LogMessage($"Posting new announcement for {testEvent.Title}", false, color: LOG_COLOR);

            //Delay allowing reservations. This is because >p post creates a new announcement.
            //As a result people would normally be able to reserve as soon as a test is over.
            JobManager.AddJob(AllowReservationsStopCount, s => s
                .WithName("[AllowReservationsStopCount]").ToRunOnceIn(30).Minutes());

            _ = _log.LogMessage("AllowReservationsStopCount scheduled for:" +
                                $"\n{JobManager.GetSchedule("[AllowReservationsStopCount]").NextRun}", false,
                color: LOG_COLOR);

            try
            {
                //Make the announcement and store to a variable
                var playtestAnnouncementMessage = await testEvent.AnnouncmentChannel.SendMessageAsync(
                    embed: _announcementMessage.CreatePlaytestEmbed(testEvent));

                //Hand off the message and time to be stored in the DB for use on restarts
                DatabaseUtil.StoreAnnouncement(playtestAnnouncementMessage, testEvent.Title, testEvent.Game.ToString());

                SchedulePlaytestAnnouncements(testEvent);

                testEvent.SetAnnouncementMessage(playtestAnnouncementMessage);

                //Store the titles along with the game key so we can check against them later.
                if (_knownTests.ContainsKey(testEvent.Game))
                    _knownTests.Remove(testEvent.Game);

                _knownTests.Add(testEvent.Game, testEvent.EventEditTime.Value);
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
            await TryAttach("csgo");
            await TryAttach("tf2");

            async Task TryAttach(string game)
            {
                var testEvent = _calendar.GetNextPlaytestEvent(game);

                if (testEvent == null)
                {
                    if (_dataService.RSettings.ProgramSettings.Debug)
                        _ = _log.LogMessage($"No test to reattach to for {game}.", false, color: LOG_COLOR);

                    return;
                }

                //Get the last known message
                var oldMessage = DatabaseUtil.GetAnnouncementMessage(testEvent.Game);

                //No message found in the DB, do nothing. Likely to happen when DB is new.
                if (oldMessage == null)
                {
                    if (_dataService.RSettings.ProgramSettings.Debug)
                        _ = _log.LogMessage($"No message found in DB to reattach to for {game}", false,
                            color: LOG_COLOR);

                    return;
                }

                //Make sure a test is valid
                if (!testEvent.IsValid)
                {
                    if (_dataService.RSettings.ProgramSettings.Debug)
                        _ = _log.LogMessage($"No valid test found to post for {game}", false, color: LOG_COLOR);

                    return;
                }

                _ = _log.LogMessage("Attempting to get old announcement message\n" +
                                    $"ID: {oldMessage.AnnouncementId} with title {oldMessage.Title}",
                    false, color: LOG_COLOR);


                var eventTitle = testEvent.Title;
                if (eventTitle != null && eventTitle.Equals(oldMessage.Title))
                {
                    try
                    {
                        testEvent.SetAnnouncementMessage(
                            await testEvent.AnnouncmentChannel.GetMessageAsync(oldMessage.AnnouncementId) as
                                IUserMessage);

                        if (testEvent.AnnouncementMessage != null)
                            _ = _log.LogMessage($"Retrieved old announcement for: {testEvent.Title}", false,
                                color: LOG_COLOR);
                    }
                    catch
                    {
                        _ = _log.LogMessage("Unable to retrieve old announcement message!", false, color: LOG_COLOR);
                    }

                    //Store the titles along with the game key so we can check against them later.
                    if (_knownTests.ContainsKey(testEvent.Game))
                        _knownTests.Remove(testEvent.Game);

                    _knownTests.Add(testEvent.Game, testEvent.EventEditTime.Value);
                }
                else
                {
                    _ = _log.LogMessage("Messages do not match, deleting old message", false, color: LOG_COLOR);
                    try
                    {
                        await testEvent.AnnouncmentChannel.DeleteMessageAsync(oldMessage.AnnouncementId);
                        testEvent.SetAnnouncementMessage(null);
                    }
                    catch
                    {
                        _ = _log.LogMessage("Could not delete old message - it was likely deleted manually",
                            false, color: LOG_COLOR);
                    }
                }
            }
        }

        public void ClearScheduledAnnouncements(string game)
        {
            JobManager.RemoveJob($"[Playtest1Hour_{game}]");
            JobManager.RemoveJob($"[Playtest15Minute_{game}]");
            JobManager.RemoveJob($"[Playtest20Minute_{game}]");
            JobManager.RemoveJob($"[PlaytestStarting_{game}]");
            JobManager.RemoveJob($"[QueryPlayerCount_{game}]");
        }

        public void ScheduleAllPlaytestAnnouncements()
        {
            SchedulePlaytestAnnouncements(_calendar.GetNextPlaytestEvent("csgo"));
            SchedulePlaytestAnnouncements(_calendar.GetNextPlaytestEvent("tf2"));
        }

        public void SchedulePlaytestAnnouncements(PlaytestEvent testEvent)
        {
            //If the test is null, likely only when first starting, abort.
            if (testEvent == null)
                return;

            if (_dataService.RSettings.ProgramSettings.Debug)
                _ = _log.LogMessage($"SchedulePlaytestAnnouncements for {testEvent.Title}", false, color: LOG_COLOR);

            var game = testEvent.Game.ToString();

            //Clear old jobs, if any.
            ClearScheduledAnnouncements(game);

            if (!testEvent.IsValid)
                return;

            var startDateTime = testEvent.StartDateTime;
            _ = _log.LogMessage($"Playtest of {testEvent.Title} scheduled for: {startDateTime}", false,
                color: LOG_COLOR);

            if (startDateTime != null && DateTime.Compare(DateTime.Now.AddMinutes(60), startDateTime.Value) < 0)
            {
                //Subtract 60.2 minutes. If .2 isn't added the announcement states wrong time.
                JobManager.AddJob(async () => await PlaytestStartingInTask(testEvent), s => s
                    .WithName($"[Playtest1Hour_{game}]").ToRunOnceAt(startDateTime.Value.AddMinutes(-60.2)));

                _ = _log.LogMessage("1 hour playtest announcement scheduled for:" +
                                    $"\n{JobManager.GetSchedule($"[Playtest1Hour_{game}]").NextRun}", false,
                    color: LOG_COLOR);
            }

            if (startDateTime != null && DateTime.Compare(DateTime.Now.AddMinutes(20), startDateTime.Value) < 0)
            {
                JobManager.AddJob(async () => await PlaytestTwentyMinuteTask(testEvent), s => s
                    .WithName($"[Playtest20Minute_{game}]").ToRunOnceAt(startDateTime.Value.AddMinutes(-20)));

                _ = _log.LogMessage("20 minute playtest announcement scheduled for:" +
                                    $"\n{JobManager.GetSchedule($"[Playtest20Minute_{game}]").NextRun}", false,
                    color: LOG_COLOR);
            }

            if (startDateTime != null && DateTime.Compare(DateTime.Now.AddMinutes(15), startDateTime.Value) < 0)
            {
                JobManager.AddJob(async () => await PlaytestFifteenMinuteTask(testEvent), s => s
                    .WithName($"[Playtest15Minute_{game}]").ToRunOnceAt(startDateTime.Value.AddMinutes(-15)));

                _ = _log.LogMessage("15 minute playtest announcement scheduled for:" +
                                    $"\n{JobManager.GetSchedule($"[Playtest15Minute_{game}]").NextRun}", false,
                    color: LOG_COLOR);
            }

            if (startDateTime != null && DateTime.Compare(DateTime.Now, startDateTime.Value) < 0)
            {
                JobManager.AddJob(async () => await PlaytestStartingTask(testEvent), s => s
                    .WithName($"[PlaytestStarting_{game}]").ToRunOnceAt(startDateTime.Value));

                _ = _log.LogMessage("Starting playtest announcement scheduled for:" +
                                    $"\n{JobManager.GetSchedule($"[PlaytestStarting_{game}]").NextRun}", false,
                    color: LOG_COLOR);
            }
        }


        /// <summary>
        ///     Posts a new announcement message and alerts playtester role
        /// </summary>
        /// <returns></returns>
        public async Task PlaytestStartingInTask(PlaytestEvent playtestEvent)
        {
            _ = _log.LogMessage($"Running playtesting starting in X minutes task for {playtestEvent.Title}", true,
                color: LOG_COLOR);

            await playtestEvent.PlaytestStartingInTask(_rconService, _logReceiverService, _announcementMessage);
        }

        /// <summary>
        ///     Server setup tasks for 20 minutes before a test
        /// </summary>
        /// <returns></returns>
        private async Task PlaytestTwentyMinuteTask(PlaytestEvent playtestEvent)
        {
            _ = _log.LogMessage($"Running playtesting starting in 20 minutes task for {playtestEvent.Title}", true,
                color: LOG_COLOR);

            await _reservationService.DisableReservations();
            JobManager.RemoveJob("[AllowReservationsStopCount]");

            await playtestEvent.PlaytestTwentyMinuteTask(_rconService, _logReceiverService);
        }

        /// <summary>
        ///     Server setup tasks for 15 minutes before a test
        /// </summary>
        /// <returns></returns>
        private async Task PlaytestFifteenMinuteTask(PlaytestEvent playtestEvent)
        {
            _ = _log.LogMessage("Running playtesting starting in 15 minutes task...", true, color: LOG_COLOR);

            await _reservationService.DisableReservations();
            JobManager.RemoveJob("[AllowReservationsStopCount]");

            await playtestEvent.PlaytestFifteenMinuteTask(_rconService, _logReceiverService);
        }

        /// <summary>
        ///     Announcement for playtest starting
        /// </summary>
        /// <returns></returns>
        private async Task PlaytestStartingTask(PlaytestEvent playtestEvent)
        {
            _ = _log.LogMessage("Running playtesting starting now task...", false, color: LOG_COLOR);

            await _reservationService.DisableReservations();
            JobManager.RemoveJob("[AllowReservationsStopCount]");

            await playtestEvent.PlaytestStartingTask(_rconService, _logReceiverService, _announcementMessage);
        }

        public async Task CallNormalTesters(int neededPlayers)
        {
            var testEvent = _calendar.GetNextPlaytestEvent();

            await testEvent.TesterRole.ModifyAsync(x => { x.Mentionable = true; });

            await testEvent.TestingChannel.SendMessageAsync(
                $"Currently looking for **{neededPlayers}** players. {testEvent.TesterRole.Mention}\n" +
                "Type `>playtester` to stop getting all playtest notifications.",
                embed: _announcementMessage.CreatePlaytestEmbed(testEvent,
                    true, testEvent.AnnouncementMessage.Id));

            await testEvent.TesterRole.ModifyAsync(x => { x.Mentionable = false; });
        }
    }
}