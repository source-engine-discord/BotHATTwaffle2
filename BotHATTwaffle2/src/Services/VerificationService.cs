using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using BotHATTwaffle2.Handlers;
using BotHATTwaffle2.Util;
using Discord;
using Discord.WebSocket;

namespace BotHATTwaffle2.Services
{
    public class VerificationService
    {
        private readonly DiscordSocketClient _client;
        private readonly DataService _dataService;
        private readonly LogHandler _log;
        public VerificationService(DataService data, DiscordSocketClient client, LogHandler log)
        {
            Console.WriteLine("Setting up Verification Service...");

            _dataService = data;
            _client = client;
            _log = log;
        }
        
        public async Task GiveUnverifiedRole(SocketGuildUser user)
        {
            await user.AddRoleAsync(_dataService.Unverified);
        }

        public async Task UserVerified(SocketGuildUser user)
        {
            await user.RemoveRoleAsync(_dataService.Unverified);
            await UserWelcomeMessage(user);
        }

        private async Task UserWelcomeMessage(SocketGuildUser user)
        {
            DatabaseUtil.RemoveJoinedUser(user.Id);

            if (_dataService.GetSocketGuildUser(user.Id) == null)
            {
                await _log.LogMessage(
                    $"Attempted to send welcome message to `{user.Username}` `{user.Id}` but they left the guild.");
                return;
            }

            try
            {
                await _log.LogMessage(
                    $"Welcomed `{user.Username}` `{user.Id}` at `{DateTime.Now}`");
                //await user.AddRoleAsync(_dataService.CSGOPlayTesterRole);
                //await user.AddRoleAsync(_dataService.TF2PlayTesterRole);

                await user.SendMessageAsync(embed: WelcomeEmbed(user));
            }
            catch
            {
                await _log.LogMessage(
                    $"Attempted to send welcome message to `{user.Username}` `{user.Id}`, but failed. " +
                    "They might have DMs off - I'll try in the BotChannel.");

                await _dataService.BotChannel.SendMessageAsync(user.Mention, embed: WelcomeEmbed(user));
            }
        }

        private Embed WelcomeEmbed(SocketGuildUser user)
        {
            var description =
                "Now that you are verified, there are a few things I wanted to tell you! Feel free to ask a question in " +
                $"any of the relevant channels you see. Just try to keep things on topic. Please take a few minutes to read {_dataService.WelcomeChannel.Mention} to learn all our rules." +
                "\n\n**Playtesting**\nWe run playtest for CSGO and TF2. You can manage notifications for these playtests by using the `>playtester` command. Type `>help playtester` for more details." +
                "\n\n**Skill Roles**\nThere are roles you can use to show what skills you have. To see what roles you can give yourself, type: `>roleme` in a DM with me, or in any channel." +
                "\n\nIf you want to see any of my commands, type: `>help`. Thanks for reading, and we hope you enjoy your stay here!";

            var embed = new EmbedBuilder()
                .WithAuthor($"Welcome, {user.Username}, to the Source Engine Discord!", user.GetAvatarUrl())
                .WithThumbnailUrl(_dataService.Guild.IconUrl)
                .WithColor(new Color(243, 128, 72))
                .WithTitle("Thanks for joining!")
                .WithDescription(description);

            return embed.Build();
        }
    }
}
