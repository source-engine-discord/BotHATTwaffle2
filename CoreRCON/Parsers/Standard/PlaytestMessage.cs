using System.Text.RegularExpressions;

namespace CoreRCON.Parsers.Standard
{
    public class PlaytestMessage : IParseable
    {
        public MessageChannel Channel { get; set; }
        public string Message { get; set; }
        public Player Player { get; set; }
    }

    public class PlaytestMessageParser : DefaultParser<PlaytestMessage>
    {
        public override string Pattern { get; } = $"(?<Sender>{playerParser.Pattern}) (?<Channel>say_team|say) \">(p|playtest)\\s?(?<Message>.+?)\"";
        private static PlayerParser playerParser { get; } = new PlayerParser();

        public override PlaytestMessage Load(GroupCollection groups)
        {
            return new PlaytestMessage
            {
                Player = playerParser.Parse(groups["Sender"]),
                Message = groups["Message"].Value,
                Channel = groups["Channel"].Value == "say" ? MessageChannel.All : MessageChannel.Team
            };
        }
    }
}