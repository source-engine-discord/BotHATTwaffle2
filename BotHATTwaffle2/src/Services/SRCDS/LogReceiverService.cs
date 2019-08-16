using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using BotHATTwaffle2.Handlers;
using BotHATTwaffle2.Models.LiteDB;
using BotHATTwaffle2.Services.Playtesting;
using BotHATTwaffle2.Util;
using CoreRCON;
using CoreRCON.Parsers.Standard;
namespace BotHATTwaffle2.Services.SRCDS
{
    public class LogReceiverService
    {
        private string _publicIpAddress;
        private readonly DataService _dataService;
        private readonly RconService _rconService;
        private readonly LogHandler _logHandler;
        private PlaytestService _playtestService;
        public bool EnableLog = false;
        private bool _enableFeedback = false;
        public Server ActiveServer;
        private string _path;
        private static bool _isActive = false;
        private static string _lastKnownPath = null;
        
        public LogReceiverService(DataService dataService, RconService rconService, LogHandler logHandler)
        {
            _rconService = rconService;
            _dataService = dataService;
            _logHandler = logHandler;
            _publicIpAddress = new WebClient().DownloadString("http://icanhazip.com/").Trim();
        }

        //Can't DI this variable, this is a workaround.
        public void SetPlayTestService(PlaytestService playtestService)
        {
            _playtestService = playtestService;
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
            await _rconService.RconCommand(ActiveServer.ServerId, $"logaddress_add {_publicIpAddress}:{_dataService.RSettings.ProgramSettings.ListenPort}");

            var ip = GeneralUtil.GetIPHost(ActiveServer.Address).AddressList.FirstOrDefault();

            //Start listening on listen port, accept messages from 'ip' on 27015.
            var log = new LogReceiver(_dataService.RSettings.ProgramSettings.ListenPort, new IPEndPoint(ip, 27015));

            await _logHandler.LogMessage($"Starting LogReceiver for `{ActiveServer.Address}` - `{ip}` using:\n" +
                                         "`logaddress_add " + _publicIpAddress + ":" + _dataService.RSettings.ProgramSettings.ListenPort + "`");

            //Start the task and run it forever in a loop. The bool changes at a later time which breaks the loop
            //and removes this client so we can make another one later on.
            await Task.Run(async () =>
            {
                log.Listen<GenericCommand>(ganericCommand => { HandleIngameCommand(ActiveServer, ganericCommand);});
                
                if (_dataService.RSettings.ProgramSettings.Debug)
                    log.ListenRaw(msg => { Console.WriteLine("RAW: "+msg); });

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
            EnableLog = false;
            _enableFeedback = false;
        }

        /// <summary>
        /// Restarts the log listener if for some reason a discord disconnect happens.
        /// </summary>
        public async Task RestartLogAfterDisconnect()
        {
            //Don't do anything unless we are active.
            if (!_isActive)
                return;

            //If we are still running, stop.
            StopLogReceiver();
            
            //All time for the existing log receiver to be completely closed, if running.
            await Task.Delay(5000);

            StartLogReceiver(ActiveServer.ServerId);
            EnableFeedback(_lastKnownPath);
        }

        public void SetNotActive()
        {
            _isActive = false;
        }

        public bool EnableFeedback(string feedbackLogName)
        {
            if (EnableLog && !_enableFeedback)
            {
                _isActive = true;
                _lastKnownPath = feedbackLogName;
                SetFileName(feedbackLogName);
                _enableFeedback = true;
                //Seed the feedback log with the current timestamp
                HandleInGameFeedback(ActiveServer, new GenericCommand
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
            return false;
        }

        public void DisableFeedback()
        {
            _enableFeedback = false;
        }

        private async void HandleIngameCommand(Server server, GenericCommand genericCommand)
        {
            switch (genericCommand.Command.Trim().ToLower())
            {
                case "fb":
                case "feedback":
                    HandleInGameFeedback(server, genericCommand);
                    break;

                case "p":
                case "playtest":
                    HandlePlaytestCommand(server, genericCommand);
                    break;

                case "r":
                case "rcon":
                    HandleInGameRcon(server, genericCommand);
                    break;

                default:
                    await _rconService.RconCommand(server.Address, $"say Unknown Command from {genericCommand.Player.Name}");
                    break;
            }
        }

        /// <summary>
        /// Checks if the user trying to use RCON is in the SteamID whitelist.
        /// </summary>
        /// <param name="server">Server to send RCON to</param>
        /// <param name="rconMessage">Command to send</param>
        /// <returns></returns>
        private async void HandleInGameRcon(Server server, GenericCommand rconMessage)
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
        private async void HandleInGameFeedback(Server server, GenericCommand message)
        {
            Directory.CreateDirectory("Feedback");

            if (!File.Exists(_path))
            {
                // Create a file to write to.
                using (StreamWriter sw = File.CreateText(_path))
                {
                    sw.WriteLine($"{DateTime.Now} - {message.Player.Name} ({message.Player.Team}): {message.Message}");
                }
            }
            else
            // This text is always added, making the file longer over time
            // if it is not deleted.
            using (StreamWriter sw = File.AppendText(_path))
            {
                sw.WriteLine($"{message.Player.Name} ({message.Player.Team}): {message.Message}");
            }

            await _rconService.RconCommand(server.ServerId, $"say Feedback from {message.Player.Name} captured!",
                false);
        }

        private void SetFileName(string fileName)
        {
            if (fileName.Contains(".txt"))
                _path = "Feedback\\" + fileName;

            _path = "Feedback\\" + fileName + ".txt";
        }

        public string GetFilePath() => _path;

        private async void HandlePlaytestCommand(Server server, GenericCommand message)
        {
            //Make sure the user has access
            if (!_dataService.RSettings.Lists.SteamIDs.Any(x => x.Contains(message.Player.SteamId)))
                return;

            //Not valid - abort
            if (!_playtestService.PlaytestCommandPreCheck())
            {
                await _rconService.RconCommand(server.Address, "say A playtest command is running, or no valid test exists.");
                return;
            }

            switch (message.Message.Trim().ToLower())
            {
                case "prestart":
                case "pre":
                    await _playtestService.PlaytestCommandPre(false);
                    break;
                case "start":
                    await _playtestService.PlaytestCommandStart(false);
                    break;
                case "post":
                    await _playtestService.PlaytestCommandPost(false);
                    break;
                case "pause":
                case "p":
                    await _playtestService.PlaytestcommandGenericAction(false,
                        "mp_pause_match;say Pausing Match!;say Pausing Match!;say Pausing Match!;say Pausing Match!",
                        $"Pausing Playtest On {server.Address}");
                    break;
                case "unpause":
                case "u":
                    await _playtestService.PlaytestcommandGenericAction(false,
                        "mp_unpause_match;say Unpausing Match!;say Unpausing Match!;say Unpausing Match!;say Unpausing Match",
                        $"Unpausing Playtest On {server.Address}");
                    break;
                case "scramble":
                case "s":
                    await _playtestService.PlaytestcommandGenericAction(false,
                        "mp_scrambleteams 1;say Scrambling Teams!;say Scrambling Teams!;say Scrambling Teams!;say Scrambling Teams!",
                        $"Scrambling teams On {server.Address}");
                    break;
                default:
                    await _rconService.RconCommand(server.Address, "Say Invalid action! Not all commands available " +
                                                                   "from ingame chat.");
                    //No command running - reset flag.
                    _playtestService.ResetCommandRunningFlag();
                    break;
            }
        }
    }
}
