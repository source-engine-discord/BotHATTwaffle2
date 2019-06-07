using System;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using BotHATTwaffle2.Services;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Calendar.v3;
using Google.Apis.Calendar.v3.Data;
using Google.Apis.Services;
using Google.Apis.Util.Store;

namespace BotHATTwaffle2.src.Services.Calendar
{
    class GoogleCalendar
    {
        private readonly CalendarService _calendar;
        private readonly DataService _dataService;
        public static PlaytestEvent TestEvent;

        public GoogleCalendar(DataService dataService)
        {
            _dataService = dataService;
            _calendar = new CalendarService(new BaseClientService.Initializer
            {
                HttpClientInitializer = GetCredential(),
                ApplicationName = "BotHATTwaffle2"
            });

            TestEvent = new PlaytestEvent();

            GetEvents();
        }

        /// <summary>
        /// Retrieves a credential for the Google Calendar API.
        /// </summary>
        /// <remarks>
        /// The secret is read from <c>client_secret.json</c> located in the executable's directory.
        /// </remarks>
        /// <remarks>
        /// The token is stored in a file in <c>/credentials/calendar-dotnet-quickstart.json</c> in
        /// <see cref="Environment.SpecialFolder.Personal"/>.
        /// </remarks>
        /// <returns>The retrieved OAuth 2.0 credential.</returns>
        private static UserCredential GetCredential()
        {
            using (var stream = new FileStream("client_secret.json", FileMode.Open, FileAccess.Read))
            {
                // TODO: Chage this to the executable's directory or make it configurable.
                string credPath = Environment.GetFolderPath(Environment.SpecialFolder.Personal);
                credPath = Path.Combine(credPath, ".credentials/calendar-dotnet-quickstart.json");

                return GoogleWebAuthorizationBroker.AuthorizeAsync(
                        GoogleClientSecrets.Load(stream).Secrets,
                        new[] { CalendarService.Scope.CalendarReadonly },
                        "user",
                        CancellationToken.None,
                        new FileDataStore(credPath, true))
                    .Result;
            }
        }

        /// <summary>
        /// Retrieves a playtesting event from the calendar.
        /// </summary>
        /// <remarks>
        /// <list type="number">
        ///	<item><description>
        /// 0	Header; possible values: <c>BEGIN_EVENT</c>, <c>NO_EVENT_FOUND</c>, <c>BAD_DESCRIPTION</c>
        ///	</description></item>
        /// <item><description>1 Starting time</description></item>
        /// <item><description>2 Title</description></item>
        /// <item><description>3 Creator</description></item>
        /// <item><description>4 Featured image link</description></item>
        /// <item><description>5 Map images link</description></item>
        /// <item><description>6 Workshop link</description></item>
        /// <item><description>7 Game mode</description></item>
        /// <item><description>8 Moderator</description></item>
        /// <item><description>9 Description</description></item>
        /// <item><description>10 Server</description></item>
        /// </list>
        /// </remarks>
        /// <returns>An array of the details of the retrieved event.</returns>
        public void GetEvents()
        {
            // TODO: Replace the array with an object.
            var finalEvent = new string[11];

            // Defines request and parameters.
            EventsResource.ListRequest request = _calendar.Events.List(_dataService.RootSettings.program_settings.testCalendarID);

            request.Q = " by "; // This will limit all search requests to ONLY get playtest events.
            request.TimeMin = DateTime.Now;
            request.ShowDeleted = false;
            request.SingleEvents = true;
            request.MaxResults = 1;
            request.OrderBy = EventsResource.ListRequest.OrderByEnum.StartTime;

            // Executes the request for events and retrieves the first event in the resulting items.
            Event eventItem = request.Execute().Items?.SingleOrDefault();

            if (eventItem == null)
            { 
                if (TestEvent.IsValid)
                    return;

                TestEvent.VoidEvent();
            }

            // Handles the event.
            try
            {
                //Replace <br>s with \n for new line, replace &nbsp as well
                string strippedHTML = eventItem.Description.Replace("<br>", "\n").Replace("&nbsp;", "");

                //Strip out HTML tags
                strippedHTML = Regex.Replace(strippedHTML, "<.*?>", String.Empty);

                // Splits description into lines and keeps only the part after the colon, if one exists.
                ImmutableArray<string> description = strippedHTML.Trim().Split('\n')
                    .Select(line => line.Substring(line.IndexOf(':') + 1).Trim())
                    .ToImmutableArray();

                TestEvent.StartDateTime = eventItem.Start.DateTime;
                TestEvent.EventEditTime = eventItem.Updated;
                TestEvent.Title = eventItem.Summary;
                TestEvent.ServerLocation = eventItem.Description;



                //finalEvent[0] = "BEGIN_EVENT";
                //finalEvent[1] = eventItem.Start.DateTime?.ToString() ?? "2/17/1993 9:34:00";
                //finalEvent[2] = eventItem.Summary; // No way to handle this - title has to exist to even find the event.
                finalEvent[3] = string.IsNullOrEmpty(description.ElementAtOrDefault(0)) ? "ERROR_CHECK_EVENT#1337" : description.ElementAtOrDefault(0);
                finalEvent[4] = string.IsNullOrEmpty(description.ElementAtOrDefault(1)) ? "https://www.tophattwaffle.com/wp-content/uploads/2017/11/header.png" : description.ElementAtOrDefault(1);
                finalEvent[5] = string.IsNullOrEmpty(description.ElementAtOrDefault(2)) ? string.Empty : description.ElementAtOrDefault(2); //This being empty or bad is handled in the playtest announcement.
                finalEvent[6] = string.IsNullOrEmpty(description.ElementAtOrDefault(3)) ? "https://steamcommunity.com/sharedfiles/filedetails/?id=267340686" : description.ElementAtOrDefault(3);
                finalEvent[7] = string.IsNullOrEmpty(description.ElementAtOrDefault(4)) ? "Casual" : description.ElementAtOrDefault(4);
                finalEvent[8] = string.IsNullOrEmpty(description.ElementAtOrDefault(5)) ? "ErrorHATTwaffle" : description.ElementAtOrDefault(5);
                finalEvent[9] = string.IsNullOrEmpty(description.ElementAtOrDefault(6)) ? "Description error in event!" : description.ElementAtOrDefault(6);
                //finalEvent[10] = eventItem.Location ?? "No Server Set";

            }
            catch (Exception e)
            {
                // TODO: Narrow the exception being caught.

                // TODO: Is this even needed now that the description is parsed more safely?
               // _dataService.ChannelLog(
                //    "There is an issue with the description on the next playtest event. This is likely caused by HTML " +
               //     $"formatting on the description.\n{e}");

                // TODO: Is nulling the elements necessary? Are they ever accessed before the first element is validated?
                finalEvent = Enumerable.Repeat<string>(null, 11).ToArray();
                finalEvent[0] = "BAD_DESCRIPTION";
            }

            Console.WriteLine("||" + string.Join("\n", finalEvent) + "||");
            Console.ReadLine();
        }
    }
}
