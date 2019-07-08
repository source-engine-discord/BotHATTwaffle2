using System;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using BotHATTwaffle2.Handlers;
using BotHATTwaffle2.Services.SRCDS;
using Discord;
using Discord.Addons.Interactive;
using Discord.Commands;

namespace BotHATTwaffle2.Services.Playtesting
{
    class KickUserRcon
    {
        private readonly SocketCommandContext _context;
        private readonly InteractiveService _interactive;
        private readonly RconService _rconService;
        private readonly LogHandler _log;

        public KickUserRcon(SocketCommandContext context, InteractiveService interactive, RconService rconService, LogHandler log)
        {
            _context = context;
            _interactive = interactive;
            _rconService = rconService;
            _log = log;
        }

        /// <summary>
        /// Gets list of users from a test server and displays it in channel.
        /// You can then kick a user from the test server based on the list.
        /// </summary>
        /// <param name="serverAddress">Server address to get users from</param>
        /// <returns></returns>
        public async Task KickPlaytestUser(string serverAddress)
        {
            string description = null;

            //Get the raw status data
            string input = await _rconService.RconCommand(serverAddress, "status");

            //Format the raw data into an array and do some cleanup
            var players = input.Replace('\r', '\0').Split('\n').Where(x => x.StartsWith("#")).Select(y => y.Trim('#').Trim()).ToList();

            //Remove the first and last index and they are a header and footer
            players.RemoveAt(0);
            players.RemoveAt(players.Count - 1);
            
            foreach (var player in players)
            {
                //Parse out the data from each line.
                string userId = player.Substring(0, player.IndexOf(' '));
                string name = Regex.Match(player, "\"([^\"]*)\"").Value.Trim('"');
                var steamId = Regex.Match(player, @"(STEAM_[\d]:[\d]:\d+)").Value;

                if (string.IsNullOrEmpty(steamId))
                    steamId = "BOT";

                description += $"[{userId}] **{name}** - `{steamId}`\n";
            }

            var embed = new EmbedBuilder()
                .WithAuthor("Type ID of player to kick, or exit to cancel")
                .WithColor(new Color(165, 55, 55)).WithDescription(description);

            var display = await _context.Channel.SendMessageAsync(embed: embed.Build());
            var choice = await _interactive.NextMessageAsync(_context, timeout:TimeSpan.FromSeconds(20));
            if (choice != null && !choice.Content.Equals("exit"))
            {
                string kickMessage = await _rconService.RconCommand(serverAddress, $"kickid {choice.Content}");
                await choice.DeleteAsync();

                embed.WithColor(new Color(55, 165, 55)).WithAuthor(kickMessage).WithDescription("");

                await display.ModifyAsync(x => x.Embed = embed.Build());

                await _log.LogMessage($"`{_context.User.Username}` kicked a user from a playtest.```{kickMessage}```");
                return;
            }

            await display.DeleteAsync();
            await choice.DeleteAsync();
        }
    }
}
