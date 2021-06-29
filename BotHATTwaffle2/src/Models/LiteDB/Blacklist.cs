using System.Collections.Generic;

namespace BotHATTwaffle2.Models.LiteDB
{
    public class Blacklist
    {
        public int Id { get; set; }
        public string Word { get; set; }
        public int AutoMuteDuration { get; set; }

        public override string ToString()
        {
            return $"DB ID: {Id}" +
                   $"\nWord: {Word}" +
                   $"\nAutoMuteDuration {AutoMuteDuration}";
        }
    }
}
