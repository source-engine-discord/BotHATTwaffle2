using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using BotHATTwaffle2.Handlers;
using BotHATTwaffle2.Services;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Renci.SshNet;

namespace BotHATTwaffle2.Util
{
    internal class DemoParser
    {
        private const ConsoleColor LOG_COLOR = ConsoleColor.DarkRed;
        private static LogHandler _log;
        private static DataService _dataService;

        private static readonly string
            mainFolderName =
                @"IDemO\"; // Changes to `string.Concat(Path.GetTempPath(), @"DemoGrabber\")` for bulk faceit demo parsing in ParseFaceItHubDemos()

        private static readonly string exeFolderName = @"IDemO\"; // NEEDS TO BE THIS WHEN RUNNING ON IDO

        //private static string exeFolderName = @"F:\GitHub Files\CSGODemoCSV\TopStatsWaffle\bin\Release\";
        private static readonly string outputFolderName = "parsed";
        private static readonly string fileName = "IDemO.exe";

        public static void SetHandlers(LogHandler log, DataService data)
        {
            _dataService = data;
            _log = log;
        }

        /// <summary>
        ///     Gets the Workshop ID as a string from a parsed Jason file.
        /// </summary>
        /// <param name="jasonFile">Jason file to get workshop ID from</param>
        /// <returns></returns>
        public static string GetWorkshopIdFromJasonFile(FileInfo jasonFile)
        {
            using (var reader = new StreamReader(jasonFile.FullName))
            {
                var jason = reader.ReadToEnd();
                var jasonObject = (JObject) JsonConvert.DeserializeObject(jason);
                return jasonObject["mapInfo"]["WorkshopID"].Value<string>();
            }
        }

        public static string GetMapnameJasonFile(FileInfo jasonFile)
        {
            using (var reader = new StreamReader(jasonFile.FullName))
            {
                var jason = reader.ReadToEnd();
                var jasonObject = (JObject)JsonConvert.DeserializeObject(jason);
                return jasonObject["mapInfo"]["MapName"].Value<string>();
            }
        }

        /// <summary>
        ///     Parses FaceIt Demos in bulk.
        /// </summary>
        /// <param name="sourcePath"></param>
        /// <param name="destinationPath"></param>
        /// <returns></returns>
        public static async Task ParseFaceitDemos(string sourcePath, string destinationPath)
        {
            //Start the process
            var processStartInfo = new ProcessStartInfo(exeFolderName + fileName,
                $"-folders \"{sourcePath}\" -output \"{destinationPath}\" -nochickens -samefilename -lowoutputmode");
            processStartInfo.WorkingDirectory = "IDemO";

            //Start demo parser with a 20m timeout
            await AsyncProcessRunner.RunAsync(processStartInfo, 20 * 60 * 1000);
        }

        /// <summary>
        ///     Parses single demos from scheduled playtesting events
        /// </summary>
        /// <param name="path"></param>
        /// <returns></returns>
        public static FileInfo ParseDemo(string path)
        {
            //Start the process
            var processStartInfo = new ProcessStartInfo(exeFolderName + fileName, $"-folders \"{path}\" -noplayerpositions");
            processStartInfo.WorkingDirectory = mainFolderName;
            var demoProcess = Process.Start(processStartInfo);

            //Unable to start for some reason. Bail.
            if (demoProcess == null)
            {
                _ = _log.LogMessage("Failed to find process to parse demo. Aborting Demo parse.", alert: true,
                    color: LOG_COLOR);
                return null;
            }

            demoProcess.WaitForExit();
            demoProcess.Close();

            //Get all the json files in the directory.
            var localDirectoryInfo = new DirectoryInfo(mainFolderName + outputFolderName);

            if (localDirectoryInfo
                    .EnumerateFiles().Count(x => x.Extension.Equals(".json", StringComparison.OrdinalIgnoreCase)) != 1)
            {
                _ = _log.LogMessage($"There is not exactly 1 JSON file in `{outputFolderName}` directory. Aborting.",
                    alert: true,
                    color: LOG_COLOR);
                return null;
            }

            var jasonFile = localDirectoryInfo.EnumerateFiles()
                .FirstOrDefault(x => x.Extension.Equals(".json", StringComparison.OrdinalIgnoreCase));

            var uploadResult = UploadDemo(jasonFile).Result;

            //Failed to upload, abort.
            if (!uploadResult)
                return null;

            //Delete all files in the directory.
            foreach (var file in localDirectoryInfo.EnumerateFiles())
            {
                _ = _log.LogMessage($"Deleting: {file.FullName}", false, color: LOG_COLOR);
                file.Delete();
            }

            return jasonFile;
        }

