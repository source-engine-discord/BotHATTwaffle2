using System.ComponentModel;
using Newtonsoft.Json;

namespace BotHATTwaffle2
{
    public class ProgramSettings
    {
        public bool Debug { get; set; }
        public string BotToken { get; set; }
        public string LogChannel { get; set; }
        public string CommandPrefix { get; set; }
        public ulong AlertUser { get; set; }
        public string TestCalendarId { get; set; }
        public string ImgurApi { get; set; }
    }
}