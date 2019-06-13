using System.Collections.Generic;
using System.ComponentModel;
using Newtonsoft.Json;

namespace BotHATTwaffle2
{
    public class AutoReplies
    {
        public List<string> Firmware { get; set; }
        public List<string> BootLoader { get; set; }
    }
}