using System.ComponentModel;
using Newtonsoft.Json;

namespace BotHATTwaffle2
{
    public class general
    {
        [DefaultValue("CHANGEME")]
        [JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
        public string welcomeMessage { get; set; }

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
        public string competitiveTestingChannel { get; set; }
    }
}
