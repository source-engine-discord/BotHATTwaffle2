using System;
using System.Collections.Generic;
using System.Text;

namespace BotHATTwaffle2.src.Models.LiteDB
{
    class ServerReservation
    {
        public int Id { get; set; }
        public ulong UserId { get; set; }
        public string Server { get; set; }
        public DateTime StartTime { get; set; }
        public bool Expired { get; set; }
    }
}
