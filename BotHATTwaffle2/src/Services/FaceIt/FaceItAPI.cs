using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using BotHATTwaffle2.Handlers;
using BotHATTwaffle2.Models.LiteDB;
using BotHATTwaffle2.Services.Steam;
using BotHATTwaffle2.Util;
using FaceitLib;
using FaceitLib.Models.ClassObjectLists;

namespace BotHATTwaffle2.Services.FaceIt
{
    public class FaceItApi
    {
        private const ConsoleColor LOG_COLOR = ConsoleColor.DarkYellow;

        private const int DL_UNZIP_RETRY_LIMIT = 4;

        private const string UPDATE_BASE_URL =
            @"https://www.tophattwaffle.com/demos/requested/build.php?idoMode=true&build=";

        private static bool _running;
        private readonly DataService _dataService;

        private readonly LogHandler _log;
        private readonly List<string> _matchesFailedParse = new List<string>();
        private readonly List<string> _matchesWithNullDemoUrls = new List<string>();
        private readonly List<string> _matchesWithNullVoting = new List<string>();
        private readonly List<string> _siteUpdateCalls = new List<string>();
        private readonly List<string> _siteUpdateResponses = new List<string>();
        private readonly Dictionary<FileInfo, string> _uploadDictionary = new Dictionary<FileInfo, string>();
        private List<FaceItGameInfo> _gameInfo = new List<FaceItGameInfo>();
        private DateTime _runStartTime;

        private Dictionary<FaceItHub, HttpStatusCode> _statusCodes;
        private int _uploadSuccessCount;

        public FaceItApi(DataService dataService, LogHandler log)
        {
            _dataService = dataService;
            _log = log;
        }

        private async Task<string> GetReport()
        {
            var failedGames = _gameInfo.Where(x => (!x.DownloadSuccess || !x.UnzipSuccess) && !x.Skip).ToArray();
            if (failedGames.Count() != 0)
            {
                var failedMessagePostCount = 0;
                var channelLog = true;
                foreach (var failedGame in failedGames)
                {
                    if (failedMessagePostCount == 5)
                    {
                        await _log.LogMessage(
                            "To prevent spam, channel logging is now disabled, see bot console for more logs", true,
                            alert: true, color: LOG_COLOR);
                        channelLog = false;
                    }

                    await _log.LogMessage("Something happened downloading or unzipping a demo!\n" +
                                          $"UID: {failedGame.GetGameUID()}\n" +
                                          $"Download Status: {failedGame.DownloadSuccess}\n" +
                                          $"Unzip Status: {failedGame.UnzipSuccess}\n" +
                                          $"Download Response: {failedGame.DownloadResponse}\n" +
                                          $"Unzip Response: {failedGame.UnzipResponse}", channelLog,
                        color: LOG_COLOR);

                    failedMessagePostCount++;
                }
            }

            //Display out matches that are null for demo URL
            var message = "";
            foreach (var nullMatch in _matchesWithNullDemoUrls)
            {
                message += $"`{nullMatch}`\n";

                if (message.Length > 1800)
                {
                    await _log.LogMessage($"Match IDs with null Demo URLs:\n{message}");
                    message = "";
                }
            }

            await _log.LogMessage($"Match IDs with null Demo URLs:\n{message}");

            //Display out matches that are null for voting
            message = "";
            foreach (var nullMatch in _matchesWithNullVoting)
            {
                message += $"`{nullMatch}`\n";

                if (message.Length > 1800)
                {
                    await _log.LogMessage($"Match IDs with null Voting:\n{message}");
                    message = "";
                }
            }

            await _log.LogMessage($"Match IDs with null Voting:\n{message}");

            //Display out matches that are null for voting
            message = "";
            foreach (var failedParse in _matchesFailedParse)
            {
                message += $"`{failedParse}`\n";

                if (message.Length > 1800)
                {
                    await _log.LogMessage($"Match IDs that failed parsing:\n{message}");
                    message = "";
                }
            }

            await _log.LogMessage($"Match IDs that failed parsing:\n{message}");

            return $"Start Time: `{_runStartTime}` | Ran for `{DateTime.Now.Subtract(_runStartTime).ToString()}`\n" +
                   $"Total Matches: {_gameInfo.Count}\n" +
                   $"Total New Matches (Not Previously Seen): {_gameInfo.Count(x => !x.Skip)}\n" +
                   $"Demos Downloaded: `{_gameInfo.Count(x => x.DownloadSuccess && !x.Skip)}`\n" +
                   $"Demos Unzipped: `{_gameInfo.Count(x => x.UnzipSuccess && !x.Skip)}`\n" +
                   $"Failed Downloads: `{_gameInfo.Count(x => !x.DownloadSuccess && !x.Skip)}`\n" +
                   $"Failed Unzipped: `{_gameInfo.Count(x => !x.UnzipSuccess && !x.Skip)}`\n" +
                   $"Files Uploaded: `{_uploadSuccessCount}`\n" +
                   $"Data Downloaded: `{Math.Round(_gameInfo.Select(x => x.DownloadSize).Sum() / 1024f / 1024f, 2)}MB`\n" +
                   $"Demos that Failed to Parse: `{_matchesFailedParse.Count}`\n" +
                   $"Matches with Null Demo URLs: `{_matchesWithNullDemoUrls.Count}`\n" +
                   $"Matches with Null Voting: `{_matchesWithNullVoting.Count}`\n\n" +
                   "Update Responses:\n" +
                   $"{string.Join("\n", _siteUpdateResponses)}";
        }

