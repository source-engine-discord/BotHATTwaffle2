using System;
using System.Collections.Generic;

namespace BotHATTwaffle2.src.Models.FaceIt
{
    public class FaceItHubDemosRequest
    {
        public DateTime DateFrom { get; set; }
        public DateTime DateTo { get; set; }
        public string Gamemode { get; set; }
        public List<string> HubRegions { get; set; }

        public override string ToString()
        {
            return $"Date From:{DateFrom}" +
                   $"\nDate To:{DateTo}" +
                   $"\nGamemode: {Gamemode}" +
                   $"\nHub Regions:{HubRegions}";
        }
    }
}