using System;

namespace BotHATTwaffle2.Models.LiteDB
{
    public class ServerReservation
    {
        public int Id { get; set; }
        public ulong UserId { get; set; }
        public string ServerId { get; set; }
        public DateTime StartTime { get; set; }
        public bool Announced { get; set; }

        public override string ToString()
        {
            return $"DB ID: {Id}" +
                   $"\nUser ID: {UserId}" +
                   $"\nServer ID: {ServerId}" +
                   $"\nStart Time: {StartTime}" +
                   $"\nAnnounced: {Announced}";
        }
    }
}
