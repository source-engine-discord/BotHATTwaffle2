﻿using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using BotHATTwaffle2.Services;
using BotHATTwaffle2.Util;
using Discord;
using Discord.Commands;
using Discord.WebSocket;
using Discord.Addons.Interactive;
using BotHATTwaffle2.Commands.Preconditions;

namespace BotHATTwaffle2.Commands
{
    public class VerificationModule : InteractiveBase
    {
        private readonly DiscordSocketClient _client;
        private readonly CommandService _commands;
        private readonly DataService _dataService;
        private readonly VerificationService _verificationService;
        private readonly InteractiveService _interactive;

        public VerificationModule(DiscordSocketClient client, CommandService commands, DataService data, VerificationService verificationService, InteractiveService interactive)
        {
            _client = client;
            _commands = commands;
            _dataService = data;
            _verificationService = verificationService;
            _interactive = interactive;
        }

        [Command("Verify", RunMode = RunMode.Async)]
        [Summary("User verification")]
        [RequireRole("Unverified")]
        public async Task VerifyAsync()
        {
            Random random = new Random();

            int var1 = random.Next(0, 10);
            int var2 = random.Next(0, 10);
            int mathOperator = random.Next(0, 3);
            int answer = 0;
            string mathOperatorString = null;
            switch (mathOperator)
            {
                case 0:
                    mathOperatorString = "+";
                    answer = var1 + var2;
                    break;
                case 1:
                    mathOperatorString = "-";
                    answer = var1 - var2;
                    break;
                case 2:
                    mathOperatorString = "*";
                    answer = var1 * var2;
                    break;
            }

            await ReplyAsync($"{Context.User.Mention} Please answer the following question: ```{var1} {mathOperatorString} {var2}```");
            var reply = await NextMessageAsync();

            if (reply == null)
            {
                return;
            }

            if (!Int32.TryParse(reply.Content, out var userResponse))
            {
                await ReplyAsync($"Invalid response from {Context.User.Mention}. Please run the verify command again.");
                return;
            }

            if (userResponse == answer)
            {
                await _verificationService.UserVerified(Context.User as SocketGuildUser);
                await ReplyAsync($"{Context.User.Mention} has been verified!");
                return;
            }

            await ReplyAsync($"Incorrect answer from {Context.User.Mention}. Please run the verify command again.");
        }
    }
}
