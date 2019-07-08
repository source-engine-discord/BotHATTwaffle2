using System;
using System.Collections.Generic;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.Primitives;
using System.Threading.Tasks;
using SixLabors.Fonts;
using Google.Apis.Calendar.v3.Data;
using System.IO;
using Discord.Commands;

namespace BotHATTwaffle2.Services.Playtesting
{
    public class CalendarBuilder
    {
        public async Task DiscordPlaytestCalender(SocketCommandContext calContext, Events calPlaytestEvents)
        {
            // Gonna just yeet out of here if there are no playtests
            if (calPlaytestEvents.Items.Count == 0) return;

            // Dimensions of our image to make
            int width = 1371;
            int height = 836;

            using (Image<Rgba32> image = new Image<Rgba32>(width, height))
            {

                // Change colors here
                Rgba32 dateColor = Rgba32.Black;
                Rgba32 playtestTitleColor = Rgba32.GhostWhite;
                Rgba32 playtestTimeColor = Rgba32.Black;
                Rgba32 backgroundColor = Rgba32.Gray;

                // Changes background color obv
                image.Mutate(x => x
                    .BackgroundColor(backgroundColor)
                );

                // Puts the lines over the calendar
                using (Image<Rgba32> lineImage = Image.Load("calendar-line-overlay.png"))
                {
                    image.Mutate(x => x
                        .DrawImage(lineImage, 1f)
                    );
                }

                // Getting the current time (start of calendar) and setting up our fonts
                var currentDateTime = DateTime.Now;
                var fontDates = SystemFonts.CreateFont("Arial", 40, FontStyle.Bold);
                var fontPTName = SystemFonts.CreateFont("Arial", 15, FontStyle.Regular);
                var fontPT = SystemFonts.CreateFont("Arial", 23, FontStyle.Regular);

                // Here are the coordinate lists for the date headings
                List<float> xPosList = new List<float>() { 99f, 294f, 489f, 684f, 880f, 1075f, 1272f };
                List<float> yPosList = new List<float>() { 10f, 174f, 338f, 503f, 677f };

                // Need to set up these variables here for scoping reasons
                SizeF customSize;
                DateTime customDateTime;
                int iterator = 0;

                // These loops iterative place the date headings on the calendar
                foreach (var yPos in yPosList)
                {
                    foreach (var xPos in xPosList)
                    {
                        customDateTime = currentDateTime.AddDays(iterator);
                        customSize = TextMeasurer.Measure($"{customDateTime.ToString("MMM")} {customDateTime.Day}", new RendererOptions(fontDates));

                        image.Mutate(x => x
                            .DrawText($"{customDateTime.ToString("MMM")} {customDateTime.Day}", fontDates, dateColor, new PointF(xPos - (customSize.Width / 2), yPos)));
                        iterator++;
                    }
                }

                // These are similar to the date headings, but these are for the playtest names and times
                // They are in groups of 3. So each set of 3 is in the same row
                List<float> playtestTitlesYPosList = new List<float>() { 55f, 88f, 123f, 220f, 253f, 288f, 385f, 418f, 453f, 552f, 590f, 626f, 725f, 759f, 795f };
                List<float> playtestTimesYPosList = new List<float>() { 70f, 104f, 140f, 235f, 269f, 305f, 400f, 434f, 470f, 567f, 605f, 643f, 739f, 774f, 812f };
                SizeF customSize2 = TextMeasurer.Measure($"Gongji by Squidski | Casual:", new RendererOptions(fontPTName));
                SizeF customSize3 = TextMeasurer.Measure($"00:00 - 00:00", new RendererOptions(fontPT));

                // Here's the big boi. Putting the playtest events on the calendar
                List<int> numOfPlaytests = new List<int>() { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 };

                foreach (var playtestEvent in calPlaytestEvents.Items)
                {
                    // These variables just exist to make names shorter
                    DateTime playtestSDT = playtestEvent.Start.DateTime.Value;
                    int numDaysSeparate = (playtestSDT.Day - currentDateTime.Day);
                    string shortenedEventSummary = playtestEvent.Summary.Length > 28 ? $"{playtestEvent.Summary.Substring(0, 23)}..." : playtestEvent.Summary;

                    // These are the sizes of the info from the calendar. They are required to center the text properly
                    SizeF titleSize = TextMeasurer.Measure(shortenedEventSummary, new RendererOptions(fontPTName));
                    SizeF timeSize = TextMeasurer.Measure($"{playtestSDT.Hour}:{playtestSDT.Minute.ToString("D2")} - {playtestSDT.Hour + 2}:{playtestSDT.Minute.ToString("D2")}", new RendererOptions(fontPT));

                    // Break out of this iteration if the current date already has 3 playtests
                    if (numOfPlaytests[numDaysSeparate] > 2) continue;

                    // Basically this finds out where in the Y postition lists we should start (since they are broken up into sets)
                    int groupStartIndex;
                    if (numDaysSeparate < 7) groupStartIndex = 0;
                    else if (numDaysSeparate < 14) groupStartIndex = 3;
                    else if (numDaysSeparate < 21) groupStartIndex = 6;
                    else if (numDaysSeparate < 28) groupStartIndex = 9;
                    else groupStartIndex = 12;

                    // Get the column that the day would be in (so we can get the x coordinate)
                    int columnNum = 7;
                    for (int i = 0; i < 7; i++)
                    {
                        if ((new List<int> { i, i + 7, i + 14, i + 21, i + 28 }).Contains(numDaysSeparate))
                        {
                            columnNum = i;
                            break;
                        }
                    }
                    if (columnNum == 7) return;  // But if for some reason it didn't fit into any of these columns, something is broken

                    // This gets kinda complicated here, but basically the Y coordinates are calculated from our current playtests in the same day, as well as from our "group" from the list earlier
                    float titleYCoord = playtestTitlesYPosList[groupStartIndex] + numOfPlaytests[numDaysSeparate];
                    float timeYCoord = playtestTimesYPosList[groupStartIndex] + numOfPlaytests[numDaysSeparate];

                    // Finally drawing on the screen. Lots of variable names here. This is Squidski's fault by the way.
                    image.Mutate(x => x
                        .DrawText($"{shortenedEventSummary}", fontPTName, playtestTitleColor, new PointF(xPosList[columnNum] - (titleSize.Width / 2), titleYCoord))
                        .DrawText($"{playtestSDT.Hour}:{playtestSDT.Minute.ToString("D2")} - {playtestSDT.Hour + 2}:{playtestSDT.Minute.ToString("D2")}", fontPT, playtestTimeColor, new PointF(xPosList[columnNum] - (timeSize.Width / 2), timeYCoord))
                    );

                    numOfPlaytests[numDaysSeparate]++;
                }

                image.Save("filled-calendar.png");
                await calContext.Channel.SendFileAsync("filled-calendar.png");
            }
        }
    }
}
