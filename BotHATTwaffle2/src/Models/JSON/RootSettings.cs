using System.Collections.Generic;

namespace BotHATTwaffle2.Models.JSON
{
    public class RootSettings
    {
        public ProgramSettings ProgramSettings { get; set; }
        public General General { get; set; }
        public Lists Lists { get; set; }
        public AutoReplies AutoReplies { get; set; }
        public UserRoles UserRoles { get; set; }
        public List<FaceItHub> FaceItHubs { get; set; }
    }
}