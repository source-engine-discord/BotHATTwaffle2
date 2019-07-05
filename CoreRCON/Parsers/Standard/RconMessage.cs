using System.Text.RegularExpressions;

namespace CoreRCON.Parsers.Standard
{
    public class RconMessage : IParseable
    {
        public MessageChannel Channel { get; set; }
        public string Message { get; set; }
        public Player Player { get; set; }
    }

    public class RconMessageParser : DefaultParser<RconMessage>
    {
        public override string Pattern { get; } = $"(?<Sender>{playerParser.Pattern}) (?<Channel>say_team|say) \">(r|rcon)\\s(?<Message>.+?)\"";
        private static PlayerParser playerParser { get; } = new PlayerParser();

        public override RconMessage Load(GroupCollection groups)
        {
            return new RconMessage
            {
                Player = playerParser.Parse(groups["Sender"]),
                Message = groups["Message"].Value,
                Channel = groups["Channel"].Value == "say" ? MessageChannel.All : MessageChannel.Team
            };
        }
    }
}