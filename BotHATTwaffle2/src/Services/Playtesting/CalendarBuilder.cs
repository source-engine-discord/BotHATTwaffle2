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



            // Here's a bunch of stuff you can easily change
            // WARNING: Take caution before modifying width and/or height, as font scaling has not been implemented
            int width = 1372;
            int height = 1029;
            Rgba32 dateColor = Rgba32.Black;
            Rgba32 playtestTitleColor = Rgba32.GhostWhite;
            Rgba32 playtestTimeColor = Rgba32.Black;
            Rgba32 backgroundColor = new Rgba32(.21176f, .22353f, .24706f); // This is Discord's dark theme's exact background color
            Rgba32 lineColor = Rgba32.Black;
            string playtestDateFontName = "Arial";
            string playtestMapFontName = "Arial";
            string playtestTimeFontName = "Arial";
            // Modifying anything besides these variables may cause instability, so be extremely cautious if you do so




            using (Image<Rgba32> image = new Image<Rgba32>(width, height))
            {

                // Changes background color
                image.Mutate(x => x
                    .BackgroundColor(backgroundColor)
                );

                // Puts the lines over the calendar
                float widthF = (float)width;
                float heightF = (float)height;
                Pen<Rgba32> calLinePen = new Pen<Rgba32>(lineColor, 2f);

                image.Mutate(x => x
                    .Draw(calLinePen, new RectangleF(0f, 0f, 1f, heightF)) // Vertical: 1 (Far Left)
                    .Draw(calLinePen, new RectangleF((widthF * .14296f), 0f, 1f, heightF)) // Vertical: 2
                    .Draw(calLinePen, new RectangleF((widthF * .28592f), 0f, 1f, heightF)) // Vertical: 3
                    .Draw(calLinePen, new RectangleF((widthF * .42888f), 0f, 1f, heightF)) // Vertical: 4
                    .Draw(calLinePen, new RectangleF((widthF * .57039f), 0f, 1f, heightF)) // Vertical: 5
                    .Draw(calLinePen, new RectangleF((widthF * .71335f), 0f, 1f, heightF)) // Vertical: 6
                    .Draw(calLinePen, new RectangleF((widthF * .85631f), 0f, 1f, heightF)) // Vertical: 7
                    .Draw(calLinePen, new RectangleF((widthF - 1f), 0f, 1f, heightF)) // Vertical: 8 (Far Right) (This one should be weight - 1 for the first value)

                    .Draw(calLinePen, new RectangleF(0f, 0f, widthF, 1f)) // Horizontal: 1 (Top) (Thin)
                    .Draw(calLinePen, new RectangleF(0f, (heightF * .03141f), widthF, 0f)) // Horizonal: 2 (Thick)
                    .Draw(calLinePen, new RectangleF(0f, (heightF * .20046f), widthF, 0f)) // Horizonal: 3 (Thin)
                    .Draw(calLinePen, new RectangleF(0f, (heightF * .23072f), widthF, 0f)) // Horizonal: 4 (Thick)
                    .Draw(calLinePen, new RectangleF(0f, (heightF * .39977f), widthF, 0f)) // Horizonal: 5 (Thin)
                    .Draw(calLinePen, new RectangleF(0f, (heightF * .43002f), widthF, 0f)) // Horizontal: 6 (Thick)
                    .Draw(calLinePen, new RectangleF(0f, (heightF * .59907f), widthF, 0f)) // Horizontal: 7 (Thin)
                    .Draw(calLinePen, new RectangleF(0f, (heightF * .62933f), widthF, 0f)) // Horizonal: 8 (Thick)
                    .Draw(calLinePen, new RectangleF(0f, (heightF * .79838f), widthF, 0f)) // Horizontal: 9 (Thin)
                    .Draw(calLinePen, new RectangleF(0f, (heightF * .82863f) , widthF, 0f)) // Horizontal: 10 (Thick)
                    .Draw(calLinePen, new RectangleF(0f, (heightF - 1f), widthF, 1f)) // Horizontal: 11 (Bottom) (Thin) (The second value should be height - 1)
                );

                // Getting the current time (start of calendar) and setting up our fonts

                var fontDates = SystemFonts.CreateFont(playtestDateFontName, 20, FontStyle.Bold);
                var fontPTName = SystemFonts.CreateFont(playtestMapFontName, 23, FontStyle.Regular);
                var fontPT = SystemFonts.CreateFont(playtestTimeFontName, 25, FontStyle.Regular);
                var currentDateTime = DateTime.Now;

                // Here are the coordinate lists for the date headings (and xPos is also used by the playtest events)
                List<float> xPosList = new List<float>() { (widthF * .07148f), (widthF * .21384f), (widthF * .35667f), (widthF * .49854f), (widthF * .64114f), (widthF * .78337f), (widthF * .92706f) };
                List<float> yPosList = new List<float>() { (heightF * .00796f), (heightF * .20652f), (heightF * .40629f), (heightF * .60366f), (heightF * .80509f) };

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
                // They are in groups of 2. So each set of 2 is in the same row
                List<float> playtestTitlesYPosList = new List<float>() { (heightF * .04998f),  (heightF * .12696f),   (heightF * .24914f),  (heightF * .32615f),   (heightF * .44833f), (heightF * .52531f),    (heightF * .64749f), (heightF * .72447f),   (heightF * .84665f), (height * .92363f),  };
                List<float> playtestTimesYPosList = new List<float>()  { (heightF * .07689f),  (heightF * .15388f),   (heightF * .27605f),  (heightF * .35304f),   (heightF * .47522f), (heightF * .55220f),    (heightF * .67438f), (heightF * .75136f),   (heightF * .87354f), (height * .95232f),  };

                // Here's the big boi. Putting the playtest events on the calendar
                List<int> numOfPlaytests = new List<int>() { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 };

                foreach (var playtestEvent in calPlaytestEvents.Items)
                {
                    // These variables just exist to make names shorter
                    DateTime playtestSDT = playtestEvent.Start.DateTime.Value;
                    int numDaysSeparate = (playtestSDT.Day - currentDateTime.Day);
                    string shortenedEventSummary = "";

                    // Here we're just grabbing the mapname from the playtest title
                    foreach (var entry in playtestEvent.Summary.Split(" "))
                    {
                        if (entry != "by")
                        {
                            shortenedEventSummary += entry;
                        }
                        else break;
                    }

                    // These are the sizes of the info from the calendar. They are required to center the text properly
                    SizeF titleSize = TextMeasurer.Measure(shortenedEventSummary, new RendererOptions(fontPTName));
                    SizeF timeSize = TextMeasurer.Measure($"{playtestSDT.Hour}:{playtestSDT.Minute.ToString("D2")} - {playtestSDT.Hour + 2}:{playtestSDT.Minute.ToString("D2")}", new RendererOptions(fontPT));

                    // Break out of this iteration if the current date already has 2 playtests
                    if (numOfPlaytests[numDaysSeparate] > 1) continue;

                    // Basically this finds out where in the Y postition lists we should start (since they are broken up into sets)
                    int groupStartIndex;
                    if (numDaysSeparate < 7) groupStartIndex = 0;
                    else if (numDaysSeparate < 14) groupStartIndex = 2;
                    else if (numDaysSeparate < 21) groupStartIndex = 4;
                    else if (numDaysSeparate < 28) groupStartIndex = 6;
                    else groupStartIndex = 8;

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
                    float titleYCoord = playtestTitlesYPosList[groupStartIndex + numOfPlaytests[numDaysSeparate]];
                    float timeYCoord = playtestTimesYPosList[groupStartIndex + numOfPlaytests[numDaysSeparate]];

                    // Finally drawing on the screen. Lots of variable names here. This is Squidski's fault by the way.
                    image.Mutate(x => x
                        .DrawText($"{shortenedEventSummary}", fontPTName, playtestTitleColor, new PointF(xPosList[columnNum] - (titleSize.Width / 2), titleYCoord))
                        .DrawText($"{playtestSDT.Hour}:{playtestSDT.Minute.ToString("D2")} - {playtestSDT.Hour + 2}:{playtestSDT.Minute.ToString("D2")}", fontPT, playtestTimeColor, new PointF(xPosList[columnNum] - (timeSize.Width / 2), timeYCoord))
                    );

                    numOfPlaytests[numDaysSeparate]++;
                }

                image.Save("finishedCalendar.png");
                await calContext.Channel.SendFileAsync("finishedCalendar.png");
                image.Dispose();
            }
        }
    }
}
