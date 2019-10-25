using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BotHATTwaffle2.Handlers;
using BotHATTwaffle2.Util;
using CoreRCON;

namespace BotHATTwaffle2.Services.SRCDS
{
    public class RconService
    {
        private const ConsoleColor LOG_COLOR = ConsoleColor.Magenta;
        private static Dictionary<string, RCON> _rconClients;
        private static bool _running;
        private readonly DataService _dataService;
        private readonly LogHandler _log;

        public RconService(DataService dataService, LogHandler log)
        {
            _dataService = dataService;
            _log = log;

            _rconClients = new Dictionary<string, RCON>();
        }

        /// <summary>
        ///     Gets an already connect RCON client, or makes a new one
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

                ushort serverPort = 27015;
                var targetDns = server.Address;

                //Does the address contain a port? If so we need to split it and our queries
                if (server.Address.Contains(':'))
                {
                    var splitServer = server.Address.Split(':');
                    targetDns = splitServer[0];

                    if (!ushort.TryParse(splitServer[1], out serverPort))
                        throw new NullReferenceException(
                            "Malformed server port in address. Verify that it is stored as: subDomain.Domain.TLD:port");
                }

                var iPHostEntry = GeneralUtil.GetIPHost(targetDns);

                var rconClient = new RCON(iPHostEntry.AddressList.FirstOrDefault(), serverPort, server.RconPassword);

                _rconClients.Add(serverId, rconClient);

                rconClient.OnDisconnected += () =>
                {
                    _ = _log.LogMessage($"RCON client for `{serverId}` has been disposed.", false, color: LOG_COLOR);

                    if (_rconClients.ContainsKey(serverId))
                        _rconClients.Remove(serverId);
                };

                rconClient.OnLog += logMessage => { _ = _log.LogMessage(logMessage, color: LOG_COLOR); };
            }

            return _rconClients[serverId];
        }

        /// <summary>
        ///     Sends an RCON command to a specific server. Will automatically retry, or display error messages if something
        ///     goes wrong.
        /// </summary>
        /// <param name="serverId">Server to send command to</param>
        /// <param name="command">Command to send</param>
        /// <returns>Server reply, if any</returns>
        public async Task<string> RconCommand(string serverId, string command, bool log = true, int retryCount = 0)
        {
            var recursiveRetryCount = retryCount;

            //We've used all our retry attempts
            if (recursiveRetryCount >= 3)
                return
                    $"Failed to communicate with RCON server {serverId} after retrying {recursiveRetryCount} times. The" +
                    " server may be running, but I was unable to properly communicate with it." +
                    $"\n\n{command} WAS NOT sent.";

            //Spool all new commands if one is in progress. Only spool new incoming commands instances.
            if (recursiveRetryCount == 0)
                while (_running)
                {
                    _ = _log.LogMessage("Waiting for another instance of RCON to finish before sending\n" +
                                        $"{command}\nTo: {serverId}", false, color: LOG_COLOR);
                    await Task.Delay(500);
                }

            _running = true;

            serverId = GeneralUtil.GetServerCode(serverId);
            var reply = "";
            RCON client = null;
            var reconnectCount = 0;

            //start the task, so we can wait on it later with a timeout timer.
            var t = Task.Run(async () =>
            {
                //Ensure we have an RCON client that is connected.
                //Should the connection never become connected, we end here by setting reply and ending the loop completely.
                while (true)
                {
                    client = await GetOrCreateRconClient(serverId);
                    //Break loop if connected
                    if (client.GetConnected())
                        break;

                    await Task.Delay(250);
                    //Destroy old client, force removal from dictionary
                    client.Dispose();
                    if (_rconClients.ContainsKey(serverId))
                        _rconClients.Remove(serverId);

                    //Timeout retries for failed connections
                    if (reconnectCount > 2)
                    {
                        reply =
                            "Failed to establish connection to rcon server. Is SRCDS running?" +
                            $"\nIPEndPoint: {client.GetIpEndPoint()}\nConnected: {client.GetConnected()}";
                        break;
                    }

                    reconnectCount++;
                }

                //Only send if the client is connected.
                if (client.GetConnected())
                    try
                    {
                        reply = await client.SendCommandAsync(command);
                    }
                    catch (Exception e)
                    {
                        await _log.LogMessage(
                            $"Failed to communicate with RCON server {serverId}. Will retry...\n{e.Message}", false,
                            color: LOG_COLOR);
                        client.Dispose();

                        //Retry the command while incrementing the retry counter
                        reply = await RconCommand(serverId, command, log, recursiveRetryCount + 1);
                    }
            });

            //Ultimate timeout, should hopefully never be here.
            if (await Task.WhenAny(t, Task.Delay(10 * 1000)) != t)
            {
                try
                {
                    client.Dispose();
                }
                catch
                {
                    Console.WriteLine("Failed disposing");
                }

                await _log.LogMessage(
                    $"Failed to communicate with RCON server {serverId} within the timeout period." +
                    $"\nRetry count is `{recursiveRetryCount}` of `3`" +
                    $"\n`{serverId}`\n`{command}`", color: LOG_COLOR);

                //Freak timeout event - Try it all again.
                reply = await RconCommand(serverId, command, log, recursiveRetryCount + 1);
            }

            //Release the next instance
            _running = false;

            reply = FormatRconServerReply(reply);

            if (string.IsNullOrWhiteSpace(reply))
                reply = $"{command} was sent, but provided no reply.";

            //Only want to log for the first instance of this command. Prevents double logging from recursive calls.
            if (log && recursiveRetryCount == 0)
                await _log.LogMessage($"**Sending:** `{command}`\n**To:** `{serverId}`\n**Response Was:** `{reply}`",
                    color: LOG_COLOR);

            return reply;
        }

        /// <summary>
        ///     Gets status from server, then sets a parameter containing the player count of the server.
        /// </summary>
        /// <param name="serverId">SeverId to get status from</param>
        /// <returns></returns>
        public async Task GetPlayCountFromServer(string serverId)
        {
            var returned = await RconCommand(serverId, "status", false);
            var replyArray = returned.Split(new[] {"\r\n", "\r", "\n"}, StringSplitOptions.None);

            //Only get the line with player count
            var results = replyArray.Where(l => l.StartsWith("players"));

            //Remove extra information from string
            var formatted = results.FirstOrDefault()?.Substring(10);

            _dataService.SetPlayerCount(formatted?.Substring(0, formatted.IndexOf(" ", StringComparison.Ordinal)));
        }

        /// <summary>
        ///     Formats the reply for displaying to users. Removes log messages, and updates message if empty.
        /// </summary>
        /// <param name="input"></param>
        /// <returns></returns>
        private string FormatRconServerReply(string input)
        {
            var replyArray = input.Split(new[] {"\r\n", "\r", "\n"}, StringSplitOptions.None);
            return string.Join("\n", replyArray.Where(x => !x.Trim().StartsWith("L ")));
        }

        /// <summary>
        ///     If the RCON server hasn't been used in a while, just yeet some shit its way to make it wake tf up.
        /// </summary>
        /// <param name="serverId">Server ID to wake up</param>
        /// <returns></returns>
        public async Task WakeRconServer(string serverId)
        {
            //Using GUIDs for basically random text.
            await RconCommand(serverId, "//WakeServer_" + Guid.NewGuid().ToString().Substring(0, 6), false);
        }

        /// <summary>
        ///     Gets the current running level, and workshop ID from a test server.
        ///     If array.length == 3 it is a workshop map, with the ID in [1] and map name in [2]
        ///     Otherwise it is a stock level with the name in [0]
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