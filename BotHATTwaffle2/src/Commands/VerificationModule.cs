using System;
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
        public async Task VerifyAsync()
        {
            if (Context.Message.Channel.Id != _dataService.VerificationChannel.Id)
            {
                await Context.Message.DeleteAsync();
                var display = await ReplyAsync("You cannot use this command outside of the verfication channel.");
                await Task.Delay(5000);
                await display.DeleteAsync();
                return;
            }

            Random random = new Random();
            string query = "";
            int answer = 0;

            if (random.Next(1, 10001) == 10000)
            {
                switch (random.Next(0, 3))
                {
                    case 0:
                        query = "e^(iπ)+1";
                        answer = 0;
                        break;
                    case 1:
                        query = "2147483647 + 1";
                        answer = -2147483648;
                        break;
                    case 2:
                        query = "true + true";
                        answer = 2;
                        break;
                }
            }
            else
            {

                int lowRand = 0;
                int mathOperator = random.Next(0, 4);
                string mathOperatorString = null;
                //Don't let division by 0 happen
                if (mathOperator == 3)
                    lowRand = 1;

                int var1 = random.Next(lowRand, 10);
                int var2 = random.Next(lowRand, 10);
                
                //Make sure var1 is always higher because people can't handle negative numbers.
                if (var2 > var1)
                {
                    var temp = var1;
                    var1 = var2;
                    var2 = temp;
                }

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
                        mathOperatorString = "✕";
                        answer = var1 * var2;
                        break;
                    case 3: //thank Tanooki for this shit.
                        mathOperatorString = "÷";
                        int bigNumber = var1 * var2;
                        answer = bigNumber / var1;
                        //Swap some shit around for display purposes
                        var2 = var1;
                        var1 = bigNumber;
                        break;
                }
                query = $"{var1} {mathOperatorString} {var2}";
            }


            await ReplyAsync($"{Context.User.Mention} Please answer the following question:\n```{query} = ?```");
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
