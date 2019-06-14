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
    ///     Retrieves a role by parsing a string.
    /// </summary>
    /// Derived from https://github.com/RogueException/Discord.Net/blob/dev/src/Discord.Net.Commands/Readers/RoleTypeReader.cs
    /// <typeparam name="T">The role's type.</typeparam>
    public static class RoleTypeReader<T> where T : class, IRole
    {
        /// <summary>
        ///     Tries to parse a given string as a role.
        /// </summary>
        /// <param name="guild">The guild in which to search for the role.</param>
        /// <param name="input">A string representing a role by mention, id, or name.</param>
        /// <returns>The results of the parse.</returns>
        public static Task<TypeReaderResult> ReadAsync(IGuild guild, string input)
        {
            if (guild != null)
            {
                var results = new Dictionary<ulong, TypeReaderValue>();
                var roles = guild.Roles;
                ulong id;

                // By Mention (1.0)
                if (MentionUtils.TryParseRole(input, out id))
                    AddResult(results, guild.GetRole(id) as T, 1.00f);

                // By Id (0.9)
                if (ulong.TryParse(input, NumberStyles.None, CultureInfo.InvariantCulture, out id))
                    AddResult(results, guild.GetRole(id) as T, 0.90f);

                // By Name (0.7-0.8)
                // Acounts for name being null because GetrolesAsync returns categories in 1.0.
                foreach (var role in roles.Where(x => string.Equals(input, x.Name, StringComparison.OrdinalIgnoreCase)))
                    AddResult(results, role as T, role.Name == input ? 0.80f : 0.70f);

                if (results.Count > 0)
                    return Task.FromResult(TypeReaderResult.FromSuccess(results.Values));
            }

            return Task.FromResult(TypeReaderResult.FromError(CommandError.ObjectNotFound, "Role not found."));
        }

        /// <summary>
        ///     Gets the best role result for the given string.
        /// </summary>
        /// <param name="guild">The guild in which to search for the role.</param>
        /// <param name="input">A string representing a role by mention, id, or name.</param>
        /// <returns>The role result with the highest score or <c>null</c> if no results exist.</returns>
        public static async Task<T> GetBestResultAsync(IGuild guild, string input)
        {
            var result = await ReadAsync(guild, input);

            return GetBestResult(result);
        }

        /// <summary>
        ///     Gets the best role result for the given string.
        /// </summary>
        /// <param name="context">The context in which to search for the role.</param>
        /// <param name="input">A string representing a role by mention, id, or name.</param>
        /// <returns>The role result with the highest score or <c>null</c> if no results exist.</returns>
        public static async Task<T> GetBestResultAsync(ICommandContext context, string input)
        {
            return await GetBestResultAsync(context.Guild, input);
        }

        /// <summary>
        ///     Gets the best role result from the given results.
        /// </summary>
        /// <param name="result">The results of a parse.</param>
        /// <returns>The role result with the highest score or <c>null</c> if no results exist.</returns>
        public static T GetBestResult(TypeReaderResult result)
        {
            if (result.IsSuccess)
                return result.Values.OrderByDescending(v => v.Score).First().Value as T;

            return null;
        }

        /// <summary>
        ///     Adds a result to the given dictionary.
        /// </summary>
        /// <param name="results">The dictionary to which to add the result.</param>
        /// <param name="role">The result's role.</param>
        /// <param name="score">The result's score.</param>
        private static void AddResult(IDictionary<ulong, TypeReaderValue> results, T role, float score)
        {
            if (role != null && !results.ContainsKey(role.Id))
                results.Add(role.Id, new TypeReaderValue(role, score));
        }
    }
}
