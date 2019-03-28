using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;

using Discord;
using Discord.Commands;


namespace BotHATTwaffle2.Commands.Readers
{ 
    /// <summary>
    /// Retrieves a channel by parsing a string.
    /// </summary>
    /// Derived from https://github.com/RogueException/Discord.Net/blob/dev/src/Discord.Net.Commands/Readers/ChannelTypeReader.cs
    /// <typeparam name="T">The channel's type.</typeparam>
    public static class ChannelTypeReader<T> where T : class, IChannel
    {
        /// <summary>
        /// Tries to parses a given string as a channel.
        /// </summary>
        /// <remarks>
        /// See flow chart by Still#2876:
        /// https://cdn.discordapp.com/attachments/381889909113225237/409209957683036160/ChannelTypeReader.png
        /// </remarks>
        /// <param name="guild">The guild in which to search for the channel.</param>
        /// <param name="input">A string representing a channel by mention, id, or name.</param>
        /// <returns>The results of the parse.</returns>
        public static async Task<TypeReaderResult> ReadAsync(IGuild guild, string input)
        {
            if (guild != null)
            {
                var results = new Dictionary<ulong, TypeReaderValue>();
                var channels = await guild.GetChannelsAsync(CacheMode.CacheOnly).ConfigureAwait(false);
                ulong id;

                // By Mention (1.0)
                if (MentionUtils.TryParseChannel(input, out id))
                    AddResult(results, await guild.GetChannelAsync(id, CacheMode.CacheOnly).ConfigureAwait(false) as T, 1.00f);

                // By Id (0.9)
                if (ulong.TryParse(input, NumberStyles.None, CultureInfo.InvariantCulture, out id))
                    AddResult(results, await guild.GetChannelAsync(id, CacheMode.CacheOnly).ConfigureAwait(false) as T, 0.90f);

                // By Name (0.7-0.8)
                // Acounts for name being null because GetChannelsAsync returns categories in 1.0.
                foreach (var channel in channels.Where(c => c.Name?.Equals(input, StringComparison.OrdinalIgnoreCase) ?? false))
                    AddResult(results, channel as T, channel.Name == input ? 0.80f : 0.70f);

                if (results.Count > 0)
                    return TypeReaderResult.FromSuccess(results.Values);
            }

            return TypeReaderResult.FromError(CommandError.ObjectNotFound, "Channel not found.");
        }

        /// <summary>
        /// Gets the best channel result for the given string.
        /// </summary>
        /// <param name="guild">The guild in which to search for the channel.</param>
        /// <param name="input">A string representing a channel by mention, id, or name.</param>
        /// <returns>The channel result with the highest score or <c>null</c> if no results exist.</returns>
        public static async Task<T> GetBestResultAsync(IGuild guild, string input)
        {
            TypeReaderResult result = await ReadAsync(guild, input);

            return GetBestResult(result);
        }

        /// <summary>
        /// Gets the best channel result for the given string.
        /// </summary>
        /// <param name="context">The context in which to search for the channel.</param>
        /// <param name="input">A string representing a channel by mention, id, or name.</param>
        /// <returns>The channel result with the highest score or <c>null</c> if no results exist.</returns>
        public static async Task<T> GetBestResultAsync(ICommandContext context, string input) =>
            await GetBestResultAsync(context.Guild, input);

        /// <summary>
        /// Gets the best channel result from the given results.
        /// </summary>
        /// <param name="result">The results of a parse.</param>
        /// <returns>The channel result with the highest score or <c>null</c> if no results exist.</returns>
        public static T GetBestResult(TypeReaderResult result)
        {
            if (result.IsSuccess)
                return result.Values.OrderByDescending(v => v.Score).First().Value as T;

            return null;
        }

        /// <summary>
        /// Adds a result to the given dictionary.
        /// </summary>
        /// <param name="results">The dictionary to which to add the result.</param>
        /// <param name="channel">The result's channel.</param>
        /// <param name="score">The result's score.</param>
        private static void AddResult(IDictionary<ulong, TypeReaderValue> results, T channel, float score)
        {
            if (channel != null && !results.ContainsKey(channel.Id))
                results.Add(channel.Id, new TypeReaderValue(channel, score));
        }
    }
}
