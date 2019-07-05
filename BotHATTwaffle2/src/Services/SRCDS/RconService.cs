using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using BotHATTwaffle2.Handlers;
using BotHATTwaffle2.Util;
using CoreRCON;

namespace BotHATTwaffle2.Services.SRCDS
{
    public class RconService
    {
        private readonly DataService _dataService;
        private LogHandler _log;
        private const ConsoleColor LOG_COLOR = ConsoleColor.Cyan;
        private static Dictionary<string, RCON> _rconClients;
        private static bool _running = false;

        public RconService(DataService dataService, LogHandler log)
        {
            _dataService = dataService;
            _log = log;

            _rconClients = new Dictionary<string, RCON>();
        }

        /// <summary>
        /// Gets an already connect RCON client, or makes a new one
        /// </summary>
        /// <param name="serverId">Server ID to get client for</param>
        /// <returns>RCON client</returns>
        private RCON GetOrCreateRconClient(string serverId)
        {
            if (!_rconClients.ContainsKey(serverId))
            {
                //Validate server before adding
                var server = DatabaseUtil.GetTestServer(serverId);

                //Server cannot be null.
                if (server == null)
                    throw new NullReferenceException(nameof(serverId));

                var iPHostEntry = GeneralUtil.GetIPHost(server.Address);
                
                var rconClient = new RCON(iPHostEntry.AddressList.FirstOrDefault(),27015, server.RconPassword);
                _rconClients.Add(serverId, rconClient);

                rconClient.OnDisconnected += () =>
                {
                    _ = _log.LogMessage($"RCON client for `{serverId}` has been disposed.", channel:false, color:LOG_COLOR);
                    _rconClients.Remove(serverId);
                };

                rconClient.OnLog += logMessage => {  _ = _log.LogMessage(logMessage, color:LOG_COLOR); };
            }

            return _rconClients[serverId];
        }

        /// <summary>
        /// Sends an RCON command to a specific server. Will automatically retry, or display error messages if something
        /// goes wrong.
        /// </summary>
        /// <param name="serverId">Server to send command to</param>
        /// <param name="command">Command to send</param>
        /// <returns>Server reply, if any</returns>
        public async Task<string> RconCommand(string serverId, string command)
        {
            //Spool all commands if one is in progress.
            while(_running)
            {
                await _log.LogMessage($"Waiting for another instance of RCON to finish before sending\n" +
                                      $"{command}\nTo: {serverId}", channel: false, color: LOG_COLOR);
                await Task.Delay(250);
            }
            _running = true;

            bool _requireRetry = false;
            string reply = null;
            serverId = GeneralUtil.GetServerCode(serverId);
            var client = GetOrCreateRconClient(serverId);

            //Execute sending the command, then have a 5second timeout. If no reply, we can assume this connection is broken.
            var commandResult =  client.SendCommandAsync(command);
            if (commandResult == await Task.WhenAny(commandResult, Task.Delay(5000)))
            {
                try
                {
                    reply = await commandResult;
                }
                catch
                {
                    await _log.LogMessage($"Failed to communicate with RCON server {serverId} on first try. Will try again.",
                        channel:false, color:LOG_COLOR);
                    client.Dispose();
                    _requireRetry = true;
                }
            }
            else
            {
                client.Dispose();
                _requireRetry = true;
            }

            if (_requireRetry)
            {
                //Backoff before retry.
                await Task.Delay(1000);
                client = GetOrCreateRconClient(serverId);
                commandResult = client.SendCommandAsync(command);
                if (commandResult == await Task.WhenAny(commandResult, Task.Delay(10000)))
                {
                    try
                    {
                        reply = await commandResult;
                    }
                    catch
                    {
                        reply = "Failed to communicate with RCON Server after 2 tries. This is likely due to a network issue.";
                        await _log.LogMessage($"Failed to communicate with RCON server {serverId} after 2 tries."
                            , color: LOG_COLOR);
                    }
                }
            }
            //Release the next instance
            _running = false;
            
            reply = FormatRconServerReply(reply);

            //Ignore logging status replies... This is a one off that just causes too much spam.
            if (!command.Contains("status", StringComparison.OrdinalIgnoreCase))
                await _log.LogMessage($"**Sending:** `{command}`\n**To:** `{serverId}`\n**Response Was:** `{reply}`", color: LOG_COLOR);


            if (string.IsNullOrWhiteSpace(reply))
                return $"{command} was sent, but provided no reply.";
            
            return reply;
        }

        /// <summary>
        /// Gets status from server, then sets a parameter containing the player count of the server.
        /// </summary>
        /// <param name="serverId">SeverId to get status from</param>
        /// <returns></returns>
        public async Task GetPlayCountFromServer(string serverId)
        {
            var returned = await RconCommand(serverId, "status");
            var replyArray = returned.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
            
            //Only get the line with player count
            var results = replyArray.Where(l => l.StartsWith("players"));
            
            //Remove extra information from string
            var formatted = results.FirstOrDefault()?.Substring(10);
            
            _dataService.PlayerCount = formatted?.Substring(0, formatted.IndexOf(" ", StringComparison.Ordinal));
            Console.WriteLine(_dataService.PlayerCount);
        }

        /// <summary>
        /// Formats the reply for displaying to users. Removes log messages, and updates message if empty.
        /// </summary>
        /// <param name="input"></param>
        /// <returns></returns>
        private string FormatRconServerReply(string input)
        {
            var replyArray = input.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
            return string.Join("\n", replyArray.Where(x => !x.Trim().StartsWith("L ")));
        }
    }
}
