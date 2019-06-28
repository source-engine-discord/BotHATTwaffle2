using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using BotHATTwaffle2.Handlers;
using BotHATTwaffle2.Services;
using Imgur.API.Authentication.Impl;
using Imgur.API.Endpoints.Impl;

namespace BotHATTwaffle2.Util
{
    public static class GeneralUtil
    {
        private static LogHandler _log;
        private static DataService _dataService;
        private const ConsoleColor LOG_COLOR = ConsoleColor.DarkBlue;
        public static void SetHandlers(LogHandler log, DataService data)
        {
            _log = log;
            _dataService = data;
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
                _ = _log.LogMessage(e.ToString(), alert: true, color: LOG_COLOR);
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
                                    $"\nImages Found:\n{string.Join("\n", images)}", false, color: LOG_COLOR);

                return images;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Takes a full / partial server address to find the server code.
        /// </summary>
        /// <param name="fullServerAddress">Full address of server</param>
        /// <returns>Server code</returns>
        public static string GetServerCode(string fullServerAddress)
        {
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
    }
}
