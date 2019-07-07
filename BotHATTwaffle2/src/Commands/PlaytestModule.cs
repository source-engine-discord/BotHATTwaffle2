using System;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using BotHATTwaffle2.Handlers;
using BotHATTwaffle2.Models.LiteDB;
using BotHATTwaffle2.Services;
using BotHATTwaffle2.Services.Calendar;
using BotHATTwaffle2.Services.Playtesting;
using BotHATTwaffle2.Services.SRCDS;
using BotHATTwaffle2.Services.Steam;
using BotHATTwaffle2.Util;
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
        private readonly InteractiveService _interactive;
        private readonly LogHandler _log;
        private readonly ReservationService _reservationService;
        private readonly GoogleCalendar _calendar;
        private readonly PlaytestService _playtestService;
        private readonly RconService _rconService;
        private const ConsoleColor LOG_COLOR = ConsoleColor.Magenta;

        public PlaytestModule(DiscordSocketClient client, DataService dataService,
            ReservationService reservationService, RconService rconService,
            InteractiveService interactive, LogHandler log, GoogleCalendar calendar, PlaytestService playtestService)
        {
            _playtestService = playtestService;
            _client = client;
            _dataService = dataService;
            _reservationService = reservationService;
            _interactive = interactive;
            _log = log;
            _calendar = calendar;
            _rconService = rconService;
        }

        [Command("Schedule", RunMode = RunMode.Async)]
        [Alias("pts")]
        [Summary("Allows users to view testing queue and schedule.")]
        [Remarks("For members, displays test in the queue and scheduled on the calendar." +
                 "If you're moderation staff, allows for officially scheduling the playtest event after making any needed changes.")]
        public async Task ScheduleTestAsync([Summary("If `true`, displays scheduled tests as well.")][Optional]bool getAll)
        {
            var embed = await _playtestService.GetUpcomingEvents(true, getAll);
            var display = await ReplyAsync(embed: embed.Build());

            var user = _dataService.GetSocketGuildUser(Context.User.Id);

            if (user.Roles.Contains(_dataService.ModeratorRole)
            || user.Roles.Contains(_dataService.AdminRole))
            {
                _dataService.IgnoreListenList.Add(Context.User);

                var requestBuilder = new RequestBuilder(Context, _interactive, _dataService, _log, _calendar, _playtestService);
                await requestBuilder.SchedulePlaytestAsync(display);

                _dataService.IgnoreListenList.Remove(Context.User);
            }
        }

        [Command("Request", RunMode = RunMode.Async)]
        [Alias("ptr")]
        [Summary("Requests a playtest event.")]
        [Remarks("Creates a playtest request using an interactive system. To start, type `>Request`\n\n" +
                 "If you have a filled out template, you can send that with the command to skip the interactive builder. " +
                 "An example of the filled out template is on the playtesting webpage.\n\nTemplate:" +
                 "```>Request Date:\nEmails:\nMapName:\nDiscord:\nImgur:\nWorkshop:\nType:\nDescription:\nSpawns:\nPreviousTest:\nServer:```")]
        public async Task PlaytestRequestAsync([Summary("A pre-built playtest event based on the template.")][Optional][Remainder]string playtestInformation)
        {
            _dataService.IgnoreListenList.Add(Context.User);
            var requestBuilder = new RequestBuilder(Context, _interactive, _dataService, _log, _calendar, _playtestService);

            if (playtestInformation != null)
            {
                //If we are here from a full dump, split it to handle
                string[] split = playtestInformation.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);

                if (split.Length != 11)
                {
                    await ReplyAsync("Invalid bulk playtest quest submission. Consult the help documents.");
                    return;
                }

                await requestBuilder.BuildPlaytestRequestBulk(split);
            }
            else
            {
                var upcoming = await _playtestService.GetUpcomingEvents(true, true);
                await ReplyAsync(embed: upcoming.Build());
                await requestBuilder.BuildPlaytestRequestWizard();
            }

            _dataService.IgnoreListenList.Remove(Context.User);
        }

        [Command("PublicServer")]
        [Alias("ps")]
        [Summary("Reserves a public test server.")]
        [Remarks("Reserves a public test server using a server address/code. To see all servers type `>servers`. " +
                 "Example: `>ps vpn`\nYou may also include a workshop ID to automatically load that level on the server as well. " +
                 "Example: `>ps vpn 267340686`")]
        [RequireContext(ContextType.Guild)]
        [RequireUserPermission(ChannelPermission.UseExternalEmojis)]
        public async Task PublicTestStartAsync(
            [Summary("The server to reserve")] string serverCode,
            [Optional][Summary("The ID of a Steam Workshop map for the server to host")]
            string workshopId)
        {
            //Check if reservations can be made.
            if (!_reservationService.CanReserve)
            {
                await ReplyAsync(embed: new EmbedBuilder()
                    .WithAuthor("Cannot reserve servers at this time")
                    .WithDescription(
                        "Servers cannot be reserved 1 hour before the test, lasting until the test event is over.")
                    .WithColor(new Color(255, 100, 0))
                    .WithThumbnailUrl(_dataService.Guild.IconUrl)
                    .Build());

                return;
            }

            //Check if the user already has a reservation
            var server = DatabaseUtil.GetTestServerFromReservationUserId(Context.User.Id);
            if (server != null)
            {
                var hasServerEmbed = new EmbedBuilder()
                    .WithAuthor("You already have a server reservation", _dataService.Guild.IconUrl)
                    .WithDescription("")
                    .WithColor(new Color(165, 55, 55));

                hasServerEmbed.AddField("Connect With",
                    $"`connect {server.Address}; password {_dataService.RSettings.General.CasualPassword}`", true);
                await ReplyAsync(embed: hasServerEmbed.Build());
                return;
            }

            string formattedServer = GeneralUtil.GetServerCode(serverCode);

            //Attempt add, see if successful
            var success = DatabaseUtil.AddServerReservation(new ServerReservation
            {
                UserId = Context.User.Id,
                ServerId = formattedServer,
                StartTime = DateTime.Now,
                Announced = false
            });

            //Failed to add, let user know why
            if (!success)
            {
                await ReplyAsync(embed: new EmbedBuilder()
                    .WithAuthor("Could not reserve server")
                    .WithDescription($"A reservation on `{serverCode}` could not be made. Someone else already has" +
                                     " it reserved, or it does not exist.\n\nType `>Servers` to see all servers.\n" +
                                     "Type `>ShowReservations` to see all active reservations.")
                    .WithColor(new Color(165, 55, 55))
                    .WithThumbnailUrl(_dataService.Guild.IconUrl)
                    .Build());

                return;
            }

            //Since we've inserted, get the new entry.
            server = DatabaseUtil.GetTestServerFromReservationUserId(Context.User.Id);

            //Add the job to release the server
            JobManager.AddJob(async () => await _dataService.TestingChannel.SendMessageAsync($"{Context.User.Mention}",
            embed: _reservationService.ReleaseServer(Context.User.Id, "The reservation has expired.")),
                s => s.WithName($"[TSRelease_{formattedServer}_{Context.User.Id}]").ToRunOnceIn(2).Hours());

            var rconCommand = $"sv_password {_dataService.RSettings.General.CasualPassword}";

            if (workshopId != null)
                rconCommand += $"; host_workshop_map {workshopId}";

            _ = _rconService.RconCommand(server.Address, rconCommand);

            var embed = new EmbedBuilder()
                .WithAuthor($"{server.Address} is reserved for 2 hours!",
                    Context.User.GetAvatarUrl())
                .WithDescription("Use the commands below to run your community test!" +
                                 "\nIf you didn't include a workshop ID, change to your level using:" +
                                 "\n`>PC host_workshop_map [ID]` Example:\n`>PC host_workshop_map 267340686`")
                .WithColor(new Color(55, 165, 55))
                .WithThumbnailUrl(_dataService.Guild.IconUrl);
            embed.AddField("Connect With",
                $"`connect {server.Address}; password {_dataService.RSettings.General.CasualPassword}`", true);
            embed.AddField("To Send Commands", "`>PC [command]` or\n`>PublicCommand [command]`", true);
            embed.AddField("View Allowed Commands", "`>PC` or\n`>PublicCommand`", true);
            embed.AddField("Mention Community Testers", "`>PA` or\n`>PublicAnnounce`", true);
            embed.AddField("View Remaining Time", "`>SR` or\n`>ShowReservations`", true);
            embed.AddField("End Reservation Early", "`>RS` or\n`>ReleaseServer`", true);

            await ReplyAsync(embed: embed.Build());

            await _log.LogMessage($"`{Context.User}` `{Context.User.Id}` has reserved `{server.Address}`", color:LOG_COLOR);
        }

        [Command("PublicAnnounce")]
        [Alias("pa")]
        [Summary("Announces that you are looking for playtesters.")]
        [Remarks("Mentions the Community Tester role that you are looking for testers for your level. Community " +
                 "Testers will only be mentioned the first time you use to command, this is to prevent spam.")]
        [RequireContext(ContextType.Guild)]
        [RequireUserPermission(ChannelPermission.UseExternalEmojis)]
        public async Task PublicAnnounceAsync()
        {
            //Check if the user already has a reservation
            var reservation = DatabaseUtil.GetServerReservation(Context.User.Id);
            if (reservation == null)
            {
                await ReplyAsync(embed: new EmbedBuilder()
                    .WithAuthor("You don't have a server reservation", _dataService.Guild.IconUrl)
                    .WithDescription("Get one using the `>PublicServer` command.")
                    .WithColor(new Color(165, 55, 55))
                    .Build());

                return;
            }

            var server = DatabaseUtil.GetTestServer(reservation.ServerId);

            string mention = null;
            if (!reservation.Announced)
            {
                await _dataService.CommunityTesterRole.ModifyAsync(x => { x.Mentionable = true; });
                mention = _dataService.CommunityTesterRole.Mention;
                DatabaseUtil.UpdateAnnouncedServerReservation(Context.User.Id);
            }

            var result = await _playtestService.GetRunningLevelAsync(server.Address);

            var embed = new EmbedBuilder();

            //Length 3 means workshop
            if (result.Length == 3)
            {
                embed = await new Workshop().HandleWorkshopEmbeds(Context.Message, _dataService, inputId: result[1]);

                await _dataService.TestingChannel.SendMessageAsync($"{mention} {Context.User.Mention} " +
                                                                   $"needs players to help test `{result[2]}`\nYou can join using: `connect {server.Address}; password {_dataService.RSettings.General.CasualPassword}`" +
                                                                   $"\nType `>roleme Community Tester` to get this role.",
                    embed: embed.Build());
            }
            else
            {
                //No embed
                await _dataService.TestingChannel.SendMessageAsync($"{mention} {Context.User.Mention} " +
                                                                   $"needs players to help test `{result[0]}`\nYou can join using: `connect {server.Address}; password {_dataService.RSettings.General.CasualPassword}`");
            }

            if (!reservation.Announced)
                await _dataService.CommunityTesterRole.ModifyAsync(x => { x.Mentionable = false; });

            await _log.LogMessage($"`{Context.User}` `{Context.User.Id}` alerted for their community playtest on `{server.Address}`", color: LOG_COLOR);
        }

        [Command("PublicDemo", RunMode = RunMode.Async)]
        [Alias("pd")]
        [Summary("Starts or stops demo recording in a public test")]
        [Remarks("Starts recording a GOTV demo in a public test. To start recording, type `>PublicDemo start`. " +
                 "Make sure the desired gamemode is active before starting a recording!\n\nOnce you're done recording " +
                 "type `>PublicDemo stop` to stop the recording and download it from the server.")]
        [RequireContext(ContextType.Guild)]
        [RequireUserPermission(ChannelPermission.UseExternalEmojis)]
        public async Task PublicDemoAsync(string command)
        {
            var reservation = DatabaseUtil.GetServerReservation(Context.User.Id);
            if (reservation == null)
            {
                await ReplyAsync(embed: new EmbedBuilder()
                    .WithAuthor("You don't have a server reservation", _dataService.Guild.IconUrl)
                    .WithDescription("Get one using the `>PublicServer` command.")
                    .WithColor(new Color(165, 55, 55))
                    .Build());
                return;
            }

            var levelInfo = await _playtestService.GetRunningLevelAsync(reservation.ServerId);
            if (levelInfo.Length != 3)
            {
                await ReplyAsync(embed: new EmbedBuilder()
                    .WithAuthor("Unable to use PublicDemo", _dataService.Guild.IconUrl)
                    .WithDescription("You can only record playtest demos on workshop levels.")
                    .WithColor(new Color(165, 55, 55))
                    .Build());
                return;
            }

            //Populate a test event that will get sent to the download.
            var testInfo = new PlaytestCommandInfo
            {
                DemoName = $"{levelInfo[2]}_Community",
                WorkshopId = levelInfo[1],
                ServerAddress = reservation.ServerId,
            };

            switch (command.ToLower())
            {
                case"start":
                    var demoReply = await _rconService.RconCommand(testInfo.ServerAddress, $"tv_stoprecord; tv_record {testInfo.DemoName}" +
                                                                                           $";say {testInfo.DemoName} now recording!");
                    await ReplyAsync(embed: new EmbedBuilder()
                        .WithAuthor($"Command sent to {testInfo.ServerAddress}", _dataService.Guild.IconUrl)
                        .WithDescription($"```{demoReply}```")
                        .WithColor(new Color(55, 165, 55)).Build());
                    break;

                case "stop":
                    var stopReply = await _rconService.RconCommand(testInfo.ServerAddress, $"tv_stoprecord;say {testInfo.DemoName} stopped recording!");

                    //Download demo, don't wait.
                    _ = Task.Run(() =>
                    {
                        DownloadHandler.DownloadPlaytestDemo(testInfo);
                    });
                    
                    const string demoUrl = "http://demos.tophattwaffle.com";
                    var embed = new EmbedBuilder()
                        .WithAuthor($"Download playtest demo for {testInfo.DemoName}", _dataService.Guild.IconUrl, demoUrl)
                        .WithThumbnailUrl(_client.CurrentUser.GetAvatarUrl())
                        .WithColor(new Color(243, 128, 72))
                        .WithDescription(
                            $"[Download Demo Here]({demoUrl}) | [Playtesting Information](https://www.tophattwaffle.com/playtesting/)");

                    await _dataService.TestingChannel.SendMessageAsync(Context.User.Mention, embed: embed.Build());
                    break;

                default:
                    await ReplyAsync("Invalid command. Consult `>Help PublicDemo`");
                    break;
            }

        }

        [Command("PublicCommand", RunMode = RunMode.Async)]
        [Alias("pc")]
        [Summary("Sends commands to your reserved server.")]
        [Remarks("Send server commands to your server. For security reasons only certain commands are allowed. To" +
                 " see the list of commands, type `>pc`. To send a command type `>pc [command]` Example: `pc sv_cheats 1`. " +
                 "If you believe another command should be added, ask TopHATTwaffle.")]
        [RequireContext(ContextType.Guild)]
        [RequireUserPermission(ChannelPermission.UseExternalEmojis)]
        public async Task PublicCommandAsync([Optional] [Remainder] string command)
        {
            //Allow viewing of commands without a reservation
            if (command == null)
            {

                StringBuilder sv = new StringBuilder(),mp = new StringBuilder(), bot = new StringBuilder(), exec = new StringBuilder(), misc = new StringBuilder();

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

                    if (s.StartsWith("exec", StringComparison.OrdinalIgnoreCase) ||
                        s.StartsWith("game", StringComparison.OrdinalIgnoreCase))
                    {
                        exec.AppendLine(s);
                        continue;
                    }

                    misc.AppendLine(s);
                }
                

                await ReplyAsync(embed: new EmbedBuilder()
                    .WithAuthor("Allowed test server commands", _dataService.Guild.IconUrl)
                    .WithColor(new Color(55, 55, 165))
                    .AddField("SV Commands", sv, true)
                    .AddField("MP Commands", mp, true)
                    .AddField("BOT Commands", bot, true)
                    .AddField("Game Mode Commands", exec, true)
                    .AddField("Other Commands", misc, true).Build());

                return;
            }

            //Check if the user already has a reservation
            var server = DatabaseUtil.GetTestServerFromReservationUserId(Context.User.Id);
            if (server == null)
            {
                await ReplyAsync(embed: new EmbedBuilder()
                    .WithAuthor("You don't have a server reservation", _dataService.Guild.IconUrl)
                    .WithDescription("Get one using the `>PublicServer` command.")
                    .WithColor(new Color(165, 55, 55)).Build());

                return;
            }

            if (Context.Message.Content.Contains(';'))
            {
                await ReplyAsync(embed: new EmbedBuilder()
                    .WithAuthor("Invalid command!", _dataService.Guild.IconUrl)
                    .WithDescription("Commands cannot contain the `;` character.")
                    .WithColor(new Color(165, 55, 55)).Build());
                return;
            }

            if (_dataService.RSettings.Lists.PublicCommands.Any(x =>
                Context.Message.Content.Contains(x, StringComparison.OrdinalIgnoreCase)))
            {
                if (command.Equals("kick", StringComparison.OrdinalIgnoreCase))
                {
                    var kick = new KickUserRcon(Context, _interactive, _rconService, _log);
                    await kick.KickPlaytestUser(server.Address);
                    return;
                }

                string reply;
                IUserMessage delayed = null;
                var rconCommand = _rconService.RconCommand(server.Address, command);
                var waiting = Task.Delay(2000);
                if (rconCommand == await Task.WhenAny(rconCommand, waiting))
                {
                    reply = await rconCommand;
                }
                else
                {
                    delayed = await ReplyAsync(embed: new EmbedBuilder()
                        .WithDescription($"⏰RCON command to `{server.Address}` is taking longer than normal...\nSit tight while I'll " +
                                         "try a few more times.")
                        .WithColor(new Color(165, 55, 55)).Build());
                    reply = await rconCommand;
                }

                await ReplyAsync(embed: new EmbedBuilder()
                    .WithAuthor($"Command sent to {server.Address}", _dataService.Guild.IconUrl)
                    .WithDescription($"```{reply}```")
                    .WithColor(new Color(55, 165, 55)).Build());

                if (delayed != null)
                    await delayed.DeleteAsync();
            }
            else
            {
                await ReplyAsync(embed: new EmbedBuilder()
                    .WithAuthor("Invalid command!", _dataService.Guild.IconUrl)
                    .WithDescription("Type `>PublicCommand` to see what commands can be sent.")
                    .WithColor(new Color(165, 55, 55)).Build());
            }
        }

        [Command("ShowReservations")]
        [Alias("sr")]
        [Summary("Shows all current server reservations with the time remaining.")]
        [RequireContext(ContextType.Guild)]
        [RequireUserPermission(ChannelPermission.UseExternalEmojis)]
        public async Task ShowReservationsAsync()
        {
            var embed = new EmbedBuilder()
                .WithAuthor("Current Server Reservations",
                    _dataService.Guild.IconUrl)
                .WithColor(new Color(55, 165, 55));

            foreach (var serverReservation in DatabaseUtil.GetAllServerReservation())
            {
                var user = "" + serverReservation.UserId;

                try
                {
                    user = _dataService.Guild.GetUser(serverReservation.UserId).ToString();
                }
                catch
                {
                    //Can't get user, just display ID instead
                }

                var timeLeft = serverReservation.StartTime.AddHours(2).Subtract(DateTime.Now);
                embed.AddField(DatabaseUtil.GetTestServer(serverReservation.ServerId).Address,
                    $"User: `{user}`\nTime Left: `{timeLeft:h\'H \'m\'M \'s\'S'}`");
            }

            if (embed.Fields.Count == 0)
                embed.AddField("No Server Reservations Active", "Server reservations are currently " +
                                                                (_reservationService.CanReserve
                                                                    ? "allowed"
                                                                    : "not allowed"));

            await ReplyAsync(embed: embed.Build());
        }

        [Command("ReleaseServer")]
        [Alias("rs")]
        [Summary("Manually ends your server reservation early.")]
        [RequireContext(ContextType.Guild)]
        [RequireUserPermission(ChannelPermission.UseExternalEmojis)]
        public async Task ReleaseServerReservationAsync()
        {
            //Check if the user already has a reservation
            var hasServer = DatabaseUtil.GetTestServerFromReservationUserId(Context.User.Id);
            if (hasServer == null)
            {
                await ReplyAsync(embed: new EmbedBuilder()
                    .WithAuthor("You don't have a server reservation", _dataService.Guild.IconUrl)
                    .WithDescription("Get one using the `>PublicServer` command.")
                    .WithColor(new Color(165, 55, 55)).Build());

                return;
            }

            await ReplyAsync(embed: _reservationService.ReleaseServer(Context.User.Id,
                $"{Context.User} has released the " +
                "server reservation manually."));

            await _log.LogMessage($"`{Context.User}` `{Context.User.Id}` has released `{hasServer.Address}` manually", color: LOG_COLOR);
        }

        [Command("Servers")]
        [Summary("Displays all playtest servers.")]
        public async Task ServersAsync()
        {
            var foundServers = DatabaseUtil.GetAllTestServers();
            var embed = new EmbedBuilder()
                .WithAuthor("Source Engine Discord CS:GO Test Servers")
                .WithFooter($"Total of {foundServers.Count()} servers.")
                .WithThumbnailUrl(_dataService.Guild.IconUrl)
                .WithColor(new Color(255, 135, 57));

            foreach (var server in foundServers) embed.AddField(server.Address, server.Description, true);

            await ReplyAsync(embed: embed.Build());
        }

        [Command("Playtester")]
        [Summary("Join or leave playtest notifications.")]
        [Remarks("Toggles your subscription to playtest notifications.")]
        public async Task PlaytesterAsync()
        {
            var user = _dataService.GetSocketGuildUser(Context.User.Id);
            if (user.Roles.Contains(_dataService.PlayTesterRole))
            {
                await ReplyAsync($"Sorry to see you go from playtest notifications {Context.User.Mention}!");
                await user.RemoveRoleAsync(_dataService.PlayTesterRole);
            }
            else
            {
                await ReplyAsync($"Thanks for subscribing to playtest notifications {Context.User.Mention}!");
                await user.AddRoleAsync(_dataService.PlayTesterRole);
            }
        }
    }
}