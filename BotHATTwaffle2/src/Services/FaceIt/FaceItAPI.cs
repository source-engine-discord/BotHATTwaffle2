using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using BotHATTwaffle2.Handlers;
using BotHATTwaffle2.Models.FaceIt;
using BotHATTwaffle2.Models.JSON;
using BotHATTwaffle2.Models.LiteDB;
using BotHATTwaffle2.Services.Steam;
using BotHATTwaffle2.src.Util;
using BotHATTwaffle2.Util;
using Newtonsoft.Json.Linq;

namespace BotHATTwaffle2.Services.FaceIt
{
    public class FaceItApi
    {
        private const ConsoleColor LOG_COLOR = ConsoleColor.DarkYellow;

        // Behaviour handing when hammering API endpoints
        private const bool KEEP_CALLING_API = true;
        private const int API_LIMIT = 100;
        private const int ERROR_CALLING_API_COUNT_MAX = 20;
        private const string HUB_BASE_URL = @"https://open.faceit.com/data/v4/hubs/";
        private const int DOWNLOAD_AND_ZIP_RETRY_LIMIT = 20;
        private const string UPDATE_BASE_URL = @"https://www.tophattwaffle.com/demos/requested/build.php?idoMode=true&build=";

        private static bool _running;

        private readonly DataService _dataService;
        private readonly LogHandler _log;
        private readonly string _tempPath;

        private int _demosDownloaded;
        private int _demosUnZipped;
        private int _demosUploaded;
        private long _downloadedData;
        private string _updateResponses;

        private List<string> _siteUpdateCalls = new List<string>();

        private DateTime _startTime;

        public FaceItApi(DataService dataService, LogHandler log)
        {
            _dataService = dataService;
            _log = log;
            _tempPath = string.Concat(Path.GetTempPath(), @"DemoGrabber");
        }

        public async Task<string> GetDemos(DateTime startTime, DateTime endTime)
        {
            if (_running)
                return "Already getting demos...";

            _startTime = DateTime.Now;

            _running = true;
            foreach (var hub in _dataService.RSettings.FaceItHubs)
            {
                var reply = await CallHubApiEndPoint(hub, startTime, endTime);

                if (reply.FileNames.Count == 0)
                {
                    await _log.LogMessage($"No items found in API request. Skipping call to {hub.HubName}", false,
                        color: LOG_COLOR);
                    continue;
                }

                var dlResult = await DownloadHubDemos(hub.HubName, reply);

                if (dlResult.Length == 0)
                {
                    await _log.LogMessage($"No items for {hub.HubName}. We likely already have them all.", false,
                        color: LOG_COLOR);
                    continue;
                }

                await ParseDemos(dlResult);
                await UploadParsedFiles(dlResult, hub);
                DeleteDemoFiles(dlResult);
            }

            await UpdateWebsiteFiles();

            var report = GetReport();
            await _log.LogMessage(report);
            _running = false;
            return report;
        }

        private async Task UpdateWebsiteFiles()
        {
            var web = new WebClient();
            foreach (var tag in _siteUpdateCalls)
            {
                await _log.LogMessage($"Calling site update URL: " + UPDATE_BASE_URL + tag,false, color: LOG_COLOR);

                var reply = await web.DownloadStringTaskAsync(UPDATE_BASE_URL + tag);
                _updateResponses += $"{tag}: `{reply}`\n";

                await _log.LogMessage($"Response: " + reply,false, color: LOG_COLOR);
            }
            
        }

        private string GetReport()
        {
            if (_updateResponses == null)
                _updateResponses = "`No updates requested`";
            return $"Start Time: `{_startTime}` | Ran for `{DateTime.Now.Subtract(_startTime).ToString()}`" +
                   $"\nDemos Downloaded: `{_demosDownloaded}`" +
                   $"\nDemos Unzipped: `{_demosUnZipped}`" +
                   $"\nFiles Uploaded: `{_demosUploaded}`" +
                   $"\nData Downloaded: `{Math.Round(_downloadedData / 1024f / 1024f, 2)}MB`" +
                   $"\nUpdate Responses:\n{_updateResponses.Trim()}";
        }

