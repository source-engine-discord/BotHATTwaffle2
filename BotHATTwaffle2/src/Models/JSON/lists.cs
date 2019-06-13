using System.Collections.Generic;
using System.ComponentModel;
using Newtonsoft.Json;

namespace BotHATTwaffle2
{
    public class Lists
    {
        [DefaultValue("CHANGEME")]
        [JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
        public List<string> Roles { get; set; }

        [DefaultValue("CHANGEME")]
        [JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
        public List<string> Playing { get; set; }
    }
}