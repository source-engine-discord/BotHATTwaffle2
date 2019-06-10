using System.ComponentModel;
using Newtonsoft.Json;

namespace BotHATTwaffle2
{
    public class program_settings
    {
        [DefaultValue(false)]
        [JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
        public bool debug { get; set; }

        [DefaultValue("CHANGEME")]
        [JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
        public string botToken { get; set; }

        [DefaultValue("Logs")]
        [JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
        public string logChannel { get; set; }

        [DefaultValue(">")]
        [JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
        public string commandPrefix { get; set; }

        [DefaultValue("CHANGEME#1111")]
        [JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
        public ulong alertUser { get; set; }

        [DefaultValue("CHANGEME")]
        [JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
        public string testCalendarID { get; set; }

        public string imgurAPI { get; set; }
    }
}