        private async Task UploadParsedFiles(DemoResult[] demoResults, FaceItHub hub)
        {
            var hubSeasons = DatabaseUtil.GetHubTypes();
            var uploadDictionary = new Dictionary<FileInfo, string>();
            foreach (var demo in demoResults)
            {
                if (demo.Skip || demo.DownloadFailed || demo.UnzipFailed)
                    continue;

                if (_dataService.RSettings.ProgramSettings.Debug)
                    await _log.LogMessage("STARTING UPLOAD FOR " + demo.JsonLocation, false, color: LOG_COLOR);

                //Get the hub with the desired date and season tags.
                FaceItHubSeason targetSeason = null;
                var hubTypeTags = hubSeasons.Where(x => x.Type.Equals(hub.HubType, StringComparison.OrdinalIgnoreCase));
                targetSeason =
                    hubTypeTags.FirstOrDefault(x => x.StartDate < demo.DemoDate && x.EndDate > demo.DemoDate);

                var tag = targetSeason?.TagName;

                if (targetSeason == null)
                {
                    tag = "UNKNOWN";
                    _ = _log.LogMessage(
                        $"Hub seasons have no definitions in the database for date `{demo.DemoDate}`!\n`{demo.Filename}`",
                        false,color: LOG_COLOR);
                }

                var dir = Path.GetDirectoryName(demo.JsonLocation);
                FileInfo targetFile;
                try
                {
                    targetFile = new FileInfo(Directory.GetFiles(dir).FirstOrDefault(x => x.Contains(demo.Filename)) ?? throw new InvalidOperationException());
                }
                catch (Exception e)
                {
                    if (_dataService.RSettings.ProgramSettings.Debug)
                        await _log.LogMessage($"Issue getting file {demo.Filename}\n{e}", color: LOG_COLOR);
                    continue;
                }

                string uploadTags = $"{tag}_{demo.MapName}";
                string radarDir = $"{_dataService.RSettings.ProgramSettings.FaceItDemoPath}\\Radars\\{tag}";

                //Get the WS ID from the demos
                Directory.CreateDirectory(radarDir);
                var radarPng = Directory.GetFiles(radarDir, $"*{demo.MapName}*.png", SearchOption.AllDirectories);
                var radarTxt = Directory.GetFiles(radarDir, $"*{demo.MapName}*.txt", SearchOption.AllDirectories);

                //No radar or text file found. We need to get them and include in the upload.
                if (radarTxt.Length == 0 || radarPng.Length == 0)
                {
                    if (_dataService.RSettings.ProgramSettings.Debug)
                        await _log.LogMessage($"Getting radar files for {targetFile}", false, color: LOG_COLOR);

                    var wsId = DemoParser.GetWorkshopIdFromJasonFile(targetFile);

                    var sapi = new SteamAPI(_dataService, _log);
                    var radarFiles = await sapi.GetWorkshopMapRadarFiles(_tempPath, wsId);

                    if(radarFiles != null)
                        foreach (var radarFile in radarFiles)
                        {
                            if(File.Exists($"{radarDir}\\{radarFile.Name}"))
                                File.Delete($"{radarDir}\\{radarFile.Name}");

                            File.Move(radarFile.FullName, $"{radarDir}\\{radarFile.Name}");
                            uploadDictionary.Add(new FileInfo($"{radarDir}\\{radarFile.Name}"),uploadTags);
                        }
                }
                else
                {
                    if (_dataService.RSettings.ProgramSettings.Debug)
                        await _log.LogMessage($"Skipping radar files for {targetFile}", false, color: LOG_COLOR);
                }

                //Add the tag to be called later
                if (!_siteUpdateCalls.Contains(uploadTags))
                    _siteUpdateCalls.Add(uploadTags);

                uploadDictionary.Add(targetFile, uploadTags);
            }

            var uploadResult = await DemoParser.UploadFaceitDemosAndRadars(uploadDictionary);

            if (uploadResult)
                _demosUploaded += uploadDictionary.Count;
        }

        private void DeleteDemoFiles(DemoResult[] demoResult)
        {
            foreach (var demo in demoResult)
                if (File.Exists(demo.FileLocationDemo))
                    File.Delete(demo.FileLocationDemo);
        }

