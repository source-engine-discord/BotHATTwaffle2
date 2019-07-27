﻿using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
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
        private const ConsoleColor LOG_COLOR = ConsoleColor.Magenta;
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
        private async Task<RCON> GetOrCreateRconClient(string serverId)
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

                //Test sending a command. Should the command fail, return null.
                try
                {
                    await rconClient.SendCommandAsync("//Waking Server");
                }
                catch
                {
                    return null;
                }

                _rconClients.Add(serverId, rconClient);

                rconClient.OnDisconnected += () =>
                {
                    _ = _log.LogMessage($"RCON client for `{serverId}` has been disposed.", channel:false, color:LOG_COLOR);

                    if (_rconClients.ContainsKey(serverId))
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
        public async Task<string> RconCommand(string serverId, string command, bool log = true)
        {
            //Spool all commands if one is in progress.
            while(_running)
            {
                await _log.LogMessage($"Waiting for another instance of RCON to finish before sending\n" +
                                      $"{command}\nTo: {serverId}", channel: false, color: LOG_COLOR);
                await Task.Delay(500);
            }
            _running = true;

            string reply = null;
            serverId = GeneralUtil.GetServerCode(serverId);
            RCON client = null;
            bool loop = true;

            //start the task, so we can wait on it later with a timeout timer.
            var t = Task.Run(async () =>
            {
                while (loop)
                {
                    client = await GetOrCreateRconClient(serverId);

                    //If the client is null, meaning a connection issue. Continue the loop to try again.
                    if (client == null)
                    {
                        //Delay before creating new client.
                        await Task.Delay(500);
                        continue;
                    }
                    
                    try
                    {
                        var commandResult = client.SendCommandAsync(command);

                        if (await Task.WhenAny(commandResult, Task.Delay(2000)) != commandResult)
                        {
                            //Dispose if timeout
                            client.Dispose();
                        }
                        reply = await commandResult;
                        //Success sending, break loop
                        break;
                    }
                    catch
                    {
                        await _log.LogMessage(
                            $"Failed to communicate with RCON server {serverId}. Will retry...", false, color: LOG_COLOR);
                        client.Dispose();
                    }
                }
            });
            
            //Ultimate timeout
            if (await Task.WhenAny(t, Task.Delay(10 * 1000)) != t)
            {
                loop = false;
                _running = false;

                await _log.LogMessage(
                    $"Failed to communicate with RCON server {serverId} within the timeout period.\nThe following command **was not** sent.\n" +
                    $"`{serverId}`\n`{command}`", color: LOG_COLOR);

                return $"Failed to communicate with RCON server {serverId} within the timeout period. The server may not be running";
            }

            //Release the next instance
            _running = false;
            
            reply = FormatRconServerReply(reply);

            if (string.IsNullOrWhiteSpace(reply))
                reply = $"{command} was sent, but provided no reply.";

            if (log)
               await _log.LogMessage($"**Sending:** `{command}`\n**To:** `{serverId}`\n**Response Was:** `{reply}`", color: LOG_COLOR);

            return reply;
        }

        /// <summary>
        /// Gets status from server, then sets a parameter containing the player count of the server.
        /// </summary>
        /// <param name="serverId">SeverId to get status from</param>
        /// <returns></returns>
        public async Task GetPlayCountFromServer(string serverId)
        {
            var returned = await RconCommand(serverId, "status", false);
            var replyArray = returned.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
            
            //Only get the line with player count
            var results = replyArray.Where(l => l.StartsWith("players"));
            
            //Remove extra information from string
            var formatted = results.FirstOrDefault()?.Substring(10);
            
            _dataService.PlayerCount = formatted?.Substring(0, formatted.IndexOf(" ", StringComparison.Ordinal));
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

        /// <summary>
        /// If the RCON server hasn't been used in a while, just yeet some shit its way to make it wake tf up.
        /// </summary>
        /// <param name="serverId">Server ID to wake up</param>
        /// <returns></returns>
        public async Task WakeRconServer(string serverId)
        {
            //Using GUIDs for basically random text.
            await RconCommand(serverId, "//WakeServer_" + Guid.NewGuid().ToString().Substring(0, 6), false);
            await RconCommand(serverId, "//WakeServer_" + Guid.NewGuid().ToString().Substring(0, 6), false);
            await RconCommand(serverId, "//WakeServer_" + Guid.NewGuid().ToString().Substring(0, 6), false);
        }

        /// <summary>
        /// Gets the current running level, and workshop ID from a test server.
        /// If array.length == 3 it is a workshop map, with the ID in [1] and map name in [2]
        /// Otherwise it is a stock level with the name in [0]
        /// </summary>
        /// <param name="server">Server to query</param>
        /// <returns>An array populated with the result, or null if failed</returns>
        public async Task<string[]> GetRunningLevelAsync(string server)
        {
            var reply = await RconCommand(server, "host_map");

            try
            {
                reply = reply.Substring(14, reply.IndexOf(".bsp", StringComparison.Ordinal) - 14);
            }
            catch
            {
                //Only end up here if there is a server issue.
                return null;
            }

            return reply.Split('/');
        }
    }
}
