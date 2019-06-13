using System;
using System.Collections.Generic;
using System.Text;

namespace BotHATTwaffle2.src.Models.LiteDB
{
    class UserJoinMessage
    {
        public int Id { get; set; }
        public ulong UserId { get; set; }
        public DateTime JoinTime { get; set; }
        public bool MessageSent { get; set;}
    }
}
