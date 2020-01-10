using System;
using System.Collections.Generic;

namespace BotHATTwaffle2.Models.FaceIt
{
    public class FaceItHubEndpointsResponsesInfo
    {
        public IDictionary<string, DateTime> DemoDate = new Dictionary<string, DateTime>();
        public IDictionary<string, string> DemoMapnames = new Dictionary<string, string>();
        public IDictionary<string, List<string>> DemoUrls = new Dictionary<string, List<string>>();
        public List<string> FailedApiCalls;

        public List<string> FileNames;
    }
}