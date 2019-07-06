using System;

namespace BotHATTwaffle2.Models.LiteDB
{
    /// <summary>
    /// This class is used to store information for the >playtest command so the information can persist
    /// between restarts.
    /// </summary>
    public class PlaytestCommandInfo
    {
        public int Id { get; set; }
        public string Mode { get; set; }
        public string DemoName { get; set; }
        public string WorkshopId { get; set; }
        public string ServerAddress { get; set; }
        public string Title { get; set; }
        public string ThumbNailImage { get; set; }
        public string ImageAlbum { get; set; }
        public string CreatorMentions { get; set; }
        public DateTime StartDateTime { get; set; }

        public override string ToString()
        {
            return $"DB ID: {Id}" +
                   $"\nMode: {Mode}" +
                   $"\nDemoName {DemoName}" +
                   $"\nWorkshop ID: {WorkshopId}" +
                   $"\nServerAddress: {ServerAddress}" +
                   $"\nTitle: {Title}" +
                   $"\nThumbNailImage: {ThumbNailImage}" +
                   $"\nImageAlbum {ImageAlbum}" +
                   $"\nCreatorMentions {CreatorMentions}";
        }
    }
}