﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using BotHATTwaffle2.Models.JSON.Steam;
using BotHATTwaffle2.Util;
using bsp_pakfile;
using Discord;
using Discord.WebSocket;
using ICSharpCode.SharpZipLib.Zip;
using Newtonsoft.Json;
using Color = Discord.Color;
using ImageFormat = Pfim.ImageFormat;
using ZipFile = System.IO.Compression.ZipFile;

namespace BotHATTwaffle2.Services.Steam
{
    public class Workshop
    {
        private static RootWorkshop workshopJsonGameData;

        private bool EnsureGameListCache()
        {
            if (workshopJsonGameData != null)
                return true;

            // So basically the only way to get game name from appid is to get a list of a user's owned games, then match our appid from the workshop item with their game (and yoink the name)
            using (var clientGame = new HttpClient())
            {
                Console.WriteLine("FETCHING GAMES FROM STEAM");
                clientGame.BaseAddress = new Uri("https://api.steampowered.com/ISteamApps/GetAppList/v2/");
                var responseGame = clientGame.GetAsync("").Result;
                responseGame.EnsureSuccessStatusCode();
                var resultGame = responseGame.Content.ReadAsStringAsync().Result;

                // Don't embed anything if the third GET request fails (hopefully it doesn't)
                if (resultGame == "{}") return false;
                //Deserialize version 3, electric boogaloo
                workshopJsonGameData = JsonConvert.DeserializeObject<RootWorkshop>(resultGame);
            }

            return true;
        }

        public async Task DownloadWorkshopBsp(DataService _dataService, string fileLocation, string workshopId)
        {
            //var apiKey = _dataService.RSettings.ProgramSettings.SteamworksAPI;

            //TODO: JIM, REWRITE THIS. WE DO NOT NEED TO WRITE THE API CALL AGAIN. PUT IT IN A SINGULAR METHOD

            // Send the POST request for item info
            using (var clientItem = new HttpClient())
            {
                //Define our key value pairs
                var kvp1 = new KeyValuePair<string, string>("itemcount", "1");

                //Create empty key value pair and populate it based input variables.
                var kvp2 = new KeyValuePair<string, string>("publishedfileids[0]", workshopId);

                var contentItem = new FormUrlEncodedContent(new[]
                {
                    kvp1, kvp2
                });

                string resultContentItem;
                RootWorkshop workshopJsonItem;
                var retryCount = 0;

                while (true)
                {
                    try
                    {
                        // Send the actual post request
                        clientItem.BaseAddress =
                            new Uri("https://api.steampowered.com/ISteamRemoteStorage/GetPublishedFileDetails/v1/");
                        var resultItem = await clientItem.PostAsync("", contentItem);
                        resultContentItem = await resultItem.Content.ReadAsStringAsync();
                    }
                    catch (Exception e)
                    {
                        //Don't know what can happen here. Unless we crash later on, just going to catch everything
                        Console.WriteLine(e);
                        return;
                    }

                    //Check if response is empty
                    if (resultContentItem == "{}")
                        return;

                    if (_dataService.RSettings.ProgramSettings.Debug)
                        Console.WriteLine(resultContentItem);

                    // Build workshop item embed, and set up author and game data embeds here for scoping reasons
                    try
                    {
                        workshopJsonItem = JsonConvert.DeserializeObject<RootWorkshop>(resultContentItem);
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine("Error parsing JSON from STEAM. The response was:\n" + resultContentItem);

                        if (retryCount <= 3)
                        {
                            Console.WriteLine("Retrying in 2 seconds...");
                            await Task.Delay(2000);
                            retryCount++;
                            continue;
                        }

                        //Something happened getting the response from Steam. We got a response but it wasn't valid?
                        Console.WriteLine(e);
                        Console.WriteLine("Aborting workshop embed...");
                        return;
                    }

                    break;
                }

                // If the file is a screenshot, artwork, video, or guide we don't need to embed it because Discord will do it for us
                if (workshopJsonItem.response.publishedfiledetails[0].result != 1 ||
                    !workshopJsonItem.response.publishedfiledetails[0].filename.ToLower().Contains(".bsp")
                ) // assuming 1 == map submission ??
                    return;

                // Download the bsp
                Console.WriteLine("Downloading BSPs");
                var fileName = workshopJsonItem.response.publishedfiledetails[0].filename
                    .Split(new[] {"mymaps/", ".bsp"}, StringSplitOptions.None).Skip(1).FirstOrDefault();
                var fileNameBsp = workshopJsonItem.response.publishedfiledetails[0].filename
                    .Split(new[] {"mymaps/"}, StringSplitOptions.None).LastOrDefault();
                var fileLocationZippedBsp = string.Concat(fileLocation, "\\Zipped BSPs\\", fileNameBsp);
                var fileLocationBsp = string.Concat(fileLocation, "\\BSPs\\", fileNameBsp);
                var fileLocationOverviewPng = string.Concat(fileLocation, "\\Overviews\\", fileName, "_radar.png");
                var fileLocationOverviewTxt = string.Concat(fileLocation, "\\Overviews\\", fileName, ".txt");

                // create folders if needed
                if (!Directory.Exists(fileLocation))
                    Directory.CreateDirectory(fileLocation);
                if (!Directory.Exists(string.Concat(fileLocation, "\\Zipped BSPs\\")))
                    Directory.CreateDirectory(string.Concat(fileLocation, "\\Zipped BSPs\\"));
                if (!Directory.Exists(string.Concat(fileLocation, "\\BSPs\\")))
                    Directory.CreateDirectory(string.Concat(fileLocation, "\\BSPs\\"));
                if (!Directory.Exists(string.Concat(fileLocation, "\\Overviews\\")))
                    Directory.CreateDirectory(string.Concat(fileLocation, "\\Overviews\\"));

                if (!File.Exists(fileLocationBsp) &&
                    (!File.Exists(fileLocationOverviewPng) || !File.Exists(fileLocationOverviewTxt)))
                {
                    var downloadUrl = workshopJsonItem.response.publishedfiledetails[0].file_url;

                    using (var client = new WebClient())
                    {
                        // download zip file
                        client.Headers.Add("User-Agent: Other");
                        try
                        {
                            client.DownloadFile(downloadUrl, fileLocationZippedBsp);
                        }
                        catch (WebException e)
                        {
                            Console.WriteLine("Error downloading demo.");

                            if (File.Exists(fileLocationZippedBsp)) File.Delete(fileLocationZippedBsp);

                            client.Dispose();

                            await Task.Delay(1000);
                        }

                        client.Dispose();
                    }

                    // unzip bsp file
                    ZipFile.ExtractToDirectory(fileLocationZippedBsp, string.Concat(fileLocation, "\\BSPs\\"));

                    // delete the zipped bsp file
                    File.Delete(fileLocationZippedBsp);
                }

                // grab overview files from bsp
                if (!File.Exists(fileLocationOverviewPng) || !File.Exists(fileLocationOverviewTxt))
                    GrabOverviewFilesFromBsp(fileLocation, fileLocationBsp);
            }
        }

