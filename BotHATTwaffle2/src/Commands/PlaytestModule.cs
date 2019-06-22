using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using BotHATTwaffle2.Handlers;
using BotHATTwaffle2.Models.LiteDB;
using BotHATTwaffle2.Services;
using BotHATTwaffle2.Services.Playtesting;
using BotHATTwaffle2.Services.Steam;
using BotHATTwaffle2.src.Handlers;
using Discord;
using Discord.Addons.Interactive;
using Discord.Commands;
using Discord.WebSocket;
using FluentScheduler;

namespace BotHATTwaffle2.Commands
{
    public class PlaytestModule : InteractiveBase
    {
        private readonly DiscordSocketClient _client;
        private readonly DataService _dataService;
        private readonly ReservationService _reservationService;
        private readonly InteractiveService _interactive;
        private readonly LogHandler _log;

        public PlaytestModule(DiscordSocketClient client, DataService dataService,
            ReservationService reservationService,
            InteractiveService interactive, LogHandler log)
        {
            _client = client;
            _dataService = dataService;
            _reservationService = reservationService;
            _interactive = interactive;
            _log = log;
        }

        [Command("PublicServer")]
        [Alias("ps")]
        [RequireContext(ContextType.Guild)]
        [RequireUserPermission(ChannelPermission.UseExternalEmojis)]
        public async Task PublicTestStartAsync(
            [Summary("The server to reserve.")]
            string serverCode,
            [Optional][Summary("The ID of a Steam Workshop map for the server to host.")]
            string workshopId)
        {
            //Check if reservations can be made.
            if (!_reservationService.CanReserve)
            {
                await ReplyAsync(embed:new EmbedBuilder()
                    .WithAuthor("Cannot reserve servers at this time")
                    .WithDescription("Servers cannot be reserved 1 hour before the test, lasting until the test event is over.")
                    .WithColor(new Color(255,100,0))
                    .WithThumbnailUrl(_dataService.Guild.IconUrl)
                    .Build());

                return;
            }

            //Check if the user already has a reservation
            var hasServer = DatabaseHandler.GetTestServerFromReservationUserId(Context.User.Id);
            if (hasServer != null)
            {
                var hasServerEmbed = new EmbedBuilder()
                    .WithAuthor("You already have a server reservation", _dataService.Guild.IconUrl)
                    .WithDescription($"")
                    .WithColor(new Color(165, 55, 55));

                hasServerEmbed.AddField("Connect With", $"`connect {hasServer.Address}; password {_dataService.RSettings.General.CasualPassword}`", true);
                await ReplyAsync(embed: hasServerEmbed.Build());
                return;
            }

            //Attempt add, see if successful
            var success = DatabaseHandler.AddServerReservation(new ServerReservation
            {
                UserId = Context.User.Id,
                ServerId = _dataService.GetServerCode(serverCode),
                StartTime = DateTime.Now,
                Announced = false
            });

            //Failed to add, let user know why
            if (!success)
            {
                await ReplyAsync(embed: new EmbedBuilder()
                    .WithAuthor("Could not reserve server")
                    .WithDescription($"A reservation on `{serverCode}` could not be made. Someone else already has" +
                                     $" it reserved, or it does not exist.\n\nType `>Servers` to see all servers.\n" +
                                     $"Type `>ShowReservations` to see all active reservations.")
                    .WithColor(new Color(165, 55, 55))
                    .WithThumbnailUrl(_dataService.Guild.IconUrl)
                    .Build());
                
                return;
            }

            //Add the job to release the server
            JobManager.AddJob(() => _reservationService.ReleaseServer(Context.User.Id, "The reservation has expired."), s => s
                .WithName($"[TSRelease_{serverCode}_{Context.User.Id}]").ToRunOnceIn(2).Hours());

            var server = DatabaseHandler.GetTestServerFromReservationUserId(Context.User.Id);

            string rconCommand = $"sv_password {_dataService.RSettings.General.CasualPassword}";

            if (workshopId != null)
                rconCommand += $"; host_workshop_map {workshopId}";

            await _dataService.RconCommand(server.Address, rconCommand);

            var embed = new EmbedBuilder()
                .WithAuthor($"{server.Address} is reserved for 2 hours!",
                    Context.User.GetAvatarUrl())
                .WithDescription("Use the commands below to run your community test!" +
                                 "\nIf you didn't include a workshop ID, change to your level using:" +
                                 "\n`>PC host_workshop_map [ID]` Example:\n`>PC host_workshop_map 267340686`")
                .WithColor(new Color(55, 165, 55))
                .WithThumbnailUrl(_dataService.Guild.IconUrl);
            embed.AddField("Connect With", $"`connect {server.Address}; password {_dataService.RSettings.General.CasualPassword}`", true);
            embed.AddField("To Send Commands", "`>PC [command]` or\n`>PublicCommand [command]`", true);
            embed.AddField("View Allowed Commands", "`>PC` or\n`>PublicCommand`", true);
            embed.AddField("Mention Community Testers", "`>PA` or\n`>PublicAnnounce`", true);
            embed.AddField("View Remaining Time", "`>SR` or\n`>ShowReservations`", true);
            embed.AddField("End Reservation Early", "`>RS` or\n`>ReleaseServer`", true);
            
            await ReplyAsync(embed: embed.Build());
        }

