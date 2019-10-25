using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BotHATTwaffle2.Handlers;
using BotHATTwaffle2.Models.LiteDB;
using BotHATTwaffle2.Services.Calendar.PlaytestEvents;
using Discord.WebSocket;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Calendar.v3;
using Google.Apis.Calendar.v3.Data;
using Google.Apis.Services;
using Google.Apis.Util.Store;

namespace BotHATTwaffle2.Services.Calendar
{
    public class GoogleCalendar
    {
        private const ConsoleColor LOG_COLOR = ConsoleColor.Gray;

        private static List<PlaytestEvent> _playtestEvents;

        private static CsgoPlaytestEvent _activeCsgoPlaytestEvent;
        private static Tf2PlaytestEvent _activeTf2PlaytestEvent;
        private static PlaytestEvent _previousPlaytestEvent;
        private readonly CalendarService _calendar;
        private readonly DataService _dataService;
        private readonly LogHandler _log;

        public GoogleCalendar(DataService dataService, LogHandler log)
        {
            _log = log;
            _dataService = dataService;
            Console.Write("Getting or checking Calendar OAuth Credentials... ");
            _calendar = new CalendarService(new BaseClientService.Initializer
            {
                HttpClientInitializer = GetCalendarCredentials(),
                ApplicationName = "BotHATTwaffle 2"
            });
            Console.WriteLine("Done!");
        }

        /// <summary>
        ///     Retrieves a credential for the Google Calendar API.
        /// </summary>
        /// <remarks>
        ///     The secret is read from <c>client_secret.json</c> located in the executables directory.
        /// </remarks>
        /// <remarks>
        ///     The token is stored in a file in <c>/credentials/calendar-dotnet-quickstart.json</c> in
        ///     <see cref="Environment.SpecialFolder.Personal" />.
        /// </remarks>
        /// <returns>The retrieved OAuth 2.0 credential.</returns>
        private UserCredential GetCalendarCredentials()
        {
            using (var stream = new FileStream("client_secret.json", FileMode.Open, FileAccess.Read))
            {
                return GoogleWebAuthorizationBroker.AuthorizeAsync(
                        GoogleClientSecrets.Load(stream).Secrets,
                        new[] {CalendarService.Scope.Calendar},
                        "user",
                        CancellationToken.None,
                        new FileDataStore(".credentials/calendar.json"))
                    .Result;
            }
        }

        public void SetPreviousPlaytestEvent(PlaytestEvent playtestEvent)
        {
            _previousPlaytestEvent = playtestEvent;
        }

