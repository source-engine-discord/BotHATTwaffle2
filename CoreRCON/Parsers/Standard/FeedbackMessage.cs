using System.Text.RegularExpressions;

namespace CoreRCON.Parsers.Standard
{
    public class FeedbackMessage : IParseable
    {
        public MessageChannel Channel { get; set; }
        public string Message { get; set; }
        public Player Player { get; set; }
    }

    public class FeedbackMessageParser : DefaultParser<FeedbackMessage>
    {
        public override string Pattern { get; } = $"(?<Sender>{playerParser.Pattern}) (?<Channel>say_team|say) \">(fb|feedback)\\s?(?<Message>.+?)\"";
        private static PlayerParser playerParser { get; } = new PlayerParser();

        public override FeedbackMessage Load(GroupCollection groups)
        {
            return new FeedbackMessage
            {
                Player = playerParser.Parse(groups["Sender"]),
                Message = groups["Message"].Value,
                Channel = groups["Channel"].Value == "say" ? MessageChannel.All : MessageChannel.Team
            };
        }
    }
}