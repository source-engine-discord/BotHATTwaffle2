using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Renci.SshNet;
using BotHATTwaffle2.Handlers;
using BotHATTwaffle2.Services;

namespace BotHATTwaffle2.src.Util
{
    class DemoParser
    {
        private const ConsoleColor LOG_COLOR = ConsoleColor.DarkRed;
        private static LogHandler _log;
        private static DataService _dataService;

        public static void SetHandlers(LogHandler log, DataService data)
        {
            _dataService = data;
            _log = log;
        }

        private static bool CanParse()
        {
            return File.Exists(@"IDemO\CSGODemoCSV.exe");
        }

        public static FileInfo ParseDemo(string path)
        {
            if (!CanParse())
                return null;

            //Start the process
            ProcessStartInfo processStartInfo = new ProcessStartInfo(@"IDemO\CSGODemoCSV.exe", $"-folders \"{path}\"");
            processStartInfo.WorkingDirectory = "IDemO";
            Process demoProcess = Process.Start(processStartInfo);
            
            //Unable to start for some reason. Bail.
            if (demoProcess == null)
            {
                _ = _log.LogMessage("Failed to file process to parse demo. Aborting Demo parse.",alert:true, color: LOG_COLOR);
                return null;
            }

            demoProcess.WaitForExit();
            demoProcess.Close();

            //Get all the json files in the directory.
            var localDirectoryInfo = new DirectoryInfo(@"IDemO\matches");

            if (localDirectoryInfo
                    .EnumerateFiles().Count(x => x.Extension.Equals(".json", StringComparison.OrdinalIgnoreCase)) != 1)
            {
                _ = _log.LogMessage("There is not exactly 1 JSON file in `matches` directory. Aborting.", alert: true, color: LOG_COLOR);
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
                _ = _log.LogMessage($"Deleting: {file.FullName}",channel: false, color: LOG_COLOR);
                file.Delete();
            }

            return jasonFile;
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
                    await _log.LogMessage($"Failed to connect to SFTP server. {_dataService.RSettings.ProgramSettings.DemoFtpServer}" +
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
                    await _log.LogMessage($"Failed to download file from playtest server. {_dataService.RSettings.ProgramSettings.DemoFtpServer}" +
                                          $"\n{e.Message}", alert:true, color: LOG_COLOR);
                    return false;
                }

                client.Disconnect();
            }

            _ = _log.LogMessage($"Uploaded {file.FullName} to server where it can be viewed.", color: LOG_COLOR);
            return true;
        }
    }
}
