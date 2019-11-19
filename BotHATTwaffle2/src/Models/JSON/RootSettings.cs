using System.Collections.Generic;
using BotHATTwaffle2.Models.JSON;

namespace BotHATTwaffle2
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