        public async Task<string> GetDemos(DateTime startTime, DateTime endTime)
        {
            if (_running)
                return "Already getting demos!";

            _runStartTime = DateTime.Now;
            _running = true;

            //Populate the matches list
            await GetMatches(startTime, endTime);

            if (_gameInfo.Count == 0)
            {
                var msg = "No matches were found on any endpoint!";
                await _log.LogMessage(msg);
                _running = false;
                return msg;
            }

            //Start downloading the files and unzip
            await HandleMatchDemos();

            //Parse the demos
            await HandleDemoParsing();

            //Handle games after they are parsed
            await HandleParsedGames();

            //Send the files to the uploader
            _uploadSuccessCount = await DemoParser.UploadFaceitDemosAndRadars(_uploadDictionary);

            //Delete the old .dem files, we don't need them anymore
            DeleteOldFiles();

            //Update the files on the website
            await UpdateWebsiteFiles();

            var report = await GetReport();
            await _log.LogMessage(report);
            _running = false;
            return report;
        }

        private async Task UpdateWebsiteFiles()
        {
            await _log.LogMessage("Calling site update URLs...", false, color: LOG_COLOR);
            var web = new WebClient();
            foreach (var tag in _siteUpdateCalls)
            {
                var reply = await web.DownloadStringTaskAsync(UPDATE_BASE_URL + tag);
                _siteUpdateResponses.Add($"{tag}: `{reply}`");
            }
        }

        private void DeleteOldFiles()
        {
            _ = _log.LogMessage("Deleting old demo files...", false, color: LOG_COLOR);

            foreach (var demo in _gameInfo)
                if (File.Exists(demo.GetPathTempDemo()))
                    File.Delete(demo.GetPathTempDemo());
        }

