using System;

namespace BotHATTwaffle2.Models.LiteDB
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
