using System.Collections.Generic;
using Discord;
using Discord.Commands;

namespace BotHATTwaffle2.Services
{
    /// <summary>
    /// Contains utility functions for help commands.
    /// </summary>
    public interface IHelpService
    {
        /// <summary>
        /// Adds a field to an <paramref name="embed"/> containing the names and summaries of a <paramref name="module"/>'s
        /// commands.
        /// </summary>
        /// <param name="module">The field's module.</param>
        /// <param name="embed">The embed to which to add the field.</param>
        void AddModuleField(ModuleInfo module, ref EmbedBuilder embed);

        /// <summary>
        /// Retrieves the names of the required contexts from a command's preconditions.
        /// </summary>
        /// <param name="preconditions">The command's preconditions.</param>
        /// <returns>A newline-delimited string of the alphabetically sorted names of required contexts.</returns>
        string GetContexts(IEnumerable<PreconditionAttribute> preconditions);

        /// <summary>
        /// Formats the parameters of a command to be displayed in an embed.
        /// </summary>
        /// <param name="parameters">The command's parameters.</param>
        /// <rerturns>A newline-delimited string containing the command's parameters.</rerturns>
        string GetParameters(IReadOnlyCollection<ParameterInfo> parameters);

        /// <summary>
        /// Retrieves the names of the required permissions from a command's preconditions.
        /// </summary>
        /// <param name="preconditions">The command's preconditions.</param>
        /// <returns>A newline-delimited string of the alphabetically sorted names of required permissions.</returns>
        string GetPermissions(IEnumerable<PreconditionAttribute> preconditions);

        /// <summary>
        /// Retrieves the names of the required roles from a command's preconditions.
        /// </summary>
        /// <param name="preconditions">The command's preconditions.</param>
        /// <param name="context">The command's context.</param>
        /// <returns>A newline-delimited string of the alphabetically sorted names of required roles.</returns>
        string GetRoles(IEnumerable<PreconditionAttribute> preconditions, ICommandContext context);

        /// <summary>
        /// Creates a usage string for a command.
        /// </summary>
        /// <param name="command">The command for which to create a usage string.</param>
        /// <returns>The formatted usage string.</returns>
        string GetUsage(CommandInfo command);
    }
}