        /// <summary>
        ///     Gets matches from all hubs inside the database.
        /// </summary>
        /// <param name="startTime">Start date to get demos from</param>
        /// <param name="endTime">End time to get demos to</param>
        /// <returns>Task Complete</returns>
        private async Task GetMatches(DateTime startTime, DateTime endTime)
        {
            var faceit = new FaceitClient(_dataService.RSettings.ProgramSettings.FaceitAPIKey);

            _gameInfo = new List<FaceItGameInfo>();

            //Status for each hub call
            _statusCodes = new Dictionary<FaceItHub, HttpStatusCode>();

            foreach (var hub in DatabaseUtil.GetHubs())
            {
                await _log.LogMessage($"Calling Faceit Endpoint: `{hub.HubName}`", color: LOG_COLOR);

                var callResult = new List<MatchesListObject>();

                //hub
                if (hub.Endpoint == 0)
                    callResult = await faceit.GetMatchesFromHubBetweenDates(hub.HubGUID, startTime, endTime);
                //Championships
                else if (hub.Endpoint == 1)
                    callResult = await faceit.GetMatchesFromChampionshipBetweenDates(hub.HubGUID, startTime, endTime);

                var callStatus = faceit.GetStatusCode();

                //Store the status code for later, used for logging purposes.
                _statusCodes.Add(hub, callStatus);

                //If status wasn't OK, go to the next hub.
                if (callStatus != HttpStatusCode.OK)
                    continue;

                //Add each individual game into a list representing a single game
                foreach (var match in callResult)
                {
                    if (match.DemoURL == null)
                    {
                        _matchesWithNullDemoUrls.Add(match.MatchID);
                        continue;
                    }

                    if (match.Voting == null)
                    {
                        _matchesWithNullVoting.Add(match.MatchID);
                        continue;
                    }

                    for (var i = 0; i < match.DemoURL.Count; i++)
                        _gameInfo.Add(new FaceItGameInfo(match, hub,
                            _dataService.RSettings.ProgramSettings.FaceItDemoPath, i));
                }
            }
        }

        private async Task HandleParsedGames()
        {
            var hubTags = DatabaseUtil.GetHubTags();

            foreach (var game in _gameInfo.Where(x => x.DownloadSuccess && x.UnzipSuccess))
            {
                //Don't re-upload something we already uploaded
                if (game.Skip)
                    continue;

                if (_dataService.RSettings.ProgramSettings.Debug)
                    await _log.LogMessage("Adding " + game.GetPathLocalJson() + " to the upload list!", false,
                        color: LOG_COLOR);

                //Get the hub with the desired date and season tags.
                FaceItHubTag targetTag = null;
                targetTag = hubTags.FirstOrDefault(x =>
                    x.StartDate.ToUniversalTime() < game.GetStartDate() && x.EndDate.ToUniversalTime() >
                                                                        game.GetStartDate()
                                                                        && x.Type.Equals(game.GetHubType(),
                                                                            StringComparison.OrdinalIgnoreCase));

                //Try to get set our selected tag.
                var selectedTag = targetTag?.TagName;

                //targetTag was unknown, so default it.
                if (targetTag == null)
                {
                    selectedTag = "UNKNOWN";
                    _ = _log.LogMessage(
                        $"Hub seasons have no definitions in the database for date `{game.GetStartDate()}`!\n`{game.GetGameUID()}`",
                        false, color: LOG_COLOR);
                }

                //Get the json file to be sent to the server
                var jsonDir = Path.GetDirectoryName(game.GetPathLocalJson());
                FileInfo targetFile;
                try
                {
                    targetFile = new FileInfo(
                        Directory.GetFiles(jsonDir).FirstOrDefault(x => x.Contains(game.GetGameUID())) ??
                        throw new InvalidOperationException());
                }
                catch (Exception e)
                {
                    if (_dataService.RSettings.ProgramSettings.Debug)
                        await _log.LogMessage($"Issue getting file {game.GetGameUID()} It likely failed to parse\n{e}",
                            color: LOG_COLOR);
                    _matchesFailedParse.Add(game.GetMatchId());
                    continue;
                }

                //Determine paths on the remote server
                var remoteDirectory = $"{selectedTag}_{game.GetMapName()}";
                //Path to local directory containing radars
                var localRadarDir = $"{_dataService.RSettings.ProgramSettings.FaceItDemoPath}\\Radars\\{selectedTag}";

                //Create the directory where we will store the radar files
                Directory.CreateDirectory(localRadarDir);

                //Get the radar file paths
                var radarPng = Directory.GetFiles(localRadarDir, $"*{game.GetMapName()}*.png",
                    SearchOption.AllDirectories);
                var radarTxt = Directory.GetFiles(localRadarDir, $"*{game.GetMapName()}*.txt",
                    SearchOption.AllDirectories);

                //No radar or text file found. We need to get them and include in the upload.
                if (radarTxt.Length == 0 || radarPng.Length == 0)
                {
                    if (_dataService.RSettings.ProgramSettings.Debug)
                        await _log.LogMessage($"Getting radar files for {targetFile}", false, color: LOG_COLOR);

                    var wsId = DemoParser.GetWorkshopIdFromJasonFile(targetFile);

                    var steamApi = new SteamAPI(_dataService, _log);

                    //Handle radar files
                    var radarFiles =
                        await steamApi.GetWorkshopMapRadarFiles(game.GetBaseTempPath().TrimEnd('\\'), wsId);

                    if (radarFiles != null)
                        foreach (var radarFile in radarFiles)
                        {
                            if (File.Exists($"{localRadarDir}\\{radarFile.Name}"))
                                File.Delete($"{localRadarDir}\\{radarFile.Name}");

                            File.Move(radarFile.FullName, $"{localRadarDir}\\{radarFile.Name}");

                            //Add to list of dict to upload
                            _uploadDictionary.Add(new FileInfo($"{localRadarDir}\\{radarFile.Name}"), remoteDirectory);
                        }
                }
                else
                {
                    if (_dataService.RSettings.ProgramSettings.Debug)
                        await _log.LogMessage($"Skipping radar files for {targetFile}", false, color: LOG_COLOR);
                }

                //Add the tag to be called later
                if (!_siteUpdateCalls.Contains(remoteDirectory))
                    _siteUpdateCalls.Add(remoteDirectory);

                //Add to dict of files to upload
                _uploadDictionary.Add(targetFile, remoteDirectory);
            }
        }