        [Command("PublicAnnounce")]
        [Alias("pa")]
        [RequireContext(ContextType.Guild)]
        [RequireUserPermission(ChannelPermission.UseExternalEmojis)]
        public async Task PublicAnnounceAsync()
        {
            //Check if the user already has a reservation
            var reservation = DatabaseHandler.GetServerReservation(Context.User.Id);
            if (reservation == null)
            {
                await ReplyAsync(embed: new EmbedBuilder()
                    .WithAuthor("You don't have a server reservation", _dataService.Guild.IconUrl)
                    .WithDescription($"Get one using the `>PublicServer` command.")
                    .WithColor(new Color(165, 55, 55)).Build());

                return;
            }

            var server = DatabaseHandler.GetTestServer(reservation.ServerId);

            string mention = null;
            if (!reservation.Announced)
            {
                await _dataService.CommunityTesterRole.ModifyAsync(x => { x.Mentionable = true; });
                mention = _dataService.CommunityTesterRole.Mention;
                DatabaseHandler.UpdateAnnouncedServerReservation(Context.User.Id);
            }

            string reply = await _dataService.RconCommand(server.Address, "host_map");
            reply = reply.Substring(14, reply.IndexOf(".bsp", StringComparison.Ordinal) - 14);
            string[] result = reply.Split('/');

            var embed = new EmbedBuilder();

            Console.WriteLine(string.Join("\n", result));

            if (result.Length == 3)
            {
                reply = result[2];
                embed = await new Workshop().HandleWorkshopEmbeds(Context.Message, _dataService, inputId: result[1]);
            }

            await _dataService.TestingChannel.SendMessageAsync($"{mention} {Context.User.Mention} " +
             $"needs players to help test `{reply}`\nYou can join using: `connect {server.Address}`",embed: embed.Build());

            if (!reservation.Announced)
                await _dataService.CommunityTesterRole.ModifyAsync(x => { x.Mentionable = false; });
        }

        [Command("PublicCommand", RunMode = RunMode.Async)]
        [Alias("pc")]
        [RequireContext(ContextType.Guild)]
        [RequireUserPermission(ChannelPermission.UseExternalEmojis)]
        public async Task PublicCommandAsync([Optional][Remainder]string command)
        {
            //Allow viewing of commands without a reservation
            if (command == null)
            {
                StringBuilder sv = new StringBuilder();
                StringBuilder mp = new StringBuilder();
                StringBuilder bot = new StringBuilder();
                StringBuilder exec = new StringBuilder();
                StringBuilder misc = new StringBuilder();

                foreach (var s in _dataService.RSettings.Lists.PublicCommands)
                {
                    if (s.StartsWith("sv", StringComparison.OrdinalIgnoreCase))
                    {
                        sv.AppendLine(s);
                        continue;
                    }
                    if (s.StartsWith("mp", StringComparison.OrdinalIgnoreCase))
                    {
                        mp.AppendLine(s);
                        continue;
                    }
                    if (s.StartsWith("bot", StringComparison.OrdinalIgnoreCase))
                    {
                        bot.AppendLine(s);
                        continue;
                    }
                    if (s.StartsWith("exec", StringComparison.OrdinalIgnoreCase) || s.StartsWith("game", StringComparison.OrdinalIgnoreCase))
                    {
                        exec.AppendLine(s);
                        continue;
                    }
                    misc.AppendLine(s);
                }

                var commandsEmbed = new EmbedBuilder()
                    .WithAuthor("Allowed test server commands", _dataService.Guild.IconUrl)
                    .WithColor(new Color(55, 55, 165));

                commandsEmbed.AddField("SV Commands", sv,true);
                commandsEmbed.AddField("MP Commands", mp, true);
                commandsEmbed.AddField("BOT Commands", bot, true);
                commandsEmbed.AddField("Game Mode Commands", exec, true);
                commandsEmbed.AddField("Other Commands", misc, true);

                await ReplyAsync(embed: commandsEmbed.Build());

                return;
            }

            //Check if the user already has a reservation
            var server = DatabaseHandler.GetTestServerFromReservationUserId(Context.User.Id);
            if (server == null)
            {
                await ReplyAsync(embed: new EmbedBuilder()
                    .WithAuthor("You don't have a server reservation", _dataService.Guild.IconUrl)
                    .WithDescription($"Get one using the `>PublicServer` command.")
                    .WithColor(new Color(165, 55, 55)).Build());

                return;
            }

            if (Context.Message.Content.Contains(';'))
            {
                await ReplyAsync(embed: new EmbedBuilder()
                    .WithAuthor("Invalid command!", _dataService.Guild.IconUrl)
                    .WithDescription($"Commands cannot contain the `;` character.")
                    .WithColor(new Color(165, 55, 55)).Build());
                return;
            }

            if (_dataService.RSettings.Lists.PublicCommands.Any(x =>
                Context.Message.Content.Contains(x, StringComparison.OrdinalIgnoreCase)))
            {
                if (command.Equals("kick", StringComparison.OrdinalIgnoreCase))
                {
                    var kick = new KickUserRcon(Context, _interactive, _dataService, _log);
                    await kick.KickPlaytestUser(server.Address);
                    return;
                }

                var result = await _dataService.RconCommand(server.Address, command);

                if (result.Length > 1000)
                    result = result.Substring(0, 1000) + "[OUTPUT OMITTED]";

                await ReplyAsync(embed: new EmbedBuilder()
                    .WithAuthor($"Command sent to {server.Address}", _dataService.Guild.IconUrl)
                    .WithDescription($"```{result}```")
                    .WithColor(new Color(55, 165, 55)).Build());
            }
            else
            {
                await ReplyAsync(embed: new EmbedBuilder()
                    .WithAuthor("Invalid command!", _dataService.Guild.IconUrl)
                    .WithDescription($"Type `>PublicCommand` to see what commands can be sent.")
                    .WithColor(new Color(165, 55, 55)).Build());
            }
        }

