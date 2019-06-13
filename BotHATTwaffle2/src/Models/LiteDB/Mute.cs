using System;
using System.Collections.Generic;
using System.Text;

namespace BotHATTwaffle2.src.Models.LiteDB
{
    class Mute
    {
        public int id { get; set; }
        public ulong UserID { get; set; }
        public string Username { get; set; }
        public string Reason { get; set; }
        public int Duration { get; set; }
        public DateTime Mutetime { get; set; }
        public ulong ModeratorID { get; set; }
        public bool Expired { get; set; }
    }
}
