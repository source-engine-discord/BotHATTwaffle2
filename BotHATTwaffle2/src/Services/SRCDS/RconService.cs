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

        public RconService(DataService dataService, LogHandler log)
        {
            _dataService = dataService;
            _log = log;

            _rconClients = new Dictionary<string, RCON>();
        }

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

        public async Task<string> RconCommand(string serverId, string command)
        {
            string reply;
            serverId = GeneralUtil.GetServerCode(serverId);
            var client = GetOrCreateRconClient(serverId);
            var commandResult =  client.SendCommandOrRetry(command);
            if (commandResult == await Task.WhenAny(commandResult, Task.Delay(5000)))
            {
                reply = await commandResult;
            }
            else
            {
                //If we timed out, we can assume the server isn't going to reply on this session any more.
                reply = "Timed out waiting for a reply, please try again";
                client.Dispose();
                _rconClients.Remove(serverId);
            }

            if (string.IsNullOrWhiteSpace(reply))
                return $"{command} was sent, but provided no reply.";

            reply = FormatRconServerReply(reply);

            //Ignore logging status replies... This is a one off that just causes too much spam.
            if(!command.Contains("status",StringComparison.OrdinalIgnoreCase))
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
