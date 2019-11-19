namespace BotHATTwaffle2
{
    public class ProgramSettings
    {
        public bool Debug { get; set; }
        public string BotToken { get; set; }
        public string LogChannel { get; set; }
        public string CommandPrefix { get; set; }
        public ulong AlertUser { get; set; }
        public string TestCalendarId { get; set; }
        public string ImgurApi { get; set; }
        public string PlaytestDemoPath { get; set; }
        public string SteamworksAPI { get; set; }
        public ushort ListenPort { get; set; }
        public string DemoFtpUser { get; set; }
        public string DemoFtpPassword { get; set; }
        public string DemoFtpPath { get; set; }
        public string DemoFtpServer { get; set; }
        public string FaceitAPIKey { get; set; }
        public string FaceItDemoPath { get; set; }
    }
}