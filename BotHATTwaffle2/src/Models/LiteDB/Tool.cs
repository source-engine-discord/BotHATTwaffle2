using Discord;

namespace BotHATTwaffle2.Models.LiteDB
{
    public class Tool
    {
        public int Id { get; set; }
        public string Command { get; set; }
        public string AuthorName { get; set; }
        public string Url { get; set; }
        public string ThumbnailUrl { get; set; }
        public string Color { get; set; }
        public string Description { get; set; }

        /// <summary>
        /// Gets the discord color object from this item.
        /// </summary>
        /// <returns>Color object for use in Discord embed</returns>
        public Color GetColor()
        {
            var split = Color.Split(" ");
            int r = int.Parse(split[0]);
            int g = int.Parse(split[1]);
            int b = int.Parse(split[2]);
            return new Color(r,g,b);
        }

        public override string ToString()
        {
            return $"DB ID: {Id}" +
                   $"Command: {Command}" +
                   $"AuthorName: {AuthorName}" +
                   $"Url: {Url}" +
                   $"ThumbnailUrl: {ThumbnailUrl}" +
                   $"Color: {Color}" +
                   $"Description: {Description}";
        }
    }
}