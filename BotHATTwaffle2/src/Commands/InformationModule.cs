using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
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
            //var regex = new Regex(@"(?<=<title.*>)([\s\S]*)(?=</title>)", RegexOptions.IgnoreCase);
            //term = regex.Match(term).Value.Trim();

            string builtUrl = $"https://developer.valvesoftware.com/w/api.php?action=opensearch&search={term}&limit=5&format=json";
            string siteData = "";
            // This try/catch block will catch all errors that the server sends (if siteData is null)
            try
            {
                // Makes the HTTP GET request
                HttpWebRequest request = (HttpWebRequest)WebRequest.Create(builtUrl);
                request.AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate;
                using (HttpWebResponse response = (HttpWebResponse)await request.GetResponseAsync())
                using (Stream stream = response.GetResponseStream())
                using (StreamReader reader = new StreamReader(stream))
                {
                    // Writes raw JSON data to siteData
                    siteData = reader.ReadToEnd();
                }
            }
            catch
            {
                Console.WriteLine("There was some error sending a GET request to the VDC server. Giving 'no results found' message...");
            }

            // Pull just the URLs that it gives from the GET request
            List<string> dataArray = new List<string>();
            dataArray.Add("");
            MatchCollection matches = Regex.Matches(siteData, @"\b((https?|ftp|file)://|(www|ftp)\.)[-A-Z0-9+()&@#/%?=~_|$!:,.;]*[A-Z0-9+()&@#/%=~_|$]", RegexOptions.IgnoreCase);
            foreach (Match match in matches)
            {
                dataArray.Add(match.Value.ToString());
            }

            // If we get a good (non-empty) response then proceed, otherwise default the URL to the main page
            if (string.IsNullOrEmpty(dataArray[0])) builtUrl = "https://developer.valvesoftware.com/wiki/Main_Page";

            // Defaults the URL if it isn't properly formatted for some reason
            if (!Uri.IsWellFormedUriString(builtUrl, UriKind.Absolute))
            {
                builtUrl = "https://developer.valvesoftware.com/wiki/Main_Page";
                term = "Valve Developer Community";
            }

            // Build the embed based on results from GET request
            var informationEmbed = new EmbedBuilder()
                .WithAuthor($"Valve Developer Community Wiki", _client.Guilds.FirstOrDefault()?.IconUrl, builtUrl)
                .WithImageUrl("https://developer.valvesoftware.com/w/skins/valve/images-valve/logo.png")
                .WithColor(new Color(71, 126, 159))
                .WithFooter("This search is limited to the first 5 results");

            if (dataArray.Count == 1)
            {
                informationEmbed.AddField($"No results found for {term}", "[Click here to go to the VDC homepage](" + builtUrl + ")");
            }
            else
            {
                dataArray.RemoveAt(0);
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
