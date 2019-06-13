using System.ComponentModel;
using Newtonsoft.Json;

namespace BotHATTwaffle2
{
    public class DebugValues
    {
        public ulong Moderator { get; set; }
        public ulong Admin { get; set; }
        public ulong Playtester { get; set; }
        public ulong Muted { get; set; }
        public ulong Active { get; set; }
        public ulong Patreons { get; set; }
        public ulong CommunityTester { get; set; }
        public ulong CompetitiveTester { get; set; }
        public ulong Bots { get; set; }
        public string GeneralChannel { get; set; }
        public string WelcomeChannel { get; set; }
        public string AnnouncementChannel { get; set; }
        public string TestingChannel { get; set; }
        public string CompetitiveTestingChannel { get; set; }
    }
}