        private async Task HandleDemoParsing()
        {
            var parsedFolders = new List<string>();
            var parsingWork = new List<Task>();
            foreach (var game in _gameInfo.Where(x => x.DownloadSuccess && x.UnzipSuccess))
            {
                //Did we already parse this source folder?
                if (!parsedFolders.Contains(game.GetBaseTempPath()))
                {
                    if (_dataService.RSettings.ProgramSettings.Debug)
                        await _log.LogMessage($"Starting Parser Instance for {game.GetBaseTempPath()}", false,
                            color: LOG_COLOR);

                    //Make sure we don't parse this folder again
                    parsedFolders.Add(game.GetBaseTempPath());

                    //The destination for the parsed demo
                    var destination = Path.GetDirectoryName(game.GetPathLocalJson());

                    Directory.CreateDirectory(destination);

                    //Spawn the parser
                    parsingWork.Add(DemoParser.ParseFaceitDemos(game.GetBaseTempPath().TrimEnd('\\'), destination));
                }

                //8 Parser instances max
                if (parsingWork.Count >= 8)
                {
                    await Task.WhenAny(parsingWork);

                    //Remove all finished tasks so we can spawn more
                    parsingWork.RemoveAll(x => x.IsCompleted);
                }
            }

            //Wait for all the finish
            await Task.WhenAll(parsingWork);
        }


        /// <summary>
        ///     Starts multiple tasks at once to increase the speed of processing.
        /// </summary>
        /// <returns></returns>
        private async Task HandleMatchDemos()
        {
            var demoTasks = new List<Task>();

            foreach (var game in _gameInfo)
            {
                //Determine if we should skip this game because we already have it
                var dir = Path.GetDirectoryName(game.GetPathLocalJson());
                if (Directory.Exists(dir))
                {
                    var localFiles = Directory.GetFiles(dir);
                    if (localFiles.Any(x => x.Contains(game.GetGameUID())))
                    {
                        if (_dataService.RSettings.ProgramSettings.Debug)
                            await _log.LogMessage($"Already have {game.GetPathLocalJson()}! Skipping", false,
                                color: LOG_COLOR);

                        game.SetSkip(true);
                        continue;
                    }
                }

                demoTasks.Add(DownloadAndUnzipDemo(game));

                if (demoTasks.Count >= 8)
                    //Wait for a task to complete, then remove it from the list.
                    demoTasks.Remove(await Task.WhenAny(demoTasks));
            }

            //Wait for the rest of the tasks to finish
            await Task.WhenAll(demoTasks);
        }

