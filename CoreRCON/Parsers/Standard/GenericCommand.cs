using System.Text.RegularExpressions;

namespace CoreRCON.Parsers.Standard
{
    public class GenericCommand : IParseable
    {
        public MessageChannel Channel { get; set; }
        public string Message { get; set; }
        public string Command { get; set; }
        public Player Player { get; set; }
    }

    public class GenericCommandParser : DefaultParser<GenericCommand>
    {
        public override string Pattern { get; } = $"(?<Sender>{playerParser.Pattern}) (?<Channel>say_team|say) \">(?<Command>.+?)\\s+?(?<Message>.+?)\"";
        private static PlayerParser playerParser { get; } = new PlayerParser();

        public override GenericCommand Load(GroupCollection groups)
        {
            return new GenericCommand
            {
                Command = groups["Command"].Value,
                Player = playerParser.Parse(groups["Sender"]),
                Message = groups["Message"].Value,
                Channel = groups["Channel"].Value == "say" ? MessageChannel.All : MessageChannel.Team
            };
        }
    }
}