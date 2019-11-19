using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using BotHATTwaffle2.Handlers;
using BotHATTwaffle2.Models.FaceIt;
using BotHATTwaffle2.Models.JSON;
using BotHATTwaffle2.src.Models.FaceIt;
using Newtonsoft.Json.Linq;

namespace BotHATTwaffle2.Services.FaceIt
{
    public class FaceItAPI
    {
        private readonly DataService _dataService;
        private readonly LogHandler _log;
        private readonly string _tempPath;

        // Behaviour handing when hammering API endpoints
        private const bool keepCallingApi = true;
        private const int apiLimit = 2;
        private const int errorCallingApiCountMax = 20;
        private const string hubBaseUrl = @"https://open.faceit.com/data/v4/hubs/";
        private const int downloadAndZipRetryLimit = 20;

        public FaceItAPI(DataService dataService, LogHandler log)
        {
            _dataService = dataService;
            _log = log;
            _tempPath = string.Concat(Path.GetTempPath(), @"DemoGrabber\");
        }

        public async Task GetOneDay()
        {
            Console.WriteLine("API END POINT");
            var reply =  await CallHubApiEndPoint(_dataService.RSettings.FaceItHubs[0], DateTime.Now.AddDays(-3), DateTime.Now);

            Console.WriteLine("DOWNLOAD HUB DEMOS");
            var dlResult = await DownloadHubDemos(_dataService.RSettings.FaceItHubs[0].HubName, reply);
        }

        private async Task<FaceItHubEndpointsResponsesInfo> CallHubApiEndPoint(FaceItHub faceItHub, DateTime dateFrom, DateTime dateTo)
        {
            //These become faceItHubEndpointsResponsesInfo
            List<string> fileNames = new List<string>();
            IDictionary<string, string> demoMapnames = new Dictionary<string, string>();
            IDictionary<string, string> demoUrls = new Dictionary<string, string>();
            List<string> failedApiCalls = new List<string>();

            //TODO: Should these be UTC??? JIMMMMMMMMMMM
            int dateToDownloadFrom = (int)(dateFrom.ToUniversalTime().Subtract(new DateTime(1970, 1, 1))).TotalSeconds;
            int dateToDownloadUntil = (int)(dateTo.ToUniversalTime().Subtract(new DateTime(1970, 1, 1))).TotalSeconds;

            while (keepCallingApi)
            {
                int errorCallingApiCount = 0;

                int apiOffset = 0;

                string apiEndpoint = string.Concat(hubBaseUrl, faceItHub.HubGuid, "/matches?type=past&offset=", apiOffset, "&limit=", apiLimit);

            StartOfCallingApi:;

                if (errorCallingApiCount > errorCallingApiCountMax)
                {
                    failedApiCalls.Add(apiEndpoint);
                    _ = _log.LogMessage($"Skipped calling Faceit Endpoint {apiEndpoint}");
                    goto FinishedCallingApis;
                }

                //HTTP Request
                var request = (HttpWebRequest)WebRequest.Create(apiEndpoint);
                request.Method = "GET";
                request.ContentType = "application/json";
                request.Headers["Authorization"] = "Bearer " + _dataService.RSettings.ProgramSettings.FaceitAPIKey;

                await _log.LogMessage($"Calling Faceit API endpoint for {faceItHub.HubName}: {apiEndpoint}", channel:false);

                var apiCallcontent = string.Empty;
                try
                {
                    using (var response = (HttpWebResponse)request.GetResponse())
                    {
                        using (var stream = response.GetResponseStream())
                        {
                            using (var sr = new StreamReader(stream))
                            {
                                apiCallcontent = sr.ReadToEnd();
                            }
                        }

                        JObject json = JObject.Parse(apiCallcontent);
                        var jsonItems = json != null ? json["items"] : null;

                        if (jsonItems == null)
                        {
                            goto FinishedCallingApis;
                        }

                        var maxtoCheck = (jsonItems.Count() < apiLimit) ? jsonItems.Count() : apiLimit;

                        if (maxtoCheck == 0)
                        {
                            goto FinishedCallingApis;
                        }

                        for (int matchNumber = 0; matchNumber < maxtoCheck; matchNumber++)
                        {
                            var jsonItemsCurrentGame = jsonItems.ElementAt(matchNumber);

                            var serialisedDemoMapname = (jsonItemsCurrentGame["voting"] != null && jsonItemsCurrentGame["voting"]["map"] != null && jsonItemsCurrentGame["voting"]["map"]["pick"] != null) // No idea what map it is if there is no voting stage
                                                        ? jsonItemsCurrentGame["voting"]["map"]["pick"]
                                                        : "Unknown";
                            var demoMapname = (serialisedDemoMapname != null && serialisedDemoMapname.FirstOrDefault() != null)
                                                ? serialisedDemoMapname.FirstOrDefault().ToString()
                                                : (serialisedDemoMapname.ToString() == "Unknown"
                                                    ? serialisedDemoMapname.ToString()
                                                    : null
                                                );

                            var serialisedDemoMatchStatus = jsonItemsCurrentGame["status"];
                            var matchStatus = (serialisedDemoMatchStatus != null) ? serialisedDemoMatchStatus.ToString() : null;

                            var serialisedMatchFinishedAt = jsonItemsCurrentGame["finished_at"];
                            var matchFinishedAtString = (serialisedMatchFinishedAt != null) ? serialisedMatchFinishedAt.ToString() : null;
                            int.TryParse(matchFinishedAtString, out int matchFinishedAt);

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
                            else if (matchStatus != null && matchStatus.Equals("finished", StringComparison.OrdinalIgnoreCase))
                            {
                                var serialisedDemoUrl = jsonItemsCurrentGame["demo_url"];
                                var demoUrl = (serialisedDemoUrl != null && serialisedDemoUrl.FirstOrDefault() != null) ? serialisedDemoUrl.FirstOrDefault().ToString() : null;

                                var serialisedMatchId = jsonItemsCurrentGame["match_id"];
                                var fileName = (serialisedMatchId != null) ? serialisedMatchId.ToString() : null;

                                // if a game has ended since the api was last called, the last demo from the previous call will have been returned again, so skip it
                                if (!string.IsNullOrWhiteSpace(fileName) && !fileNames.Contains(fileName))
                                {
                                    fileNames.Add(fileName);
                                    demoUrls.Add(new KeyValuePair<string, string>(fileName, demoUrl));
                                    demoMapnames.Add(new KeyValuePair<string, string>(fileName, demoMapname));
                                }
                            }
                        }
                    }
                }
                catch (WebException e)
                {
                    _ = _log.LogMessage($"Error calling Faceit API for {faceItHub.HubGuid}, will retry. Reason was: \n{e}", channel: false);

                    //Give the API a delay
                    await Task.Delay(3000);

                    errorCallingApiCount++;
                    goto StartOfCallingApi;
                }
                apiOffset += apiLimit;
            }

            FinishedCallingApis:;

            _ = _log.LogMessage($"API Call Success!", channel: false);

            Console.WriteLine($"Names {fileNames.Count} \n DemoURL {demoUrls.Count}");

            return new FaceItHubEndpointsResponsesInfo()
            {
                FileNames = fileNames,
                DemoUrls = demoUrls,
                DemoMapnames = demoMapnames,
                FailedApiCalls = failedApiCalls
            };
        }

        /// <summary>
        /// Handles downloading the demo file, and stores the GZ file.
        /// </summary>
        /// <param name="remotePath">Remote file to download</param>
        /// <param name="localPath">Local file to store to</param>
        /// <returns>True if successful, false otherwise</returns>
        private async Task<bool> DownloadHubDemo(string remotePath, string localPath)
        {
            Directory.CreateDirectory(localPath);

            using (var client = new WebClient())
            {
                // download zip file
                client.Headers.Add("User-Agent: Other");
                try
                {
                    await client.DownloadFileTaskAsync(remotePath, localPath);
                }
                catch (WebException e)
                {
                    Console.WriteLine("Error downloading demo, retrying. " + remotePath);

                    if (File.Exists(localPath))
                    {
                        File.Delete(localPath);
                    }
                    client.Dispose();
                    return false;
                }
            }
            return true;
        }

        /// <summary>
        /// Unzips a GZ file that contains a demo file. Produces a .dem file.
        /// If failure occurs, the demo file is deleted.
        /// </summary>
        /// <param name="sourceFile">File to extract</param>
        /// <param name="destinationFile">Destination file</param>
        /// <returns>True if successful, false otherwise.</returns>
        private async Task<bool> UnzipDemo(string sourceFile, string destinationFile)
        {
            FileInfo gzipFileName = new FileInfo(sourceFile);
            using (FileStream fileToDecompressAsStream = gzipFileName.OpenRead())
            {
                using (FileStream decompressedStream = File.Create(destinationFile))
                {
                    using (GZipStream decompressionStream = new GZipStream(fileToDecompressAsStream, CompressionMode.Decompress))
                    {
                        try
                        {
                            await decompressionStream.CopyToAsync(decompressedStream);
                        }
                        catch (Exception e)
                        {
                            Console.WriteLine("Failed to unzip - Going to re-download file..." + sourceFile);
                            File.Delete(sourceFile);
                            return false;
                        }
                    }
                }
                Console.WriteLine("Unzipped demo: " + destinationFile);
            }
            return true;
        }

        private async Task<DemoResult> AcquireDemo(DemoResult demoResult)
        {
            //TODO: What is this checking? Do we need this var at all? There is probably a better way to check for existing files.
            if (File.Exists(demoResult.FileLocationOldJson))
            {
                Console.WriteLine("Demo already existed, skipping... " + demoResult.Filename);
                return demoResult;
            }
            
            for(int i = 0; i < downloadAndZipRetryLimit; i++)
            {
                //Attempt to download demo
                if (await DownloadHubDemo(demoResult.DemoUrl, demoResult.FileLocationGz))
                {
                    //Download worked
                    demoResult.DownloadFailed = false;

                    //Try and unzip
                    if (await UnzipDemo(demoResult.FileLocationGz, demoResult.FileLocationDemo))
                    {
                        //Unzip worked, abort
                        demoResult.UnzipFailed = false;
                        break;
                    }

                    //Unzip failed
                    demoResult.UnzipFailed = true;
                    Console.WriteLine("Skipped downloading demo: " + demoResult.Filename + "\n");
                }
                else
                {
                    demoResult.DownloadFailed = true;
                    Console.WriteLine("Skipped unzipping demo: " + demoResult.Filename + "\n");
                }

                await Task.Delay(3000);
            }

            //Delete gz file
            File.Delete(demoResult.FileLocationGz);

            return demoResult;
        }

        private async Task<FaceItHubDownloadedDemosInfo> DownloadHubDemos(string hubName, FaceItHubEndpointsResponsesInfo faceItHubEndpointsResponsesInfo)
            {
                List<string> downloadedDemos = new List<string>();
                List<string> unzippedDemos = new List<string>();
                List<string> failedDownloads = new List<string>();
                List<string> failedUnzips = new List<string>();

                List<Task<DemoResult>> demoTasks = new List<Task<DemoResult>>();

                foreach (var fileName in faceItHubEndpointsResponsesInfo.FileNames)
                {
                    if (faceItHubEndpointsResponsesInfo.DemoUrls.Keys.Any(k => k == fileName))
                    {
                        string fileLocation = string.Concat(_tempPath, hubName, @"\",
                            faceItHubEndpointsResponsesInfo.DemoMapnames[fileName], @"\");
                        string fileLocationGz = string.Concat(fileLocation, fileName, ".gz");
                        string fileLocationDem = string.Concat(fileLocation, fileName, ".dem");
                        string fileLocationOldJson = string.Concat(fileLocation, "parsed\\",
                            faceItHubEndpointsResponsesInfo.DemoMapnames[fileName], "_", fileName, ".json");
                        string demoUrl = faceItHubEndpointsResponsesInfo.DemoUrls[fileName];

                        demoTasks.Add(AcquireDemo(new DemoResult(fileName,
                            fileLocation,
                            fileLocationGz,
                            fileLocationDem,
                            fileLocationOldJson,
                            demoUrl)));

                        Console.WriteLine($"ADDING DOWNLOAD JOB FOR {fileName}");
                    }
                }

                //Await all results to finish - then we can handle them.
                var fullDemoResults = await Task.WhenAll<DemoResult>(demoTasks);

                foreach (var demo in fullDemoResults)
                {
                    if(demo.Skip)
                        continue;

                    if(demo.DownloadFailed)
                        failedDownloads.Add(demo.Filename);
                    else
                        downloadedDemos.Add(demo.Filename);

                    if (demo.UnzipFailed)
                        failedUnzips.Add(demo.Filename);
                    else
                        unzippedDemos.Add(demo.Filename);
                }

                Console.WriteLine("Done downloading demos.\n");

                return new FaceItHubDownloadedDemosInfo()
                {
                    DownloadedDemos = downloadedDemos,
                    UnzippedDemos = unzippedDemos,
                    FailedDownloads = failedDownloads,
                    FailedUnzips = failedUnzips
                };
            }
        }
    }
