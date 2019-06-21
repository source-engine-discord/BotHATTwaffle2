using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Web;
using BotHATTwaffle2.Handlers;
using BotHATTwaffle2.Services;
using Discord;
using Discord.Commands;
using Discord.WebSocket;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace BotHATTwaffle2.Commands
{
    public class InformationModule : ModuleBase<SocketCommandContext>
    {
        private readonly DiscordSocketClient _client;
        private readonly DataService _dataService;
        private readonly Random _random;

        public InformationModule(DiscordSocketClient client, DataService data, Random random)
        {
            _client = client;
            _dataService = data;
            _random = random;
        }

        [Command("VDC", RunMode = RunMode.Async)]
        [Summary("Searches the VDC and replies with the results.")]
        [Remarks(
           "Searches the Valve Developer Community and returns a link to the results. Use of proper, full terms returns " +
           "better results e.g. `func_detail` over `detail`.")]
        [Alias("v")]
        public async Task SearchAsync(
           [Summary("The term for which to search.")] [Remainder]
            string term)
        {
            await Context.Channel.TriggerTypingAsync();

            // Scrub user input to make it safe for a link
            term = term.Replace(' ', '+');
            term = HttpUtility.UrlEncode(term);
            Console.WriteLine($"ENCODED: {term}");

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

            //TODO: Add video/tutorial searches
        }
    }
}
