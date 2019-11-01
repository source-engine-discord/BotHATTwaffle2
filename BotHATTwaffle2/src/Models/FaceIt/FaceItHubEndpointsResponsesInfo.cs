using System.Collections.Generic;

namespace BotHATTwaffle2.src.Models.FaceIt
{
    public class FaceItHubEndpointsResponsesInfo
    {
        public FaceItHubEndpointsResponsesInfo() {}
        
        public List<string> FileNames;
        public IDictionary<string, string> DemoMapnames = new Dictionary<string, string>();
        public IDictionary<string, string> DemoUrls = new Dictionary<string, string>();
        public List<string> FailedApiCalls;
    }
}
