using Discord.Commands;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Discord.Addons.Interactive
{
    public class ReactionCallbackData
    {
        private readonly ICollection<ReactionCallbackItem> _items;

        public string Text { get; }
        public Embed Embed { get; }
        public TimeSpan? Timeout { get; }
        public TimeSpan? Cooldown { get; }
        public IEnumerable<ReactionCallbackItem> Callbacks => _items;
        public bool RemoveReaction { get; }
        public bool DeleteMessage { get; }
        public bool AllowMultipleTimes { get; }

        public ReactionCallbackData(string text, Embed embed = null, bool allowMultipleTimes = false, bool removeReaction = false, bool deleteMessage = false, TimeSpan? cooldown = null, TimeSpan ? timeout = null)
        {
            Text = text;
            Embed = embed;
            Timeout = timeout;
            RemoveReaction = removeReaction;
            DeleteMessage = deleteMessage;
            AllowMultipleTimes = allowMultipleTimes;
            Cooldown = cooldown;
            _items = new List<ReactionCallbackItem>();
        }

        public ReactionCallbackData WithCallback(IEmote reaction, Func<SocketCommandContext, Task> callback)
        {
            var item = new ReactionCallbackItem(reaction, callback);
            _items.Add(item);
            return this;
        }
    }
}