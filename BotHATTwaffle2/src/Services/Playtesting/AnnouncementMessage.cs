using System;
using BotHATTwaffle2.Services.Calendar;
using BotHATTwaffle2.src.Handlers;
using Discord;

namespace BotHATTwaffle2.Services.Playtesting
{
    public class AnnouncementMessage
    {
        private const ConsoleColor LOG_COLOR = ConsoleColor.Magenta;
        private static int _lastImageIndex;
        private readonly GoogleCalendar _calendar;
        private readonly DataService _data;
        private readonly LogHandler _log;
        private readonly Random _random;

        public AnnouncementMessage(GoogleCalendar calendar, DataService data, Random random, LogHandler log)
        {
            _log = log;
            _calendar = calendar;
            _data = data;
            _random = random;
        }

        /// <summary>
        /// Creates a playtest embed with all information setup as needed.
        /// This expects the calendar to have the latest even cached.
        /// </summary>
        /// <param name="isCasual">If true, shows password. Otherwise password will be hidden</param>
        /// <param name="smallEmbed">Should the message be formatted in small style</param>
        /// <param name="fullMessage">ID of full message, used in small embeds</param>
        /// <returns>Prebuilt embed</returns>
        public Embed CreatePlaytestEmbed(bool isCasual = true, bool smallEmbed = false, ulong fullMessage = 0)
        {
            if (_data.RSettings.ProgramSettings.Debug)
                _ = _log.LogMessage("Creating Playtest Embed", false, color: LOG_COLOR);

            var testEvent = _calendar.GetTestEventNoUpdate();

            //What type of test
            var testType = "Casual";
            if (!testEvent.IsCasual)
                testType = "Competitive";

            //If more than 1 creator, randomly change between them for their index on the thumbnail
            var creatorIndex = 0;
            var creatorSpelling = "Creator";
            var creatorProfile =
                $"[{testEvent.Creators[0].Username}](https://discordapp.com/users/{testEvent.Creators[0].Id})";
            if (testEvent.Creators.Count > 1)
            {
                if (_data.RSettings.ProgramSettings.Debug)
                    _ = _log.LogMessage($"Multiple Test Creators found for embed [{testEvent.Creators.Count}]",
                        false, color: LOG_COLOR);

                creatorIndex = _random.Next(0, testEvent.Creators.Count);
                creatorSpelling = "Creators";

                for (var i = 1; i < testEvent.Creators.Count; i++)
                    creatorProfile +=
                        $"\n[{testEvent.Creators[i].Username}](https://discordapp.com/users/{testEvent.Creators[i].Id})";
            }

            if (_data.RSettings.ProgramSettings.Debug)
                _ = _log.LogMessage(
                    $"Creators string\n{creatorProfile}\nUsing creator index {creatorIndex} of {testEvent.Creators.Count - 1} (0 Index!)",
                    false, color: LOG_COLOR);

            //Timezone information
            var utcTime = testEvent.StartDateTime.GetValueOrDefault().ToUniversalTime();
            var est = TimeZoneInfo
                .ConvertTimeFromUtc(utcTime, TimeZoneInfo.FindSystemTimeZoneById("Eastern Standard Time"))
                .ToString("ddd HH:mm");
            var pst = TimeZoneInfo
                .ConvertTimeFromUtc(utcTime, TimeZoneInfo.FindSystemTimeZoneById("Pacific Standard Time"))
                .ToString("ddd HH:mm");
            var gmt = TimeZoneInfo.ConvertTimeFromUtc(utcTime, TimeZoneInfo.FindSystemTimeZoneById("GMT Standard Time"))
                .ToString("ddd HH:mm");

            //Figure out how far away from start we are
            string countdownString = null;
            var countdown = testEvent.StartDateTime.GetValueOrDefault().Subtract(DateTime.Now);
            if (testEvent.StartDateTime.GetValueOrDefault().CompareTo(DateTime.Now) < 0)
                countdownString = $"Started: {countdown:h\'H \'m\'M\'} ago!";
            else
                countdownString = countdown.ToString("d'D 'h'H 'm'M'").TrimStart(' ', 'D', 'H', '0');

            //What image should be displayed
            var embedImageUrl = _data.RSettings.General.FallbackTestImageUrl;
            if (testEvent.CanUseGallery)
            {
                var randomIndex = _random.Next(testEvent.GalleryImages.Count);
                while (_lastImageIndex == randomIndex) randomIndex = _random.Next(testEvent.GalleryImages.Count);

                if (_data.RSettings.ProgramSettings.Debug)
                    _ = _log.LogMessage($"Using random gallery index {randomIndex} of {testEvent.GalleryImages.Count - 1} (0 Index!)",
                        false, color: LOG_COLOR);

                _lastImageIndex = randomIndex;
                embedImageUrl = testEvent.GalleryImages[randomIndex];
            }

            //Display the correct password, or omit for comp
            string displayedConnectionInfo;
            string footer;

            if (isCasual)
            {
                displayedConnectionInfo = $"`{testEvent.ServerLocation}; password {_data.RSettings.General.CasualPassword}`";
                footer = "All players welcome to join";
            }
            else
            {
                displayedConnectionInfo = $"*This is a competitive 5v5 test, where not everyone can play. 15 minutes before start time, you can use the following to check the level out in a sandbox server:*\n" +
                                          $"`connect {_calendar.GetTestEventNoUpdate().CompCasualServer}; password {_data.RSettings.General.CasualPassword}`";
                footer = "Connection info hidden due to competitive test";
            }

            //Setup the basic embed
            var playtestEmbed = new EmbedBuilder()
                .WithAuthor($"{testEvent.Title} | {testType}")
                .WithTitle("Workshop Link")
                .WithUrl(testEvent.WorkshopLink.ToString())
                .WithDescription(testEvent.Description)
                .WithColor(new Color(243, 128, 72))
                .WithFooter(footer);

            playtestEmbed.AddField("Test Starts In", countdownString, true);
            playtestEmbed.AddField(creatorSpelling, creatorProfile, true);
            playtestEmbed.AddField("Moderator",
                $"[{testEvent.Moderator.Username}](https://discordapp.com/users/{testEvent.Moderator.Id})", true);

            if (_data.IncludePlayerCount)
            {
                playtestEmbed.AddField("Players Connected", _data.PlayerCount, true);
            }

            playtestEmbed.AddField("Connect to",
                $"{displayedConnectionInfo}");
            
            //Small VS large embed differences
            string information;
            if (smallEmbed)
            {
                playtestEmbed.ThumbnailUrl = embedImageUrl;
                information = $"[Screenshots]({testEvent.ImageGallery}) | " +
                              $"[Testing Information](https://www.tophattwaffle.com/playtesting) | " +
                              $"[More Information](https://discordapp.com/channels/{_data.Guild.Id}/{_data.AnnouncementChannel.Id}/{fullMessage})";
            }
            else
            {
                information = $"[Screenshots]({testEvent.ImageGallery}) | " +
                              $"[Testing Information](https://www.tophattwaffle.com/playtesting)";
                playtestEmbed.ImageUrl = embedImageUrl;
                playtestEmbed.ThumbnailUrl = testEvent.Creators[creatorIndex].GetAvatarUrl();
                playtestEmbed.AddField("When",
                    $"{testEvent.StartDateTime.GetValueOrDefault():MMMM ddd d, HH:mm} | {est} EST | {pst} PST | {gmt} GMT");
            }

            playtestEmbed.AddField("Information",information);

            return playtestEmbed.Build();
        }
    }
}