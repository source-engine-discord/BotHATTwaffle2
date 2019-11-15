namespace BotHATTwaffle2.Models.LiteDB
{
    internal class PreviousTest
    {
        public PreviousTest(string title)
        {
            Id = 1;
            Title = title;
        }

        public int Id { get; set; }
        public string Title { get; set; }
    }
}
