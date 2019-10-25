using System.Collections.Generic;

namespace BotHATTwaffle2.src.Models.JSON.Steam
{
    public class Response
    {
        public int result { get; set; }
        public int resultcount { get; set; }
        public List<Publishedfiledetail> publishedfiledetails { get; set; }
        public List<Player> players { get; set; }
        public int game_count { get; set; }
    }
}