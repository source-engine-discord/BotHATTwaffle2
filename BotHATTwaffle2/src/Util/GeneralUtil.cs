using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;
using BotHATTwaffle2.Handlers;
using BotHATTwaffle2.Services;
using Discord;
using HtmlAgilityPack;
using Imgur.API.Authentication.Impl;
using Imgur.API.Endpoints.Impl;

namespace BotHATTwaffle2.Util
{
    public static class GeneralUtil
    {
        private const ConsoleColor LOG_COLOR = ConsoleColor.White;
        private static LogHandler _log;
        private static DataService _dataService;
        private static Random _random;

        public static void SetHandlers(LogHandler log, DataService data, Random random)
        {
            _log = log;
            _dataService = data;
            _random = random;
        }

        /// <summary>
        ///     Takes a original SteamID and finds the SteamID64 version
        /// </summary>
        /// <param name="steamID">STEAM_1:0:123456 string</param>
        /// <returns>The SteamID64 version</returns>
        public static long TranslateSteamID(string steamID)
        {
            long result = 0;

            var template = new Regex(@"STEAM_(\d):([0-1]):(\d+)");
            var matches = template.Matches(steamID);
            if (matches.Count <= 0) return 0;
            var parts = matches[0].Groups;
            if (parts.Count != 4) return 0;

            var x = long.Parse(parts[1].Value) << 24;
            var y = long.Parse(parts[2].Value);
            var z = long.Parse(parts[3].Value) << 1;

            result = ((1 + (1 << 20) + x) << 32) | (y + z);
            return result;
        }

        /// <summary>
        ///     Validates a URI as good
        /// </summary>
        /// <param name="input">Input string</param>
        /// <returns>Returns URI object, or null.</returns>
        public static Uri ValidateUri(string input)
        {
            try
            {
                if (Uri.IsWellFormedUriString(input, UriKind.Absolute))
                    return new Uri(input, UriKind.Absolute);

                throw new UriFormatException($"Unable to create URI for input {input}");
            }
            catch (UriFormatException e)
            {
                _ = _log.LogMessage(e.ToString(), alert: true);
            }

            return null;
        }