        /// <summary>
        ///     Uploads many FaceIt demos in a single session
        /// </summary>
        /// <param name="uploadDictionary"></param>
        /// <returns>-1 if failed, upload count if successful.</returns>
        public static async Task<int> UploadFaceitDemosAndRadars(Dictionary<FileInfo, string> uploadDictionary)
        {
            _ = _log.LogMessage($"Starting upload of {uploadDictionary.Count} files!");

            var uploadCount = 0;
            using (var client = new SftpClient(_dataService.RSettings.ProgramSettings.DemoFtpServer,
                _dataService.RSettings.ProgramSettings.DemoFtpUser,
                _dataService.RSettings.ProgramSettings.DemoFtpPassword))
            {
                try
                {
                    client.Connect();
                }
                catch (Exception e)
                {
                    await _log.LogMessage(
                        $"Failed to connect to SFTP server. {_dataService.RSettings.ProgramSettings.DemoFtpServer}" +
                        $"\n {e.Message}", alert: true, color: LOG_COLOR);
                    return -1;
                }
                
                //Get a listing of directories for uploading
                var dirListing = client.ListDirectory(_dataService.RSettings.ProgramSettings.FaceItDemoFtpPath);

                foreach (var upload in uploadDictionary)
                    try
                    {
                        //Check if the destination dir exists
                        if (!dirListing.Any(x => x.Name.Equals(upload.Value)))
                        {
                            //Create it if not
                            client.CreateDirectory(
                                $"{_dataService.RSettings.ProgramSettings.FaceItDemoFtpPath}/{upload.Value}");

                            //Refresh out directory listing
                            dirListing = client.ListDirectory(_dataService.RSettings.ProgramSettings.FaceItDemoFtpPath);
                        }

                        using (var fileStream = File.OpenRead(upload.Key.FullName))
                        {
                            if (_dataService.RSettings.ProgramSettings.Debug)
                                _ = _log.LogMessage($"Uploading {upload.Key.FullName}\n" +
                                                    $"File {uploadCount} of {uploadDictionary.Count}", false, color: LOG_COLOR);

                            client.UploadFile(fileStream,
                                $"{_dataService.RSettings.ProgramSettings.FaceItDemoFtpPath}/{upload.Value}/{upload.Key.Name}",
                                true);
                        }
                        uploadCount++;

                        if (uploadCount % 10 == 0)
                        {
                            if (!_dataService.RSettings.ProgramSettings.Debug)
                                _ = _log.LogMessage($"Uploading file {uploadCount} of {uploadDictionary.Count}", false, color: LOG_COLOR);
                        }
                    }
                    catch (Exception e)
                    {
                        await _log.LogMessage(
                            $"Failed uploading {upload.Key.FullName}\n{e.Message}", color: LOG_COLOR);
                    }

                client.Disconnect();
            }

            _ = _log.LogMessage($"Uploaded {uploadCount} files!", false, color: LOG_COLOR);
            return uploadCount;
        }

        /// <summary>
        ///     Uploads a single demo, used for scheduled playtesting events.
        /// </summary>
        /// <param name="file"></param>
        /// <returns></returns>
        private static async Task<bool> UploadDemo(FileInfo file)
        {
            using (var client = new SftpClient(_dataService.RSettings.ProgramSettings.DemoFtpServer,
                _dataService.RSettings.ProgramSettings.DemoFtpUser,
                _dataService.RSettings.ProgramSettings.DemoFtpPassword))
            {
                try
                {
                    client.Connect();
                }
                catch (Exception e)
                {
                    await _log.LogMessage(
                        $"Failed to connect to SFTP server. {_dataService.RSettings.ProgramSettings.DemoFtpServer}" +
                        $"\n {e.Message}", alert: true, color: LOG_COLOR);
                    return false;
                }

                try
                {
                    client.ChangeDirectory(_dataService.RSettings.ProgramSettings.DemoFtpPath);

                    using (var fileStream = File.OpenRead(file.FullName))
                    {
                        client.UploadFile(fileStream, file.Name, true);
                    }
                }
                catch (Exception e)
                {
                    await _log.LogMessage(
                        $"Failed to download file from playtest server. {_dataService.RSettings.ProgramSettings.DemoFtpServer}" +
                        $"\n{e.Message}", alert: true, color: LOG_COLOR);
                    return false;
                }

                client.Disconnect();
            }

            _ = _log.LogMessage($"Uploaded {file.FullName} to server where it can be viewed.", color: LOG_COLOR);
            return true;
        }
    }
}