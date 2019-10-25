using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Web;
using BotHATTwaffle2.Services;
using BotHATTwaffle2.Services.YouTube;
using BotHATTwaffle2.Util;
using Discord;
using Discord.Commands;
using Discord.WebSocket;

namespace BotHATTwaffle2.Commands
{
    public class InformationModule : ModuleBase<SocketCommandContext>
    {
        private readonly DiscordSocketClient _client;
        private readonly DataService _dataService;
        private readonly YouTube _youTube;

        public InformationModule(DiscordSocketClient client, DataService data, YouTube youTube)
        {
            _client = client;
            _dataService = data;
            _youTube = youTube;
        }

        [Command("Tutorials")]
        [Summary("Displays links to tutorial series.")]
        [Remarks(
            "`>tutorials [Optional series]` Example: `>tutorials` `>tutorials v2`\n" +
            "Displays information about all tutorial series, or the specific one you're looking for\n\n" +
            "`1` `V2Series` `v2`\n" +
            "`2` `CSGOBootcamp` `bc` `csgobootcamp`\n" +
            "`3` `3dsmax` `3ds`\n" +
            "`4` `WrittenTutorials` `written`\n" +
            "`5` `LegacySeries` `v1` `lg`\n" +
            "`6` `HammerTroubleshooting` `ht`")]
        [Alias("t")]
        public async Task TutorialsAsync([Summary("The series for which to display.")]
            string series = "all")
        {
            string authTitle;
            string bodyUrl;
            string bodyDescription;

            switch (series.ToLower())
            {
                case "v2series":
                case "v2":
                case "1":
                    authTitle = "Version 2 Tutorial Series";
                    bodyUrl = "https://goo.gl/XoVXzd";
                    bodyDescription =
                        "The Version 2 Tutorial series was created with the knowledge that I gained from " +
                        "creating the Version 1 (now legacy) series of tutorials. The goal is to help someone " +
                        "who hasn’t ever touched the tools get up and running in Source level design. You can " +
                        "watch them in any order, but they have been designed to build upon each other.";

                    break;
                case "csgobootcamp":
                case "bc":
                case "2":
                    authTitle = "CSGO Level Design Bootcamp";
                    bodyUrl = "https://goo.gl/srFBxe";
                    bodyDescription =
                        "The CSGO Boot Camp series was created for ECS to air during their Twitch streams " +
                        "between matches. It is created to help someone with no experience with the level " +
                        "design tools learn everything they need to create a competitive CSGO level. Most these " +
                        "tutorials apply to every Source game, but a handful are specific to CSGO.";

                    break;
                case "3dsmax":
                case "3ds":
                case "3":
                    authTitle = "3ds Max Tutorials";
                    bodyUrl = "https://goo.gl/JGg48X";
                    bodyDescription =
                        "There are a few sub series in the 3ds Max section. If you’re looking to create and " +
                        "export your very first Source prop, check out the **My First Prop** series.\n" +
                        "If you’re getting start with 3ds Max look at the **Beginners Guide** series, which is " +
                        "like the Version 2 Tutorial series but for 3ds Max.\nThere are a few one-off " +
                        "tutorials listed on the page as well covering WallWorm functions";

                    break;
                case "writtentutorials":
                case "written":
                case "4":
                    authTitle = "Written Tutorials";
                    bodyUrl = "https://goo.gl/i4aAqh";
                    bodyDescription =
                        "My library of written tutorials is typically about 1 off things that I want to cover. " +
                        "They are usually independent of any specific game.";

                    break;
                case "legacyseries":
                case "v1":
                case "lg":
                case "5":
                    authTitle = "Legacy Series";
                    bodyUrl = "https://goo.gl/aHFcvX";
                    bodyDescription =
                        "Hammer Troubleshooting is a smaller series that is created off user questions that I " +
                        "see come up quite often.y are usually independent of any specific game.";

                    break;
                case "hammertroubleshooting":
                case "ht":
                case "6":
                    authTitle = "Hammer Troubleshooting";
                    bodyUrl = "https://goo.gl/tBh7jT";
                    bodyDescription =
                        "The First tutorial series was my launching point for getting better at mapping. Not " +
                        "only did I learn a lot from making it, but I like to think that many others learned " +
                        "something from the series as well. The series was flawed in that it was not " +
                        "structured, and lacked quality control. But you may notice that the further along in " +
                        "the series you are, the better quality they get. Example is the 100th tutorial, it " +
                        "heavily reflects how the V2 series was created. You can view the entire series below. " +
                        "Just be warned that some of the information in these videos may not be correct, or " +
                        "even work any longer. Please watch at your own risk. I attempt to support these " +
                        "tutorials, but cannot due to time. Please watch the V2 series";

                    break;
                case "all":
                    authTitle = "All Tutorial Series Information";
                    bodyUrl = "https://www.tophattwaffle.com/tutorials/";
                    bodyDescription = "Over the years I've built up quite the collection of tutorial series!\n\n" +
                                      "[Version 2 Series](https://goo.gl/XoVXzd)\n" +
                                      "[CSGO Bootcamp](https://goo.gl/srFBxe)\n" +
                                      "[3ds Max](https://goo.gl/JGg48X)\n" +
                                      "[Written Tutorials](https://goo.gl/i4aAqh)\n" +
                                      "[Hammer Troubleshooting](https://goo.gl/tBh7jT)\n" +
                                      "[Legacy Series V1](https://goo.gl/aHFcvX)";

                    break;
                default:
                    await ReplyAsync("Unknown series. Please try `>Help Tutorials` to see all options.");
                    return;
            }

            var embed = new EmbedBuilder
            {
                Color = new Color(243, 128, 72),
                Description = bodyDescription
            };

            embed.WithAuthor(authTitle, _client.CurrentUser.GetAvatarUrl(), bodyUrl);
            await ReplyAsync(string.Empty, false, embed.Build());
        }

