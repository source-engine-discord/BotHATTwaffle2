namespace BotHATTwaffle2.Models.FaceIt
{
    public class DemoResult
    {
        public DemoResult(string filename, string fileLocation, string fileLocationGz, string fileLocationDemo,
            string fileLocationOldJson, string demoUrl)
        {
            Filename = filename;
            FileLocation = fileLocation;
            FileLocationGz = fileLocationGz;
            FileLocationDemo = fileLocationDemo;
            FileLocationOldJson = fileLocationOldJson;
            DemoUrl = demoUrl;
            Skip = false;
        }
        public bool DownloadFailed { get; set; }
        public bool UnzipFailed { get; set; }
        public string Filename { get; }
        public string FileLocation { get; }
        public string FileLocationGz { get; }
        public string FileLocationDemo { get; }
        public string FileLocationOldJson { get; }
        public string DemoUrl { get; }
        public bool Skip { get; set; }
    }
}