        private async Task<FaceItHubEndpointsResponsesInfo> CallHubApiEndPoint(FaceItHub faceItHub, DateTime startDate,
            DateTime endDate)
        {
            //These become faceItHubEndpointsResponsesInfo
            var fileNames = new List<string>();
            IDictionary<string, string> demoMapnames = new Dictionary<string, string>();
            IDictionary<string, string> demoUrls = new Dictionary<string, string>();
            IDictionary<string, DateTime> demoDates = new Dictionary<string, DateTime>();
            var failedApiCalls = new List<string>();

            var dateToDownloadFrom = (int) endDate.ToUniversalTime().Subtract(new DateTime(1970, 1, 1)).TotalSeconds;
            var dateToDownloadUntil = (int) startDate.ToUniversalTime().Subtract(new DateTime(1970, 1, 1)).TotalSeconds;

            var errorCallingApiCount = 0;
            var apiOffset = 0;

            while (KEEP_CALLING_API)
            {
                var apiEndpoint = string.Concat(HUB_BASE_URL, faceItHub.HubGuid, "/matches?type=past&offset=",
                    apiOffset,
                    "&limit=", API_LIMIT);

                StartOfCallingApi: ;

                if (errorCallingApiCount > ERROR_CALLING_API_COUNT_MAX)
                {
                    failedApiCalls.Add(apiEndpoint);
                    await _log.LogMessage($"Skipped calling FaceIt Endpoint {apiEndpoint}", alert: true);
                    goto FinishedCallingApis;
                }

                //HTTP Request
                var request = (HttpWebRequest) WebRequest.Create(apiEndpoint);
                request.Method = "GET";
                request.ContentType = "application/json";
                request.Headers["Authorization"] = "Bearer " + _dataService.RSettings.ProgramSettings.FaceitAPIKey;

                await _log.LogMessage($"Calling Faceit API endpoint for {faceItHub.HubName}\n{apiEndpoint}", false,
                    color: LOG_COLOR);

                try
                {
                    using (var response = (HttpWebResponse) request.GetResponse())
                    {
                        string apiCallcontent;
                        using (var stream = response.GetResponseStream())
                        {
                            using (var sr = new StreamReader(stream))
                            {
                                apiCallcontent = sr.ReadToEnd();
                            }
                        }

                        var json = JObject.Parse(apiCallcontent);
                        var jsonItems = json != null ? json["items"] : null;

                        if (jsonItems == null) goto FinishedCallingApis;

                        var maxtoCheck = jsonItems.Count() < API_LIMIT ? jsonItems.Count() : API_LIMIT;

                        if (maxtoCheck == 0) goto FinishedCallingApis;

                        for (var matchNumber = 0; matchNumber < maxtoCheck; matchNumber++)
                        {
                            var jsonItemsCurrentGame = jsonItems.ElementAt(matchNumber);

                            var serialisedDemoMapname =
                                jsonItemsCurrentGame["voting"] != null &&
                                jsonItemsCurrentGame["voting"]["map"] != null &&
                                jsonItemsCurrentGame["voting"]["map"]["pick"] !=
                                null // No idea what map it is if there is no voting stage
                                    ? jsonItemsCurrentGame["voting"]["map"]["pick"]
                                    : "Unknown";
                            var demoMapname = serialisedDemoMapname != null &&
                                              serialisedDemoMapname.FirstOrDefault() != null
                                ? serialisedDemoMapname.FirstOrDefault().ToString()
                                : serialisedDemoMapname.ToString() == "Unknown"
                                    ? serialisedDemoMapname.ToString()
                                    : null;

                            var serialisedDemoMatchStatus = jsonItemsCurrentGame["status"];
                            var matchStatus = serialisedDemoMatchStatus != null
                                ? serialisedDemoMatchStatus.ToString()
                                : null;

                            var serialisedMatchFinishedAt = jsonItemsCurrentGame["finished_at"];
                            var matchFinishedAtString = serialisedMatchFinishedAt != null
                                ? serialisedMatchFinishedAt.ToString()
                                : null;
                            int.TryParse(matchFinishedAtString, out var matchFinishedAt);

                            // if finished_at is past end date specified, stop grabbing new demos
                            if (matchFinishedAt > dateToDownloadFrom)
                            {
                                /* skip this since it happened after the from date */
                            }
                            else if (matchFinishedAt != 0 && matchFinishedAt < dateToDownloadUntil)
                            {
                                goto FinishedCallingApis;
                            }
                            // if the match finished, grab the demoUrl
                            else if (matchStatus != null && matchStatus.ToUpper() == "FINISHED")
                            {
                                var serialisedDemoUrl = jsonItemsCurrentGame["demo_url"];
                                var demoUrl = serialisedDemoUrl != null && serialisedDemoUrl.FirstOrDefault() != null
                                    ? serialisedDemoUrl.FirstOrDefault().ToString()
                                    : null;

                                var serialisedMatchId = jsonItemsCurrentGame["match_id"];
                                var fileName = serialisedMatchId != null ? serialisedMatchId.ToString() : null;

                                //Lets get that date.
                                var demoDate =
                                    DateTime.UnixEpoch.AddSeconds(serialisedMatchFinishedAt.ToObject<int>());

                                // if a game has ended since the api was last called, the last demo from the previous call will have been returned again, so skip it
                                if (!string.IsNullOrWhiteSpace(fileName) && !fileNames.Contains(fileName))
                                {
                                    fileNames.Add(fileName);
                                    demoUrls.Add(new KeyValuePair<string, string>(fileName, demoUrl));
                                    demoMapnames.Add(new KeyValuePair<string, string>(fileName, demoMapname));
                                    demoDates.Add(new KeyValuePair<string, DateTime>(fileName, demoDate.Date));
                                }
                            }
                        }
                    }
                }
                catch (WebException e)
                {
                    await _log.LogMessage(
                        $"Error calling Faceit API for `{faceItHub.HubName}` but will retry.\nEndpoint: `{apiEndpoint}`. Reason was:\n`{e}`", alert: false,
                        color: LOG_COLOR);

                    //Give the API a delay
                    await Task.Delay(3000);

                    errorCallingApiCount++;
                    goto StartOfCallingApi;
                }

                apiOffset += API_LIMIT;
            }

            FinishedCallingApis: ;

            await _log.LogMessage($"API Call Success for {faceItHub.HubName}", false, color: LOG_COLOR);

            return new FaceItHubEndpointsResponsesInfo
            {
                FileNames = fileNames,
                DemoUrls = demoUrls,
                DemoMapnames = demoMapnames,
                FailedApiCalls = failedApiCalls,
                DemoDate = demoDates
            };
        }

