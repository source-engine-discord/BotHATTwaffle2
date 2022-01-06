using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using BotHATTwaffle2.Models.LiteDB;
using BotHATTwaffle2.Services;
using BotHATTwaffle2.Util;
using Discord;
using Discord.WebSocket;
using Google.Apis.Safebrowsing.v4;
using Google.Apis.Safebrowsing.v4.Data;
using Google.Apis.Services;
using Newtonsoft.Json.Linq;

namespace BotHATTwaffle2.Handlers
{
    public class BlacklistHandler
    {
        private readonly List<Blacklist> _blacklist;
        private readonly SocketMessage _message;
        private readonly DataService _dataService;

        private static readonly HttpClient httpClient = new HttpClient();
        public BlacklistHandler(List<Blacklist> blacklist, SocketMessage message, DataService dataService)
        {
            _blacklist = blacklist;
            _message = message;
            _dataService = dataService;
        }
        /// <summary>
        /// Checks if a message matches in the blacklist.
        /// </summary>
        /// <returns>Returned true if the message matches a blacklist entry.</returns>
        public bool CheckBlacklist()
        {
            var blCheckResult = LevelOneBlacklistCheck();

            if (blCheckResult != null)
            {
                ApplyMute(blCheckResult);
                return true;
            }

            //TODO: Implement "Fuzzy" matching with regex as a "Level 2" check

            Regex fullURL = new Regex(@"(https?://)[\S]+", RegexOptions.Compiled | RegexOptions.IgnoreCase);
            MatchCollection matchesURL = fullURL.Matches(_message.Content);

            foreach (Match match in matchesURL)
            {
                string check = CheckURL(match.Value).Result;
                if (check != null)
                {
                    UnsafeUrl();
                    return true;
                }
            }

            //Looks up domain registration date and rejects anything registered within the past 7 days
            //fetches domain only, ignores "www.". Maybe there is a better way to write it
            Regex domain = new Regex(@"(?<=https?://)[^(www.)][^\s/]+|(?<=https?://www.)[^\s/]+", RegexOptions.Compiled | RegexOptions.IgnoreCase);
            MatchCollection matchesDomain = domain.Matches(_message.Content);

            foreach (Match match in matchesDomain)
            {
                var result = CheckDomainRegistryDate(match.Value);
                if (result)
                {
                    UnsafeUrl(true);
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Does a rough check on the blacklist to see if anything directly matches
        /// </summary>
        /// <returns>Returned null if no match is found, else returns the matching word</returns>
        private Blacklist LevelOneBlacklistCheck()
        {
            foreach (Blacklist blacklist in _blacklist)
            {
                if (_message.Content.Contains(blacklist.Word, StringComparison.OrdinalIgnoreCase))
                    return blacklist;
            }
            return null;
        }

        private async Task<string> CheckURL(string url)
        {
            Console.WriteLine($"Checking to following URL using the Google Safe Browsing API:\n{url}");
            var service = new SafebrowsingService(new BaseClientService.Initializer
            {
                ApplicationName = "dotnet-client",
                ApiKey = _dataService.RSettings.ProgramSettings.GoogleSafeBrowsingAPI
            });

            var request = service.ThreatMatches.Find(new GoogleSecuritySafebrowsingV4FindThreatMatchesRequest()
            {
                Client = new GoogleSecuritySafebrowsingV4ClientInfo
                {
                    ClientId = "Dotnet-client",
                    ClientVersion = "1.5.2"
                },
                ThreatInfo = new GoogleSecuritySafebrowsingV4ThreatInfo()
                {
                    ThreatTypes = new List<string> { "Malware", "Social_Engineering", "Unwanted_Software", "Potentially_Harmful_Application" },
                    PlatformTypes = new List<string> { "Any_Platform" },
                    ThreatEntryTypes = new List<string> { "URL" },
                    ThreatEntries = new List<GoogleSecuritySafebrowsingV4ThreatEntry>
                {
                    new GoogleSecuritySafebrowsingV4ThreatEntry
                    {
                        Url = url
                    }
                }
                }
            });

            var response = await request.ExecuteAsync();
            if (response.Matches != null)
            {
                //returns only first threat
                return response.Matches[0].ThreatType;
            }

            service.Dispose();

            return null;
        }

        private  bool CheckDomainRegistryDate(string value)
        {
            Console.WriteLine($"Checking to following URL domain registration date:\n{value}");
            httpClient.DefaultRequestHeaders.Accept.Clear();
            httpClient.DefaultRequestHeaders.Accept.Add(
            new MediaTypeWithQualityHeaderValue("application/vnd.github.v3+json"));
            httpClient.DefaultRequestHeaders.Add("User-Agent", ".NET Foundation Repository Reporter");

            var httpClientResult = httpClient.GetStringAsync("https://rdap.org/domain/" + value).Result;

            try
            {
                JObject resultJSON = JObject.Parse(httpClientResult);
                var registryDate = DateTime.Parse(
                    resultJSON["events"]
                    .Where(c => c["eventAction"].ToString() == "registration")
                    .First()["eventDate"]
                    .ToString());
                if (DateTime.Now - registryDate < TimeSpan.FromDays(7))
                {
                    return true;
                }

                return false;
            }
            catch
            {
                //URL not found 
                return false;
            }
        }


        private async void ApplyMute(Blacklist blacklist)
        {
            //Warn users for messages with no auto mute duration
            if (blacklist.AutoMuteDuration == 0)
            {
                var message = await _message.Channel.SendMessageAsync(embed: new EmbedBuilder()
                    .WithAuthor($"{_message.Author.Username}")
                    .WithDescription($"Your message has been deleted because it matched an entry in our blacklist." +
                                     $"\nPlease ask a staff member if you're unsure why.")
                    .WithColor(new Color(165, 55, 55))
                    .Build());

                await Task.Delay(10000);
                await message.DeleteAsync();
                return;
            }

            await _dataService.VoidChannel.SendMessageAsync(embed: new EmbedBuilder()
                .WithAuthor($"{_message.Author} | {_message.Author.Id} has been muted")
                .WithDescription(
                    $"**BLACKLIST VIOLATION** `{blacklist.Word}` resulted in auto mute for `{blacklist.AutoMuteDuration}` minutes." +
                    $"\nIn channel: `{_message.Channel.Name}`" +
                    $"\nTheir message was: `{_message.Content}`")
                .WithColor(new Color(165, 55, 55))
                .Build());

            if (blacklist.AutoMuteDuration >= 43200)
                await _dataService.AdminChannel.SendMessageAsync($"The blacklist just had a critical match - Likely a scammer. Please check {_dataService.VoidChannel.Mention}");

            await _dataService.MuteUser(_dataService.GetSocketGuildUser(_message.Author.Id),
                TimeSpan.FromMinutes(blacklist.AutoMuteDuration),
                $"BLACKLIST VIOLATION [{blacklist.Word}]", _message);
        }

        private async void UnsafeUrl(bool shouldMute = false)
        {
            if (shouldMute)
            {
                string msg = _message.Content;
                if (msg.Length > 1600)
                {
                    msg = msg.Substring(0, 1500) + "...";
                }

                await _dataService.VoidChannel.SendMessageAsync(embed: new EmbedBuilder()
                    .WithAuthor($"{_message.Author} | {_message.Author.Id} has been muted")
                    .WithDescription(
                        $"**BLACKLIST VIOLATION** resulted in auto mute for `43200` minutes." +
                        $"\nIf you believe this was a mistake, please contact a staff member." +
                        $"\nIn channel: `{_message.Channel.Name}`" +
                        $"\nTheir message was:\n```{msg}```")
                    .WithColor(new Color(165, 55, 55))
                    .Build());

                await _dataService.MuteUser(_dataService.GetSocketGuildUser(_message.Author.Id),
                    TimeSpan.FromMinutes(43200),
                    $"BLACKLIST VIOLATION [NEW DOMAIN LINKED]", _message);

                await _dataService.AdminChannel.SendMessageAsync($"{_message.Author} was muted because they linked a newly registered domain. Please check {_dataService.VoidChannel.Mention}");
                return;
            }

            var message = await _message.Channel.SendMessageAsync(embed: new EmbedBuilder()
                .WithAuthor($"{_message.Author.Username}")
                .WithDescription($"Your message has been deleted because it contained an unsafe URL." +
                                    $"\nPlease ask a staff member if you're unsure why.")
                .WithColor(new Color(165, 55, 55))
                .Build());

            await Task.Delay(10000);
            await message.DeleteAsync();
        }
    }
}
