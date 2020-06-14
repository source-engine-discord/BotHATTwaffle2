using System;
using System.IO;
using System.Linq;
using System.Security.Authentication;
using System.Threading.Tasks;
using BotHATTwaffle2.Handlers;
using BotHATTwaffle2.Models.LiteDB;
using BotHATTwaffle2.Services;
using FluentFTP;
using Renci.SshNet;
using Renci.SshNet.Sftp;

namespace BotHATTwaffle2.Util
{
    internal class DownloadHandler
    {
        private const ConsoleColor LOG_COLOR = ConsoleColor.DarkRed;
        private static LogHandler _log;
        private static DataService _dataService;

        public static void SetHandlers(LogHandler log, DataService data)
        {
            _dataService = data;
            _log = log;
        }

        public static async Task<string> DownloadPlaytestDemo(PlaytestCommandInfo playtestCommandInfo)
        {
            var server = DatabaseUtil.GetTestServer(playtestCommandInfo.ServerAddress);

            if (server == null)
                return null;

            var dateInsert = "";
            //Arrived from a public test, where a proper start time did not exist.
            if (playtestCommandInfo.StartDateTime == new DateTime())
            {
                dateInsert = $"{DateTime.Now:MM_dd_yyyy}_";
                playtestCommandInfo.StartDateTime = DateTime.Now;
            }

            var localPath =
                $"{_dataService.RSettings.ProgramSettings.PlaytestDemoPath}\\{playtestCommandInfo.StartDateTime:yyyy}" +
                $"\\{playtestCommandInfo.StartDateTime:MM} - {playtestCommandInfo.StartDateTime:MMMM}\\{dateInsert}{playtestCommandInfo.DemoName}";

            string remoteBspPath = null;

            if (playtestCommandInfo.Game.Equals("tf2", StringComparison.OrdinalIgnoreCase))
            {
                //TF2 is ass and stores workshop maps in a totally different folder
                remoteBspPath = $"/steamapps/workshop/content/440/{playtestCommandInfo.WorkshopId}";
            }
            else if (playtestCommandInfo.Game.Equals("csgo", StringComparison.OrdinalIgnoreCase))
            {
                remoteBspPath = $"{server.FtpPath}/maps/workshop/{playtestCommandInfo.WorkshopId}";
            }
            else
            {
                await _log.LogMessage("Game for playtest is invalid. This should never happen.", true, true, true);
                return null;
            }

            string ftpAddress = server.Address;
            if (ftpAddress.Contains(':'))
                ftpAddress = ftpAddress.Substring(0, ftpAddress.IndexOf(':'));

            switch (server.FtpType.ToLower())
            {
                case "ftps":
                case "ftp":
                    using (var client = new FtpClient(ftpAddress, server.FtpUser, server.FtpPassword))
                    {
                        if (server.FtpType == "ftps")
                        {
                            client.EncryptionMode = FtpEncryptionMode.Explicit;
                            client.SslProtocols = SslProtocols.Tls;
                            client.ValidateCertificate += (control, e) => { e.Accept = true; };
                        }

                        try
                        {
                            client.Connect();
                        }
                        catch (Exception e)
                        {
                            await _log.LogMessage($"Failed to connect to FTP server. {ftpAddress}\n {e.Message}",
                                alert: true, color: LOG_COLOR);
                            return null;
                        }

                        try
                        {
                            //Download Demo
                            client.DownloadFile($"{localPath}\\{playtestCommandInfo.DemoName}.dem",
                                GetFile(client, server.FtpPath, playtestCommandInfo.DemoName));

                            //Download BSP
                            var bspFile = GetFile(client, remoteBspPath, ".bsp");
                            client.DownloadFile($"{localPath}\\{Path.GetFileName(bspFile)}", bspFile);
                        }
                        catch (Exception e)
                        {
                            await _log.LogMessage(
                                $"Failed to download file from playtest server. {ftpAddress}\n{e.Message}",
                                color: LOG_COLOR);
                        }

                        client.Disconnect();

                        await _log.LogMessage(
                            $"```Listing of test download directory:\n{string.Join("\n", Directory.GetFiles(localPath))}```",
                            color: LOG_COLOR);
                    }

                    break;
                case "sftp":
                    using (var client = new SftpClient(ftpAddress, server.FtpUser, server.FtpPassword))
                    {
                        try
                        {
                            client.Connect();
                        }
                        catch (Exception e)
                        {
                            await _log.LogMessage($"Failed to connect to SFTP server. {ftpAddress}\n {e.Message}",
                                alert: true, color: LOG_COLOR);
                            return null;
                        }

                        Directory.CreateDirectory(localPath);

                        try
                        {
                            var remoteDemoFile = GetFile(client, server.FtpPath, playtestCommandInfo.DemoName);
                            using (Stream stream = File.OpenWrite($"{localPath}\\{remoteDemoFile.Name}"))
                            {
                                client.DownloadFile(remoteDemoFile.FullName, stream);
                            }

                            var remoteBspFile = GetFile(client, remoteBspPath, ".bsp");
                            using (Stream stream = File.OpenWrite($"{localPath}\\{remoteBspFile.Name}"))
                            {
                                client.DownloadFile(remoteBspFile.FullName, stream);
                            }
                        }
                        catch (Exception e)
                        {
                            await _log.LogMessage(
                                $"Failed to download file from playtest server. {ftpAddress}\n{e.Message}",
                                color: LOG_COLOR);
                        }

                        client.Disconnect();

                        await _log.LogMessage(
                            $"```Listing of test download directory:\n{string.Join("\n", Directory.GetFiles(localPath))}```",
                            color: LOG_COLOR);
                    }

                    break;
                default:
                    await _log.LogMessage(
                        $"The FTP type on the server is incorrectly set. {ftpAddress} is using {server.FtpType}",
                        alert: true, color: LOG_COLOR);
                    break;
            }

            //Return the path to the demo.
            return Directory.GetFiles(localPath)
                .FirstOrDefault(x => x.EndsWith(".dem", StringComparison.OrdinalIgnoreCase));
        }

        private static string GetFile(FtpClient client, string path, string name)
        {
            // If default, null is returned because string is a reference type.
            return client.GetNameListing(path).FirstOrDefault(f => f.ToLower().Contains(name.ToLower()));
        }

        private static SftpFile GetFile(SftpClient client, string path, string name)
        {
            return client.ListDirectory(path).FirstOrDefault(f => f.Name.ToLower().Contains(name.ToLower()));
        }
    }
}