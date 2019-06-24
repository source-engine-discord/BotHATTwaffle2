using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Services;
using Google.Apis.Util;
using Google.Apis.Util.Store;
using Google.Apis.YouTube.v3;
using Google.Apis.YouTube.v3.Data;

namespace BotHATTwaffle2.Services.YouTube
{
    /// <summary>
    /// YouTube Data API v3 sample: search by keyword.
    /// Relies on the Google APIs Client Library for .NET, v1.7.0 or higher.
    /// See https://developers.google.com/api-client-library/dotnet/get_started
    ///
    /// Set ApiKey to the API key value from the APIs & auth > Registered apps tab of
    ///   https://cloud.google.com/console
    /// Please ensure that you have enabled the YouTube Data API for your project.
    /// </summary>
    public class YouTube
    {
        private readonly DataService _dataService;
        public YouTube(DataService dataService)
        {

        }
        private async Task<UserCredential> GetYouTubeCredentials()
        {
            UserCredential credential;
            using (var stream = new FileStream("client_secret.json", FileMode.Open, FileAccess.Read))
            {
                credential = await GoogleWebAuthorizationBroker.AuthorizeAsync(
                    GoogleClientSecrets.Load(stream).Secrets,
                    new[] { YouTubeService.Scope.YoutubeReadonly },
                    "user",
                    CancellationToken.None,
                    new FileDataStore(this.GetType().ToString())
                );
            }
            return credential;
        }

        public async Task<SearchListResponse> YouTubeSearch(string search, long maxResults = 10)
        {
            var youtubeService = new YouTubeService(new BaseClientService.Initializer()
            {
                HttpClientInitializer = await GetYouTubeCredentials(),
                ApplicationName = this.GetType().ToString()
            });

            var searchListRequest = youtubeService.Search.List("snippet");
            searchListRequest.Q = search;
            searchListRequest.ChannelId = "UCiKV_fEUKDc2IxTvTTQLegw"; //My channel ID
            searchListRequest.MaxResults = maxResults;
            searchListRequest.Type = "video";

            // Call the search.list method to retrieve results matching the specified query term.
            try
            {
                return await searchListRequest.ExecuteAsync();
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
                return null;
            }
        }

        /// <summary>
        /// Gets a single video from a search, and ensures that the title matches a string.
        /// </summary>
        /// <param name="search">Search term</param>
        /// <param name="match">String to match on</param>
        /// <returns>Found video, null otherwise</returns>
        public async Task<SearchResult> GetOneYouTubeVideo(string search, string match)
        {
            var results = await YouTubeSearch(search, 3);
            return results?.Items.FirstOrDefault(x => x.Snippet.Title.Contains(match));
        }
    }
}