using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using BotHATTwaffle2.Models.LiteDB;
using BotHATTwaffle2.Services;
using BotHATTwaffle2.Util;
using Discord;
using Discord.WebSocket;

namespace BotHATTwaffle2.Handlers
{
    public class BlacklistHandler
    {
        private readonly List<Blacklist> _blacklist;
        private readonly SocketMessage _message;
        private readonly DataService _dataService;
        public BlacklistHandler(List<Blacklist> blacklist, SocketMessage message, DataService dataService)
        {
            _blacklist = blacklist;
            _message = message;
            _dataService = dataService;
        }
        /// <summary>
        /// Checks if a message matches in the blacklist.
        /// </summary>
        /// <returns>Returned true if the message matches a blacklist entry.</returns>
        public bool CheckBlacklist()
        {
            var blCheckResult = LevelOneBlacklistCheck();
            
            if (blCheckResult == null)
                return false;

            //TODO: Implement "Fuzzy" matching with regex as a "Level 2" check

            ApplyMute(blCheckResult);
            return true;
        }

        /// <summary>
        /// Does a rough check on the blacklist to see if anything directly matches
        /// </summary>
        /// <returns>Returned null if no match is found, else returns the matching word</returns>
        private Blacklist LevelOneBlacklistCheck()
        {
            foreach (var blacklist in _blacklist)
            {
                if (_message.Content.Contains(blacklist.Word, StringComparison.OrdinalIgnoreCase))
                    return blacklist;
            }
            return null;
        }

        private async void ApplyMute(Blacklist blacklist)
        {
            //Warn users for messages with no auto mute duration
            if (blacklist.AutoMuteDuration == 0)
            {
                var message = await _message.Channel.SendMessageAsync(embed: new EmbedBuilder()
                    .WithAuthor($"{_message.Author.Username}")
                    .WithDescription($"Your message has been deleted because it matched an entry in our blacklist." +
                                     $"\nPlease ask a staff member if you're unsure why.")
                    .WithColor(new Color(165, 55, 55))
                    .Build());

                await Task.Delay(10000);
                await message.DeleteAsync();
                return;
            }

            await _dataService.VoidChannel.SendMessageAsync(embed: new EmbedBuilder()
                .WithAuthor($"{_message.Author} | {_message.Author.Id} has been muted")
                .WithDescription(
                    $"**BLACKLIST VIOLATION** `{blacklist.Word}` resulted in auto mute for `{blacklist.AutoMuteDuration}` minutes. Their message was:\n`{_message.Content}`")
                .WithColor(new Color(165, 55, 55))
                .Build());

            if (blacklist.AutoMuteDuration >= 43200)
                await _dataService.AdminChannel.SendMessageAsync($"The blacklist just had a critical match - Likely a scammer. Please check {_dataService.VoidChannel.Mention}");

            await _dataService.MuteUser((SocketGuildUser)_message.Author,
                TimeSpan.FromMinutes(blacklist.AutoMuteDuration),
                $"BLACKLIST VIOLATION [{blacklist.Word}]", _message);
        }
    }
}
