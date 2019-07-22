using System.ComponentModel;
using Newtonsoft.Json;

namespace BotHATTwaffle2
{
    public class General
    {

        public string WelcomeMessage { get; set; }
        public string GeneralChannel { get; set; }
        public string WelcomeChannel { get; set; }
        public string AnnouncementChannel { get; set; }
        public string TestingChannel { get; set; }
        public string CompetitiveTestingChannel { get; set; }
        public string FallbackTestImageUrl { get; set; }
        public string CasualPassword { get; set; }
        public string[] CompPasswords { get; set; }
        public string CasualConfig { get; set; }
        public string CompConfig { get; set; }
        public string PostgameConfig { get; set; }
        public string WebhookChannel { get; set; }
        public string AdminChannel { get; set; }
        public string VoidChannel { get; set; }
        public string BotChannel { get; set; }
        public ulong LevelTestingVoice { get; set; }
        public int FeedbackDuration { get; set; }
    }
}