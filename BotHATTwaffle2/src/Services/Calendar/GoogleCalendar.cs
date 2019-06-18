using System;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using BotHATTwaffle2.src.Handlers;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Calendar.v3;
using Google.Apis.Calendar.v3.Data;
using Google.Apis.Services;
using Google.Apis.Util.Store;

namespace BotHATTwaffle2.Services.Calendar
{
    public class GoogleCalendar
    {
        private const ConsoleColor LogColor = ConsoleColor.DarkCyan;
        private static PlaytestEvent _testEvent;
        private readonly CalendarService _calendar;
        private readonly DataService _dataService;
        private readonly LogHandler _log;

        public GoogleCalendar(DataService dataService, LogHandler log)
        {
            _log = log;
            _dataService = dataService;
            _calendar = new CalendarService(new BaseClientService.Initializer
            {
                HttpClientInitializer = GetCredential(),
                ApplicationName = "BotHATTwaffle 2"
            });

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
        private static UserCredential GetCredential()
        {
            using (var stream = new FileStream("client_secret.json", FileMode.Open, FileAccess.Read))
            {
                var credPath = Environment.GetFolderPath(Environment.SpecialFolder.Personal);
                credPath = Path.Combine(credPath, ".credentials/calendar-dotnet-quickstart.json");

                return GoogleWebAuthorizationBroker.AuthorizeAsync(
                        GoogleClientSecrets.Load(stream).Secrets,
                        new[] {CalendarService.Scope.CalendarReadonly},
                        "user",
                        CancellationToken.None,
                        new FileDataStore(credPath, true))
                    .Result;
            }
        }

        private void GetEvents()
        {
            if (_dataService.RSettings.ProgramSettings.Debug)
                _ = _log.LogMessage("Getting test event", false, color: LogColor);

            // Defines request and parameters.
            var request = _calendar.Events.List(_dataService.RSettings.ProgramSettings.TestCalendarId);

            request.Q = " TESTEVENT "; // This will limit all search requests to ONLY get playtest events.
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
                    _ = _log.LogMessage("No event found", false, color: LogColor);
                //Scrap null out the event item so it can be ready for the next use.
                _testEvent.LastEditTime = null;
                _testEvent.VoidEvent();

                return;
            }

            if (_dataService.RSettings.ProgramSettings.Debug)
                _ = _log.LogMessage("Test event found", false, color: LogColor);

            //Update the last time the event was changed
            _testEvent.LastEditTime = eventItem.Updated;

            //An event exists and has not changed - do nothing.
            if (_testEvent.EventEditTime == _testEvent.LastEditTime && _testEvent.EventEditTime != null)
            {
                if (_dataService.RSettings.ProgramSettings.Debug)
                    _ = _log.LogMessage("Event was not changed, not rebuilding", false, color: LogColor);
                return;
            }

            _testEvent.VoidEvent(); //Something changed - rebuild

            string strippedHtml = null;

            if (_dataService.RSettings.ProgramSettings.Debug)
                _ = _log.LogMessage($"Event description BEFORE stripping:\n{eventItem.Description}\n", false,
                    color: LogColor);

            // Handles the event.
            //Replace <br>s with \n for new line, replace &nbsp as well
            strippedHtml = eventItem.Description.Replace("<br>", "\n").Replace("&nbsp;", "");

            //Strip out HTML tags
            strippedHtml = Regex.Replace(strippedHtml, "<.*?>", string.Empty);

            if (_dataService.RSettings.ProgramSettings.Debug)
                _ = _log.LogMessage($"Event description AFTER stripping:\n{strippedHtml}\n", false, color: LogColor);

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
            _testEvent.Creators = _dataService.GetSocketUser(description.ElementAtOrDefault(0), ',');

            //Imgur Album
            _testEvent.ImageGallery = _dataService.ValidateUri(description.ElementAtOrDefault(2));

            //Workshop URL
            _testEvent.WorkshopLink = _dataService.ValidateUri(description.ElementAtOrDefault(3));

            //Gamemode
            _testEvent.SetGameMode(description.ElementAtOrDefault(4));

            //Moderator
            _testEvent.Moderator = _dataService.GetSocketUser(description.ElementAtOrDefault(5));

            //Description
            _testEvent.Description = description.ElementAtOrDefault(6);

            //Gallery Images
            _testEvent.GalleryImages = _dataService.GetImgurAlbum(_testEvent.ImageGallery.ToString());

            //Test the even to see if the information is valid.
            if (!_testEvent.TestValid())
                _ = _log.LogMessage("Error in playtest event! Please check the description and try again.\n" +
                                    $"{_testEvent}\n",
                    color: LogColor);
        }

        /// <summary>
        /// Gets the latest test event from Google
        /// </summary>
        /// <returns>Latest test event Google</returns>
        public PlaytestEvent GetTestEvent()
        {
            GetEvents();
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