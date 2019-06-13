using System.ComponentModel;
using Newtonsoft.Json;

namespace BotHATTwaffle2
{
    public class debugValues
    {
        public ulong moderator { get; set; }
        public ulong admin { get; set; }
        public ulong playtester { get; set; }
        public ulong muted { get; set; }
        public ulong active { get; set; }
        public ulong patreons { get; set; }
        public ulong communityTester { get; set; }
        public ulong competitiveTester { get; set; }
        public ulong bots { get; set; }

        [DefaultValue("General")]
        [JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
        public string generalChannel { get; set; }

        [DefaultValue("welcome")]
        [JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
        public string welcomeChannel { get; set; }

        [DefaultValue("announcements")]
        [JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
        public string announcementChannel { get; set; }

        [DefaultValue("testing")]
        [JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
        public string testingChannel { get; set; }

        [DefaultValue("competitive_level_testing")]
        [JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
        public string compeitiveTestingChannel { get; set; }
    }
}