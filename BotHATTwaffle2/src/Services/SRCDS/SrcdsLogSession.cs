using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using BotHATTwaffle2.Handlers;
using BotHATTwaffle2.Models.LiteDB;
using BotHATTwaffle2.Util;

namespace BotHATTwaffle2.Services.SRCDS
{
    class SrcdsLogSession
    {
        private readonly DataService _dataService;
        private readonly LogHandler _logHandler;
        private readonly RconService _rconService;
        private Server _server;
        private IPAddress _ip;

        public SrcdsLogSession(DataService dataService, RconService rconService, LogHandler logHandler)
        {
            _rconService = rconService;
            _dataService = dataService;
            _logHandler = logHandler;
        }

        
    }
}
