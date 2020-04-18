using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using BotHATTwaffle2.Handlers;
using BotHATTwaffle2.Models.LiteDB;
using FaceitLib.Models.ClassObjectLists;

namespace BotHATTwaffle2.Services.FaceIt
{
    class FaceItGameInfo
    {
        private readonly MatchesListObject _match;
        private readonly FaceItHub _hub;
        private readonly string _tempPath = string.Concat(Path.GetTempPath(), @"DemoGrabber");
        private readonly string _jsonLocation;
        private readonly int _index;
        private readonly DateTime _demoStartTime;

        public long DownloadSize { get; private set; }

        public bool Skip { get; private set; }
        public bool DownloadSuccess { get; private set; }
        public string DownloadResponse { get; private set; }

        public bool UnzipSuccess { get; private set; }
        public string UnzipResponse { get; private set; }

        public FaceItGameInfo(MatchesListObject match, FaceItHub hub, string jsonLocation, int index)
        {
            _match = match;
            _hub = hub;
            _jsonLocation = jsonLocation;
            _index = index;

            _demoStartTime = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddSeconds(_match.StartedAt);
        }

        public string GetMapName()
        {
            return _match.Voting.Map.Pick[_index];
        }

        public string GetDemoUrl()
        {
            return _match.DemoURL[_index];
        }

        public string GetHubType()
        {
            return _hub.HubType;
        }

        public string GetGameUID()
        {
            return $"{GetMapName()}_{_index}_{_match.MatchID}";
        }

        /// <summary>
        /// Gets the base URL for local paths.
        /// </summary>
        /// <returns>Base path without file extension</returns>
        public string GetBaseTempPath()
        {
            return $"{_tempPath}\\" +
                   $"{_demoStartTime:MM_dd_yyyy}\\" +
                   $"{_hub.HubName}\\" +
                   $"{GetMapName()}\\";
        }

        /// <summary>
        /// Provides the path to where the local JSON will should be, or will be after parsing.
        /// </summary>
        /// <returns>Full path for the parsed JSON file</returns>
        public string GetPathLocalJson()
        {
            return $"{_jsonLocation}\\" +
                   $"{_demoStartTime:MM_dd_yyyy}\\" +
                   $"{_hub.HubName}\\" +
                   $"{GetGameUID()}.json";
        }

        /// <summary>
        /// Provides the path to where the the GZ file containing the demo is, or will be after downloading.
        /// </summary>
        /// <returns>Full path for the GZ file containing the demo</returns>
        public string GetPathTempGz()
        {
            return GetBaseTempPath() + GetGameUID() + ".gz";
        }

        /// <summary>
        /// Provides the path to where the the DEMO file of the demo is, or will be after extracting.
        /// </summary>
        /// <returns></returns>
        public string GetPathTempDemo()
        {
            return GetBaseTempPath() + GetGameUID() + ".dem";
        }

        public void SetDownloadSuccess(bool status)
        {
            DownloadSuccess = status;
        }

        public void SetDownloadResponse(string response)
        {
            DownloadResponse = response;
        }

        public void SetUnzipSuccess(bool status)
        {
            UnzipSuccess = status;
        }

        public void SetUnzipResponse(string response)
        {
            UnzipResponse = response;
        }

        public DateTime GetStartDate()
        {
            return _demoStartTime;
        }

        public void SetDownloadSize(long size)
        {
            DownloadSize = size;
        }

        public string GetMatchId()
        {
            return _match.MatchID;
        }

        public void SetSkip(bool skip)
        {
            Skip = skip;
        }
    }
}