        /// <summary>
        ///     Handles downloading the demo file, and stores the GZ file.
        /// </summary>
        /// <param name="remotePath">Remote file to download</param>
        /// <param name="localPath">Local file to store to</param>
        /// <returns>True if successful, false otherwise</returns>
        private async Task<bool> DownloadHubDemo(string remotePath, string localPath)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(localPath));

            if (string.IsNullOrEmpty(remotePath) || string.IsNullOrEmpty(localPath))
                return false;

            await _log.LogMessage($"Downloading: {remotePath}", false, color: LOG_COLOR);

            using (var client = new WebClient())
            {
                // download zip file
                client.Headers.Add("User-Agent: Other");
                try
                {
                    await client.DownloadFileTaskAsync(remotePath, localPath);
                    _downloadedData += new FileInfo(localPath).Length;
                }
                catch (WebException e)
                {
                    if (_dataService.RSettings.ProgramSettings.Debug)
                        await _log.LogMessage("Error downloading demo, retrying. " + remotePath, false,
                            color: LOG_COLOR);

                    if (File.Exists(localPath)) File.Delete(localPath);
                    client.Dispose();
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        ///     Unzips a GZ file that contains a demo file. Produces a .dem file.
        ///     If failure occurs, the demo file is deleted.
        /// </summary>
        /// <param name="sourceFile">File to extract</param>
        /// <param name="destinationFile">Destination file</param>
        /// <returns>True if successful, false otherwise.</returns>
        private async Task<bool> UnzipDemo(string sourceFile, string destinationFile)
        {
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
                                await _log.LogMessage("Failed to unzip - Going to re-download file..." + sourceFile,
                                    false, color: LOG_COLOR);

                            File.Delete(sourceFile);
                            return false;
                        }
                    }
                }

