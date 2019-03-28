using System.Collections.Generic;
using System.ComponentModel;
using Newtonsoft.Json;

namespace BotHATTwaffle2
{
    public class autoReplies
    {
        [DefaultValue("CHANGEME")]
        [JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
        public List<string> firmware { get; set; }

        [DefaultValue("CHANGEME")]
        [JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
        public List<string> bootLoader { get; set; }
    }
}
