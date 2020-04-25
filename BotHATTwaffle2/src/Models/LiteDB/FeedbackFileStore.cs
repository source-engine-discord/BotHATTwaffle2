namespace BotHATTwaffle2.Models.LiteDB
{
    public class FeedbackFileStore
    {
        public int Id { get; set; }
        public string ServerAddress { get; set; }
        public string FileName { get; set; }

        public override string ToString()
        {
            return $"\nDatabase ID: {Id}" +
                   $"\nServerID: {ServerAddress}" +
                   $"\nFileName: {FileName}";
        }
    }
}