                await _log.LogMessage($"Unzipped {destinationFile}", false, color: LOG_COLOR);
            }

            return true;
        }

        private async Task<DemoResult> AcquireDemo(DemoResult demoResult)
        {
            var dir = Path.GetDirectoryName(demoResult.JsonLocation);
            if (Directory.Exists(dir))
            {
                var localFiles = Directory.GetFiles(dir);
                if (localFiles.Any(x => x.Contains(demoResult.Filename)))
                {
                    if (_dataService.RSettings.ProgramSettings.Debug)
                        await _log.LogMessage($"Already have {demoResult.JsonLocation}! Skipping", false,
                            color: LOG_COLOR);
                    demoResult.Skip = true;
                    return demoResult;
                }
            }

            //Skip unknown maps
            if (demoResult.MapName.Equals("unknown", StringComparison.OrdinalIgnoreCase))
            {
                demoResult.Skip = true;
                return demoResult;
            }

            for (var i = 0; i < DOWNLOAD_AND_ZIP_RETRY_LIMIT; i++)
            {
                //Attempt to download demo
                if (await DownloadHubDemo(demoResult.DemoUrl, demoResult.FileLocationGz))
                {
                    //Download worked
                    _demosDownloaded++;
                    demoResult.DownloadFailed = false;
                    //Try and unzip
                    if (await UnzipDemo(demoResult.FileLocationGz, demoResult.FileLocationDemo))
                    {
                        //Unzip worked, break
                        _demosUnZipped++;
                        demoResult.UnzipFailed = false;
                        break;
                    }

                    demoResult.UnzipFailed = true;
                }
                else
                {
                    demoResult.DownloadFailed = true;
                }

                await Task.Delay(3000);
            }

            //Delete gz file
            if (File.Exists(demoResult.FileLocationGz))
                File.Delete(demoResult.FileLocationGz);

            return demoResult;
        }

        private async Task ParseDemos(DemoResult[] demos)
        {
            var parsedFolders = new List<string>();
            var parsingWork = new List<Task>();
            foreach (var demoResult in demos)
            {
                if (demoResult.Skip)
                    continue;

                if (!parsedFolders.Contains(demoResult.FileLocation))
                {
                    if (_dataService.RSettings.ProgramSettings.Debug)
                        await _log.LogMessage($"Starting Parser Instance for {demoResult.FileLocation}", false,
                            color: LOG_COLOR);
                    parsedFolders.Add(demoResult.FileLocation);
                    var destination = Path.GetDirectoryName(demoResult.JsonLocation);
                    Directory.CreateDirectory(destination);
                    parsingWork.Add(DemoParser.ParseFaceitDemos(demoResult.FileLocation, destination));
                }

                if (parsingWork.Count >= 8)
                {
                    await Task.WhenAny(parsingWork);

                    //Remove all finished tasks so we can spawn more
                    parsingWork.RemoveAll(x => x.IsCompleted);
                }
            }

            await Task.WhenAll(parsingWork);
        }

        private async Task<DemoResult[]> DownloadHubDemos(string hubName,
            FaceItHubEndpointsResponsesInfo faceItHubEndpointsResponsesInfo)
        {
            var demoTasks = new List<Task<DemoResult>>();
            var processedDemos = new List<DemoResult>();
            foreach (var fileName in faceItHubEndpointsResponsesInfo.FileNames)
                if (faceItHubEndpointsResponsesInfo.DemoUrls.Keys.Any(k => k == fileName))
                {
                    var mapName = faceItHubEndpointsResponsesInfo.DemoMapnames[fileName];
                    var demoUrl = faceItHubEndpointsResponsesInfo.DemoUrls[fileName];
                    var demoDate = faceItHubEndpointsResponsesInfo.DemoDate[fileName];

                    var jsonLocation =
                        $"{_dataService.RSettings.ProgramSettings.FaceItDemoPath}\\{demoDate:MM_dd_yyyy}\\" +
                        $"{hubName}\\{faceItHubEndpointsResponsesInfo.DemoMapnames[fileName]}_{fileName}.json";

                    var fileLocationGz = $"{_tempPath}\\{demoDate:MM_dd_yyyy}\\{hubName}\\{mapName}\\{fileName}.gz";
                    var fileLocationDem = $"{_tempPath}\\{demoDate:MM_dd_yyyy}\\{hubName}\\{mapName}\\{fileName}.dem";
                    var fileLocation = $"{_tempPath}\\{demoDate:MM_dd_yyyy}\\{hubName}\\{mapName}\\";

                    demoTasks.Add(AcquireDemo(new DemoResult(fileName,
                        fileLocation,
                        fileLocationGz,
                        fileLocationDem,
                        jsonLocation,
                        demoUrl,
                        demoDate,
                        mapName)));

                    if (demoTasks.Count >= 8)
                    {
                        //Get a finished task
                        var finishedTask = await Task.WhenAny(demoTasks);

                        //Slip it into our processed list
                        processedDemos.Add(await finishedTask);

                        //Remove the task so we can keep processing
                        demoTasks.Remove(finishedTask);
                    }
                }

            //Await all results to finish - then we can handle them.
            var remainingTasks = await Task.WhenAll(demoTasks);

            //Put all tasks left in the list into the processed list. 
            processedDemos.AddRange(remainingTasks);

            //Remove all demos that we should be skipping
            processedDemos.RemoveAll(x => x.Skip);
            await _log.LogMessage($"Done downloading demos for {hubName}. Total of {processedDemos.Count}.", false, color: LOG_COLOR);

            return processedDemos.ToArray();
        }
    }
}