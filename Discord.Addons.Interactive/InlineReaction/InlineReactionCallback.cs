using Discord.Commands;
using Discord.WebSocket;
using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading.Tasks;

namespace Discord.Addons.Interactive
{
    public class InlineReactionCallback : IReactionCallback
    {
        public RunMode RunMode => RunMode.Async;

        public ICriterion<SocketReaction> Criterion { get; }

        public TimeSpan? Timeout { get; }

        public SocketCommandContext Context { get; }

        public IUserMessage Message { get; private set; }

        private readonly InteractiveService _interactive;
        private readonly ReactionCallbackData _data;
        readonly ConcurrentDictionary<ulong, DateTime> _cooldowns = new ConcurrentDictionary<ulong, DateTime>();

        public InlineReactionCallback(
            InteractiveService interactive,
            SocketCommandContext context,
            ReactionCallbackData data,
            ICriterion<SocketReaction> criterion = null)
        {
            _interactive = interactive;
            Context = context;
            _data = data;
            Criterion = criterion ?? new EmptyCriterion<SocketReaction>();
            Timeout = data.Timeout;
        }

        public async Task DisplayAsync()
        {
            var message = await Context.Channel.SendMessageAsync(_data.Text, embed: _data.Embed).ConfigureAwait(false);
            Message = message;
            _interactive.AddReactionCallback(message, this);

            _ = Task.Run(async () =>
            {
                foreach (var item in _data.Callbacks)
                    await message.AddReactionAsync(item.Reaction);
            });

            if (Timeout.HasValue)
            {
                _ = Task.Delay(Timeout.Value)
                    .ContinueWith(_ => _interactive.RemoveReactionCallback(message));
            }
        }

        public async Task<bool> HandleCallbackAsync(SocketReaction reaction)
        {
            var reactionCallbackItem = _data.Callbacks.FirstOrDefault(t => t.Reaction.Equals(reaction.Emote));
            if (reactionCallbackItem == null)
                return false;

            if (_data.RemoveReaction)
            {
                if (reaction.Message.IsSpecified && reaction.User.IsSpecified)
                    await reaction.Message.Value.RemoveReactionAsync(reaction.Emote, reaction.User.Value);
            }

            if (_data.DeleteMessage)
            {
                if (reaction.Message.IsSpecified)
                    await reaction.Message.Value.DeleteAsync();
            }

            if (_data.Cooldown.HasValue)
                if (_cooldowns.TryGetValue(Message.Id, out DateTime endsAt))
                {
                    var difference = endsAt.Subtract(DateTime.UtcNow);
                    if (difference.Ticks > 0)
                    {
                        return !_data.AllowMultipleTimes;
                    }
                    var time = DateTime.UtcNow.Add(_data.Cooldown.Value);
                    _cooldowns.TryUpdate(Message.Id, time, endsAt);
                }
                else
                {
                    _cooldowns.TryAdd(Message.Id, DateTime.UtcNow.Add(_data.Cooldown.Value));
                }

            await reactionCallbackItem.Callback(Context);

            return !_data.AllowMultipleTimes;
        }
    }
}