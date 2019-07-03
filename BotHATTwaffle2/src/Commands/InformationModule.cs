using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Web;
using BotHATTwaffle2.Services;
using BotHATTwaffle2.Services.YouTube;
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
            string siteData = "";


            // Makes the HTTP GET request
            using (var client = new HttpClient())
            {
                client.BaseAddress = new Uri("https://developer.valvesoftware.com/w/");
                HttpResponseMessage response = client.GetAsync($"api.php?action=opensearch&search={term}&limit=5&format=json").Result;
                response.EnsureSuccessStatusCode();
                siteData = response.Content.ReadAsStringAsync().Result;
            }

            // Now that we've sent the request, we no longer care if the term is encoded for URLs, so we'll decode it
            term = HttpUtility.UrlDecode(term);

            // Declaring these variables here for scoping reasons
            List<string> dataArray = new List<string>();
            MatchCollection matches;

            // If we get an empty response, we don't need to regex for the URL, we can just give the "no results found message"
            if (siteData != $"[\"{term}\",[],[],[]]")
            {
                // Pull just the URLs that it gives from the GET request
                matches = Regex.Matches(siteData, @"\b((https?|ftp|file)://|(www|ftp)\.)[-A-Z0-9+()&@#/%?=~_|$!:,.;]*[A-Z0-9+()&@#/%=~_|$]", RegexOptions.IgnoreCase);
                foreach (Match match in matches)
                {
                    dataArray.Add(match.Value.ToString());
                }
            }

            // Build the embed based on results from GET request
            var informationEmbed = new EmbedBuilder()
                .WithAuthor($"Valve Developer Community Wiki", _client.Guilds.FirstOrDefault()?.IconUrl, "https://developer.valvesoftware.com/wiki/Main_Page")
                .WithImageUrl("https://developer.valvesoftware.com/w/skins/valve/images-valve/logo.png")
                .WithColor(new Color(71, 126, 159))
                .WithFooter("This search is limited to the first 5 results");

            // If we got no results from the server, then give the default "no results found" message
            if (siteData == $"[\"{term}\",[],[],[]]")
            {
                informationEmbed.AddField($"No results found for {term}", "[Click here to go to the VDC homepage](https://developer.valvesoftware.com/wiki/Main_Page)");
            }

            // However if we did, we need to check if the URL contains a ( ) in it, because then we will need to format it specially to make it work in the embed
            else
            {
                // Basically we have to put all of the results we get as the same field in the embed, so we're gonna "split" them with newlines
                string resultsConcatenated = "";
                foreach (string obj in dataArray)
                {
                    if (obj.Contains('('))
                    {
                        resultsConcatenated += $"[{obj.Substring(41)}]({obj.Replace("(", "%28").Replace(")", "%29")})\n";
                    }
                    else
                    {
                        resultsConcatenated += $"[{obj.Substring(41)}]({obj})\n";
                    }
                }
                informationEmbed.AddField($"This is what I was able to find for {term}:", resultsConcatenated);
            }

            await ReplyAsync(string.Empty, false, informationEmbed.Build());

        }

        [Command("Search", RunMode = RunMode.Async)]
        [Alias("s")]
        [Summary("Searches Tutorial Content.")]
        [Remarks("Allows users to search for tutorials on level design. There are a few commands to request a specific video.\n" +
                 "Using `>s bc [search]` will get results from the CSGO Level Design Bootcamp Series\n" +
                 "Using `>s v2 [search]` will get results from the V2 tutorial series.\n" +
                 "Using just a series and number will get that specific video. Meaning `>s bc 1` will return the " +
                 "CSGO Level Design Bootcamp day 1 video.")]
        public async Task SearchAsync([Summary("Search Term")][Remainder]string search)
        {
            string rawSearch = search;
            const string bootCamp = "CSGO Level Design Boot Camp";
            const string v2 = "Hammer Tutorial V2 Series";
            const string baseYouTubeUrl = "https://www.youtube.com/watch?v=";
            var numericStart = new Regex(@"^\d+");
            bool getSingle = false;
            string match = "";
            var embed = new EmbedBuilder();
            bool valid = false;

            if ((new string[] {"bc ", "bootcamp "}).Any(x => search.StartsWith(x,StringComparison.OrdinalIgnoreCase)))
            {
                //Remove the search shortcut text
                search = search.Substring(search.IndexOf(' ') + 1);

                //Find out if the user is requesting a specific # video
                if (numericStart.IsMatch(search))
                {
                    //Manually build the search term to best match this series
                    search = $"{bootCamp} - Day {numericStart.Match(search)} - ";
                }
                else
                {
                    //Else just concat them
                    search = $"{bootCamp} {search}";
                }
                getSingle = true;
                match = bootCamp;
            }
            else if ((new string[] {"v2 ", "version2 "}).Any(x => search.StartsWith(x, StringComparison.OrdinalIgnoreCase)))
            {
                //Remove the search shortcut text
                search = search.Substring(search.IndexOf(' ') + 1);

                //Find out if the user is requesting a specific # video
                if (numericStart.IsMatch(search))
                {
                    //Manually build the search term to best match this series
                    search = $"{v2} #{numericStart.Match(search)}";
                }
                else
                {
                    //Else just concat them
                    search = $"{v2} {search}";
                }
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
                        $"https://www.youtube.com/c/tophattwaffle")
                    .WithColor(new Color(255, 0, 0));
                string description = null;
                foreach (var result in results.Items)
                {
                    description += $"**[{HttpUtility.HtmlDecode(result.Snippet.Title)}]({baseYouTubeUrl}{result.Id.VideoId})**\n" +
                                   $"{result.Snippet.Description}\n\n";
                }

                embed.WithDescription(description.Trim());
                valid = true;
            }

            if(!valid)
            {
                //Build reply blaming YouTube
                embed.WithAuthor($"No results found!", _dataService.Guild.IconUrl)
                    .WithDescription($"I could not find anything results with your search term of `{rawSearch}`. " +
                                     $"Try a different search term.\n" +
                                     $"*If you keep seeing this, it is likely a [YouTube API quota limit](https://www.reddit.com/r/webdev/comments/aqou5b/youtube_api_v3_quota_issues/)*")
                    .WithColor(new Color(255, 0, 0));
            }

            await ReplyAsync(embed:embed.Build());
        }
    }
}
