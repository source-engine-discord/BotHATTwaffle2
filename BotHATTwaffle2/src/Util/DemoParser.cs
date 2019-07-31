using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using BotHATTwaffle2.Handlers;
using BotHATTwaffle2.Services;

namespace BotHATTwaffle2.src.Util
{
    class DemoParser
    {
        private const ConsoleColor LOG_COLOR = ConsoleColor.DarkRed;
        private static LogHandler _log;
        private static DataService _dataService;

        public static void SetHandlers(LogHandler log, DataService data)
        {
            _dataService = data;
            _log = log;
        }

        private static bool CanParse()
        {
            return File.Exists(@"IDemO\CSGODemoCSV.exe");
        }

        public static bool ParseDemo()
        {
            if (!CanParse())
                return false;


            return true;
        }
    }
}
