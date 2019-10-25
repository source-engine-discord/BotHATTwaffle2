namespace BotHATTwaffle2.Models.LiteDB
{
    public class UserSteamID
    {
        public int Id { get; set; }
        public ulong UserId { get; set; }
        public string SteamID { get; set; }

        public override string ToString()
        {
            return $"Database ID: {Id}" +
                   $"\nUserID: {UserId}" +
                   $"\nSteamID: {SteamID}";
        }
    }
}