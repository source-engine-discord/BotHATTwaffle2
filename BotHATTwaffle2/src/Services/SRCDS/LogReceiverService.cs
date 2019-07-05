using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection.Metadata.Ecma335;
using System.Threading;
using System.Threading.Tasks;
using BotHATTwaffle2.Handlers;
using BotHATTwaffle2.Models.LiteDB;
using BotHATTwaffle2.Util;
using CoreRCON;
using CoreRCON.Parsers.Standard;
namespace BotHATTwaffle2.Services.SRCDS
{
    public class LogReceiverService
    {
        private string _publicIpAddress = new WebClient().DownloadString("http://icanhazip.com/");
        private readonly DataService _dataService;
        private readonly RconService _rconService;
        private readonly LogHandler _logHandler;
        public bool EnableLog = false;
        private bool _enableFeedback = false;
        public Server ActiveServer;
        private string path;

        public LogReceiverService(DataService dataService, RconService rconService, LogHandler logHandler)
        {
            _rconService = rconService;
            _dataService = dataService;
            _logHandler = logHandler;
            SetFileName("feedback");
        }

        /// <summary>
        /// Starts listening on a port for server messages. This will also add the location of the Bot to the server's log
        /// address. The listening port needs to be forwarded on UDP
        /// </summary>
        /// <param name="serverString">Server to start listening on</param>
        public async void StartLogReceiver(string serverString)
        {
            //Cannot start another sessions while one is active
            if (EnableLog)
                return;

            ActiveServer = DatabaseUtil.GetTestServer(serverString);

            //Need a valid server
            if (ActiveServer == null)
                return;
            
            EnableLog = true;
            string externalip = new WebClient().DownloadString("http://icanhazip.com").Trim();
            await _rconService.RconCommand(ActiveServer.ServerId, $"logaddress_add {externalip}:{_dataService.RSettings.ProgramSettings.ListenPort}");

            var ip = GeneralUtil.GetIPHost(ActiveServer.Address).AddressList.FirstOrDefault();
            var log = new LogReceiver(_dataService.RSettings.ProgramSettings.ListenPort, new IPEndPoint(ip, 27015));

            await _logHandler.LogMessage($"Starting LogReceiver for `{ActiveServer.Address}` using:\n" +
                                         "`logaddress_add " + externalip + ":" + _dataService.RSettings.ProgramSettings.ListenPort + "`");

            //Start the task and run it forever in a loop. The bool changes at a later time which breaks the loop
            //and removes this client so we can make another one later on.
            await Task.Run(async () =>
            {
                log.Listen<FeedbackMessage>(chat =>
                {
                    if(_enableFeedback)
                        _ = HandleInGameFeedback(ActiveServer, chat);
                });

                log.Listen<RconMessage>(rcon => { _ = InGameRcon(ActiveServer, rcon); });

                while (EnableLog)
                {
                    await Task.Delay(1000);
                }

            });
            await _logHandler.LogMessage($"Disposing LogReceiver from `{ActiveServer.Address}` - `{ip}`");
            log.Dispose();
        }

        //Stops listening for log messages and defaults a file for logging.
        public void StopLogReceiver()
        {
            //Reset path to something default incase sessions is started outside of a test.
            SetFileName("feedback");
            EnableLog = false;
            _enableFeedback = false;
        }

        public bool EnableFeedback()
        {
            if (EnableLog && !_enableFeedback)
            {
                _enableFeedback = true;
                //Seed the feedback log with the current timestamp
                _ = HandleInGameFeedback(ActiveServer, new FeedbackMessage
                {
                    Message = $"Log Started at {DateTime.Now} CT",
                    Player = new Player
                    {
                        Name = "Ido",
                        Team = "Bot"
                    }

                });

                return true;
            }
            else return false;
        }

        public void DisableFeedback()
        {
            _enableFeedback = false;
        }

        /// <summary>
        /// Checks if the user trying to use RCON is in the SteamID whitelist.
        /// </summary>
        /// <param name="server">Server to send RCON to</param>
        /// <param name="rconMessage">Command to send</param>
        /// <returns></returns>
        private async Task InGameRcon(Server server, RconMessage rconMessage)
        {
            //Make sure the user has access
            if (!_dataService.RSettings.Lists.SteamIDs.Any(x => x.Contains(rconMessage.Player.SteamId)))
                return;

            await _rconService.RconCommand(server.Address, rconMessage.Message);
        }

        /// <summary>
        /// Adds ingame feedback to a text file which will be sent to the create at a later date.
        /// </summary>
        /// <param name="server">Server to send acks to</param>
        /// <param name="message">Message to log</param>
        /// <returns></returns>
        private async Task HandleInGameFeedback(Server server, FeedbackMessage message)
        {
            Directory.CreateDirectory("Feedback");

            if (!File.Exists(path))
            {
                // Create a file to write to.
                using (StreamWriter sw = File.CreateText(path))
                {
                    sw.WriteLine($"{message.Player.Name} ({message.Player.Team}): {message.Message}");
                }
            }
            else
            // This text is always added, making the file longer over time
            // if it is not deleted.
            using (StreamWriter sw = File.AppendText(path))
            {
                sw.WriteLine($"{message.Player.Name} ({message.Player.Team}): {message.Message}");
            }

            await _rconService.RconCommand(server.ServerId, $"say Feedback from {message.Player.Name} captured!");
        }

        public void SetFileName(string fileName)
        {
            if (fileName.Contains(".txt"))
                path = "Feedback/" + fileName;

            path = "Feedback/" + fileName + ".txt";
        }

        public string GetFilePath() => path;
    }
}
