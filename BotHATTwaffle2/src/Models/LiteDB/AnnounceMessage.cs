using System;
using System.Collections.Generic;
using System.Text;

namespace BotHATTwaffle2.src.Models.LiteDB
{
    class AnnounceMessage
    {
        public int Id { get; set; }
        public DateTime AnnouncementDateTime { get; set; }
        public ulong AnnouncementId { get; set; }
    }
}
