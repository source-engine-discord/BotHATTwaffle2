using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using BotHATTwaffle2.Handlers;
using BotHATTwaffle2.Models.LiteDB;
using BotHATTwaffle2.Util;
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
        private const ConsoleColor LOG_COLOR = ConsoleColor.DarkCyan;
        private static PlaytestEvent _testEvent;
        private readonly CalendarService _calendar;
        private readonly DataService _dataService;
        private readonly LogHandler _log;

        public GoogleCalendar(DataService dataService, LogHandler log)
        {
            _log = log;
            _dataService = dataService;
            Console.Write("Getting or checking Sheets OAuth Credentials... ");
            _calendar = new CalendarService(new BaseClientService.Initializer
            {
                HttpClientInitializer = GetCalendarCredentials(),
                ApplicationName = "BotHATTwaffle 2"
            });
            Console.WriteLine("Done!");
            _testEvent = new PlaytestEvent(_dataService, _log);
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

        private void GetNextTestEvent()
        {
            if (_dataService.RSettings.ProgramSettings.Debug)
                _ = _log.LogMessage("Getting test event", false, color: LOG_COLOR);

            // Defines request and parameters.
            var request = _calendar.Events.List(_dataService.RSettings.ProgramSettings.TestCalendarId);

            //Set the playtest search based on debug flag
            if (_dataService.RSettings.ProgramSettings.Debug)
                request.Q = " DEBUG ";
            else
                request.Q = " by "; // This will limit all search requests to ONLY get playtest events.

            request.TimeMin = DateTime.Now;
            request.ShowDeleted = false;
            request.SingleEvents = true;
            request.MaxResults = 1;
            request.OrderBy = EventsResource.ListRequest.OrderByEnum.StartTime;

            // Executes the request for events and retrieves the first event in the resulting items.
            Event eventItem = null;
            eventItem = request.Execute().Items?.FirstOrDefault();

            //If there is no event
            if (eventItem == null)
            {
                if (_dataService.RSettings.ProgramSettings.Debug)
                    _ = _log.LogMessage("No event found", false, color: LOG_COLOR);
                //Scrap null out the event item so it can be ready for the next use.
                _testEvent.LastEditTime = null;
                _testEvent.VoidEvent();

                return;
            }

            if (_dataService.RSettings.ProgramSettings.Debug)
                _ = _log.LogMessage("Test event found", false, color: LOG_COLOR);

            //Update the last time the event was changed
            _testEvent.LastEditTime = eventItem.Updated;

            //An event exists and has not changed - do nothing.
            if (_testEvent.EventEditTime == _testEvent.LastEditTime && _testEvent.EventEditTime != null)
            {
                if (_dataService.RSettings.ProgramSettings.Debug)
                    _ = _log.LogMessage("Event was not changed, not rebuilding", false, color: LOG_COLOR);
                return;
            }

            _testEvent.VoidEvent(); //Something changed - rebuild

            string strippedHtml = null;

            if (_dataService.RSettings.ProgramSettings.Debug)
                _ = _log.LogMessage($"Event description BEFORE stripping:\n{eventItem.Description}\n", false,
                    color: LOG_COLOR);

            // Handles the event.
            //Replace <br>s with \n for new line, replace &nbsp as well
            strippedHtml = eventItem.Description.Replace("<br>", "\n").Replace("&nbsp;", "");

            //Strip out HTML tags
            strippedHtml = Regex.Replace(strippedHtml, "<.*?>", string.Empty);

            if (_dataService.RSettings.ProgramSettings.Debug)
                _ = _log.LogMessage($"Event description AFTER stripping:\n{strippedHtml}\n", false, color: LOG_COLOR);

            // Splits description into lines and keeps only the part after the colon, if one exists.
            var description = strippedHtml.Trim().Split('\n')
                .Select(line => line.Substring(line.IndexOf(':') + 1).Trim())
                .ToImmutableArray();

            _testEvent.StartDateTime = eventItem.Start.DateTime;
            _testEvent.EventEditTime = eventItem.Updated;
            _testEvent.Title = eventItem.Summary;
            _testEvent.ServerLocation = eventItem.Location;
            _testEvent.EndDateTime = eventItem.End.DateTime;

            //Creators
            _testEvent.Creators = _dataService.GetSocketUsers(description.ElementAtOrDefault(0), ',');

            //Imgur Album
            _testEvent.ImageGallery = GeneralUtil.ValidateUri(description.ElementAtOrDefault(1));

            //Workshop URL
            _testEvent.WorkshopLink = GeneralUtil.ValidateUri(description.ElementAtOrDefault(2));

            //Game mode
            _testEvent.SetGameMode(description.ElementAtOrDefault(3));

            //Moderator
            _testEvent.Moderator = _dataService.GetSocketUser(description.ElementAtOrDefault(4));

            //Description
            _testEvent.Description = description.ElementAtOrDefault(5);

            //Gallery Images
            _testEvent.GalleryImages = GeneralUtil.GetImgurAlbum(_testEvent.ImageGallery.ToString());

            //Test the even to see if the information is valid.
            if (!_testEvent.TestValid())
                _ = _log.LogMessage("Error in playtest event! Please check the description and try again.\n" +
                                    $"{_testEvent}\n",
                    color: LOG_COLOR);
        }

        /// <summary>
        /// Checks the testing calendar for if a test already exists for that date.
        /// </summary>
        /// <param name="testTime">DateTime to check with</param>
        /// <returns>True if event conflict found</returns>
        public async Task<Events> GetNextMonthAsync(DateTime testTime)
        {
            var request = _calendar.Events.List(_dataService.RSettings.ProgramSettings.TestCalendarId);

            request.Q = " by ";
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
        /// Checks the testing calendar for if a test already exists for that date.
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
        /// Adds a test event to the testing calendar.
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
            {
                if(!string.IsNullOrWhiteSpace(email))
                    attendeeLists.Add(new EventAttendee { Email = email });
            }

            string description = $"Creator: {string.Join(", " ,playtestRequest.CreatorsDiscord)}\n" +
                                 $"Map Images: {playtestRequest.ImgurAlbum}\n" +
                                 $"Workshop Link: {playtestRequest.WorkshopURL}\n" +
                                 $"Game Mode: {playtestRequest.TestType}\n" +
                                 $"Moderator: {moderator.Id}\n" +
                                 $"Description: {playtestRequest.TestGoals}";

            Event newEvent = new Event()
            {
                Summary = $"{playtestRequest.MapName} by {creators.TrimEnd(',',' ')}",
                Location = playtestRequest.Preferredserver,
                Description = description,
                Start = new EventDateTime()
                {
                    DateTime = playtestRequest.TestDate,
                    TimeZone = "America/Chicago",
                },
                End = new EventDateTime()
                {
                    DateTime = playtestRequest.TestDate.AddHours(2),
                    TimeZone = "America/Chicago",
                },
                Attendees = attendeeLists
            };

            try
            {
                EventsResource.InsertRequest request = _calendar.Events.Insert(newEvent, _dataService.RSettings.ProgramSettings.TestCalendarId);
                Event createdEvent = await request.ExecuteAsync();
            }
            catch (Exception e)
            {
                await _log.LogMessage($"Issue added test event to calendar!\n{e.Message}",color:LOG_COLOR);
                return false;
            }
            
            return true;
        }
        
        /// <summary>
        /// Gets the latest test event from Google
        /// </summary>
        /// <returns>Latest test event Google</returns>
        public PlaytestEvent GetTestEvent()
        {
            GetNextTestEvent();
            return _testEvent;
        }

        /// <summary>
        /// Gets the latest cached test event
        /// </summary>
        /// <returns>Latest cached test event</returns>
        public PlaytestEvent GetTestEventNoUpdate()
        {
            return _testEvent;
        }
    }
}