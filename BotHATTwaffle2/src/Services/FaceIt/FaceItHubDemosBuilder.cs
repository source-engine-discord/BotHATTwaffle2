using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using BotHATTwaffle2.Handlers;
using BotHATTwaffle2.Services;
using BotHATTwaffle2.src.Models.FaceIt;
using BotHATTwaffle2.src.Util;
using Discord;
using Discord.Addons.Interactive;
using Discord.Commands;
using Discord.WebSocket;
using Newtonsoft.Json.Linq;

namespace BotHATTwaffle2.src.Services.FaceIt
{
    public class FaceItHubDemosBuilder
    {
        private const ConsoleColor LOG_COLOR = ConsoleColor.Red;

        //The values that are used by the wizard to build the faceit hub demos request. Also used for editing the event.
        private readonly string[] _arrayValues =
        {
            "Date From", "Date To", "Gamemode", "Hub Regions"
        };

        private readonly SocketCommandContext _context;
        private readonly DataService _dataService;
        private readonly InteractiveService _interactive;
        private readonly LogHandler _log;

        //Help text used for the wizard / Updating information
        private readonly string[] _wizardText =
        {
            "Enter the start date (closest to present day) for the matches. The time will always be between `00:00 CT on that day (meaning that matches played on the date entered WON'T be counted)`. Required format: `MM/DD/YYYY`\n" +
            "Example: `6/31/2019`",
            "Enter the end date (furthest in the past) for the matches. The time will always be between `00:00 CT on that day (meaning that matches played on the date entered WILL be counted)`. Required format: `MM/DD/YYYY`\n" +
            "Example: `7/31/2019`",
            "Enter the gamemode of the hubs\n" +
            "Example: `Defuse` or `Wingman`",
            "Enter the regions of the hubs in a comma separated format (\"All\" for each available hub in the gamemode chosen).\n" +
            "Example: `EU, NA, SA, SEA, OCE, All`",
        };

        private IUserMessage _embedMessage;

        private IUserMessage _instructionsMessage;

        private bool _requireAbort;
        private FaceItHubDemosRequest _faceItHubDemosRequest;
        private SocketMessage _userMessage;

        public FaceItHubDemosBuilder(SocketCommandContext context, InteractiveService interactive, DataService data, LogHandler log)
        {
            _context = context;
            _interactive = interactive;
            _dataService = data;
            _log = log;

            //Make the faceit hub demos request object
            _faceItHubDemosRequest = new FaceItHubDemosRequest();
        }

