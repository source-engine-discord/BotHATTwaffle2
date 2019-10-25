using System;

namespace BotHATTwaffle2.Models.LiteDB
{
    class AnnounceMessage
    {
        public int Id { get; set; }
        public string Title { get; set; }
        public ulong AnnouncementId { get; set; }
        public string Game { get; set; }
    }
}