        [Command("VDC", RunMode = RunMode.Async)]
        [Summary("Searches the VDC and replies with the results.")]
        [Remarks(
            "Searches the Valve Developer Community and returns a link to the results. Use of proper, full terms returns " +
            "better results e.g. `func_detail` over `detail`.")]
        [Alias("v")]
        public async Task VdcAsync(
            [Summary("The term for which to search.")] [Remainder]
            string term)
        {
            await Context.Channel.TriggerTypingAsync();

            // Scrub user input to make it safe for a link
            term = term.Replace(' ', '+');
            term = HttpUtility.UrlEncode(term);

            // Here's where we're putting all the data we get from the server (declared here for scoping reasons)
            var siteData = "";


            // Makes the HTTP GET request
            using (var client = new HttpClient())
            {
                client.BaseAddress = new Uri("https://developer.valvesoftware.com/w/");
                var response = client.GetAsync($"api.php?action=opensearch&search={term}&limit=5&format=json").Result;
                response.EnsureSuccessStatusCode();
                siteData = response.Content.ReadAsStringAsync().Result;
            }

            // Now that we've sent the request, we no longer care if the term is encoded for URLs, so we'll decode it
            term = HttpUtility.UrlDecode(term);

            // Declaring these variables here for scoping reasons
            var dataArray = new List<string>();
            MatchCollection matches;

            // If we get an empty response, we don't need to regex for the URL, we can just give the "no results found message"
            if (siteData != $"[\"{term}\",[],[],[]]")
            {
                // Pull just the URLs that it gives from the GET request
                matches = Regex.Matches(siteData,
                    @"\b((https?|ftp|file)://|(www|ftp)\.)[-A-Z0-9+()&@#/%?=~_|$!:,.;]*[A-Z0-9+()&@#/%=~_|$]",
                    RegexOptions.IgnoreCase);
                foreach (Match match in matches) dataArray.Add(match.Value);
            }

            // Build the embed based on results from GET request
            var informationEmbed = new EmbedBuilder()
                .WithAuthor("Valve Developer Community Wiki", _dataService.Guild.IconUrl,
                    "https://developer.valvesoftware.com/wiki/Main_Page")
                .WithImageUrl("https://developer.valvesoftware.com/w/skins/valve/images-valve/logo.png")
                .WithColor(new Color(71, 126, 159))
                .WithFooter("This search is limited to the first 5 results");

            // If we got no results from the server, then give the default "no results found" message
            if (siteData == $"[\"{term}\",[],[],[]]")
            {
                informationEmbed.AddField($"No results found for {term}",
                    "[Click here to go to the VDC homepage](https://developer.valvesoftware.com/wiki/Main_Page)");
            }

            // However if we did, we need to check if the URL contains a ( ) in it, because then we will need to format it specially to make it work in the embed
            else
            {
                // Basically we have to put all of the results we get as the same field in the embed, so we're gonna "split" them with newlines
                var resultsConcatenated = "";
                foreach (var obj in dataArray)
                    if (obj.Contains('('))
                        resultsConcatenated +=
                            $"[{obj.Substring(41)}]({obj.Replace("(", "%28").Replace(")", "%29")})\n";
                    else
                        resultsConcatenated += $"[{obj.Substring(41)}]({obj})\n";
                informationEmbed.AddField($"This is what I was able to find for {term}:", resultsConcatenated);
            }

            await ReplyAsync(string.Empty, false, informationEmbed.Build());
        }

