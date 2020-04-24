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
    public class LogReceiverServiceOld
    {
        private static bool _isActive;
        private static string _lastKnownPath;
        private readonly DataService _dataService;
        private readonly LogHandler _logHandler;
        private readonly string _publicIpAddress;
        private readonly RconService _rconService;
        private bool _enableFeedback;
        private string _path;
        private PlaytestService _playtestService;
        public Server ActiveServer;
        public bool EnableLog;

        public LogReceiverServiceOld(DataService dataService, RconService rconService, LogHandler logHandler)
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
        ///     Starts listening on a port for server messages. This will also add the location of the Bot to the server's log
        ///     address. The listening port needs to be forwarded on UDP
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
            await _rconService.RconCommand(ActiveServer.ServerId,
                $"logaddress_add {_publicIpAddress}:{_dataService.RSettings.ProgramSettings.ListenPort}");

            var ip = GeneralUtil.GetIPHost(ActiveServer.Address).AddressList.FirstOrDefault();

            //Start listening on listen port, accept messages from 'ip' on 27015.
            var log = new LogReceiver(_dataService.RSettings.ProgramSettings.ListenPort, new IPEndPoint(ip, 27015));

            await _logHandler.LogMessage($"Starting LogReceiver for `{ActiveServer.Address}` - `{ip}` using:\n" +
                                         "`logaddress_add " + _publicIpAddress + ":" +
                                         _dataService.RSettings.ProgramSettings.ListenPort + "`");

            //Start the task and run it forever in a loop. The bool changes at a later time which breaks the loop
            //and removes this client so we can make another one later on.
            await Task.Run(async () =>
            {
                log.Listen<GenericCommand>(genericCommand => { HandleIngameCommand(ActiveServer, genericCommand); });

                if (_dataService.RSettings.ProgramSettings.Debug)
                    log.ListenRaw(msg => { Console.WriteLine("RAW: " + msg); });

                while (EnableLog) await Task.Delay(1000);
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
        ///     Restarts the log listener if for some reason a discord disconnect happens.
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

                _ = _logHandler.LogMessage($"Starting in game feedback in file: {feedbackLogName}");

                return true;
            }

            return false;
        }

        public void DisableFeedback()
        {
            _enableFeedback = false;
        }

        /// <summary>
        ///     Handles ingame commands mapping to discord commands. Not all commands will work.
        ///     Since some commands require a context, we have some manual leg work to "build" the context when needed.
        /// </summary>
        /// <param name="server">Game server we are using</param>
        /// <param name="genericCommand">Generic command containing what command to fire, with what options.</param>
        private async void HandleIngameCommand(Server server, GenericCommand genericCommand)
        {
            genericCommand.Player.SteamId = GeneralUtil.TranslateSteamId3ToSteamId(genericCommand.Player.SteamId);

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

                case "done":
                    HandleInGameDone(server, genericCommand);
                    break;

                case "q":
                    HandleInGameQueue(server, genericCommand);
                    break;

                case "pc":
                    HandleInGamePublicCommand(server, genericCommand);
                    break;

                default:
                    await _rconService.RconCommand(server.Address,
                        $"say Unknown Command from {genericCommand.Player.Name}");
                    break;
            }
        }

        /// <summary>
        ///     Allows users to use public command in-game.
        /// </summary>
        /// <param name="server"></param>
        /// <param name="genericCommand"></param>
        public async void HandleInGamePublicCommand(Server server, GenericCommand genericCommand)
        {
            var user = _dataService.GetSocketGuildUserFromSteamId(genericCommand.Player.SteamId);
            if (user == null)
            {
                await _rconService.RconCommand(server.Address,
                    $"say No Discord user link found for {genericCommand.Player.Name}. See >help link in Discord");
                return;
            }

            var testServer = DatabaseUtil.GetTestServerFromReservationUserId(user.Id);

            //No reservation found, or multiple commands in 1 string
            if (testServer == null || genericCommand.Message.Contains(';'))
                return;

            //Invalid commands
            if (!_dataService.RSettings.Lists.PublicCommands.Any(x =>
                genericCommand.Message.Contains(x, StringComparison.OrdinalIgnoreCase)))
            {
                await _rconService.RconCommand(server.Address, $"say {genericCommand.Message} is not allowed");
                return;
            }

            //Send command
            await _rconService.RconCommand(server.Address, genericCommand.Message);
        }

        /// <summary>
        ///     Allows users to join the playtest Queue in-game.
        /// </summary>
        /// <param name="server"></param>
        /// <param name="genericCommand"></param>
        private async void HandleInGameQueue(Server server, GenericCommand genericCommand)
        {
            if (_playtestService.FeedbackSession == null)
            {
                await _rconService.RconCommand(server.Address, "say No feedback session currently exists!");
                return;
            }

            var user = _dataService.GetSocketGuildUserFromSteamId(genericCommand.Player.SteamId);

            if (user == null)
            {
                await _rconService.RconCommand(server.Address,
                    $"say No Discord user link found for {genericCommand.Player.Name}. See >help link in Discord");
                return;
            }

            if (!await _playtestService.FeedbackSession.AddUserToQueue(user))
                await _rconService.RconCommand(server.Address, $"say Failed to add {user} to queue");
            else
                await _rconService.RconCommand(server.Address, $"say Added {user} to queue");
        }

        /// <summary>
        ///     Allows users to remove themselves from the playtest queue in-game
        /// </summary>
        /// <param name="server"></param>
        /// <param name="genericCommand"></param>
        private async void HandleInGameDone(Server server, GenericCommand genericCommand)
        {
            if (_playtestService.FeedbackSession == null)
            {
                await _rconService.RconCommand(server.Address, "say No feedback session currently exists!");
                return;
            }

            var user = _dataService.GetSocketGuildUserFromSteamId(genericCommand.Player.SteamId);

            if (user == null)
            {
                await _rconService.RconCommand(server.Address,
                    $"say No Discord user link found for {genericCommand.Player.Name}");
                return;
            }

            if (!await _playtestService.FeedbackSession.RemoveUser(user.Id))
                await _rconService.RconCommand(server.Address, $"say Failed to remove {user} from queue");
        }

        /// <summary>
        ///     Checks if the user trying to use RCON is in the SteamID whitelist.
        /// </summary>
        /// <param name="server">Server to send RCON to</param>
        /// <param name="genericCommand">Command to send</param>
        /// <returns></returns>
        private async void HandleInGameRcon(Server server, GenericCommand genericCommand)
        {
            var user = _dataService.GetSocketGuildUserFromSteamId(genericCommand.Player.SteamId);

            if (user == null)
            {
                await _rconService.RconCommand(server.Address,
                    $"say No Discord user link found for {genericCommand.Player.Name}. See >help link in Discord");
                return;
            }

            //Make sure the user has access
            if (!user.Roles.Any(x => x.Id == _dataService.ModeratorRole.Id || x.Id == _dataService.AdminRole.Id))
            {
                await _rconService.RconCommand(server.Address,
                    $"say {genericCommand.Player.Name} does not have permissions for rcon");
                return;
            }

            await _rconService.RconCommand(server.Address, genericCommand.Message);
        }

        /// <summary>
        ///     Adds ingame feedback to a text file which will be sent to the create at a later date.
        /// </summary>
        /// <param name="server">Server to send acks to</param>
        /// <param name="genericCommand">Message to log</param>
        /// <returns></returns>
        private async void HandleInGameFeedback(Server server, GenericCommand genericCommand)
        {
            if (!_enableFeedback)
                return;

            Directory.CreateDirectory("Feedback");

            if (!File.Exists(_path))
                // Create a file to write to.
                using (var sw = File.CreateText(_path))
                {
                    sw.WriteLine(
                        $"{DateTime.Now} - {genericCommand.Player.Name} ({genericCommand.Player.Team}): {genericCommand.Message}");
                }
            else
                // This text is always added, making the file longer over time
                // if it is not deleted.
                using (var sw = File.AppendText(_path))
                {
                    sw.WriteLine(
                        $"{DateTime.Now:t} - {genericCommand.Player.Name} ({genericCommand.Player.Team}): {genericCommand.Message}");
                }

            await _rconService.RconCommand(server.ServerId, $"say Feedback from {genericCommand.Player.Name} captured!",
                false);
        }

        private void SetFileName(string fileName)
        {
            if (fileName.Contains(".txt"))
                _path = "Feedback\\" + fileName;

            _path = "Feedback\\" + fileName + ".txt";
        }

        public string GetFilePath()
        {
            return _path;
        }

        private async void HandlePlaytestCommand(Server server, GenericCommand genericCommand)
        {
            var user = _dataService.GetSocketGuildUserFromSteamId(genericCommand.Player.SteamId);

            if (user == null)
            {
                await _rconService.RconCommand(server.Address,
                    $"say No Discord user link found for {genericCommand.Player.Name}. See >help link in Discord");
                return;
            }

            //Make sure the user has access
            if (!user.Roles.Any(x => x.Id == _dataService.ModeratorRole.Id || x.Id == _dataService.AdminRole.Id))
            {
                await _rconService.RconCommand(server.Address,
                    $"say {genericCommand.Player.Name} does not have permissions for playtest command");
                return;
            }

            //Not valid - abort
            if (!_playtestService.PlaytestCommandPreCheck())
            {
                await _rconService.RconCommand(server.Address,
                    "say A playtest command is running, or no valid test exists.");
                return;
            }

            switch (genericCommand.Message.Trim().ToLower())
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