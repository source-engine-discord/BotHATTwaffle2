using System;
using System.Linq;
using System.Threading.Tasks;
using BotHATTwaffle2.Services;
using BotHATTwaffle2.Util;
using Discord;
using Discord.WebSocket;
using FluentScheduler;

namespace BotHATTwaffle2.Handlers
{
    internal class UserHandler
    {
        private readonly DiscordSocketClient _client;
        private readonly DataService _dataService;
        private readonly LogHandler _log;

        public UserHandler(DataService data, DiscordSocketClient client, LogHandler log)
        {
            Console.WriteLine("Setting up UserHandler...");

            _dataService = data;
            _client = client;
            _log = log;

            _client.UserJoined += UserJoinedEventHandler;
//            _client.UserLeft += UserLeftEventHandler;
        }

        private async Task UserJoinedEventHandler(SocketGuildUser user)
        {
            var message = _dataService.RSettings.General.WelcomeMessage;

            //Replace placeholders
            message = message.Replace("[USER]", user.Mention)
                .Replace("[WELCOME]", _dataService.WelcomeChannel.Mention);

            await _dataService.GeneralChannel.SendMessageAsync(message);

            await _log.LogMessage($"USER JOINED {user}\nI will apply a roles at {DateTime.Now.AddMinutes(10)}." +
                                                         $" They will then have playtester and can talk." +
                                                           $"\nCreated At: {user.CreatedAt}" +
                                                           $"\nJoined At: {user.JoinedAt}" +
                                                           $"\nUser ID: {user.Id}");


            DatabaseUtil.AddJoinedUser(user.Id);

            JobManager.AddJob(async () => await UserWelcomeMessage(user), s => s
                .WithName($"[UserJoin_{user.Id}]").ToRunOnceAt(DateTime.Now.AddMinutes(10)));
        }

//        private async Task UserLeftEventHandler(SocketGuildUser user)
//        {
//            
//        }

        public async Task UserWelcomeMessage(SocketGuildUser user)
        {
            try
            {
                await _log.LogMessage($"Welcomed {user.Username} at {DateTime.Now}, and assigning them the Playtester role!");
                await user.AddRoleAsync(_dataService.PlayTesterRole);
                await user.SendMessageAsync(embed:WelcomeEmbed(user));
            }
            catch
            {
                await _log.LogMessage($"Attempted to send welcome message to {user.Username}, but failed. " +
                                      $"They either have DMs off, or left the server.");
            }
            DatabaseUtil.RemoveJoinedUser(user.Id);
        }

        private Embed WelcomeEmbed(SocketGuildUser user)
        {
            string description = $"Now that the verification time has ended, there are a few things I wanted to tell you! Feel free to ask a question in " +
                                 $"any of the relevant channels you see. Just try to keep things on topic. Please spend a few minutes to read {_dataService.WelcomeChannel.Mention} to learn all our rules." +
                                 $"\n\nAdditionally, you've been given a role called `Playtester`. This role is used to notify you when we have a playtest starting. You can remove yourself from the " +
                                 $"notifications by typing: `>playtester` in a DM with me, or in any channel." +
                                 $"\n\nIf you want to see any of my commands, type: `>help`. Thanks for reading, and we hope you enjoy your stay here!" +
                                 $"\n\nThere are roles you can use to show what skills you have. To see what roles you can give yourself, type: `>roleme`" +
                                 $" in a DM with me, or in any channel." +
                                 $"\n\nGLHF, and enjoy your stay.";

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