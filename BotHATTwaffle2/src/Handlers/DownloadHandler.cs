using System;
using System.Collections.Generic;
using System.Dynamic;
using System.IO;
using System.Linq;
using System.Security.Authentication;
using System.Text;
using System.Threading.Tasks;
using BotHATTwaffle2.Models.LiteDB;
using BotHATTwaffle2.Services;
using BotHATTwaffle2.src.Handlers;
using Discord;
using Renci.SshNet;
using Renci.SshNet.Sftp;
using FluentFTP;

namespace BotHATTwaffle2.Handlers
{
    class DownloadHandler
    {
        private const ConsoleColor LOG_COLOR = ConsoleColor.DarkCyan;
        private static LogHandler _log;
        private static DataService _data;

        public static void SetHandlers(LogHandler log, DataService data)
        {
            _data = data;
            _log = log;
        }

        public static async void DownloadPlaytestDemo(PlaytestCommandInfo playtestCommandInfo)
        {
            var server = DatabaseHandler.GetTestServer(playtestCommandInfo.ServerAddress);

            if (server == null)
                return;

            string localPath = $"{_data.RSettings.ProgramSettings.PlaytestDemoPath}\\{playtestCommandInfo.StartDateTime:yyyy}" +
                               $"\\{playtestCommandInfo.StartDateTime:MM} - {playtestCommandInfo.StartDateTime:MMMM}\\{playtestCommandInfo.DemoName}";

            switch (server.FtpType.ToLower())
            {
                case "ftps":
                case "ftp":
                    using (var client = new FtpClient(server.Address, server.FtpUser, server.FtpPassword))
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
                            await _log.LogMessage($"Failed to connect to FTP server. {server.Address}\n {e.Message}", alert: true);
                            return;
                        }

                        try
                        {
                            //Download Demo
                            client.DownloadFile($"{localPath}\\{playtestCommandInfo.DemoName}.dem",
                                GetFile(client, server.FtpPath, playtestCommandInfo.DemoName));

                            //Download BSP
                            string bspFile = GetFile(client,
                                $"{server.FtpPath}/maps/workshop/{playtestCommandInfo.WorkshopId}", ".bsp");
                            client.DownloadFile($"{localPath}\\{Path.GetFileName(bspFile)}",bspFile);
                        }
                        catch (Exception e)
                        {
                            await _log.LogMessage($"Failed to download file from playtest server. {server.Address}\n{e.Message}");
                        }
                        
                        client.Disconnect();

                        await _log.LogMessage($"```Listing of test download directory:\n{string.Join("\n", Directory.GetFiles(localPath))}```");
                    }

                    break;
                case "sftp":
                    using (var client = new SftpClient(server.Address, server.FtpUser, server.FtpPassword))
                    {
                        try
                        {
                            client.Connect();
                        }
                        catch (Exception e)
                        {
                            await _log.LogMessage($"Failed to connect to SFTP server. {server.Address}\n {e.Message}", alert: true);
                            return;
                        }

                        Directory.CreateDirectory(localPath);

                        try
                        {
                            var remoteDemoFile = GetFile(client, server.FtpPath, playtestCommandInfo.DemoName);
                            using (Stream stream = File.OpenWrite($"{localPath}\\{remoteDemoFile.Name}"))
                            {
                                client.DownloadFile(remoteDemoFile.FullName, stream);
                            }

                            var remoteBspFile = GetFile(client, $"{server.FtpPath}/maps/workshop/{playtestCommandInfo.WorkshopId}", ".bsp");
                            using (Stream stream = File.OpenWrite($"{localPath}\\{remoteBspFile.Name}"))
                            {
                                client.DownloadFile(remoteBspFile.FullName, stream);
                            }
                        }
                        catch (Exception e)
                        {
                            await _log.LogMessage($"Failed to download file from playtest server. {server.Address}\n{e.Message}");
                        }

                        client.Disconnect();

                        await _log.LogMessage($"```Listing of test download directory:\n{string.Join("\n", Directory.GetFiles(localPath))}```");
                    }

                    break;
                default:
                    await _log.LogMessage($"The FTP type on the server is incorrectly set. {server.Address} is using {server.FtpType}",alert:true);
                    break;
            }
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
