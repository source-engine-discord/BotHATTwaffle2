namespace BotHATTwaffle2.Models.LiteDB
{
    class Server
    {
        public int Id { get; set; }
        public string ServerId { get; set; }
        public string Description { get; set; }
        public string Address { get; set; }
        public string RconPassword { get; set; }
        public string FtpUser { get; set; }
        public string FtpPassword { get; set; }
        public string FtpPath { get; set; }
        public string FtpType { get; set; }

        public override string ToString()
        {
            return $"\nDatabase ID: {Id}" +
                   $"\nServerID: {ServerId}" +
                   $"\nDescription: {Description}" +
                   $"\nAddress: {Address}" +
                   $"\nRconPassword: {RconPassword}" +
                   $"\nFtpUser: {FtpUser}" +
                   $"\nFtpPassword: {FtpPassword}" +
                   $"\nFtpPath: {FtpPath}" +
                   $"\nFtpType: {FtpType}";
        }
    }
}