        public void GrabOverviewFilesFromBsp(string fileLocation, string fileLocationBsp)
        {
            // TODO: This method will most definitely fail if a .BSP is grabbed that doesn't have anything packed under the resource folder. Not exactly sure how to check that right now.

            // This is just for checking how long it takes to extract the radar stuff from the BSP. You can remove this if you want
            var sw = new Stopwatch();
            sw.Start();

            // Parse BSP using our BSP model
            var bsp = new SourceBSP(fileLocationBsp);

            // Getting ready to read the pakfile
            byte[] ret;
            using (var MS = new MemoryStream(bsp.PAKFILE))
            using (var zip = new ICSharpCode.SharpZipLib.Zip.ZipFile(MS))
            {
                // Iterate over everything in the pakfile lump
                foreach (ZipEntry entry in zip)
                {
                    if (!entry.IsFile) continue;
                    var name = entry.Name;

                    // We're just gonna grab everything packed until the resource folder
                    if (!name.StartsWith("resource")) continue;

                    // We've found a file we're looking for, so we're gonna convert it to a memory stream and send that directly to a file
                    using (var stream = zip.GetInputStream(entry))
                    {
                        ret = new byte[stream.Length];
                        using (var ms = new MemoryStream(ret))
                        {
                            stream.CopyTo(ms);
                        }
                    }

                    // Changing the relative path so it goes into the Overviews folder we want
                    var nameArray = name.Split("/");
                    name = string.Concat("\\Overviews\\", nameArray[nameArray.Length - 1]);

                    // Save our overview files into the Overviews folder
                    var savePath = string.Concat(fileLocation, name);
                    File.WriteAllBytes(savePath, ret); // Write byte array to file
                }
            }

            ret = null;

            // Also part of the stopwatch/timing function. Can remove if you want later.
            sw.Stop();
            Console.WriteLine($"BSP parsed and unpacked in {sw.ElapsedMilliseconds} ms");

            // Get a list of every DDS file that we need to convert
            var ext = new List<string> {".dds"};
            var listOfDdsFiles = Directory.GetFiles(string.Concat(fileLocation, "\\Overviews\\"), "*.*",
                    SearchOption.AllDirectories)
                .Where(s => ext.Contains(Path.GetExtension(s)));

            // Iterate over every DDS File to convert to PNG
            foreach (var radarImagePath in listOfDdsFiles)
                // All this code here is just converting from DDS -> PNG
                using (var imageFile = Pfim.Pfim.FromFile(radarImagePath))
                {
                    PixelFormat format;
                    Console.WriteLine($"Radar image format found to be: {imageFile.Format}");
                    switch (imageFile.Format)
                    {
                        // I haven't seen radars anything other than RGB24 so, let's just hope there's not one like that
                        case ImageFormat.Rgb24:
                            format = PixelFormat.Format24bppRgb;
                            break;
                        default:
                            throw new NotImplementedException("The given DDS format specifics are not implemented.");
                    }

                    // Need to tell the garbage collector that wer're still using the image data
                    var handle = GCHandle.Alloc(imageFile.Data, GCHandleType.Pinned);
                    try
                    {
                        // Not sure what exactly will happen if this fails. So let's hope it doesn't
                        var data = Marshal.UnsafeAddrOfPinnedArrayElement(imageFile.Data, 0);
                        var bitmap = new Bitmap(imageFile.Width, imageFile.Height, imageFile.Stride, format, data);
                        bitmap.Save(Path.ChangeExtension(radarImagePath, ".png"),
                            System.Drawing.Imaging.ImageFormat.Png);
                    }
                    finally
                    {
                        // We no longer need the image data, so we're going to tell the garbage collector it can get rid of it, as well as just deleting the .dds
                        handle.Free();
                        File.Delete(radarImagePath);
                    }
                }

            Console.WriteLine("Successfully converted all radars to .PNG and moved TXT file");
        }

