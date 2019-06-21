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

namespace BotHATTwaffle2.Services.Steam
{
    public class Workshop
    {
        public Workshop()
        {
            Console.WriteLine("Constructor");
        }

        public async Task HandleWorkshopEmbeds(SocketMessage message, DataService _data, string images = null, string testType = null)
        {
            // Cut down the message to grab just the first URL
            Match regMatch = Regex.Match(message.Content, @"\b((https?|ftp|file)://|(www|ftp)\.)[-A-Z0-9+&@#/%?=~_|$!:,.;]*[A-Z0-9+&@#/%=~_|$]", RegexOptions.IgnoreCase);
            string workshopLink = regMatch.ToString();
            string apiKey = _data.RSettings.ProgramSettings.SteamworksAPI;

            // Send the POST request for item info
            using (var clientItem = new HttpClient())
            {
                clientItem.BaseAddress = new Uri("https://api.steampowered.com/ISteamRemoteStorage/GetPublishedFileDetails/v1/");
                var contentItem = new FormUrlEncodedContent(new[]
                {
                    new KeyValuePair<string, string>("itemcount", "1"),
                    new KeyValuePair<string, string>("publishedfileids[0]", _data.GetWorkshopIdFromFqdn(workshopLink)),
                });
                var resultItem = await clientItem.PostAsync("", contentItem);
                string resultContentItem = await resultItem.Content.ReadAsStringAsync();

                //Check if response is empty
                if (resultContentItem == "{}") return;

                // Build workshop item embed, and set up author and game data embeds here for scoping reasons
                RootWorkshop workshopJsonItem = JsonConvert.DeserializeObject<RootWorkshop>(resultContentItem);
                RootWorkshop workshopJsonAuthor;
                RootWorkshop workshopJsonGameData;

                // Send the GET request for the author information
                using (var clientAuthor = new HttpClient())
                {
                    clientAuthor.BaseAddress = new Uri("https://api.steampowered.com/ISteamUser/GetPlayerSummaries/v2/");
                    HttpResponseMessage responseAuthor = clientAuthor.GetAsync($"?key={apiKey}&steamids={workshopJsonItem.response.publishedfiledetails[0].creator}").Result;
                    responseAuthor.EnsureSuccessStatusCode();
                    string resultAuthor = responseAuthor.Content.ReadAsStringAsync().Result;

                    // Don't embed anything if getting the author fails for some reason
                    if (resultAuthor == "{}") return;

                    // If we get a good response though, we're gonna deserialize it
                    workshopJsonAuthor = JsonConvert.DeserializeObject<RootWorkshop>(resultAuthor);
                }

                // So basically the only way to get game name from appid is to get a list of a user's owned games, then match our appid from the workshop item with their game (and yoink the name)
                using (var clientGame = new HttpClient())
                {
                    clientGame.BaseAddress = new Uri("https://api.steampowered.com/IPlayerService/GetOwnedGames/v1/");
                    HttpResponseMessage responseGame = clientGame.GetAsync($"?key={apiKey}&steamid={workshopJsonItem.response.publishedfiledetails[0].creator}&include_appinfo=1&appids_filter={workshopJsonItem.response.publishedfiledetails[0].consumer_app_id}").Result;
                    responseGame.EnsureSuccessStatusCode();
                    string resultGame = responseGame.Content.ReadAsStringAsync().Result;

                    // Don't embed anything if the third GET request fails (hopefully it doesn't)
                    if (resultGame == "{}") return;

                    //Deserialize version 3, electric boogaloo
                    workshopJsonGameData = JsonConvert.DeserializeObject<RootWorkshop>(resultGame);
                }

                // Finally we can build the embed after too many HTTP requests
                var workshopItemEmbed = new EmbedBuilder()
                    .WithAuthor($"{workshopJsonItem.response.publishedfiledetails[0].title}", workshopJsonAuthor.response.players[0].avatar, workshopLink)
                    .WithTitle($"Creator: {workshopJsonAuthor.response.players[0].personaname}")
                    .WithUrl(workshopJsonAuthor.response.players[0].profileurl)
                    .WithImageUrl(workshopJsonItem.response.publishedfiledetails[0].preview_url)
                    .WithColor(new Color(71, 126, 159));

                // foreach loop to pull the game name from the list of user games
                foreach (var item in workshopJsonGameData.response.games)
                {
                    if (item.appid == workshopJsonItem.response.publishedfiledetails[0].creator_app_id)
                    {
                        workshopItemEmbed.AddField("Game", item.name, true);
                        break;
                    }
                }

                // Add every other field now
                // Get tags from Json object
                workshopItemEmbed.AddField("Tags", string.Join(", ", workshopJsonItem.response.publishedfiledetails[0].tags.Select(x => x.tag)), true);

                // If test type is null or empty, it will not be included in the embed (bot only)
                if (!string.IsNullOrEmpty(testType))
                {
                    workshopItemEmbed.AddField("Test Type", testType, false);
                }

                //TODO: perhaps strip BBcodes from description?
                workshopItemEmbed.AddField("Description", workshopJsonItem.response.publishedfiledetails[0].description.Length > 497 ? workshopJsonItem.response.publishedfiledetails[0].description.Substring(0,497) + "..." : workshopJsonItem.response.publishedfiledetails[0].description);

                // If images is null or empty, it will not be included in the embed (bot only)
                if (!string.IsNullOrEmpty(images))
                {
                    workshopItemEmbed.AddField("Links", images, false);
                }

                await message.Channel.SendMessageAsync(embed: workshopItemEmbed.Build());
            }
        }
    }
}
