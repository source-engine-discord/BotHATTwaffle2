using System;
using System.Collections.Generic;
using System.Text;

namespace BotHATTwaffle2.src.Models
{
    public class PlaytestRequest
    {
        public DateTime Timestamp { get; set; }
        public DateTime TestDate { get; set; }
        public string Name { get; set; }
        public string Email { get; set; }
        public List<string> AdditionalEmail { get; set; }
        public string MapName { get; set; }
        public List<string> CreatorsDiscord { get; set; }
        public string ImgurAlbum { get; set; }
        public string WorkshopURL { get; set; }
        public string TestType { get; set; }
        public string TestGoals { get; set; }
        public int Spawns { get; set; }
        public bool RadarTested { get; set; }
        public DateTime PreviousTestDate { get; set; }
        public string Chickens { get; set; }
        public bool OtherModCanRun { get; set; }
        public string Preferredserver { get; set; }
        public string Feedback { get; set; }


        public override string ToString()
        {
            return $"{nameof(Timestamp)}: {Timestamp}\n{nameof(TestDate)}: {TestDate}\n{nameof(Name)}: {Name}\n{nameof(Email)}: {Email}\n{nameof(AdditionalEmail)}: {string.Join(", ", AdditionalEmail)}\n{nameof(MapName)}: {MapName}\n{nameof(CreatorsDiscord)}: {string.Join(", ", CreatorsDiscord)}\n{nameof(ImgurAlbum)}: {ImgurAlbum}\n{nameof(WorkshopURL)}: {WorkshopURL}\n{nameof(TestType)}: {TestType}\n{nameof(TestGoals)}: {TestGoals}\n{nameof(Spawns)}: {Spawns}\n{nameof(RadarTested)}: {RadarTested}\n{nameof(PreviousTestDate)}: {PreviousTestDate}\n{nameof(Chickens)}: {Chickens}\n{nameof(OtherModCanRun)}: {OtherModCanRun}\n{nameof(Preferredserver)}: {Preferredserver}\n{nameof(Feedback)}: {Feedback}";
        }
    }
}