        [Command("ShowReservations")]
        [Alias("sr")]
        [RequireContext(ContextType.Guild)]
        [RequireUserPermission(ChannelPermission.UseExternalEmojis)]
        public async Task ShowReservationsAsync()
        {
            var embed = new EmbedBuilder()
                .WithAuthor($"Current Server Reservations",
                    _dataService.Guild.IconUrl)
                .WithColor(new Color(55, 165, 55));

            foreach (var serverReservation in DatabaseHandler.GetAllServerReservation())
            {
                string user = "" + serverReservation.UserId;

                try
                {
                    user = _dataService.Guild.GetUser(serverReservation.UserId).ToString();
                }
                catch
                {
                    //Can't get user, just display ID instead
                }

                TimeSpan timeLeft = serverReservation.StartTime.AddHours(2).Subtract(DateTime.Now);
                embed.AddField(DatabaseHandler.GetTestServer(serverReservation.ServerId).Address,
                    $"User: `{user}`\nTime Left: `{timeLeft:h\'H \'m\'M \'s\'S'}`");
            }

            if (embed.Fields.Count == 0)
                embed.AddField("No Server Reservations Active", $"Server reservations are currently " + 
                                                                (_reservationService.CanReserve ? "allowed" : "not allowed"));

            await ReplyAsync(embed:embed.Build());
        }

        [Command("ReleaseServer")]
        [Alias("rs")]
        [RequireContext(ContextType.Guild)]
        [RequireUserPermission(ChannelPermission.UseExternalEmojis)]
        public async Task ReleaseServerReservationAsync()
        {
            //Check if the user already has a reservation
            var hasServer = DatabaseHandler.GetTestServerFromReservationUserId(Context.User.Id);
            if (hasServer == null)
            {
                await ReplyAsync(embed: new EmbedBuilder()
                    .WithAuthor("You don't have a server reservation", _dataService.Guild.IconUrl)
                    .WithDescription($"Get one using the `>PublicServer` command.")
                    .WithColor(new Color(165, 55, 55)).Build());

                return;
            }

            await ReplyAsync(embed:_reservationService.ReleaseServer(Context.User.Id, $"{Context.User} has released the " +
                                                                                      $"server reservation manually."));
        }

        [Command("Servers")]
        [RequireContext(ContextType.Guild)]
        [Summary("Displays all playtest servers.")]
        public async Task ServersAsync()
        {
            var foundServers = DatabaseHandler.GetAllTestServers();
            var embed = new EmbedBuilder()
                .WithAuthor("Source Engine Discord CS:GO Test Servers")
                .WithFooter($"Total of {foundServers.Count()} servers.")
                .WithThumbnailUrl(_dataService.Guild.IconUrl)
                .WithColor(new Color(255,135,57));

            foreach (var server in foundServers)
            {
                embed.AddField(server.Address, server.Description, true);
            }

            await ReplyAsync(embed:embed.Build());
        }

        [Command("Playtester")]
        [RequireContext(ContextType.Guild)]
        [Summary("Join or leave playtest notifications.")]
        [Remarks("Toggles your subscription to playtest notifications.")]
        public async Task PlaytesterAsync()
        {
            if (((SocketGuildUser)Context.User).Roles.Contains(_dataService.PlayTesterRole))
            {
                await ReplyAsync($"Sorry to see you go from playtest notifications {Context.User.Mention}!");
                await ((SocketGuildUser)Context.User).RemoveRoleAsync(_dataService.PlayTesterRole);
            }
            else
            {
                await ReplyAsync($"Thanks for subscribing to playtest notifications {Context.User.Mention}!");
                await ((SocketGuildUser)Context.User).AddRoleAsync(_dataService.PlayTesterRole);
            }
        }
    }
}
