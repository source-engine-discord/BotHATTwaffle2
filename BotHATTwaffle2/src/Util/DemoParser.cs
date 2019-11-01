using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using BotHATTwaffle2.Handlers;
using BotHATTwaffle2.Services;
using Renci.SshNet;

namespace BotHATTwaffle2.src.Util
{
    internal class DemoParser
    {
        private const ConsoleColor LOG_COLOR = ConsoleColor.DarkRed;
        private static LogHandler _log;
        private static DataService _dataService;
        private static string mainFolderName = @"IDemO\"; // Changes to `string.Concat(Path.GetTempPath(), @"DemoGrabber\")` for bulk faceit demo parsing in ParseFaceItHubDemos()
        //private static string exeFolderName = @"IDemO\";
        private static string exeFolderName = @"F:\GitHub Files\CSGODemoCSV\TopStatsWaffle\bin\Release\";
        private static string outputFolderName = "matches";                                                                                         // CHANGES TO "parsed" IN IDemO VERSION 1.1.0
        private static string fileName = "CSGODemoCSV.exe";

        public static void SetHandlers(LogHandler log, DataService data)
        {
            _dataService = data;
            _log = log;
        }

        private static bool CanParse()
        {
            return File.Exists(exeFolderName + fileName);
        }

        public static FileInfo ParseDemo(string path)
        {
            mainFolderName = @"IDemO\";

            if (!CanParse())
                return null;

            //Start the process
            var processStartInfo = new ProcessStartInfo(exeFolderName + fileName, $"-folders \"{path}\"");
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
                _ = _log.LogMessage($"There is not exactly 1 JSON file in `{outputFolderName}` directory. Aborting.", alert: true,
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

        public static List<FileInfo> ParseFaceItHubDemos(string path)
        {
            mainFolderName = string.Concat(Path.GetTempPath(), @"DemoGrabber\");

            if (!CanParse())
                return null;

            //Start the process
            var processStartInfo = new ProcessStartInfo(exeFolderName + fileName, $"-folders \"{path}\" -recursive -nochickens");
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
                    .EnumerateFiles().Count(x => x.Extension.Equals(".json", StringComparison.OrdinalIgnoreCase)) == 0)
            {
                _ = _log.LogMessage($"There are no JSON files in `{outputFolderName}` directory. Aborting.", alert: true,
                    color: LOG_COLOR);
                return null;
            }

            List<FileInfo> jasonFiles = localDirectoryInfo.EnumerateFiles()
                .Where(x => x.Extension.Equals(".json", StringComparison.OrdinalIgnoreCase)).ToList();

            List<FileInfo> failedUploads = new List<FileInfo>();
            foreach (var file in jasonFiles)
            {
                var uploadResult = UploadDemo(file).Result;

                //Stored list of demos that failed to upload
                if (!uploadResult)
                {
                    failedUploads.Add(file);
                }
            }

            //Failed to upload any, abort.
            if (failedUploads.Count() > 0)
            {
                var failedUploadsString = string.Join("\n", failedUploads);
                _ = _log.LogMessage($"Failed to upload some demos: Aborting. {failedUploadsString}", alert: true,
                    color: LOG_COLOR);
            }

            //Delete all files in the directory.
            foreach (var file in localDirectoryInfo.EnumerateFiles())
            {
                _ = _log.LogMessage($"Deleting: {file.FullName}", false, color: LOG_COLOR);
                file.Delete();
            }

            return jasonFiles;
        }

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