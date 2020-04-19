using System;

namespace BotHATTwaffle2.Models.LiteDB
{
    internal class FaceItHubTag
    {
        public int Id { get; set; }
        public string TagName { get; set; }
        public string Type { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }

        public override string ToString()
        {
            return $"DB ID: {Id}" +
                   $"StartDate: {StartDate}" +
                   $"\nEndDate: {EndDate}" +
                   $"\nTagName: {TagName}" +
                   $"\nType: {Type}";
        }
    }
}