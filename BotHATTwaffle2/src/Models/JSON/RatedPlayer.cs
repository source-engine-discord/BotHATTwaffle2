using System;

namespace BotHATTwaffle2.Models.JSON
{
    class RatedPlayer
    {
        private string _name;
        public string Name
        {
            get => _name ?? "Unknown Player";
            set => _name = value;
        }

        public Int64 SteamID { get; set; }
        public double Rating { get; set; }

        public override string ToString()
        {
            return "Name: " + Name + "\nSteamID: " + SteamID + "\nRating: " + Rating;
        }
    }
}
