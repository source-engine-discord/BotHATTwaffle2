using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using BotHATTwaffle2.src.Models.JSON.Steam;
using Discord;
using Discord.WebSocket;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Linq;
using System.Net;
using BotHATTwaffle2.Models.JSON.Steam;
using BotHATTwaffle2.Util;

namespace BotHATTwaffle2.Services.Steam
{
    public class Workshop
    {
        private static RootWorkshop workshopJsonGameData;

        private bool EnsureGameListCache()
        {
            if (workshopJsonGameData != null)
                return true;

            // So basically the only way to get game name from appid is to get a list of a user's owned games, then match our appid from the workshop item with their game (and yoink the name)
            using (var clientGame = new HttpClient())
            {
                Console.WriteLine("FETCHING GAMES FROM STEAM");
                clientGame.BaseAddress = new Uri("https://api.steampowered.com/ISteamApps/GetAppList/v2/");
                HttpResponseMessage responseGame = clientGame.GetAsync("").Result;
                responseGame.EnsureSuccessStatusCode();
                string resultGame = responseGame.Content.ReadAsStringAsync().Result;

                // Don't embed anything if the third GET request fails (hopefully it doesn't)
                if (resultGame == "{}") return false;
                //Deserialize version 3, electric boogaloo
                workshopJsonGameData = JsonConvert.DeserializeObject<RootWorkshop>(resultGame);
            }

            return true;
        }

