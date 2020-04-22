using System;
using System.IO;
using BotHATTwaffle2.Models.LiteDB;
using FaceitLib.Models.ClassObjectLists;

namespace BotHATTwaffle2.Services.FaceIt
{
    internal class FaceItGameInfo
    {
        private readonly DateTime _demoStartTime;
        public FaceItHub Hub { get; }
        private readonly int _index;
        private readonly string _jsonLocation;
        private FileInfo _realJsonLocation;
        private readonly MatchesListObject _match;
        public FaceItHubTag Tag { get; private set; }
        private readonly string _tempPath;


        public FaceItGameInfo(MatchesListObject match, FaceItHub hub, string jsonLocation, int index, string tempPath)
        {
            _match = match;
            Hub = hub;
            _jsonLocation = jsonLocation;
            _index = index;
            _tempPath = tempPath;

            _demoStartTime = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddSeconds(_match.StartedAt);
        }

        public void SetHubTag(FaceItHubTag tag)
        {
            Tag = tag;
        }

        public void SetRealJsonLocation(FileInfo fileInfo)
        {
            _realJsonLocation = fileInfo;
        }

        public FileInfo GetRealJsonLocation()
        {
            return _realJsonLocation;
        }

        public long DownloadSize { get; private set; }

        public bool Skip { get; private set; }
        public bool DownloadSuccess { get; private set; }
        public string DownloadResponse { get; private set; }

        public bool UnzipSuccess { get; private set; }
        public string UnzipResponse { get; private set; }

        public string GetMapName()
        {
            return _match.Voting.Map.Pick[_index];
        }

        public string GetDemoUrl()
        {
            return _match.DemoURL[_index];
        }

        public string GetGameUid()
        {
            return $"{_index}_{_match.MatchID}";
        }

        public string GetLocalDateFolderName()
        {
            return $"{_demoStartTime:MM_dd_yyyy}";
        }

        /// <summary>
        ///     Gets the base URL for local paths.
        /// </summary>
        /// <returns>Base path without file extension</returns>
        public string GetBaseTempPath()
        {
            return $"{_tempPath}\\" +
                   $"{GetLocalDateFolderName()}\\" +
                   $"{Hub.HubName}\\" +
                   $"{GetMapName()}\\";
        }

        /// <summary>
        ///     Provides the path to where the local JSON will should be, or will be after parsing.
        /// </summary>
        /// <returns>Full path for the parsed JSON file</returns>
        public string GetPathLocalJson()
        {
            return $"{_jsonLocation}\\" +
                   $"{GetLocalDateFolderName()}\\" +
                   $"{Hub.HubName}\\" +
                   $"{GetMapName()}_{GetGameUid()}.json";
        }

        /// <summary>
        ///     Provides the path to where the the GZ file containing the demo is, or will be after downloading.
        /// </summary>
        /// <returns>Full path for the GZ file containing the demo</returns>
        public string GetPathTempGz()
        {
            return GetBaseTempPath() + GetGameUid() + ".gz";
        }

        /// <summary>
        ///     Provides the path to where the the DEMO file of the demo is, or will be after extracting.
        /// </summary>
        /// <returns></returns>
        public string GetPathTempDemo()
        {
            return GetBaseTempPath() + GetGameUid() + ".dem";
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