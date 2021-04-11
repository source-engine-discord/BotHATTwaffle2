using System;
using System.Threading.Tasks;
using BotHATTwaffle2.Services;
using BotHATTwaffle2.Util;
using Discord;
using Discord.WebSocket;
using FluentScheduler;

namespace BotHATTwaffle2.Handlers
{
    public class UserHandler
    {
        private readonly DiscordSocketClient _client;
        private readonly DataService _dataService;
        private readonly LogHandler _log;
        private readonly VerificationService _verificationService;

        public UserHandler(DataService data, DiscordSocketClient client, LogHandler log, VerificationService verificationService)
        {
            Console.WriteLine("Setting up UserHandler...");

            _dataService = data;
            _client = client;
            _log = log;
            _verificationService = verificationService;

            _client.UserJoined += UserJoinedEventHandler;
//            _client.UserLeft += UserLeftEventHandler;
        }

        private async Task UserJoinedEventHandler(SocketGuildUser user)
        {
            await _verificationService.GiveUnverifiedRole(user);

            /* Welcome message no longer used when we enabled the role gate.
            var message = _dataService.RSettings.General.WelcomeMessage;
            //Replace placeholders
            message = message.Replace("[USER]", user.Mention)
                .Replace("[WELCOME]", _dataService.WelcomeChannel.Mention);
            */

            //Ping the user in the rules channel and then delete it. This is just to get their attention.
            await _dataService.VerificationChannel.SendMessageAsync($"{user} has joined the server!");
            var userMention = await _dataService.VerificationRulesChannel.SendMessageAsync(user.Mention);
            await userMention.DeleteAsync();

            await _log.LogMessage($"USER JOINED {user}" +
                                  $"\nCreated At: {user.CreatedAt}" +
                                  $"\nJoined At: {user.JoinedAt}" +
                                  $"\nUser ID: {user.Id}");

            DatabaseUtil.AddJoinedUser(user.Id);
        }

//        private async Task UserLeftEventHandler(SocketGuildUser user)
//        {
//            
//        }

        
    }
}