        public async Task<EmbedBuilder> HandleWorkshopEmbeds(SocketMessage message, DataService _dataService, string images = null, string testType = null, string inputId = null)
        {
            // Cut down the message to grab just the first URL
            Match regMatch = Regex.Match(message.Content, @"\b((https?|ftp|file)://|(www|ftp)\.)(steamcommunity)[-A-Z0-9+&@#/%?=~_|$!:,.;]*[A-Z0-9+&@#/%=~_|$]", RegexOptions.IgnoreCase);
            string workshopLink = regMatch.ToString();
            string apiKey = _dataService.RSettings.ProgramSettings.SteamworksAPI;

            // Send the POST request for item info
            using (var clientItem = new HttpClient())
            {
                //Define our key value pairs
                var kvp1 = new KeyValuePair<string,string>("itemcount", "1");

                //Create empty key value pair and populate it based input variables.
                var kvp2 = new KeyValuePair<string, string>();
                if (inputId != null)
                    kvp2 = new KeyValuePair<string, string>("publishedfileids[0]", inputId);
                else
                    kvp2 = new KeyValuePair<string, string>("publishedfileids[0]",
                        GeneralUtil.GetWorkshopIdFromFqdn(workshopLink));
                
                var contentItem = new FormUrlEncodedContent(new[]
                {
                    kvp1,kvp2
                });

                string resultContentItem;
                try
                {
                    // Send the actual post request
                    clientItem.BaseAddress = new Uri("https://api.steampowered.com/ISteamRemoteStorage/GetPublishedFileDetails/v1/");
                    var resultItem = await clientItem.PostAsync("", contentItem);
                    resultContentItem = await resultItem.Content.ReadAsStringAsync();
                }
                catch (Exception e)
                {
                    Console.WriteLine(e);
                    return null;
                }

                //Check if response is empty
                if (resultContentItem == "{}") return null;
                RootWorkshop workshopJsonItem;
                // Build workshop item embed, and set up author and game data embeds here for scoping reasons
                try
                {
                    workshopJsonItem = JsonConvert.DeserializeObject<RootWorkshop>(resultContentItem);
                }
                catch (Exception e)
                {
                    //Something happened getting the response from Steam. We got a response but it wasn't valid?
                    Console.WriteLine("Error parsing JSON from STEAM. The response was:\n" + resultContentItem);
                    Console.WriteLine(e);
                    return null;
                }
                RootWorkshop workshopJsonAuthor;

                // If the file is a screenshot, artwork, video, or guide we don't need to embed it because Discord will do it for us
                if (workshopJsonItem.response.publishedfiledetails[0].result == 9) return null;
                if (workshopJsonItem.response.publishedfiledetails[0].filename.Contains("/screenshots/".ToLower())) return null;

                // Send the GET request for the author information
                using (var clientAuthor = new HttpClient())
                {
                    clientAuthor.BaseAddress = new Uri("https://api.steampowered.com/ISteamUser/GetPlayerSummaries/v2/");
                    HttpResponseMessage responseAuthor = clientAuthor.GetAsync($"?key={apiKey}&steamids={workshopJsonItem.response.publishedfiledetails[0].creator}").Result;
                    responseAuthor.EnsureSuccessStatusCode();
                    string resultAuthor = responseAuthor.Content.ReadAsStringAsync().Result;

                    // Don't embed anything if getting the author fails for some reason
                    if (resultAuthor == "{\"response\":{}}") return null;

                    // If we get a good response though, we're gonna deserialize it
                    workshopJsonAuthor = JsonConvert.DeserializeObject<RootWorkshop>(resultAuthor);
                }

                //Make sure a cache exists
                if (!EnsureGameListCache())
                    return null;

                if (string.IsNullOrWhiteSpace(workshopLink))
                    workshopLink = $"https://steamcommunity.com/sharedfiles/filedetails/?id={workshopJsonItem.response.publishedfiledetails[0].publishedfileid}";

                // Finally we can build the embed after too many HTTP requests
                var workshopItemEmbed = new EmbedBuilder()
                    .WithAuthor($"{workshopJsonItem.response.publishedfiledetails[0].title}", workshopJsonAuthor.response.players[0].avatar, workshopLink)
                    .WithTitle($"Creator: {workshopJsonAuthor.response.players[0].personaname}")
                    .WithUrl(workshopJsonAuthor.response.players[0].profileurl)
                    .WithImageUrl(workshopJsonItem.response.publishedfiledetails[0].preview_url)
                    .WithColor(new Color(71, 126, 159));

                var gameId = workshopJsonGameData.applist.apps.SingleOrDefault(x => x.appid == workshopJsonItem.response.publishedfiledetails[0].creator_app_id);

                if (gameId != null)
                {
                    workshopItemEmbed.AddField("Game", gameId.name, true);
                }

                // Add every other field now
                // Get tags from Json object
                string tags = string.Join(", ",
                    workshopJsonItem.response.publishedfiledetails[0].tags.Select(x => x.tag));

                if(!string.IsNullOrWhiteSpace(tags))
                    workshopItemEmbed.AddField("Tags", tags, true);

                // If test type is null or empty, it will not be included in the embed (bot only)
                if (!string.IsNullOrEmpty(testType))
                {
                    workshopItemEmbed.AddField("Test Type", testType, false);
                }

                // TODO: Strip BB Codes
                string shortDescription = Regex.Replace(workshopJsonItem.response.publishedfiledetails[0].description, @"\t|\n|\r", " ");

                if (!string.IsNullOrWhiteSpace(shortDescription))
                    workshopItemEmbed.AddField("Description", shortDescription.Length > 497 ? shortDescription.Substring(0,497) + "..." : shortDescription);

                // If images is null or empty, it will not be included in the embed (bot only)
                if (!string.IsNullOrEmpty(images))
                {
                    workshopItemEmbed.AddField("Links", images, false);
                }

                return workshopItemEmbed;
            }
        }

        public async Task SendWorkshopEmbed(SocketMessage message, DataService _dataService)
        {
            //If the invoking message has an embed, do nothing.
            await Task.Delay(2000);
            var refreshedMessage = await _dataService.GetSocketMessage(message.Channel, message.Id);
            if (refreshedMessage.Embeds.Count > 0)
                return;

            var embed = await HandleWorkshopEmbeds(message, _dataService);

            if (embed != null)
                await message.Channel.SendMessageAsync(embed: embed.Build());
        }
    }
}
