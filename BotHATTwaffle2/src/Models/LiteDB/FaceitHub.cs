namespace BotHATTwaffle2.Models.LiteDB
{
    public class FaceItHub
    {
        public int Id { get; set; }
        public string HubName { get; set; }
        public string HubGUID { get; set; }
        public string HubType { get; set; }
        public int Endpoint { get; set; } //0 = hub, 1 = championships


        public override string ToString()
        {
            return $"DB ID: {Id}" +
                   $"\nHubName: {HubName}" +
                   $"\nHubGUID: {HubGUID}" +
                   $"\nHubType: {HubType}" +
                   $"\nEndpoint: {Endpoint}";
        }
    }
}