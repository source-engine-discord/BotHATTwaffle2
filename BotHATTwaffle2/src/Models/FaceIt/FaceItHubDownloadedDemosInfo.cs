using System.Collections.Generic;

namespace BotHATTwaffle2.src.Models.FaceIt
{
    public class FaceItHubDownloadedDemosInfo
    {
        public FaceItHubDownloadedDemosInfo() {}

        public List<string> DownloadedDemos;
        public List<string> UnzippedDemos;
        public List<string> FailedDownloads;
        public List<string> FailedUnzips;
    }
}
