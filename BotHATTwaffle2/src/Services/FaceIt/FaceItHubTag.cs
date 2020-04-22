using System;
using FaceitLib.Models.ClassObjectLists;

namespace BotHATTwaffle2.Services.FaceIt
{
    public class FaceItHubTag
    {
        public string TagName { get; set; }
        public string HubGuid { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }

        public FaceItHubTag()
        {
            TagName = "UNKNOWN";
            StartDate = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            EndDate = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        }
        public override string ToString()
        {
            return $"StartDate: {StartDate}" +
                   $"\nEndDate: {EndDate}" +
                   $"\nTagName: {TagName}" +
                   $"\nType: {HubGuid}";
        }
    }
}