        [Command("Search", RunMode = RunMode.Async)]
        [Alias("s")]
        [Summary("Searches Tutorial Content.")]
        [Remarks(
            "Allows users to search for tutorials on level design. There are a few commands to request a specific video.\n" +
            "Using `>s bc [search]` will get results from the CSGO Level Design Bootcamp Series\n" +
            "Using `>s v2 [search]` will get results from the V2 tutorial series.\n" +
            "Using just a series and number will get that specific video. Meaning `>s bc 1` will return the " +
            "CSGO Level Design Bootcamp day 1 video.")]
        public async Task SearchAsync([Summary("Search Term")] [Remainder] string search)
        {
            var rawSearch = search;
            const string bootCamp = "CSGO Level Design Boot Camp";
            const string v2 = "Hammer Tutorial V2 Series";
            const string baseYouTubeUrl = "https://www.youtube.com/watch?v=";
            var numericStart = new Regex(@"^\d+");
            var getSingle = false;
            var match = "";
            var embed = new EmbedBuilder();
            var valid = false;

            if (new[] {"bc ", "bootcamp "}.Any(x => search.StartsWith(x, StringComparison.OrdinalIgnoreCase)))
            {
                //Remove the search shortcut text
                search = search.Substring(search.IndexOf(' ') + 1);

                //Find out if the user is requesting a specific # video
                if (numericStart.IsMatch(search))
                    //Manually build the search term to best match this series
                    search = $"{bootCamp} - Day {numericStart.Match(search)} - ";
                else
                    //Else just concat them
                    search = $"{bootCamp} {search}";
                getSingle = true;
                match = bootCamp;
            }
            else if (new[] {"v2 ", "version2 "}.Any(x => search.StartsWith(x, StringComparison.OrdinalIgnoreCase)))
            {
                //Remove the search shortcut text
                search = search.Substring(search.IndexOf(' ') + 1);

                //Find out if the user is requesting a specific # video
                if (numericStart.IsMatch(search))
                    //Manually build the search term to best match this series
                    search = $"{v2} #{numericStart.Match(search)}";
                else
                    //Else just concat them
                    search = $"{v2} {search}";
                getSingle = true;
                match = v2;
            }

            if (getSingle)
            {
                //Only want a single video back since a series was specified.
                var result = await _youTube.GetOneYouTubeVideo(search, match);

                //Error in request
                if (result != null)
                {
                    //Build proper reply
                    embed.WithAuthor(HttpUtility.HtmlDecode(result.Snippet.Title), _dataService.Guild.IconUrl,
                            $"{baseYouTubeUrl}{result.Id.VideoId}")
                        .WithThumbnailUrl(result.Snippet.Thumbnails.High.Url)
                        .WithDescription(HttpUtility.HtmlDecode(result.Snippet.Description))
                        .WithColor(new Color(255, 0, 0));
                    valid = true;
                }
            }
            else //Multiple replies
            {
                var results = await _youTube.YouTubeSearch(search, 3);

                embed.WithAuthor($"Search results for {rawSearch}", _dataService.Guild.IconUrl,
                        "https://www.youtube.com/c/tophattwaffle")
                    .WithColor(new Color(255, 0, 0));
                string description = null;
                foreach (var result in results.Items)
                    description +=
                        $"**[{HttpUtility.HtmlDecode(result.Snippet.Title)}]({baseYouTubeUrl}{result.Id.VideoId})**\n" +
                        $"{result.Snippet.Description}\n\n";

                embed.WithDescription(description.Trim());
                valid = true;
            }

            if (!valid)
                //Build reply blaming YouTube
                embed.WithAuthor("No results found!", _dataService.Guild.IconUrl)
                    .WithDescription($"I could not find anything results with your search term of `{rawSearch}`. " +
                                     "Try a different search term.\n" +
                                     "*If you keep seeing this, it is likely a [YouTube API quota limit](https://www.reddit.com/r/webdev/comments/aqou5b/youtube_api_v3_quota_issues/)*")
                    .WithColor(new Color(255, 0, 0));

            await ReplyAsync(embed: embed.Build());
        }

        [Command("TanookiIRL", RunMode = RunMode.Async)]
        [Summary("Displays Tanooki looking at stuff!")]
        [Alias("TanookiLooksAtThings")]
        public async Task TanookiLookAsync()
        {
            var embed = new EmbedBuilder
            {
                ImageUrl = GeneralUtil.GetRandomImgFromUrl(
                    "https://content.tophattwaffle.com/BotHATTwaffle/kimjongillookingatthings/"),
                Color = new Color(138, 43, 226)
            };

            await ReplyAsync(string.Empty, false, embed.Build());
        }
    }
}