        public async Task UpdateTestEventCache()
        {
            if (_dataService.RSettings.ProgramSettings.Debug)
                _ = _log.LogMessage("Getting test events", false, color: LOG_COLOR);

            // Defines request and parameters.
            var request = _calendar.Events.List(_dataService.RSettings.ProgramSettings.TestCalendarId);

            request.Q = " by "; // This will limit all search requests to ONLY get playtest events.

            request.ShowDeleted = false;
            request.SingleEvents = true;
            request.TimeMin = DateTime.Now;
            request.TimeMax = DateTime.Now.AddMonths(2);
            request.OrderBy = EventsResource.ListRequest.OrderByEnum.StartTime;

            // Executes the request for events and retrieves the first event in the resulting items.
            var requestResult = await request.ExecuteAsync();
            var eventItems = requestResult.Items;

            var tempPlaytestEvents = new List<PlaytestEvent>();

            //Prase into correct object types
            foreach (var eventItem in eventItems)
            {
                if (eventItem.Summary.StartsWith("csgo", StringComparison.OrdinalIgnoreCase))
                    tempPlaytestEvents.Add(new CsgoPlaytestEvent(_dataService, _log, eventItem));

                if (eventItem.Summary.StartsWith("tf2", StringComparison.OrdinalIgnoreCase))
                    tempPlaytestEvents.Add(new Tf2PlaytestEvent(_dataService, _log, eventItem));
            }

            if (tempPlaytestEvents.Count == 0)
            {
                _playtestEvents = null;

                if (_dataService.RSettings.ProgramSettings.Debug)
                    _ = _log.LogMessage("No events found.", false, color: LOG_COLOR);

                return;
            }

            //Prevent the previous playtest event from counting as another playtest.
            foreach (var tempPlaytestEvent in tempPlaytestEvents)
                if (tempPlaytestEvent.Equals(_previousPlaytestEvent))
                    tempPlaytestEvents.Remove(tempPlaytestEvent);

            _playtestEvents = tempPlaytestEvents;

            if (_dataService.RSettings.ProgramSettings.Debug)
                _ = _log.LogMessage($"{_playtestEvents.Count} test events found!", false, color: LOG_COLOR);

            //Get next active tests
            var tempNextTf2Test =
                _playtestEvents.FirstOrDefault(x => x.Game == PlaytestEvent.Games.TF2) as Tf2PlaytestEvent;

            var tempNextCsgoTest =
                _playtestEvents.FirstOrDefault(x => x.Game == PlaytestEvent.Games.CSGO) as CsgoPlaytestEvent;

            if (tempNextCsgoTest == null)
            {
                //Make the active test null
                _activeCsgoPlaytestEvent = null;
            }
            else if (!tempNextCsgoTest.Equals(_activeCsgoPlaytestEvent))
            {
                //Try deleting the old message
                try
                {
                    await _activeCsgoPlaytestEvent.AnnouncmentChannel.DeleteMessageAsync(_activeCsgoPlaytestEvent
                        .AnnouncementMessage);
                }
                catch
                {
                }

                _activeCsgoPlaytestEvent = tempNextCsgoTest;
                if (_activeCsgoPlaytestEvent.TestValid())
                {
                    if (_dataService.RSettings.ProgramSettings.Debug)
                        await _log.LogMessage(
                            $"{_activeCsgoPlaytestEvent.Title} is valid and switched to active event.", false,
                            color: LOG_COLOR);
                }
                else
                {
                    await _log.LogMessage($"Test not valid!\n{_activeCsgoPlaytestEvent}", alert: true,
                        color: LOG_COLOR);
                }
            }

            if (tempNextTf2Test == null)
            {
                //Make the active test null
                _activeTf2PlaytestEvent = null;
            }
            else if (!tempNextTf2Test.Equals(_activeTf2PlaytestEvent))
            {
                //Try deleting the old message
                try
                {
                    await _activeTf2PlaytestEvent.AnnouncmentChannel.DeleteMessageAsync(_activeTf2PlaytestEvent
                        .AnnouncementMessage);
                }
                catch
                {
                }

                _activeTf2PlaytestEvent = tempNextTf2Test;
                if (_activeTf2PlaytestEvent.TestValid())
                {
                    if (_dataService.RSettings.ProgramSettings.Debug)
                        await _log.LogMessage($"{_activeTf2PlaytestEvent.Title} is valid and switched to active event.",
                            false, color: LOG_COLOR);
                }
                else
                {
                    await _log.LogMessage($"Test not valid!\n{_activeTf2PlaytestEvent}", alert: true, color: LOG_COLOR);
                }
            }
        }

        /// <summary>
        ///     Checks the testing calendar for if a test already exists for that date.
        /// </summary>
        /// <param name="testTime">DateTime to check with</param>
        /// <returns>True if event conflict found</returns>
        public async Task<Events> GetNextMonthAsync(DateTime testTime)
        {
            var request = _calendar.Events.List(_dataService.RSettings.ProgramSettings.TestCalendarId);

            request.Q = "by";
            request.OrderBy = EventsResource.ListRequest.OrderByEnum.StartTime;
            request.ShowDeleted = false;
            request.SingleEvents = true;
            request.TimeMin = testTime;
            request.TimeMax = testTime.Date.AddMonths(1);

            // Executes the request for events and retrieves the first event in the resulting items.
            var events = await request.ExecuteAsync();

            return events;
        }

        /// <summary>
        ///     Checks the testing calendar for if a test already exists for that date.
        /// </summary>
        /// <param name="testTime">DateTime to check with</param>
        /// <returns>True if event conflict found</returns>
        public async Task<Events> CheckForScheduleConflict(DateTime testTime)
        {
            var request = _calendar.Events.List(_dataService.RSettings.ProgramSettings.TestCalendarId);

            //Lets actually get closed days
            //request.Q = " by ";
            request.OrderBy = EventsResource.ListRequest.OrderByEnum.StartTime;
            request.ShowDeleted = false;
            request.SingleEvents = true;
            request.TimeMin = testTime.Date;
            request.TimeMax = testTime.Date.AddDays(1).AddSeconds(-1); //Sets to 23:59:59 same day

            // Executes the request for events and retrieves the first event in the resulting items.
            var events = await request.ExecuteAsync();

            return events;
        }