        /// <summary>
        ///     Provides a list or URLs for each image in an imgur album, or null if not possible
        /// </summary>
        /// <param name="albumUrl">URL of imgur album</param>
        /// <returns>List or URLs, or null</returns>
        public static List<string> GetImgurAlbum(string albumUrl)
        {
            try
            {
                var albumId = albumUrl.Replace(@"/gallery/", @"/a/")
                    .Substring(albumUrl.IndexOf(@"/a/", StringComparison.Ordinal) + 3);
                var client = new ImgurClient(_dataService.RSettings.ProgramSettings.ImgurApi);
                var endpoint = new AlbumEndpoint(client);

                var images = endpoint.GetAlbumAsync(albumId).Result.Images.Select(i => i.Link).ToList();

                _ = _log.LogMessage("Getting Imgur Info from Imgur API" +
                                    $"\nAlbum URL: {albumUrl}" +
                                    $"\nAlbum ID: {albumId}" +
                                    $"\nClient Credits Remaining: {client.RateLimit.ClientRemaining} of {client.RateLimit.ClientLimit}" +
                                    $"\nImages Found:\n{string.Join("\n", images)}", false);

                return images;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        ///     Takes a full / partial server address to find the server code.
        /// </summary>
        /// <param name="fullServerAddress">Full address of server</param>
        /// <returns>Server code</returns>
        public static string GetServerCode(string fullServerAddress)
        {
            fullServerAddress = fullServerAddress.ToLower();
            if (fullServerAddress.Contains('.'))
                return fullServerAddress.Substring(0, fullServerAddress.IndexOf(".", StringComparison.Ordinal));

            return fullServerAddress;
        }

        /// <summary>
        ///     Gets the workshop ID from a FQDN workshop link
        /// </summary>
        /// <param name="workshopUrl">FQDN of workshop link</param>
        /// <returns>Workshop ID</returns>
        public static string GetWorkshopIdFromFqdn(string workshopUrl)
        {
            return Regex.Match(workshopUrl, @"(\d+)").Value;
        }

        /// <summary>
        ///     Validates if a string is a SteamWorkshop URL
        /// </summary>
        /// <param name="workshopUrl">String to validate</param>
        /// <returns>True if valid, false otherwise</returns>
        public static bool ValidateWorkshopURL(string workshopUrl)
        {
            return Regex.IsMatch(workshopUrl, @"^(https:\/\/steamcommunity.com/sharedfiles/filedetails/\?id=)\d+$");
        }

        /// <summary>
        ///     Converts a ConsoleColor into a Discord.Color
        /// </summary>
        /// <param name="c">Input color</param>
        /// <returns>DiscordColor</returns>
        public static Color ColorFromConsoleColor(ConsoleColor c)
        {
            uint[] cColors =
            {
                0x000000, //Black = 0
                0x000080, //DarkBlue = 1
                0x008000, //DarkGreen = 2
                0x008080, //DarkCyan = 3
                0x800000, //DarkRed = 4
                0x800080, //DarkMagenta = 5
                0x808000, //DarkYellow = 6
                0xC0C0C0, //Gray = 7
                0x808080, //DarkGray = 8
                0x0000FF, //Blue = 9
                0x00FF00, //Green = 10
                0x00FFFF, //Cyan = 11
                0xFF0000, //Red = 12
                0xFF00FF, //Magenta = 13
                0xFFFF00, //Yellow = 14
                0xFFFFFF //White = 15
            };

            return new Color(cColors[(int) c]);
        }

        public static void Shuffle<T>(T[] array)
        {
            var random = new Random(DateTime.Now.Millisecond);
            var n = array.Length;
            for (var i = 0; i < n; i++)
            {
                // Use Next on random instance with an argument.
                // ... The argument is an exclusive bound.
                //     So we will not go past the end of the array.
                var r = i + random.Next(n - i);
                var t = array[r];
                array[r] = array[i];
                array[i] = t;
            }
        }

        /// <summary>
        ///     Gets a IPHostEntry from a FQDN or an IP address.
        /// </summary>
        /// <param name="address">FQDN or address</param>
        /// <returns>Populated IPHostEntry object, null if not found</returns>
        public static IPHostEntry GetIPHost(string address)
        {
            if (address.Contains(':')) address = address.Substring(0, address.IndexOf(":", StringComparison.Ordinal));
            IPHostEntry iPHostEntry = null;
            try
            {
                iPHostEntry = Dns.GetHostEntry(address);
            }
            catch (Exception e)
            {
                _ = _log.LogMessage($"Failed to get iPHostEntry for `{address}`", alert: true);
                Console.WriteLine(e);
                throw;
            }

            return iPHostEntry;
        }

        /// <summary>
        ///     Provided a URL, will scan the page for all files that end in a file
        ///     It then picks one at random and returns that
        ///     Example Page: https://content.tophattwaffle.com/BotHATTwaffle/catfacts/
        /// </summary>
        /// <param name="inUrl">URL to look at</param>
        /// <returns>inUrl + ImageName.ext</returns>
        public static string GetRandomImgFromUrl(string inUrl)
        {
            //New web client
            var htmlWeb = new HtmlWeb();

            //Load page
            var htmlDocument = htmlWeb.Load(inUrl);

            //Add each image to a list
            var validImg = htmlDocument.DocumentNode.SelectNodes("//a[@href]").Select(link =>
                    link.GetAttributeValue("href", string.Empty).Replace(@"\", "").Replace("\"", ""))
                .Where(Path.HasExtension).ToList();

            return inUrl + validImg[_random.Next(0, validImg.Count)];
        }
    }
}