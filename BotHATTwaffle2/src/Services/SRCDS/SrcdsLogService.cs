using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Text;
using System.Threading.Tasks;
using BotHATTwaffle2.Handlers;
using BotHATTwaffle2.Models.LiteDB;
using BotHATTwaffle2.Services.Playtesting;
using BotHATTwaffle2.Util;
using CoreRCON;
using CoreRCON.Parsers.Standard;
using Discord.WebSocket;
using SixLabors.Shapes;

namespace BotHATTwaffle2.Services.SRCDS
{
    public class SrcdsLogService
    {
        private const ConsoleColor LOG_COLOR = ConsoleColor.Green;
        private readonly DataService _dataService;
        private readonly LogHandler _logHandler;
        private readonly RconService _rconService;
        private readonly PlaytestService _playtestService;
        private LogReceiver _logReceiver;
        private Dictionary<IPEndPoint, Server> _serverIdDictionary = new Dictionary<IPEndPoint, Server>();
        private List<FeedbackFile> _feedbackFiles = new List<FeedbackFile>();
        private readonly ushort _port;
        private int _restartCount = 0;
        private const int RESTART_LIMIT = 5;

        public SrcdsLogService(DataService dataService, RconService rconService, LogHandler logHandler, PlaytestService playtestService)
        {
            //Setup vars
            _rconService = rconService;
            _dataService = dataService;
            _logHandler = logHandler;
            _playtestService = playtestService;
            _port = _dataService.RSettings.ProgramSettings.ListenPort;

            Console.WriteLine("Setting up SRCDS Log Service...");

            //Need to map servers
            var servers = DatabaseUtil.GetAllTestServers();

            foreach (var server in servers)
                _serverIdDictionary.Add(GeneralUtil.GetIpEndPointFromString(server.Address), server);

            Start();
        }

        private async void Start()
        {
            //Let's start the listener on our listen port. Allow all IPs from the server list.
            _ = _logHandler.LogMessage($"Starting LogService on {_port}");
            _logReceiver = new LogReceiver(_port, CreateIpEndPoints());

            await Task.Run(() =>
            {
                if (_dataService.RSettings.ProgramSettings.Debug)
                    _logReceiver.ListenRaw(msg => { Console.WriteLine($"RAW LOG FROM {_logReceiver.lastServer.Address}: " + msg); });

                _logReceiver.Listen<GenericCommand>(genericCommand => { HandleIngameCommand(_logReceiver.lastServer, genericCommand); });
            });

            //We are going to use this loop to make sure log services are still running. Should they fail, we will tear
            //down and rebuild.
            while (true)
            {
                bool portBound = IPGlobalProperties.GetIPGlobalProperties()
                    .GetActiveUdpListeners().Any(p => p.Port == _port);

                if (!portBound)
                {
                    if(_restartCount < RESTART_LIMIT)
                    {
                        try
                        {
                            _logReceiver.Dispose();
                        }
                        catch (Exception e)
                        {
                            await _logHandler.LogMessage($"Attempted dispose, but had an issue.\n{e}", alert: true, color: LOG_COLOR);
                        }

                        _restartCount++;

                        //Restart the service
                        Start();
                        await _logHandler.LogMessage($"Log service has been restarted {_restartCount} times!", color: LOG_COLOR);

                        //Reset the retry count in 15 minutes after the first issue
                        //I expect the log service to die sometimes for whatever reason.
                        if(_restartCount == 0)
                            _ = Task.Run(async () =>
                              {
                                  await Task.Delay(TimeSpan.FromMinutes(15));
                                  _restartCount = 0;
                              });
                    }
                    else
                    {
                        await _logHandler.LogMessage($"The SrcdsLogService has restarted over {RESTART_LIMIT} in the last 15 minutes. " +
                                                     $"I will not restart the service again.",alert:true, color:LOG_COLOR);
                    }
                    return;
                }

                //Hol up for a bit before rechecking
                await Task.Delay(5000);
            }
        }

        /// <summary>
        /// Creates IPEndPoint's based on all of the servers we have. We use this to start log services with allowed IPs.
        /// </summary>
        /// <returns></returns>
        private IPEndPoint[] CreateIpEndPoints()
        {
            var ipEndPoints = new List<IPEndPoint>();
            foreach (var server in _serverIdDictionary)
            {
                var ipep = server.Key;
                Console.WriteLine($"Adding {ipep.Address}:{ipep.Port} to listener sources!");
                ipEndPoints.Add(ipep);
            }
            return ipEndPoints.ToArray();
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

            //Handle commands that don't need a steam user mapping.
            switch (genericCommand.Command.Trim().ToLower())
            {
                case "fb":
                case "feedback":
                    HandleInGameFeedback(server, genericCommand);
                    return;
            }

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
        /// <param name="user"></param>
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
        /// <param name="user"></param>
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
        /// <param name="user"></param>
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
        /// <param name="user"></param>
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
        /// Creates a feedback instance for a server.
        /// </summary>
        /// <param name="server">Server to create the file for</param>
        /// <param name="fileName">File name to create and store feedback in</param>
        /// <returns>True if created, false if it cannot because it found an existing one.</returns>
        public bool CreateFeedbackFile(Server server, string fileName)
        {
            //Make sure we don't have a server already
            if (GetFeedbackFile(server) == null)
                return false;

            var fbf = new FeedbackFile(server, fileName, _rconService);
            _ = fbf.LogFeedback(new GenericCommand
            {
                Message = $"Log Started at {DateTime.Now} CT",
                Player = new Player
                {
                    Name = "Ido",
                    Team = "Bot"
                }
            });

            _feedbackFiles.Add(fbf);
            return true;
        }

        /// <summary>
        /// Attempts removal of a FeedbackFile for a server. Make sure to get the path to the file before removing
        /// </summary>
        /// <param name="server">Server to attempt removal for</param>
        /// <returns>True if removal complete, false if it can't find one.</returns>
        public bool RemoveFeedbackFile(Server server)
        {
            var targetFile = GetFeedbackFile(server);

            _feedbackFiles.Remove(targetFile);
            return true;
        }

        /// <summary>
        /// Returns a Feedback file, if it exists for a server
        /// </summary>
        /// <param name="server">Server to get the feedback file for</param>
        /// <returns>FeedbackFile for the server, or null if none found.</returns>
        public FeedbackFile GetFeedbackFile(Server server)
        {
            //We need a feedback file instance to work with.
            FeedbackFile feedback = null;
            if (_feedbackFiles.Any(x => x.Server.Equals(server)))
            {
                feedback = _feedbackFiles.FirstOrDefault(x => x.Server.Equals(server));
            }

            return feedback;
        }

        /// <summary>
        /// Adds ingame feedback to a text file which will be sent to the create at a later date.
        /// </summary>
        /// <param name="server">Server to send acks to</param>
        /// <param name="genericCommand">Message to log</param>
        /// <returns></returns>
        private async void HandleInGameFeedback(Server server, GenericCommand genericCommand)
        {
            var feedbackFile = GetFeedbackFile(server);

            if (feedbackFile == null)
            {
                //Handle somehow not getting a server, likely due to not having a playtest
                await _rconService.RconCommand(server.Address, $"say There is no feedback session started on {server.Address}");
                return;
            }

            await feedbackFile.LogFeedback(genericCommand);
        }
    }
}