        private async Task DownloadAndUnzipDemo(FaceItGameInfo faceItGameInfo)
        {
            for (var i = 0; i < DL_UNZIP_RETRY_LIMIT; i++)
            {
                var downloadResult = await DownloadDemo(faceItGameInfo);

                var unzipResult = false;

                //Only unzip if download was a success
                if (downloadResult)
                    unzipResult = await UnzipDemo(faceItGameInfo);

                //Unzip was a success, return
                if (unzipResult)
                    return;

                await Task.Delay(5000);
            }
        }

        private async Task<bool> UnzipDemo(FaceItGameInfo faceItGameInfo)
        {
            var sourceFile = faceItGameInfo.GetPathTempGz();
            var destinationFile = faceItGameInfo.GetPathTempDemo();

            var gzipFileName = new FileInfo(sourceFile);

            using (var fileToDecompressAsStream = gzipFileName.OpenRead())
            {
                using (var decompressedStream = File.Create(destinationFile))
                {
                    using (var decompressionStream =
                        new GZipStream(fileToDecompressAsStream, CompressionMode.Decompress))
                    {
                        try
                        {
                            await decompressionStream.CopyToAsync(decompressedStream);
                        }
                        catch (Exception e)
                        {
                            if (_dataService.RSettings.ProgramSettings.Debug)
                                await _log.LogMessage(
                                    "Failed to unzip. Will retry if the limit was not reached. " + sourceFile, false,
                                    color: LOG_COLOR);

                            //Delete the file so we can re-download it
                            if (File.Exists(sourceFile))
                                File.Delete(sourceFile);

                            faceItGameInfo.SetUnzipSuccess(false);
                            faceItGameInfo.SetUnzipResponse(e.ToString());
                            return false;
                        }
                    }
                }

                await _log.LogMessage($"Unzipped {destinationFile}", false, color: LOG_COLOR);
            }

            faceItGameInfo.SetUnzipResponse("Unzip Successful");
            faceItGameInfo.SetUnzipSuccess(true);

            //Unzipped file, can delete the GZ
            if (File.Exists(sourceFile))
                File.Delete(sourceFile);

            return true;
        }

        private async Task<bool> DownloadDemo(FaceItGameInfo faceItGameInfo)
        {
            var localPath = faceItGameInfo.GetPathTempGz();
            var remotePath = faceItGameInfo.GetDemoUrl();

            Directory.CreateDirectory(Path.GetDirectoryName(localPath));

            await _log.LogMessage($"Downloading: {remotePath}", false, color: LOG_COLOR);

            using (var client = new WebClient())
            {
                client.Headers.Add("User-Agent: Other");
                try
                {
                    await client.DownloadFileTaskAsync(remotePath, localPath);
                    faceItGameInfo.SetDownloadSize(new FileInfo(localPath).Length);
                }
                catch (Exception e)
                {
                    if (_dataService.RSettings.ProgramSettings.Debug)
                        await _log.LogMessage("Error downloading demo, retrying. " + remotePath, false,
                            color: LOG_COLOR);

                    if (File.Exists(localPath)) File.Delete(localPath);
                    client.Dispose();

                    faceItGameInfo.SetDownloadSuccess(false);
                    faceItGameInfo.SetDownloadResponse(e.ToString());

                    return false;
                }

                await _log.LogMessage($"Downloaded {remotePath}", false, color: LOG_COLOR);
            }

            faceItGameInfo.SetDownloadResponse("Download Successful");
            faceItGameInfo.SetDownloadSuccess(true);
            return true;
        }
    }
}