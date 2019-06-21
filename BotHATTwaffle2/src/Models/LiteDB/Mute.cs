using System;

namespace BotHATTwaffle2.Models.LiteDB
{
    class Mute
    {
        public int Id { get; set; }
        public ulong UserId { get; set; }
        public string Username { get; set; }
        public string Reason { get; set; }
        public double Duration { get; set; }
        public DateTime MuteTime { get; set; }
        public ulong ModeratorId { get; set; }
        public bool Expired { get; set; }

        public override string ToString()
        {
            return $"DB ID: {Id}" +
                   $"\nUserID: {UserId}" +
                   $"\nUsername: {Username}" +
                   $"\nReason: {Reason}" +
                   $"\nDuration: {Duration}" +
                   $"\nMuteTime: {MuteTime}" +
                   $"\nModeratorId: {ModeratorId}" +
                   $"\nExpired: {Expired}";
        }
    }
}
