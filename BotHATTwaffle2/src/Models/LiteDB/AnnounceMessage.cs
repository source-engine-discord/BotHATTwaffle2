using System;

namespace BotHATTwaffle2.Models.LiteDB
{
    class AnnounceMessage
    {
        public int Id { get; set; }
        public DateTime AnnouncementDateTime { get; set; }
        public ulong AnnouncementId { get; set; }
    }
}
