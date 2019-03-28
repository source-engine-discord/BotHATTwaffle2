using System.Collections.Generic;
using System.ComponentModel;
using Newtonsoft.Json;

namespace BotHATTwaffle2
{
    public class lists
    {
        [DefaultValue("CHANGEME")]
        [JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
        public List<string> roles { get; set; }

        [DefaultValue("CHANGEME")]
        [JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
        public List<string> playing { get; set; }
    }
}
