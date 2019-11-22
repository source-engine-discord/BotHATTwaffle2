using System;

namespace BotHATTwaffle2.Models.FaceIt
{
    public class DemoResult
    {
        public DemoResult(string filename, string fileLocation, string fileLocationGz, string fileLocationDemo,
            string jsonLocation, string demoUrl, DateTime demoDate, string mapName)
        {
            Filename = filename;
            FileLocation = fileLocation.TrimEnd('\\');
            FileLocationGz = fileLocationGz;
            FileLocationDemo = fileLocationDemo;
            JsonLocation = jsonLocation;
            DemoUrl = demoUrl;
            Skip = false;
            DemoDate = demoDate;
            MapName = mapName;
        }
        public bool DownloadFailed { get; set; }
        public bool UnzipFailed { get; set; }
        public string Filename { get; }
        public string FileLocation { get; }
        public string FileLocationGz { get; }
        public string FileLocationDemo { get; }
        public string JsonLocation { get; }
        public string DemoUrl { get; }
        public string MapName { get; }
        public bool Skip { get; set; }
        public DateTime DemoDate { get; }
    }
}