        /// <summary>
        ///     Adds a test event to the testing calendar.
        /// </summary>
        /// <param name="playtestRequest">Playtest request to add</param>
        /// <param name="moderator">Moderator for the test</param>
        /// <returns>True if successful, false otherwise.</returns>
        public async Task<bool> AddTestEvent(PlaytestRequest playtestRequest, SocketUser moderator)
        {
            string creators = null;
            foreach (var creator in playtestRequest.CreatorsDiscord)
            {
                var user = _dataService.GetSocketUser(creator);
                if (user != null)
                    creators += $"{user.Username}, ";
                else
                    creators += $"Unknown[{creator}]";
            }

            //Create list and add the required attendee.
            var attendeeLists = new List<EventAttendee>();

            //Add every other user's email
            foreach (var email in playtestRequest.Emails)
                if (!string.IsNullOrWhiteSpace(email))
                    attendeeLists.Add(new EventAttendee {Email = email});

            var description = $"Creator: {string.Join(", ", playtestRequest.CreatorsDiscord)}\n" +
                              $"Map Images: {playtestRequest.ImgurAlbum}\n" +
                              $"Workshop Link: {playtestRequest.WorkshopURL}\n" +
                              $"Game Mode: {playtestRequest.TestType}\n" +
                              $"Moderator: {moderator.Id}\n" +
                              $"Description: {playtestRequest.TestGoals}";

            var newEvent = new Event
            {
                Summary =
                    $"{playtestRequest.Game.ToUpper()} | {playtestRequest.MapName} by {creators.TrimEnd(',', ' ')}",
                Location = playtestRequest.Preferredserver,
                Description = description,
                Start = new EventDateTime
                {
                    DateTime = playtestRequest.TestDate,
                    TimeZone = "America/Chicago"
                },
                End = new EventDateTime
                {
                    DateTime = playtestRequest.TestDate.AddHours(2),
                    TimeZone = "America/Chicago"
                },
                Attendees = attendeeLists
            };

            try
            {
                var request = _calendar.Events.Insert(newEvent, _dataService.RSettings.ProgramSettings.TestCalendarId);
                var createdEvent = await request.ExecuteAsync();
            }
            catch (Exception e)
            {
                await _log.LogMessage($"Issue added test event to calendar!\n{e.Message}", color: LOG_COLOR);
                return false;
            }

            return true;
        }

        /// <summary>
        ///     Returns the next valid playtest event.
        /// </summary>
        /// <returns>Valid playtest event. Null if no tests found. Null if test is invalid.</returns>
        public PlaytestEvent GetNextPlaytestEvent()
        {
            var playtest = _playtestEvents.FirstOrDefault();

            if (playtest.Equals(_activeCsgoPlaytestEvent))
                return _activeCsgoPlaytestEvent;

            if (playtest.Equals(_activeTf2PlaytestEvent))
                return _activeTf2PlaytestEvent;

            return null;
        }

        /// <summary>
        ///     Returns the next valid playtest event for the specific game.
        /// </summary>
        /// <returns>Valid playtest event. Null if no tests found. Null if test is invalid.</returns>
        public PlaytestEvent GetNextPlaytestEvent(string game)
        {
            PlaytestEvent.Games testGame;

            if (game.Equals("csgo", StringComparison.OrdinalIgnoreCase))
            {
                testGame = PlaytestEvent.Games.CSGO;
            }
            else if (game.Equals("tf2", StringComparison.OrdinalIgnoreCase))
            {
                testGame = PlaytestEvent.Games.TF2;
            }
            else
            {
                _ = _log.LogMessage($"Invalid game requested when looking for playtest event! {game}");
                return null;
            }

            return GetNextPlaytestEvent(testGame);
        }

        /// <summary>
        ///     Returns the next valid playtest event for the specific game enum
        /// </summary>
        /// <param name="game"></param>
        /// <returns></returns>
        public PlaytestEvent GetNextPlaytestEvent(PlaytestEvent.Games game)
        {
            var playtest = _playtestEvents?.FirstOrDefault(x => x.Game == game);

            if (playtest == null)
                return null;

            if (playtest.Equals(_activeCsgoPlaytestEvent))
                return _activeCsgoPlaytestEvent;

            if (playtest.Equals(_activeTf2PlaytestEvent))
                return _activeTf2PlaytestEvent;

            return null;
        }

        /// <summary>
        ///     Returns a valid "Stacked" playtest event. A playtest is "stacked" when it has a start time that would have
        ///     setup events happen while another test event is active. Meaning a test ending at 2pm, with one starting at 2:30.
        ///     The 2:30 test is "stacked" since it has a setup task taking place at 1:30, before the 2pm end of the other test.
        /// </summary>
        /// <returns>Returns stacked playtest</returns>
        public PlaytestEvent GetStackedPlaytestEvent()
        {
            //If empty, or only 1 test found, return null
            if (_playtestEvents.Count < 2)
                return null;

            var activeTest = GetNextPlaytestEvent();
            if (activeTest == null)
                return null;

            return _playtestEvents.SingleOrDefault
            (x => activeTest.EndDateTime.GetValueOrDefault() > x.StartDateTime.GetValueOrDefault().AddHours(-1) &&
                  activeTest.EndDateTime.GetValueOrDefault() <= x.StartDateTime.GetValueOrDefault());
        }
    }
}