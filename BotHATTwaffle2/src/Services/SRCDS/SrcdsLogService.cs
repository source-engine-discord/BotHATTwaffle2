using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using BotHATTwaffle2.Handlers;
using BotHATTwaffle2.Models.LiteDB;
using BotHATTwaffle2.Util;
using CoreRCON;
using CoreRCON.Parsers.Standard;

namespace BotHATTwaffle2.Services.SRCDS
{
    class SrcdsLogService
    {
        private readonly DataService _dataService;
        private readonly LogHandler _logHandler;
        private readonly RconService _rconService;
        private readonly string _publicIpAddress;
        private LogReceiver _logReceiver;

        public SrcdsLogService(DataService dataService, RconService rconService, LogHandler logHandler)
        {
            _rconService = rconService;
            _dataService = dataService;
            _logHandler = logHandler;
            _publicIpAddress = new WebClient().DownloadString("http://icanhazip.com/").Trim();

            //Let's start the listener on our listen port. Allow all IPs from the server list.
            _ = _logHandler.LogMessage($"Starting LogService on {_dataService.RSettings.ProgramSettings.ListenPort}");
            _logReceiver = new LogReceiver(_dataService.RSettings.ProgramSettings.ListenPort, CreateIpEndPoints());

            Start();
        }

        private async void Start()
        {
            await Task.Run(async () =>
            {
                if (_dataService.RSettings.ProgramSettings.Debug)
                    _logReceiver.ListenRaw(msg => { Console.WriteLine("RAW: " + msg); });
            });
        }

        
        private IPEndPoint[] CreateIpEndPoints()
        {
            var ipEndPoints = new List<IPEndPoint>();
            var servers = DatabaseUtil.GetAllTestServers();
            foreach (var server in servers)
            {
                var ipep = GeneralUtil.GetIpEndPointFromString(server.Address);
                _ = _logHandler.LogMessage($"Adding {ipep.Address}:{ipep.Port} to listener sources!");
                ipEndPoints.Add(ipep);
            }
            return ipEndPoints.ToArray();
        }
    }
}
