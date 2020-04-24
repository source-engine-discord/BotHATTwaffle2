using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using BotHATTwaffle2.Handlers;
using BotHATTwaffle2.Models.LiteDB;
using BotHATTwaffle2.Services.Playtesting;
using BotHATTwaffle2.Util;
using CoreRCON;
using CoreRCON.Parsers.Standard;
using Discord.WebSocket;

namespace BotHATTwaffle2.Services.SRCDS
{
    class SrcdsLogService
    {
        private readonly DataService _dataService;
        private readonly LogHandler _logHandler;
        private readonly RconService _rconService;
        private readonly string _publicIpAddress;
        private readonly PlaytestService _playtestService;
        private LogReceiver _logReceiver;
        private Dictionary<IPEndPoint, Server> _serverIdDictionary = new Dictionary<IPEndPoint, Server>();

        public SrcdsLogService(DataService dataService, RconService rconService, LogHandler logHandler, PlaytestService playtestService)
        {
            //Setup vars
            _rconService = rconService;
            _dataService = dataService;
            _logHandler = logHandler;
            _publicIpAddress = new WebClient().DownloadString("http://icanhazip.com/").Trim();
            _playtestService = playtestService;
            
            //Need to map servers
            var servers = DatabaseUtil.GetAllTestServers();

            foreach (var server in servers)
                _serverIdDictionary.Add(GeneralUtil.GetIpEndPointFromString(server.Address), server);


            //Let's start the listener on our listen port. Allow all IPs from the server list.
            _ = _logHandler.LogMessage($"Starting LogService on {_dataService.RSettings.ProgramSettings.ListenPort}");
            _logReceiver = new LogReceiver(_dataService.RSettings.ProgramSettings.ListenPort, CreateIpEndPoints());

            Start();
        }

        private IPEndPoint[] CreateIpEndPoints()
        {
            var ipEndPoints = new List<IPEndPoint>();
            foreach (var server in _serverIdDictionary)
            {
                var ipep = server.Key;
                _ = _logHandler.LogMessage($"Adding {ipep.Address}:{ipep.Port} to listener sources!");
                ipEndPoints.Add(ipep);
            }
            return ipEndPoints.ToArray();
        }

        private async void Start()
        {
            await Task.Run(async () =>
            {
                if (_dataService.RSettings.ProgramSettings.Debug)
                    _logReceiver.ListenRaw(msg => { Console.WriteLine($"RAW LOG FROM {_logReceiver.lastServer.Address}: " + msg); });

                _logReceiver.Listen<GenericCommand>(genericCommand => { HandleIngameCommand(_logReceiver.lastServer, genericCommand); });
            });
        }

        /// <summary>
        ///     Handles ingame commands mapping to discord commands. Not all commands will work.
        ///     Since some commands require a context, we have some manual leg work to "build" the context when needed.
        /// </summary>
        /// <param name="server">Game server we are using</param>
        /// <param name="genericCommand">Generic command containing what command to fire, with what options.</param>
        private async void HandleIngameCommand(IPEndPoint ipServer, GenericCommand genericCommand)
        {
            Server server = null;
            if (_serverIdDictionary.ContainsKey(ipServer))
                server = _serverIdDictionary[ipServer];

            genericCommand.Player.SteamId = GeneralUtil.TranslateSteamId3ToSteamId(genericCommand.Player.SteamId);
            var user = _dataService.GetSocketGuildUserFromSteamId(genericCommand.Player.SteamId);

            if (user == null)
            {
                await _rconService.RconCommand(server.Address,
                    $"say No Discord user link found for {genericCommand.Player.Name}. See >help link in Discord");
                return;
            }

            switch (genericCommand.Command.Trim().ToLower())
            {
                case "fb":
                case "feedback":
                    HandleInGameFeedback(server, genericCommand);
                    break;

                case "p":
                case "playtest":
                    HandlePlaytestCommand(server, genericCommand, user);
                    break;

                case "r":
                case "rcon":
                    HandleInGameRcon(server, genericCommand, user);
                    break;

                case "done":
                    HandleInGameDone(server, genericCommand, user);
                    break;

                case "q":
                    HandleInGameQueue(server, genericCommand, user);
                    break;

                case "pc":
                    HandleInGamePublicCommand(server, genericCommand, user);
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
        public async void HandleInGamePublicCommand(Server server, GenericCommand genericCommand, SocketGuildUser user)
        {
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
        private async void HandleInGameQueue(Server server, GenericCommand genericCommand, SocketGuildUser user)
        {
            if (_playtestService.FeedbackSession == null)
            {
                await _rconService.RconCommand(server.Address, "say No feedback session currently exists!");
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
        private async void HandleInGameDone(Server server, GenericCommand genericCommand, SocketGuildUser user)
        {
            if (_playtestService.FeedbackSession == null)
            {
                await _rconService.RconCommand(server.Address, "say No feedback session currently exists!");
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
        private async void HandleInGameRcon(Server server, GenericCommand genericCommand, SocketGuildUser user)
        {
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
        /// Handles a playtest command when invoked from in game
        /// </summary>
        /// <param name="server"></param>
        /// <param name="genericCommand"></param>
        /// <param name="user"></param>
        private async void HandlePlaytestCommand(Server server, GenericCommand genericCommand, SocketGuildUser user)
        {
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
    }
}
