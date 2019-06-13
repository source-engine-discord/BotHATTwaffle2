using System;
using System.Collections.Generic;
using System.Text;

namespace BotHATTwaffle2.src.Models.LiteDB
{
    class Server
    {
        public int id { get; set; }
        public string ServerID { get; set; }
        public string Description { get; set; }
        public string Address { get; set; }
        public string RconPassword { get; set; }
        public string FtpUser { get; set; }
        public string FtpPassword { get; set; }
        public string FtpPath { get; set; }
        public string FtpType { get; set; }
    }
}
