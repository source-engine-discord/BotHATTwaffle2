using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using BotHATTwaffle2.Handlers;
using BotHATTwaffle2.Models.JSON.Steam;
using ICSharpCode.SharpZipLib.Zip;
using Newtonsoft.Json;
using ImageFormat = Pfim.ImageFormat;
using ZipFile = System.IO.Compression.ZipFile;

namespace BotHATTwaffle2.Services.Steam
{
    public class SteamAPI
    {
        private const ConsoleColor LOG_COLOR = ConsoleColor.Cyan;
        private static RootWorkshop _workshopJsonGameData;
        private readonly DataService _dataService;
        private readonly LogHandler _log;

        public SteamAPI(DataService dataService, LogHandler log)
        {
            _dataService = dataService;
            _log = log;
        }

        public async Task<RootWorkshop> GetWorkshopGames()
        {
            if (_workshopJsonGameData != null)
                return _workshopJsonGameData;

            // So basically the only way to get game name from appid is to get a list of a user's owned games, then match our appid from the workshop item with their game (and yoink the name)
            using (var clientGame = new HttpClient())
            {
                await _log.LogMessage("Getting games from SteamAPI", color: LOG_COLOR);
                clientGame.BaseAddress = new Uri("https://api.steampowered.com/ISteamApps/GetAppList/v2/");
                var responseGame = clientGame.GetAsync("").Result;
                responseGame.EnsureSuccessStatusCode();
                var resultGame = responseGame.Content.ReadAsStringAsync().Result;

                if (resultGame == "{}")
                    return null;

                _workshopJsonGameData = JsonConvert.DeserializeObject<RootWorkshop>(resultGame);
            }

            return _workshopJsonGameData;
        }

        public async Task<RootWorkshop> GetWorkshopItem(string workshopId)
        {
            RootWorkshop workshopJsonItem;
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

                var retryCount = 0;

                clientItem.BaseAddress =
                    new Uri("https://api.steampowered.com/ISteamRemoteStorage/GetPublishedFileDetails/v1/");
                while (true)
                {
                    string resultContentItem;
                    try
                    {
                        // Send the actual post request
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
                        await _log.LogMessage($"Response from Steam:\n{resultContentItem}", false, color: LOG_COLOR);

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
                            Console.WriteLine("Retrying in 10 seconds...");
                            await Task.Delay(10000);
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
            }

            return workshopJsonItem;
        }

        public async Task<FileInfo> DownloadWorkshopMap(string destinationFileLocation, string workshopId)
        {
            var workshopJsonItem = await GetWorkshopItem(workshopId);

            if (workshopJsonItem == null)
                return null;

            // If the file is a screenshot, artwork, video, or guide we don't need to embed it because Discord will do it for us
            if (workshopJsonItem.response.publishedfiledetails[0].result != 1 ||
                !workshopJsonItem.response.publishedfiledetails[0].filename.ToLower().Contains(".bsp")
            ) // assuming 1 == map submission ??
                return null;

            // Download the bsp
            Console.WriteLine("Downloading BSPs");
            var fileName = workshopJsonItem.response.publishedfiledetails[0].filename
                .Split(new[] {"mymaps/", ".bsp"}, StringSplitOptions.None).Skip(1).FirstOrDefault();
            var fileNameBsp = workshopJsonItem.response.publishedfiledetails[0].filename
                .Split(new[] {"mymaps/"}, StringSplitOptions.None).LastOrDefault();
            var fileLocationZippedBsp = string.Concat(destinationFileLocation, fileNameBsp);
            var fileLocationBsp = string.Concat(destinationFileLocation, @"\", fileNameBsp);
            var fileLocationOverviewPng = string.Concat(destinationFileLocation, fileName, "_radar.png");
            var fileLocationOverviewTxt = string.Concat(destinationFileLocation, fileName, ".txt");

            //Create dirs if we need them to exist
            Directory.CreateDirectory(destinationFileLocation);

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
                ZipFile.ExtractToDirectory(fileLocationZippedBsp, destinationFileLocation);

                // delete the zipped bsp file
                File.Delete(fileLocationZippedBsp);
            }

            Console.WriteLine(fileLocationBsp);
            return new FileInfo(fileLocationBsp);
        }

        public async Task<List<FileInfo>> GetWorkshopMapRadarFiles(string destinationFileLocation, string workshopId)
        {
            var radarFiles = new List<FileInfo>();
            var bspFile = await DownloadWorkshopMap(destinationFileLocation, workshopId);

            if (bspFile == null)
                return null;

            // Parse BSP using our BSP model
            var bsp = new SourceBSP(bspFile.FullName);

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
                    name = nameArray[nameArray.Length - 1];

                    //Make sure that the path ends with a back slash
                    var validatedSavePath = destinationFileLocation;
                    if (!validatedSavePath.EndsWith('\\'))
                        validatedSavePath += '\\';

                    // Save our overview files into the Overviews folder
                    var savePath = string.Concat(validatedSavePath, name);

                    File.WriteAllBytes(savePath, ret); // Write byte array to file
                }
            }

            // Get a list of every DDS file that we need to convert
            var ext = new List<string> {".dds"};
            var listOfDdsFiles = Directory.GetFiles(destinationFileLocation, "*.*", SearchOption.AllDirectories)
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
                        case ImageFormat.Rgb24:
                            format = PixelFormat.Format24bppRgb;
                            break;
                        case ImageFormat.Rgba32:
                            format = PixelFormat.Format32bppArgb;
                            break;
                        case ImageFormat.Rgba16:
                            format = PixelFormat.Format16bppArgb1555;
                            break;
                        default:
                            return null;
                    }

                    // Need to tell the garbage collector that we're still using the image data
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

            //Delete the BSP, we don't want it anymore
            File.Delete(bspFile.FullName);

            var extractedFiles = Directory.GetFiles(destinationFileLocation,
                $"{Path.GetFileNameWithoutExtension(bspFile.Name)}*.*", SearchOption.AllDirectories);
            foreach (var f in extractedFiles)
                if (f.EndsWith(".png") || f.EndsWith(".txt"))
                    radarFiles.Add(new FileInfo(f));

            return radarFiles;
        }

        public async Task<RootWorkshop> GetWorkshopAuthor(RootWorkshop rootWorkshop)
        {
            RootWorkshop workshopJsonAuthor;
            // If the file is a screenshot, artwork, video, or guide we don't need to embed it because Discord will do it for us
            if (rootWorkshop.response.publishedfiledetails[0].result == 9) return null;
            if (rootWorkshop.response.publishedfiledetails[0].filename
                .Contains("/screenshots/".ToLower())) return null;

            var retryCount = 0;
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
                                $"?key={_dataService.RSettings.ProgramSettings.SteamworksAPI}&steamids={rootWorkshop.response.publishedfiledetails[0].creator}")
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

            return workshopJsonAuthor;
        }
    }
}