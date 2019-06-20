using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Discord;
using Discord.WebSocket;

namespace BotHATTwaffle2.Services.Steam
{
    public class Workshop
    {
        public Workshop()
        {
            Console.WriteLine("Constructor");
        }

        public async Task<string> HandleWorkshopEmbeds(SocketMessage message)
        {
            // Cut down the message to grab just the first URL
            Match regMatch = Regex.Match(message.Content, @"\b((https?|ftp|file)://|(www|ftp)\.)[-A-Z0-9+&@#/%?=~_|$!:,.;]*[A-Z0-9+&@#/%=~_|$]", RegexOptions.IgnoreCase);
            string workshopLink = regMatch.ToString();
            //if (!Uri.IsWellFormedUriString(workshopLink, UriKind.Absolute))
            //{

            //}

            // Send the POST request
            using (var client = new HttpClient())
            {
                client.BaseAddress = new Uri("https://api.steampowered.com/ISteamRemoteStorage/GetCollectionDetails/v1/");
                var content = new FormUrlEncodedContent(new[]
                {
                    new KeyValuePair<string, string>("collectioncount", "1"),
                    new KeyValuePair<string, string>("publishedfileids[0]", "1764994203"),
                });
                var result = await client.PostAsync("", content);
                string resultContent = await result.Content.ReadAsStringAsync();
                Console.WriteLine(resultContent);
                return resultContent;
            }
            // Build the browser session class
        }
    }
}
