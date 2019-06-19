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

            string builtUrl = $"https://developer.valvesoftware.com/w/api.php?action=query&list=search&srsearch={term}&srwhat=nearmatch&format=json";
            string siteTitle;
            string siteData = "";
            string changeSearchLinkText = "Search Results";

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

            // Parse the JSON response

            // If we get a good (non-empty) response then proceed, otherwise default the URL to the main page
            if (siteData.Contains("snippet"))
            {
                // Slicing up the string because all we want is a super small string between [[ ]]
                String[] dataList = siteData.Split(":");
                siteData = dataList[6].ToString();
                siteData = siteData.Substring(siteData.IndexOf("[") + 2);
                siteData = siteData.Substring(0, siteData.Length - 10);
                siteTitle = siteData;
                siteData = siteData.Replace(' ', '_');

                // Now we build the URL so we can give the user their search results
                builtUrl = $"https://developer.valvesoftware.com/wiki/{siteData}";
            }
            else
            {
                siteTitle = "No results found!";
                changeSearchLinkText = "Click Here to go to VDC Homepage";
                builtUrl = "https://developer.valvesoftware.com/wiki/Main_Page";
            }

            // Defaults the URL if it isn't properly formatted for some reason
            if (!Uri.IsWellFormedUriString(builtUrl, UriKind.Absolute))
            {
                builtUrl = "https://developer.valvesoftware.com/wiki/Main_Page";
                term = "Valve Developer Community";
            }

            // Build the embed based on results from GET request
            var builder = new EmbedBuilder
            {
                Author = new EmbedAuthorBuilder
                {
                    Name = $"This is what I was able to find for {term}",
                    IconUrl = _client.Guilds.FirstOrDefault()?.IconUrl
                },
                Title = changeSearchLinkText,
                Url = builtUrl,
                ImageUrl = "https://developer.valvesoftware.com/w/skins/valve/images-valve/logo.png",
                Color = new Color(71, 126, 159),
                Description = siteTitle
            };

            await ReplyAsync(string.Empty, false, builder.Build());

            //TODO: Add video/tutorial searches
        }
    }
}