        public async Task<EmbedBuilder> HandleWorkshopEmbeds(SocketMessage message, DataService _dataService,
            string images = null, string testType = null, string inputId = null)
        {
            // Cut down the message to grab just the first URL
            var regMatch = Regex.Match(message.Content,
                @"\b((https?|ftp|file)://|(www|ftp)\.)(steamcommunity)[-A-Z0-9+&@#/%?=~_|$!:,.;]*[A-Z0-9+&@#/%=~_|$]",
                RegexOptions.IgnoreCase);
            var workshopLink = regMatch.ToString();
            var apiKey = _dataService.RSettings.ProgramSettings.SteamworksAPI;

            // Send the POST request for item info
            using (var clientItem = new HttpClient())
            {
                //Define our key value pairs
                var kvp1 = new KeyValuePair<string, string>("itemcount", "1");

                //Create empty key value pair and populate it based input variables.
                var kvp2 = new KeyValuePair<string, string>();
                if (inputId != null)
                    kvp2 = new KeyValuePair<string, string>("publishedfileids[0]", inputId);
                else
                    kvp2 = new KeyValuePair<string, string>("publishedfileids[0]",
                        GeneralUtil.GetWorkshopIdFromFqdn(workshopLink));

                var contentItem = new FormUrlEncodedContent(new[]
                {
                    kvp1, kvp2
                });

                string resultContentItem;
                RootWorkshop workshopJsonItem;
                var retryCount = 0;

                while (true)
                {
                    try
                    {
                        // Send the actual post request
                        clientItem.BaseAddress =
                            new Uri("https://api.steampowered.com/ISteamRemoteStorage/GetPublishedFileDetails/v1/");
                        var resultItem = await clientItem.PostAsync("", contentItem);
                        resultContentItem = await resultItem.Content.ReadAsStringAsync();
                    }
                    catch (Exception e)
                    {
                        //Don't know what can happen here. Unless we crash later on, just going to catch everything
                        Console.WriteLine(e);
                        return null;
                    }

                    //Check if response is empty
                    if (resultContentItem == "{}") return null;

                    if (_dataService.RSettings.ProgramSettings.Debug)
                        Console.WriteLine(resultContentItem);

                    // Build workshop item embed, and set up author and game data embeds here for scoping reasons
                    try
                    {
                        workshopJsonItem = JsonConvert.DeserializeObject<RootWorkshop>(resultContentItem);
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine("Error parsing JSON from STEAM. The response was:\n" + resultContentItem);

                        if (retryCount <= 3)
                        {
                            Console.WriteLine("Retrying in 2 seconds...");
                            await Task.Delay(2000);
                            retryCount++;
                            continue;
                        }

                        //Something happened getting the response from Steam. We got a response but it wasn't valid?
                        Console.WriteLine(e);
                        Console.WriteLine("Aborting workshop embed...");
                        return null;
                    }

                    break;
                }

                RootWorkshop workshopJsonAuthor;

                // If the file is a screenshot, artwork, video, or guide we don't need to embed it because Discord will do it for us
                if (workshopJsonItem.response.publishedfiledetails[0].result == 9) return null;
                if (workshopJsonItem.response.publishedfiledetails[0].filename
                    .Contains("/screenshots/".ToLower())) return null;

                while (true)
                    // Send the GET request for the author information
                    using (var clientAuthor = new HttpClient())
                    {
                        string resultAuthor = null;
                        try
                        {
                            clientAuthor.BaseAddress =
                                new Uri("https://api.steampowered.com/ISteamUser/GetPlayerSummaries/v2/");
                            var responseAuthor = clientAuthor
                                .GetAsync(
                                    $"?key={apiKey}&steamids={workshopJsonItem.response.publishedfiledetails[0].creator}")
                                .Result;
                            responseAuthor.EnsureSuccessStatusCode();
                            resultAuthor = responseAuthor.Content.ReadAsStringAsync().Result;

                            // Don't embed anything if getting the author fails for some reason
                            if (resultAuthor == "{\"response\":{}}") return null;

                            // If we get a good response though, we're gonna deserialize it

                            workshopJsonAuthor = JsonConvert.DeserializeObject<RootWorkshop>(resultAuthor);
                        }
                        catch (Exception e)
                        {
                            Console.WriteLine("Error parsing JSON from STEAM. The response was:\n" + resultAuthor);

                            if (retryCount <= 3)
                            {
                                Console.WriteLine("Retrying in 2 seconds...");
                                await Task.Delay(2000);
                                retryCount++;
                                continue;
                            }

                            //Something happened getting the response from Steam. We got a response but it wasn't valid?
                            Console.WriteLine(e);
                            Console.WriteLine("Aborting workshop embed...");
                            return null;
                        }

                        break;
                    }

                //Make sure a cache exists
                if (!EnsureGameListCache())
                    return null;

                if (string.IsNullOrWhiteSpace(workshopLink))
                    workshopLink =
                        $"https://steamcommunity.com/sharedfiles/filedetails/?id={workshopJsonItem.response.publishedfiledetails[0].publishedfileid}";

                // Finally we can build the embed after too many HTTP requests
                var workshopItemEmbed = new EmbedBuilder()
                    .WithAuthor($"{workshopJsonItem.response.publishedfiledetails[0].title}",
                        workshopJsonAuthor.response.players[0].avatar, workshopLink)
                    .WithTitle($"Creator: {workshopJsonAuthor.response.players[0].personaname}")
                    .WithUrl(workshopJsonAuthor.response.players[0].profileurl)
                    .WithImageUrl(workshopJsonItem.response.publishedfiledetails[0].preview_url)
                    .WithColor(new Color(71, 126, 159));

                var gameId = workshopJsonGameData.applist.apps.SingleOrDefault(x =>
                    x.appid == workshopJsonItem.response.publishedfiledetails[0].creator_app_id);

                if (gameId != null) workshopItemEmbed.AddField("Game", gameId.name, true);

                // Add every other field now
                // Get tags from Json object
                var tags = string.Join(", ",
                    workshopJsonItem.response.publishedfiledetails[0].tags.Select(x => x.tag));

                if (!string.IsNullOrWhiteSpace(tags))
                    workshopItemEmbed.AddField("Tags", tags, true);

                // If test type is null or empty, it will not be included in the embed (bot only)
                if (!string.IsNullOrEmpty(testType)) workshopItemEmbed.AddField("Test Type", testType);

                // TODO: Strip BB Codes
                var shortDescription = Regex.Replace(workshopJsonItem.response.publishedfiledetails[0].description,
                    @"\t|\n|\r", " ");

                if (!string.IsNullOrWhiteSpace(shortDescription))
                    workshopItemEmbed.AddField("Description",
                        shortDescription.Length > 497 ? shortDescription.Substring(0, 497) + "..." : shortDescription);

                // If images is null or empty, it will not be included in the embed (bot only)
                if (!string.IsNullOrEmpty(images)) workshopItemEmbed.AddField("Links", images);

                return workshopItemEmbed;
            }
        }

        public async Task SendWorkshopEmbed(SocketMessage message, DataService _dataService)
        {
            await message.Channel.TriggerTypingAsync();
            //If the invoking message has an embed, do nothing.
            await Task.Delay(2000);
            var refreshedMessage = await _dataService.GetSocketMessage(message.Channel, message.Id);
            if (refreshedMessage.Embeds.Count > 0)
                return;

            var embed = await HandleWorkshopEmbeds(message, _dataService);

            if (embed != null)
                await message.Channel.SendMessageAsync(embed: embed.Build());
        }
    }
}