        /// <summary>
        ///     Decides which FaceIt API endpoints need to be called, before calling CallHubApiEndpoint() and DownloadHubDemos().
        /// </summary>
        private string SortFaceItApiCalls()
        {
            const string apiHubEU = "8f865128-f6a3-4299-ace7-e074d6002c34", apiHubNA = "dd5c0ffd-3472-4383-963f-e357a124832c",
                         apiHubSA = "e674e8c1-20d6-473b-8e01-489aa31b2d8b", apiHubSEA = "71473488-0974-45d2-a21c-6c8e891a30ee", apiHubOCE = "e61dd961-92fd-4865-a1cb-01702240fd0c";
            const string apiHubWingmanEU = "9bff9b5b-cfe6-43f2-b3f1-c80e9b1674a7", apiHubWingmanNA = "8f2c8a7c-8702-4955-bd06-6caf9b670276";

            string localPath = string.Concat(Path.GetTempPath(), @"DemoGrabber\");

            Dictionary<string, FaceItHubEndpointsResponsesInfo> faceItHubEndpointsResponsesInfos = new Dictionary<string, FaceItHubEndpointsResponsesInfo>();
            Dictionary<string, FaceItHubDownloadedDemosInfo> faceItHubDownloadedDemosInfos = new Dictionary<string, FaceItHubDownloadedDemosInfo>();

            Dictionary<string, List<string>> filesToDownload = new Dictionary<string, List<string>>();
            Dictionary<string, List<string>> demosDownloaded = new Dictionary<string, List<string>>();
            Dictionary<string, List<string>> demosUnzipped = new Dictionary<string, List<string>>();

            Dictionary<string, List<string>> failedApiCalls = new Dictionary<string, List<string>>();
            Dictionary<string, List<string>> failedDownloads = new Dictionary<string, List<string>>();
            Dictionary<string, List<string>> failedUnzips = new Dictionary<string, List<string>>();

            Dictionary<string, string> apiHubs = new Dictionary<string, string>();
            if (_faceItHubDemosRequest.Gamemode.ToLower() == "defuse")
            {
                if (_faceItHubDemosRequest.HubRegions.Contains("EU"))
                    apiHubs.Add("EU", apiHubEU);
                if (_faceItHubDemosRequest.HubRegions.Contains("NA"))
                    apiHubs.Add("NA", apiHubNA);
                if (_faceItHubDemosRequest.HubRegions.Contains("SA"))
                    apiHubs.Add("SA", apiHubSA);
                if (_faceItHubDemosRequest.HubRegions.Contains("SEA"))
                    apiHubs.Add("SEA", apiHubSEA);
                if (_faceItHubDemosRequest.HubRegions.Contains("OCE"))
                    apiHubs.Add("OCE", apiHubOCE);
            }
            else if (_faceItHubDemosRequest.Gamemode.ToLower() == "wingman")
            {
                if (_faceItHubDemosRequest.HubRegions.Contains("EU"))
                    apiHubs.Add("WingmanEU", apiHubWingmanEU);
                if (_faceItHubDemosRequest.HubRegions.Contains("NA"))
                    apiHubs.Add("WingmanNA", apiHubWingmanNA);
            }
            else // gamemode not valid
            {
                return null;
            }

            foreach (var hub in apiHubs)
            {
                faceItHubEndpointsResponsesInfos[hub.Key] = CallHubApiEndpoint(hub.Value);

                filesToDownload[hub.Key] = faceItHubEndpointsResponsesInfos[hub.Key].FileNames;
                failedApiCalls[hub.Key] = faceItHubEndpointsResponsesInfos[hub.Key].FailedApiCalls;
            }

            foreach (var hub in faceItHubEndpointsResponsesInfos)
            {
                faceItHubDownloadedDemosInfos[hub.Key] = DownloadHubDemos(localPath, filesToDownload[hub.Key], hub.Key, hub.Value);

                demosDownloaded[hub.Key] = faceItHubDownloadedDemosInfos[hub.Key].DownloadedDemos;
                demosUnzipped[hub.Key] = faceItHubDownloadedDemosInfos[hub.Key].UnzippedDemos;
                failedDownloads[hub.Key] = faceItHubDownloadedDemosInfos[hub.Key].FailedDownloads;
                failedUnzips[hub.Key] = faceItHubDownloadedDemosInfos[hub.Key].FailedUnzips;
            }

            // print failures
            foreach (var hub in failedApiCalls)
            {
                foreach (var failure in hub.Value)
                {
                    Console.WriteLine(string.Concat("Failed to call api endpoints: ", failure, "\n"));
                }
            }

            foreach (var hub in failedDownloads)
            {
                foreach (var failure in hub.Value)
                {
                    Console.WriteLine(string.Concat("Failed to download demos: ", failure, "\n"));
                }
            }

            foreach (var hub in failedUnzips)
            {
                foreach (var failure in hub.Value)
                {
                    Console.WriteLine(string.Concat("Failed to unzip demos: ", failure, "\n"));
                }
            }

            return localPath;
        }

        /// <summary>
        ///     Calls the FaceIt API endpoint for the hub provided, upping the offset until the dateUntil has passed.
        /// </summary>
        /// <param name="hubApiEndpoint"> API endpoint for a hub </param>
        /// <returns></returns>
        private FaceItHubEndpointsResponsesInfo CallHubApiEndpoint(string hubApiEndpoint)
        {
            FaceItHubEndpointsResponsesInfo faceItHubEndpointsResponsesInfo;

            List<string> fileNames = new List<string>();
            IDictionary<string, string> demoMapnames = new Dictionary<string, string>();
            IDictionary<string, string> demoUrls = new Dictionary<string, string>();
            List<string> failedApiCalls = new List<string>();

            var keepCallingApi = true;
            var apiOffset = 0;
            var apiLimit = 100;
            int errorCallingApiCountMax = 20;

            int dateToDownloadFrom = (int)(_faceItHubDemosRequest.DateFrom.Subtract(new DateTime(1970, 1, 1))).TotalSeconds;
            int dateToDownloadUntil = (int)(_faceItHubDemosRequest.DateTo.Subtract(new DateTime(1970, 1, 1))).TotalSeconds;

            while (keepCallingApi)
            {
                int errorCallingApiCount = 0;

                string apiEndpoint = string.Concat("https://open.faceit.com/data/v4/hubs/", hubApiEndpoint, "/matches?type=past&offset=", apiOffset, "&limit=", apiLimit);

                StartOfCallingApi:;

                if (errorCallingApiCount > errorCallingApiCountMax)
                {
                    failedApiCalls.Add(apiEndpoint);
                    Console.WriteLine("Skipped calling endpoint: " + apiEndpoint + "\n");
                    goto FinishedCallingApis;
                }

                var request = (HttpWebRequest)WebRequest.Create(apiEndpoint);
                request.Method = "GET";
                request.ContentType = "application/json";
                request.Headers["Authorization"] = "Bearer 06ee211b-9e87-4db6-81a9-521807220089";

                Console.WriteLine("Calling api: " + apiEndpoint);

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
                                keepCallingApi = false;
                                goto FinishedCallingApis;
                            }
                            // if the match finished, grab the demoUrl
                            else if (matchStatus != null && matchStatus.ToUpper() == "FINISHED")
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
                    Console.WriteLine("Error calling api, retrying.");
                    errorCallingApiCount++;
                    goto StartOfCallingApi;
                }
                apiOffset += apiLimit;
            }

