using System;
using System.Collections.Generic;

namespace BotHATTwaffle2.Models.LiteDB
{
    public class PlaytestRequest
    {
        public PlaytestRequest()
        {
            Emails = new List<string>();
            CreatorsDiscord = new List<string>();
        }

        public int Id { get; set; }
        public DateTime Timestamp { get; set; }
        public DateTime TestDate { get; set; }
        public List<string> Emails { get; set; }
        public string MapName { get; set; }
        public List<string> CreatorsDiscord { get; set; }
        public string ImgurAlbum { get; set; }
        public string WorkshopURL { get; set; }
        public string TestType { get; set; }
        public string TestGoals { get; set; }
        public int Spawns { get; set; }
        public DateTime PreviousTestDate { get; set; }
        public string Preferredserver { get; set; }
        public string Game { get; set; }

        public override string ToString()
        {
            return $"Date:{TestDate}" +
                   $"\nEmails:{string.Join(", ", Emails)}" +
                   $"\nGame: {Game}" +
                   $"\nMapName:{MapName}" +
                   $"\nDiscord:{string.Join(", ", CreatorsDiscord)}" +
                   $"\nImgur:{ImgurAlbum}" +
                   $"\nWorkshop:{WorkshopURL}" +
                   $"\nType:{TestType}" +
                   $"\nDescription:{TestGoals}" +
                   $"\nSpawns:{Spawns}" +
                   $"\nPreviousTest:{PreviousTestDate}" +
                   $"\nServer:{Preferredserver}";
        }
    }
}