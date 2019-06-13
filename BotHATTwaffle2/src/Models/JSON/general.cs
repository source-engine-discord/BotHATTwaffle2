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
    }
}