            FinishedCallingApis:;

            faceItHubEndpointsResponsesInfo = new FaceItHubEndpointsResponsesInfo()
            {
                FileNames = fileNames,
                DemoUrls = demoUrls,
                DemoMapnames = demoMapnames,
                FailedApiCalls = failedApiCalls
            };

            return faceItHubEndpointsResponsesInfo;
        }

        /// <summary>
        ///     Downloads the zipped demos from faceit via urls and unzips them.
        /// </summary>
        /// <param name="localPath"> Base file location path </param>
        /// <param name="filesToDownload"> List of urls to download files from </param>
        /// <param name="hubName"> The region of a hub, used for sorting the demos downloaded into folders for different hubs </param>
        /// <param name="faceItHubEndpointsResponsesInfo"> Information retrieved from calling the API endpoints previously for a specific hub </param>
        /// <returns></returns>
        private FaceItHubDownloadedDemosInfo DownloadHubDemos(string localPath, List<string> filesToDownload, string hubName, FaceItHubEndpointsResponsesInfo faceItHubEndpointsResponsesInfo)
        {
            FaceItHubDownloadedDemosInfo faceItHubDownloadedDemosInfo = new FaceItHubDownloadedDemosInfo();

            List<string> downloadedDemos = new List<string>();
            List<string> unzippedDemos = new List<string>();
            List<string> failedDownloads = new List<string>();
            List<string> failedUnzips = new List<string>();

            int errorDownloadingDemoCountMax = 20, errorUnzippingDemoCountMax = 20;

            // get demos
            foreach (var fileName in filesToDownload)
            {
                var errorDownloadingDemoCount = 0;
                var errorUnzippingDemoCount = 0;

                StartOfDemoDownload:;

                if (errorDownloadingDemoCount > errorDownloadingDemoCountMax)
                {
                    failedDownloads.Add(fileName);
                    Console.WriteLine("Skipped downloading demo: " + fileName + "\n");
                    goto FinishedDownloadingDemo;
                }
                else if (errorUnzippingDemoCount > errorUnzippingDemoCountMax)
                {
                    failedUnzips.Add(fileName);
                    Console.WriteLine("Skipped unzipping demo: " + fileName + "\n");
                    goto FinishedDownloadingDemo;
                }

                if (faceItHubEndpointsResponsesInfo.DemoUrls.Keys.Any(k => k == fileName))
                {
                    string fileLocation = string.Concat(localPath, hubName, @"\", faceItHubEndpointsResponsesInfo.DemoMapnames[fileName], @"\");
                    string fileLocationGz = string.Concat(fileLocation, fileName, ".gz");
                    string fileLocationDem = string.Concat(fileLocation, fileName, ".dem");

                    // create folders if needed
                    if (!Directory.Exists(fileLocation))
                    {
                        Directory.CreateDirectory(fileLocation);
                    }

                    if (!File.Exists(fileLocationDem))
                    {
                        using (var client = new WebClient())
                        {
                            // download zip file
                            client.Headers.Add("User-Agent: Other");
                            try
                            {
                                client.DownloadFile(faceItHubEndpointsResponsesInfo.DemoUrls[fileName], fileLocationGz);
                            }
                            catch (WebException e)
                            {
                                Console.WriteLine("Error downloading demo, retrying.");

                                if (File.Exists(fileLocationGz))
                                {
                                    File.Delete(fileLocationGz);
                                }

                                client.Dispose();

                                Thread.Sleep(1000);
                                errorDownloadingDemoCount++;
                                goto StartOfDemoDownload;
                            }

                            downloadedDemos.Add(fileName);

                            Console.WriteLine("Downloaded zipped demo: " + fileName);

                            // unzip file
                            FileInfo gzipFileName = new FileInfo(fileLocationGz);
                            using (FileStream fileToDecompressAsStream = gzipFileName.OpenRead())
                            {
                                using (FileStream decompressedStream = File.Create(fileLocationDem))
                                {
                                    using (GZipStream decompressionStream = new GZipStream(fileToDecompressAsStream, CompressionMode.Decompress))
                                    {
                                        try
                                        {
                                            decompressionStream.CopyTo(decompressedStream);
                                        }
                                        catch (Exception e)
                                        {
                                            Console.WriteLine("Error unzipping demo, retrying.");
                                            errorUnzippingDemoCount++;
                                            goto StartOfDemoDownload;
                                        }
                                    }
                                }
                                Console.WriteLine("Unzipped demo: " + fileName);
                            }

                            unzippedDemos.Add(fileName);

                            // delete the zip file
                            File.Delete(fileLocationGz);

                            client.Dispose();
                        }
                        Console.WriteLine();
                    }
                    else
                    {
                        Console.WriteLine("Demo already existed, skipping... " + fileName);
                    }
                }

                FinishedDownloadingDemo:;
            }

            Console.WriteLine("Done downloading demos.\n");

            faceItHubDownloadedDemosInfo = new FaceItHubDownloadedDemosInfo()
            {
                DownloadedDemos = downloadedDemos,
                UnzippedDemos = unzippedDemos,
                FailedDownloads = failedDownloads,
                FailedUnzips = failedUnzips
            };

            return faceItHubDownloadedDemosInfo;
        }

        /// <summary>
        ///     Used to confirm if a user wants to submit their faceit hub demos request, or make further changes.
        /// </summary>
        /// <returns></returns>
        private async Task ConfirmRequest()
        {
            if (_faceItHubDemosRequest.DateFrom == new DateTime() ||
                _faceItHubDemosRequest.DateTo == new DateTime() ||
                string.IsNullOrWhiteSpace(_faceItHubDemosRequest.Gamemode) ||
                _faceItHubDemosRequest.HubRegions.Count() == 0
            ) {
                await CancelRequest();
                return;
            }

            var demosPath = SortFaceItApiCalls(); //Call APIs, download the demos and unzip

            List<FileInfo> jasonFiles = new List<FileInfo>();
            try
            {
                jasonFiles = DemoParser.ParseFaceItHubDemos(Path.GetDirectoryName(demosPath)).Result;
            }
            catch (Exception e)
            {
                Console.WriteLine("JIMBULKCODE\nJIMBULKCODE\nJIMBULKCODE\nJIMBULKCODE\nJIMBULKCODE\nJIMBULKCODE\nJIMBULKCODE\nJIMBULKCODE\n" +
                                  e.Message);
            }
        }

        /// <summary>
        ///     Validates a specific faceit hub demos element as valid.
        /// </summary>
        /// <param name="type">Type of data to validate</param>
        /// <param name="data">Data to validate</param>
        /// <returns></returns>
        private async Task ValidateInformationLoop(string type, string data)
        {
            //Try to parse the data, if failed collect new data.
            while (!await ParseInformation(type, data))
            {
                //If data is utterly and completely un-usable, back out completely.
                if (_requireAbort)
                {
                    await CancelRequest();
                    await _context.Channel.SendMessageAsync("Unable to parse data. Consult the help documents.");
                    return;
                }

                _userMessage = await _interactive.NextMessageAsync(_context);
                data = _userMessage.Content;

                if (_userMessage == null ||
                    _userMessage.Content.Equals("exit", StringComparison.OrdinalIgnoreCase))
                {
                    await CancelRequest();
                    return;
                }
            }
        }

        /// <summary>
        ///     Entry point for someone submitting a faceit hub demos request from the bulk request template
        /// </summary>
        /// <param name="input">Prebuilt template containing all faceit hub demos information</param>
        /// <returns></returns>
        public async Task BuildFaceItHubDemosBulk(string[] input)
        {
            foreach (var line in input)
            {
                var splitLine = line.Split(new[] { ':' }, 2).Select(x => x.Trim()).ToArray();

                await ValidateInformationLoop(splitLine[0], splitLine[1]);
            }

            //Move onto confirmation
            await ConfirmRequest();
        }

        /// <summary>
        ///     Updates the instructions and display embed to reflect the current state of the faceit hub demos request.
        /// </summary>
        /// <param name="instructions">Instructions to display to the user</param>
        /// <returns></returns>
        private async Task Display(string instructions)
        {
            if (_embedMessage == null)
                _embedMessage = await _context.Channel.SendMessageAsync("Type `exit` to abort at any time.",
                    embed: RebuildEmbed().Build());
            else
                await _embedMessage.ModifyAsync(x => x.Embed = RebuildEmbed().Build());

            if (_instructionsMessage == null)
                _instructionsMessage = await _context.Channel.SendMessageAsync(instructions);
            else
                await _instructionsMessage.ModifyAsync(x => x.Content = instructions);

            //Message exists
            if (_userMessage != null)
                await _userMessage.DeleteAsync();
        }

        /// <summary>
        ///     Rebuilds the faceit hub demos embed with the most up to date information.
        /// </summary>
        /// <returns>EmbedBuilder object containing all relevant information</returns>
        private EmbedBuilder RebuildEmbed()
        {
            var embed = new EmbedBuilder()
                .WithFooter($"Current CT Time: {DateTime.Now}")
                .WithColor(new Color(0x752424));

            if (_faceItHubDemosRequest.DateFrom != new DateTime())
                embed.AddField("[0] Date From:", _faceItHubDemosRequest.DateFrom, true)
                    .WithColor(new Color(0xa53737));

            if (_faceItHubDemosRequest.DateTo != new DateTime())
                embed.AddField("[1] Date To:", _faceItHubDemosRequest.DateTo, true)
                    .WithColor(new Color(0x9a4237));

            if (!string.IsNullOrWhiteSpace(_faceItHubDemosRequest.Gamemode))
                embed.AddField("[2] Gamemode", _faceItHubDemosRequest.Gamemode, true)
                    .WithColor(new Color(0x8f4d37));

            if (_faceItHubDemosRequest.HubRegions != null && _faceItHubDemosRequest.HubRegions.Count() > 0)
                embed.AddField("[3] Hub Regions", string.Join("\n", _faceItHubDemosRequest.HubRegions), true)
                    .WithColor(new Color(0x796337));

            return embed;
        }

        /// <summary>
        ///     Cancels the request. Does some basic cleanup tasks.
        /// </summary>
        /// <returns></returns>
        private async Task CancelRequest()
        {
            if (_userMessage != null)
                await _context.Channel.SendMessageAsync("FaceIt Hub demos request was not filled out correctly!");
            else
                await _context.Channel.SendMessageAsync("Interactive builder timed out!");

            await _embedMessage.DeleteAsync();
            await _instructionsMessage.DeleteAsync();
        }

        /// <summary>
        ///     Parses, and validates information before it is stored in a faceit hub demos request object.
        /// </summary>
        /// <param name="type">Type of data to parse</param>
        /// <param name="data">Data to parse</param>
        /// <returns>True if information is valid, false otherwise</returns>
        private async Task<bool> ParseInformation(string type, string data)
        {
            if (string.IsNullOrWhiteSpace(data))
                data = "Nothing provided yet!";

            switch (type.Trim().ToLower())
            {
                case "date from":
                    var splitData = data.Split(new[] { '/' }).ToArray();
                    try
                    {
                        _faceItHubDemosRequest.DateFrom = new DateTime(int.Parse(splitData[2]), int.Parse(splitData[0]), int.Parse(splitData[1]));
                        return true;
                    }
                    catch
                    {
                        await Display($"Unable to parse DateTime.\nYou provided `{data}`\n" + _wizardText[0]);
                        return false;
                    }

                case "date to":
                    splitData = data.Split(new[] { '/' }).ToArray();
                    try
                    {
                        _faceItHubDemosRequest.DateTo = new DateTime(int.Parse(splitData[2]), int.Parse(splitData[0]), int.Parse(splitData[1]));

                        if (_faceItHubDemosRequest.DateTo > DateTime.Now)
                        {
                            await Display($"Date To cannot be in the future.\nYou provided `{data}`\n" + _wizardText[0]);
                            return false;
                        }
                        else if (_faceItHubDemosRequest.DateTo >= _faceItHubDemosRequest.DateFrom)
                        {
                            await Display($"Date To cannot be closer to the present than Date From.\nYou provided `{data}`\n" + _wizardText[0]);
                            return false;
                        }

                        return true;
                    }
                    catch
                    {
                        await Display($"Unable to parse DateTime.\nYou provided `{data}`\n" + _wizardText[0]);
                        return false;
                    }

                case "gamemode":
                    if (data.Contains("defuse", StringComparison.OrdinalIgnoreCase))
                    {
                        _faceItHubDemosRequest.Gamemode = "Defuse";
                        return true;
                    }

                    if (data.Contains("wingman", StringComparison.OrdinalIgnoreCase))
                    {
                        _faceItHubDemosRequest.Gamemode = "Wingman";
                        return true;
                    }

                    await Display($"Invalid gamemode.\nYou provided `{data}`\n" +
                                  _wizardText[2]);
                    return false;

                case "hub regions":
                    var regions = new List<string>();
                    if (data.Contains("eu", StringComparison.OrdinalIgnoreCase))
                    {
                        regions.Add("EU");
                    }

                    if (data.Contains("na", StringComparison.OrdinalIgnoreCase))
                    {
                        regions.Add("NA");
                    }

                    if (data.Contains("sa", StringComparison.OrdinalIgnoreCase))
                    {
                        regions.Add("SA");
                    }

                    if (data.Contains("sea", StringComparison.OrdinalIgnoreCase))
                    {
                        regions.Add("SEA");
                    }

                    if (data.Contains("oce", StringComparison.OrdinalIgnoreCase))
                    {
                        regions.Add("OCE");
                    }

                    // make sure that regions are valid against the gamemode inputted
                    if (
                        !string.IsNullOrWhiteSpace(_faceItHubDemosRequest.Gamemode) && _faceItHubDemosRequest.Gamemode.ToLower() == "wingman" &&
                        regions.Contains("SA") || regions.Contains("SEA") || regions.Contains("OCE")
                    ) {
                        await Display($"Invalid hub region for the gamemode selected.\nYou provided `{data}` for the gamemode `{_faceItHubDemosRequest.Gamemode}`\n" +
                                  _wizardText[2]);
                        return false;
                    }

                    // return if at least one valid hub region has been provided
                    if (regions.Count() > 0)
                    {
                        _faceItHubDemosRequest.HubRegions = regions;
                        return true;
                    }

                    await Display($"Invalid gamemode.\nYou provided `{data}`\n" +
                                  _wizardText[2]);
                    return false;

                default:
                    await Display("Unknown FaceIt Hub demos information.");
                    _requireAbort = true;
                    return false;
            }
        }
    }
}
