using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Sockets;
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
        private const ConsoleColor LOG_COLOR = ConsoleColor.DarkCyan;
        private readonly GoogleCalendar _calendar;
        private readonly DiscordSocketClient _client;
        private readonly DataService _dataService;
        private readonly InteractiveService _interactive;
        private readonly LogHandler _log;
        private readonly SrcdsLogService _srcdsLogService;
        private readonly PlaytestService _playtestService;
        private readonly RconService _rconService;
        private readonly ReservationService _reservationService;
        private readonly ScheduleHandler _scheduleHandler;
        private static List<ulong> AIdoUserList = new List<ulong>();
        private Random AIdoGroupRandom = new Random();

        public PlaytestModule(DiscordSocketClient client, DataService dataService,
            ReservationService reservationService, RconService rconService,
            InteractiveService interactive, LogHandler log, GoogleCalendar calendar, PlaytestService playtestService,
            ScheduleHandler scheduleHandler, SrcdsLogService srcdsLogService)
        {
            _playtestService = playtestService;
            _client = client;
            _dataService = dataService;
            _reservationService = reservationService;
            _interactive = interactive;
            _log = log;
            _calendar = calendar;
            _rconService = rconService;
            _scheduleHandler = scheduleHandler;
            _srcdsLogService = srcdsLogService;
        }

        [Command("FeedbackQueue", RunMode = RunMode.Async)]
        [Alias("fbq")]
        [RequireContext(ContextType.Guild)]
        [RequireUserPermission(GuildPermission.KickMembers)]
        [Summary("Moderator tool for voice feedback")]
        [Remarks("Use without any parameters to start a new voice feedback session. `>fbq` Otherwise format is:" +
                 "`>fbq [command] [user]` where user may be empty depending on the command." +
                 "\n`s` / `start` - Starts the feedback session. Requires users to be in the queue." +
                 "\n`e` / `end` - Ends the feedback session completely, which disposes of the queue." +
                 "\n`p` / `pause` - Pauses the feedback session." +
                 "\n`push` / `pri` - Pushes a user to be next after the current user giving feedback." +
                 "\n`pop` / `remove` - Removes a user from the queue." +
                 "\n`#` - Changes feedback duration. Example: `>fbq 5` sets to 5 minutes.")]
        public async Task FeedbackQueueAsync([Optional] string command, [Optional] SocketUser user)
        {
            await Context.Message.DeleteAsync();
            if (command == null)
            {
                if (_playtestService.CreateVoiceFeedbackSession())
                {
                    await _dataService.CSGOTestingChannel.SendMessageAsync(embed: new EmbedBuilder()
                        .WithAuthor("New Feedback Queue Session Started!")
                        .WithDescription(
                            "In order to keep things moving and civil, a Feedback Queue has been started!" +
                            "\n\nUsers can enter the queue with `>q` and wait for their turn. When it is their turn, " +
                            $"they will have {_dataService.RSettings.General.FeedbackDuration} minutes to give their feedback. " +
                            "This is to give everyone a chance to speak, without some users taking a long time." +
                            "\n\nIf you have a lot to say, please wait until the end when the majority of users have " +
                            "already given their feedback." +
                            "\n\nWhen done with your feedback, or to remove yourself from the queue, type `>done`." +
                            "\n\nIf you need to go sooner, please let a moderator know.")
                        .WithColor(55, 165, 55)
                        .Build());
                    await _log.LogMessage($"`{Context.User}` has started a feedback queue!", color: LOG_COLOR);
                }
                else
                {
                    await ReplyAsync(
                        "Unable to create new feedback session. Possible reasons:" +
                        "\n• A session already exists" +
                        "\n• The active test is not valid" +
                        "\n• The active test is not CSGO");
                }

                return;
            }

            //Check if we can actually run the command.
            if (!await IsValid())
                return;

            //Set duration
            if (int.TryParse(command, out var duration))
            {
                _playtestService.FeedbackSession.SetDuration(duration);
                return;
            }

            switch (command.ToLower())
            {
                case "s":
                case "start":
                    if (!await _playtestService.FeedbackSession.StartFeedback())
                    {
                        var msg = await ReplyAsync(embed: new EmbedBuilder()
                            .WithAuthor("Feedback already started!")
                            .WithColor(165, 55, 55).Build());
                        _ = Task.Run(async () =>
                        {
                            await Task.Delay(5000);
                            await msg.DeleteAsync();
                        });

                        return;
                    }

                    await _log.LogMessage($"`{Context.User}` has started feedback queue session", color: LOG_COLOR);
                    _scheduleHandler.DisablePlayingUpdate();
                    break;
                case "e":
                case "end":
                    _playtestService.EndVoiceFeedbackSession();
                    await ReplyAsync(embed: new EmbedBuilder()
                        .WithAuthor("Feedback Ended!")
                        .WithColor(165, 55, 55).Build());
                    _scheduleHandler.EnablePlayingUpdate();
                    await _log.LogMessage($"`{Context.User}` has ended a feedback queue!", color: LOG_COLOR);
                    break;
                case "p":
                case "pause":
                    _playtestService.FeedbackSession.PauseFeedback();
                    await _log.LogMessage($"`{Context.User}` has paused a feedback queue!", color: LOG_COLOR);
                    var msgP = await ReplyAsync(embed: new EmbedBuilder()
                        .WithAuthor("Pausing Feedback...")
                        .WithColor(165, 55, 55).Build());
                    _ = Task.Run(async () =>
                    {
                        await Task.Delay(5000);
                        await msgP.DeleteAsync();
                    });
                    break;
                case "push":
                case "pri":
                    if (user == null)
                    {
                        var msg = await ReplyAsync(embed: new EmbedBuilder()
                            .WithAuthor("User cannot be empty.")
                            .WithColor(165, 55, 55).Build());
                        _ = Task.Run(async () =>
                        {
                            await Task.Delay(5000);
                            await msg.DeleteAsync();
                        });
                        return;
                    }

                    await _playtestService.FeedbackSession.AddUserToTopQueue(user);
                    await _log.LogMessage($"`{Context.User}` has pushed {user} to the top of the feedback queue",
                        color: LOG_COLOR);
                    break;
                case "pop":
                case "remove":
                    if (user == null)
                    {
                        var msg = await ReplyAsync(embed: new EmbedBuilder()
                            .WithAuthor("User cannot be empty.")
                            .WithColor(165, 55, 55).Build());
                        _ = Task.Run(async () =>
                        {
                            await Task.Delay(5000);
                            await msg.DeleteAsync();
                        });
                        return;
                    }

                    if (!await _playtestService.FeedbackSession.RemoveUser(user.Id))
                    {
                        var msg = await ReplyAsync(embed: new EmbedBuilder()
                            .WithAuthor("User not currently in queue")
                            .WithColor(165, 55, 55).Build());
                        _ = Task.Run(async () =>
                        {
                            await Task.Delay(5000);
                            await msg.DeleteAsync();
                        });
                    }

                    await _log.LogMessage($"`{Context.User}` has removed {user} from the feedback queue",
                        color: LOG_COLOR);
                    break;
                default:
                    await ReplyAsync(embed: new EmbedBuilder()
                        .WithAuthor("Unknown command")
                        .WithDescription("Please see `>help fbq`")
                        .WithColor(165, 55, 55).Build());
                    break;
            }

            async Task<bool> IsValid()
            {
                if (_playtestService.FeedbackSession == null)
                {
                    await ReplyAsync(embed: new EmbedBuilder()
                        .WithAuthor("A feedback session must be started first")
                        .WithDescription("Please see `>help fbq`")
                        .WithColor(165, 55, 55).Build());
                    return false;
                }

                return true;
            }
        }

        
        [Command("AIdo", RunMode = RunMode.Async)]
        [Alias("AI")]
        [RequireContext(ContextType.Guild)]
        [Summary("Analyzes your level using the Ido Neural Network.")]
        [Remarks("Uses a Neural Network that has been trained using dozens of previous playtest demos and maps. " +
                 "Provide a workshop URL to your level to have Ido analyze the level and provide feedback.\n" +
                 "Example: `>aido https://steamcommunity.com/sharedfiles/filedetails/?id=804710334`" +
                 "\nThis command is a joke created for April Fools 2021 - but no one reads help docs.")]
        public async Task AIdoAsync(string workshopLink)
        {
            //Lets guard uses by users and total uses.
            if (AIdoUserList.Contains(Context.User.Id))
            {
                await ReplyAsync("You cannot analyze more than 1 map at a time.");
                return;
            }

            if (AIdoUserList.Count >= 5)
            {
                await ReplyAsync("I'm already analyzing 5 levels. Give AIdo a break, AIdo works for free.");
                return;
            }

            AIdoUserList.Add(Context.User.Id);

            var message = await ReplyAsync($"Getting AI Analysis ready for {Context.Message.Author.Username}...");

            //Get from the workshop
            var workshopId = GeneralUtil.GetWorkshopIdFromFqdn(workshopLink);
            var steamApi = new SteamAPI(_dataService, _log);
            var workshopJsonItem = await steamApi.GetWorkshopItem(workshopId);
            int wsIdAsInt = 0;
            if (!int.TryParse(workshopId.Trim(), out wsIdAsInt))
                try
                {
                    //Workshop IDs can overflow an INT32, so lets just substring out the first 9 digits and use those instead.
                    int.TryParse(workshopId.Substring(0,9), out wsIdAsInt);
                }
                catch
                {
                    //Heck, I don't know man.
                    await ReplyAsync("Failed to properly parse workshop ID. Check your URL, and try again.");
                    AIdoUserList.Remove(Context.User.Id);
                    return;
                }

            //Sometimes Steam API sucks...
            if (workshopJsonItem == null || wsIdAsInt == 0)
            {
                await message.ModifyAsync(x =>
                    x.Content = "Unable get workshop item from Steam API. Check your URL, and try again.");
                AIdoUserList.Remove(Context.User.Id);
                return;
            }

            var wsGames = await steamApi.GetWorkshopGames();
            var gameId = wsGames?.applist.apps.SingleOrDefault(x =>
                x.appid == workshopJsonItem.response.publishedfiledetails[0].creator_app_id);

            string tags = null;
            try
            {
                tags = string.Join(", ",
                    workshopJsonItem.response.publishedfiledetails[0].tags.Select(x => x.tag));
            } catch 
            { 
                //Ignored
            }

            //Validate that we have a valid API response for CSGO that is classic game mode
            if (gameId == null || gameId.appid != 730 || tags == null || !tags.Contains("Classic", StringComparison.OrdinalIgnoreCase))
            {
                await message.ModifyAsync(x =>
                    x.Content = "**I can only analyze CS:GO levels with a classic game mode.**\nUnlisted maps cannot be analyzed.");
                AIdoUserList.Remove(Context.User.Id);
                return;
            }

            var workshopJsonAuthor = await steamApi.GetWorkshopAuthor(workshopJsonItem);
            EmbedBuilder workshopItemEmbed;
            // Finally we can build the embed after too many HTTP requests
            try
            {
                workshopItemEmbed = new EmbedBuilder()
                    .WithAuthor($"Running AI analysis on {workshopJsonItem.response.publishedfiledetails[0].title}",
                        workshopJsonAuthor.response.players[0].avatar, workshopLink)
                    .WithTitle($"Creator: {workshopJsonAuthor.response.players[0].personaname}")
                    .WithUrl(workshopJsonAuthor.response.players[0].profileurl)
                    .WithThumbnailUrl(workshopJsonItem.response.publishedfiledetails[0].preview_url)
                    .WithColor(new Color(71, 126, 159));
            }
            catch
            {
                //We end up here if we get a response from an unlisted object.
                await message.ModifyAsync(x =>
                    x.Content = "Unable get workshop item from Steam API. Check your URL, and try again.");
                AIdoUserList.Remove(Context.User.Id);
                return;
            }

            string statusReason = "Initializing...";
            await message.DeleteAsync();
            message = await ReplyAsync($"`░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░` 0%\nStatus: `{statusReason}`", embed: workshopItemEmbed.Build());

            //Ensure that we use the same random each time if the same map is submitted more than once. This ensures the same "results"
            var seededRandom = new Random(wsIdAsInt);

            var delay = seededRandom.Next(8, 60);
            //Why tf did I use i + 2? Could have just used + 1, but I guess I'm stupid.
            for (int i = 0; delay > i; i = i + 2)
            {
                string progress = "";
                decimal count = i / (decimal)delay * 50;

                //Progress bar stuff
                for (int j = 0; j < count; j++)
                {
                    progress += "█";
                }

                for (int j = progress.Length; j < 50; j++)
                {
                    progress += "░";
                }

                if(count < 9)
                {
                    statusReason = "Downloading map from Steam Workshop...";
                }
                else if(count < 14)
                {
                    statusReason = "Setting up Neural Network...";
                }
                else if (count < 44)
                {
                    statusReason = "Analyzing level...";
                }
                else if (count < 50)
                {
                    statusReason = "Finishing up...";
                }

                //Get a RNG based for each map
                var rngDelayOffset = seededRandom.Next(6,15);

                //Add even more delay based on a shared random, for variance.
                //Then add more delay for each concurrent run - this simulates server load. kek
                await Task.Delay((rngDelayOffset * 100) + (AIdoGroupRandom.Next(2,10) * 100) + (AIdoUserList.Count * 750));
                await message.ModifyAsync(x => x.Content = $"`{progress}` {Math.Round(count * 2, 0)}%\nStatus: `{statusReason}`");
            }

            //Make em wait to finish, Windows file transfer style
            await Task.Delay(4 * 1000);
            await message.ModifyAsync(x => x.Content = $"`██████████████████████████████████████████████████` 100%\nStatus: `Complete!`");

            string[] feedbackArray = { "Multiple sight lines in middle are too long.",
                "Sight lines at B site are too long.",
                "A site cover too heavily favors CTs.",
                "A site will be difficult to retake post plant.",
                "B site is impossible to take without more utility options.",
                "Middle contains some 'janky' head shot angles that would be unfun to play against.",
                "B site has too few utility options.",
                "Too much of the level favors close quarters combat, making rifle play difficult.",
                "If CTs get too aggressive they flank the T side too easily.",
                "T side is required to bait too much utility before they are able to take any map control.",
                "Middle feels meaningless to take.",
                "Once you lose middle, it will become impossible to rotate.",
                "There is no safe plant at A site.",
                "There is no safe plant at B site.",
                "CT spawn has menacing angles.",
                "Too many headshot angles.",
                "The color of the floor at B does not match the walls.",
                "Rotate timings are off.",
                "You can do a 4-man boost out of the map in mid.",
                "Initial timings are bad.",
                "Map lacks interesting verticality.",
                "Not enough chickens in the level.",
                "Stairs are not clipped properly.",
                "Map is lacking proper clipping.",
                "Too much verticality.",
                "Bomb sites could use more cover.",
                "Bomb sites have too much cover.",
                "Unreadable radar. Make it with Terri's Auto Radar here: https://github.com/18swenskiq/CS-GO-Auto-Radar",
                "Needs more spawns.",
                "After the round starts, T spawn serves no purpose.",
                "CTs spawn too close the A site.",
                "Map is lacking proper optimization.",
                "Brush ID " + seededRandom.Next(400, 18999) + " has bad lighting.",
                "A main is too CT sided.",
                "B main is too T sided.",
                "Map was found to have a " + seededRandom.Next(60,96) + "% similarity to de_dust2",
                "Map was detected to have been a decompiled version of an existing map, please do not steal other peoples work.",
                "A site is very small.",
                "Middle to B opens into too many angles at once.",
                "Middle to A pathing is too large.",
                "Map favors snipers too much.",
                "CT rotate times are too fast compared to T rotate times.",
                "Brush geometry created by carve detected on map. Please do not use carve.",
                "Railings should have collisions disabled.",
                "Navmesh is corrupt, please rebuild and resubmit.",
                "Map has many dark areas with bad visibility.",
                "Map is too cluttered.",
                "Very awkward head shot angles from short B to T entrance at middle.",
                "Eco rounds are always a loss.",
                "Bomb is able to fall though a displacement floor.",
                "Long A is too much risk for too little reward.",
                "It might be best to remake the map from scratch."
            };

            int feedbackCount = seededRandom.Next(6, 14) + 4;
            List<string> feedBackList = new List<string>();

            //Lets get UNIQUE feedback for this map.
            for (int i = 0; i < feedbackCount; i++)
            {
                while (22 == 22)//Don't ask
                {
                    var choice = feedbackArray[seededRandom.Next(0, feedbackArray.Length)];
                    if (!feedBackList.Contains(choice))
                    {
                        feedBackList.Add(choice);
                        break;
                    }
                }
            }

            string output = "";

            //Build the output and give fake confidence values.
            for (int i = 0; i < feedBackList.Count; i++)
            {
                output += $"(Confidence {seededRandom.Next(70, 101)}%) - {i + 1}: {feedBackList[i]}\n";
            }

            //Output
            await ReplyAsync($"{Context.Message.Author.Mention} Analyzed Feedback for {workshopJsonItem.response.publishedfiledetails[0].title}:\n```{output}```");
            AIdoUserList.Remove(Context.User.Id);
        }
        
        [Command("Queue", RunMode = RunMode.Async)]
        [Alias("q")]
        [RequireContext(ContextType.Guild)]
        [Summary("Places yourself in the feedback queue.")]
        public async Task EnterFeedbackQueue()
        {
            await Context.Message.DeleteAsync();
            if (_playtestService.FeedbackSession == null)
            {
                var msg = await ReplyAsync(embed: new EmbedBuilder()
                    .WithAuthor("A feedback session must be started before you can do that")
                    .WithColor(165, 55, 55).Build());
                await Task.Delay(5000);
                await msg.DeleteAsync();
                return;
            }

            if (!await _playtestService.FeedbackSession.AddUserToQueue(Context.User))
            {
                var msg = await ReplyAsync(embed: new EmbedBuilder()
                    .WithAuthor($"{Context.User} unable to be added to queue.")
                    .WithDescription("To enter the queue, you must be in a voice channel.\n" +
                                     "Or you are already in the queue.")
                    .WithColor(165, 55, 55).Build());
                _ = Task.Run(async () =>
                {
                    await Task.Delay(5000);
                    await msg.DeleteAsync();
                });
            }
        }

        [Command("FeedbackDone", RunMode = RunMode.Async)]
        [Alias("fbd", "done")]
        [RequireContext(ContextType.Guild)]
        [Summary("Signals that you're done giving feedback, or want to remove yourself from the queue.")]
        public async Task DoneFeedbackQueue()
        {
            await Context.Message.DeleteAsync();
            if (_playtestService.FeedbackSession == null)
            {
                var msg = await ReplyAsync(embed: new EmbedBuilder()
                    .WithAuthor("A feedback session must be started before you can do that")
                    .WithColor(165, 55, 55).Build());
                await Task.Delay(5000);
                await msg.DeleteAsync();
                return;
            }

            if (!await _playtestService.FeedbackSession.RemoveUser(Context.User.Id))
            {
                var msg = await ReplyAsync(embed: new EmbedBuilder()
                    .WithAuthor($"{Context.User} is not in the queue")
                    .WithColor(165, 55, 55).Build());
                _ = Task.Run(async () =>
                {
                    await Task.Delay(5000);
                    await msg.DeleteAsync();
                });
            }
        }

        [Command("Schedule", RunMode = RunMode.Async)]
        [Alias("pts", "upcoming")]
        [Summary("Allows users to view testing queue and schedule.")]
        [Remarks("For members, displays test in the queue and scheduled on the calendar." +
                 "If you're moderation staff, allows for officially scheduling the playtest event after making any needed changes.")]
        public async Task ScheduleTestAsync([Summary("If `true`, displays scheduled tests as well.")] [Optional]
            bool getList)
        {
            if (!getList)
            {
                await Context.Channel.TriggerTypingAsync();
                var calendarBuilder = new CalendarBuilder(await _calendar.GetNextMonthAsync(DateTime.Now),
                    DatabaseUtil.GetAllPlaytestRequests());
                await calendarBuilder.DiscordPlaytestCalender(Context);
                await Context.Channel.SendFileAsync("renderedCalendar.png",
                    "**Currently Scheduled and Requested Tests**" +
                    "\nGreen are scheduled tests, blue are requested.\nType `>pts true` to get a list of test instead of an image.\n" +
                    $"All times are CT Timezone. Current time CT: `{DateTime.Now:g}`");
            }
            else
            {
                var user = _dataService.GetSocketGuildUser(Context.User.Id);
                var embed = await _playtestService.GetUpcomingEvents(true, true);

                if (user.Roles.Any(x => x.Id == _dataService.ModeratorRole.Id || x.Id == _dataService.AdminRole.Id))
                {
                    _dataService.IgnoreListenList.Add(Context.User.Id);

                    var display = await ReplyAsync(embed: embed.Build());

                    var requestBuilder = new RequestBuilder(Context, _interactive, _dataService, _log, _calendar,
                        _playtestService);
                    await requestBuilder.SchedulePlaytestAsync(display);

                    _dataService.IgnoreListenList.Remove(Context.User.Id);
                    return;
                }

                await ReplyAsync(embed: embed.Build());
            }
        }

        [Command("Request", RunMode = RunMode.Async)]
        [Alias("ptr")]
        [Summary("Requests a playtest event.")]
        [Remarks("Creates a playtest request using an interactive system. To start, type `>Request`\n\n" +
                 "If you have a filled out template, you can send that with the command to skip the interactive builder. " +
                 "You can also use this command to edit an existing request." +
                 "\nAn example of the filled out template is on the playtesting webpage.\n\nTemplate:" +
                 "```>Request Date:\nEmails:\nMapName:\nDiscord:\nImgur:\nWorkshop:\nType:\nDescription:\nSpawns:\nPreviousTest:\nServer:```")]
        public async Task PlaytestRequestAsync(
            [Summary("A pre-built playtest event based on the template.")] [Optional] [Remainder]
            string playtestInformation)
        {
            _dataService.IgnoreListenList.Add(Context.User.Id);

            var requestBuilder =
                new RequestBuilder(Context, _interactive, _dataService, _log, _calendar, _playtestService);

            var existingTests = DatabaseUtil.GetAllPlaytestRequests()
                .FirstOrDefault(x => x.CreatorsDiscord.Contains(Context.User.Id.ToString()));

            if (existingTests != null)
            {
                await requestBuilder.UserEditRequest(existingTests);
            }
            else if (playtestInformation != null)
            {
                //If we are here from a full dump, split it to handle
                var split = playtestInformation.Split(new[] {"\r\n", "\r", "\n"}, StringSplitOptions.None);

                if (split.Length != 12)
                {
                    await ReplyAsync("Invalid bulk playtest quest submission. Consult the help documents.");
                    _dataService.IgnoreListenList.Remove(Context.User.Id);
                    return;
                }

                await requestBuilder.BuildPlaytestRequestBulk(split);
            }
            else
            {
                await Context.Channel.TriggerTypingAsync();
                var calendarBuilder = new CalendarBuilder(await _calendar.GetNextMonthAsync(DateTime.Now),
                    DatabaseUtil.GetAllPlaytestRequests());
                await calendarBuilder.DiscordPlaytestCalender(Context);
                await Context.Channel.SendFileAsync("renderedCalendar.png",
                    "**Currently Scheduled and Requested Tests**" +
                    "\nGreen are scheduled tests, blue are requested.\n" +
                    "https://www.tophattwaffle.com/playtesting" +
                    $"\nAll times are CT Timezone. Current time CT: `{DateTime.Now:g}`");

                await requestBuilder.BuildPlaytestRequestWizard();
            }

            _dataService.IgnoreListenList.Remove(Context.User.Id);
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
            [Optional] [Summary("The ID of a Steam Workshop map for the server to host")]
            string workshopId)
        {
            //Check if reservations can be made.
            if (!_reservationService.CanReserve)
            {
                await ReplyAsync(embed: new EmbedBuilder()
                    .WithAuthor("Cannot reserve servers at this time")
                    .WithDescription(
                        "Servers cannot be reserved 20 minutes before a playtest, lasting until shortly after the test event is over.")
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

            var formattedServer = GeneralUtil.GetServerCode(serverCode);

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
            JobManager.AddJob(async () => await _dataService.BotChannel.SendMessageAsync(
                    $"{Context.User.Mention}",
                    embed: _reservationService.ReleaseServer(Context.User.Id, "The reservation has expired.", Context.Channel as SocketTextChannel)),
                s => s.WithName($"[TSRelease_{formattedServer}_{Context.User.Id}]").ToRunOnceIn(3).Hours());

            var rconCommand = $"sv_password {_dataService.RSettings.General.CasualPassword}";

            if (workshopId != null)
                rconCommand += $"; host_workshop_map {workshopId}";

            _ = _rconService.RconCommand(server.Address, rconCommand);

            var embed = new EmbedBuilder()
                .WithAuthor($"{server.Address} is reserved for 3 hours!",
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

            //Remove a file if it needs to be.
            _srcdsLogService.RemoveFeedbackFile(server);

            //Make a new one.
            _srcdsLogService.CreateFeedbackFile(server, DateTime.Now.ToString("MM_dd_yyyy") + "_" + Context.User.Id.ToString());

            embed.AddField("Ingame Chat Active", "You may use `>pc` in-game to send commands to the server!");
            embed.AddField("Ingame Feedback Active", "You may use `>fb` in-game to send feedback to the log! Type `>rs` to collect the log.");
            embed.AddField("Playtest Demo", "You may use `>pd start` to start recording a GOTV demo, then `>pd stop` to finish. See `>help pd` for more info.");

            await ReplyAsync(embed: embed.Build());

            await _log.LogMessage($"`{Context.User}` `{Context.User.Id}` has reserved `{server.Address}`",
                color: LOG_COLOR);
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

            var result = await _rconService.GetRunningLevelAsync(server.Address);
            if (result == null)
            {
                await ReplyAsync("I attempted to get the running level from the server, but the response did not" +
                                 " make any sense. I have not announced for your level. Please try again in a moment.");
                return;
            }

            string mention = null;
            if (!reservation.Announced)
            {
                await _dataService.CommunityTesterRole.ModifyAsync(x => { x.Mentionable = true; });
                mention = _dataService.CommunityTesterRole.Mention;
                DatabaseUtil.UpdateAnnouncedServerReservation(Context.User.Id);
            }

            SocketTextChannel testingChannel = null;
            if (server.Game.Equals("csgo", StringComparison.OrdinalIgnoreCase))
                testingChannel = _dataService.CSGOTestingChannel;
            else if (server.Game.Equals("tf2", StringComparison.OrdinalIgnoreCase))
                testingChannel = _dataService.TF2TestingChannel;

            if (testingChannel == null)
            {
                await ReplyAsync(
                    "The game server's marked game isn't valid. This should never happen. I alerted an admin.");
                await _log.LogMessage(
                    "Force announce was run, but the game for the server is not CSGO or TF2. This should never happen.",
                    alert: true);
                return;
            }

            var embed = new EmbedBuilder();

            //Length 3 means workshop
            if (result.Length == 3)
            {
                embed = await new Workshop(_dataService, _log).HandleWorkshopEmbeds(Context.Message,
                    inputId: result[1]);

                string message = $"{mention} {Context.User.Mention} " +
                                 $"needs players to help test `{result[2]}`\nYou can join using: `connect {server.Address}; password {_dataService.RSettings.General.CasualPassword}`" +
                                 "\nType `>roleme Community Tester` to get this role.";

                if (embed != null)
                {
                    await testingChannel.SendMessageAsync(message,embed: embed.Build());
                }
                else //No embed cause the workshop level is likely private
                {
                    await testingChannel.SendMessageAsync(message);
                }
            }
            else
            {
                //No embed
                await testingChannel.SendMessageAsync($"{mention} {Context.User.Mention} " +
                                                      $"needs players to help test `{result[0]}`\nYou can join using: `connect {server.Address}; password {_dataService.RSettings.General.CasualPassword}`");
            }

            if (!reservation.Announced)
                await _dataService.CommunityTesterRole.ModifyAsync(x => { x.Mentionable = false; });

            await _log.LogMessage(
                $"`{Context.User}` `{Context.User.Id}` alerted for their community playtest on `{server.Address}`",
                color: LOG_COLOR);
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

            var levelInfo = await _rconService.GetRunningLevelAsync(reservation.ServerId);
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
                Game = "csgo"
            };

            switch (command.ToLower())
            {
                case "start":
                    var demoReply = await _rconService.RconCommand(testInfo.ServerAddress,
                        $"tv_stoprecord; tv_record {testInfo.DemoName}" +
                        $";say {testInfo.DemoName} now recording!");
                    await ReplyAsync(embed: new EmbedBuilder()
                        .WithAuthor($"Command sent to {testInfo.ServerAddress}", _dataService.Guild.IconUrl)
                        .WithDescription($"```{demoReply}```")
                        .WithColor(new Color(55, 165, 55)).Build());
                    break;

                case "stop":
                    var stopReply = await _rconService.RconCommand(testInfo.ServerAddress,
                        $"tv_stoprecord;say {testInfo.DemoName} stopped recording!");

                    await ReplyAsync(embed: new EmbedBuilder()
                        .WithAuthor($"Command sent to {testInfo.ServerAddress}", _dataService.Guild.IconUrl)
                        .WithDescription("```Stopping Demo Recording and fetching from server...```")
                        .WithColor(new Color(55, 165, 55)).Build());

                    //Download demo, don't wait.
                    _ = Task.Run(() => { _ = DownloadHandler.DownloadPlaytestDemo(testInfo); });

                    const string demoUrl = "http://demos.tophattwaffle.com";
                    var embed = new EmbedBuilder()
                        .WithAuthor($"Download playtest demo for {testInfo.DemoName}", _dataService.Guild.IconUrl,
                            demoUrl)
                        .WithThumbnailUrl(_client.CurrentUser.GetAvatarUrl())
                        .WithColor(new Color(243, 128, 72))
                        .WithDescription(
                            $"[Download Demo Here]({demoUrl}) | [Playtesting Information](https://www.tophattwaffle.com/playtesting/)");

                    await _dataService.CSGOTestingChannel.SendMessageAsync(Context.User.Mention, embed: embed.Build());
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
                StringBuilder sv = new StringBuilder(),
                    mp = new StringBuilder(),
                    bot = new StringBuilder(),
                    exec = new StringBuilder(),
                    misc = new StringBuilder();

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

                await Context.Channel.TriggerTypingAsync();
                string reply;
                IUserMessage delayed = null;
                var rconCommand = _rconService.RconCommand(server.Address, command);
                var waiting = Task.Delay(4000);
                if (rconCommand == await Task.WhenAny(rconCommand, waiting))
                {
                    reply = await rconCommand;
                }
                else
                {
                    delayed = await ReplyAsync(embed: new EmbedBuilder()
                        .WithDescription(
                            $"⏰RCON command to `{server.Address}` is taking longer than normal...\nSit tight while I'll " +
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
                $"{Context.User} has released the server reservation manually.", Context.Channel as SocketTextChannel));

            await _log.LogMessage($"`{Context.User}` `{Context.User.Id}` has released `{hasServer.Address}` manually",
                color: LOG_COLOR);
        }

        [Command("Servers")]
        [Summary("Displays all playtest servers.")]
        public async Task ServersAsync()
        {
            var foundServers = DatabaseUtil.GetAllTestServers();
            var embed = new EmbedBuilder()
                .WithAuthor("Source Engine Discord Test Servers")
                .WithFooter($"Total of {foundServers.Count()} servers.")
                .WithThumbnailUrl(_dataService.Guild.IconUrl)
                .WithColor(new Color(255, 135, 57))
                .WithDescription("All servers have a default password of `se`.\n" +
                                 "Example of connect information: `connect can.tophattwaffle.com;password se`");

            foreach (var server in foundServers)
                embed.AddField(server.Address, $"Server Type: `{server.Game.ToUpper()}`\n{server.Description}", true);

            await ReplyAsync(embed: embed.Build());
        }

        [Command("Playtester")]
        [Alias("pt")]
        [Summary("Changes what playtest notifications your get.")]
        [Remarks(
            "Type `>playtester` or `>pt Unsubscribe` to remove all subscriptions (Does not remove competitive)." +
            "\nType `>playtester Subscribe` to add all subscriptions." +
            "\nType `>playtester CSGO/TF2` to toggle the specific game subscription." +
            "\nType `>playtester Community` to toggle community tester." +
            "\nType `>playtester Comp` to remove the competitive tester. You cannot re-add it yourself if removed." +
            "\nType `>playtester Show` to see your subscriptions.")]
        public async Task PlaytesterAsync([Optional] string game)
        {
            var user = _dataService.GetSocketGuildUser(Context.User.Id);
            var embed = new EmbedBuilder()
                .WithAuthor($"Playtest Subscriptions - {user}", Context.User.GetAvatarUrl())
                .WithColor(55, 55, 165);
            var description = "";

            var csgoStatus = -1;
            var communityStatus = -1;
            var tf2Status = -1;
            var compStatus = -1;

            if (string.IsNullOrWhiteSpace(game))
                game = "unsubscribe";

            switch (game.ToLower())
            {
                case "unsubscribe":
                    description += "**All subscriptions removed!**\n";
                    await user.RemoveRoleAsync(_dataService.CSGOPlayTesterRole);
                    await user.RemoveRoleAsync(_dataService.TF2PlayTesterRole);
                    csgoStatus = 0;
                    tf2Status = 0;
                    break;

                case "subscribe":
                    description += "**All subscriptions added!**\n";
                    await user.AddRoleAsync(_dataService.CSGOPlayTesterRole);
                    await user.AddRoleAsync(_dataService.TF2PlayTesterRole);
                    csgoStatus = 1;
                    tf2Status = 1;
                    break;

                case "tf2":
                    description += "**Toggled TF2 Subscription!**\n";
                    tf2Status = await ToggleRole(_dataService.TF2PlayTesterRole);
                    break;

                case "community":
                    description += "**Toggled Community Subscription!**\n";
                    communityStatus = await ToggleRole(_dataService.CommunityTesterRole);
                    break;

                case "comp":
                    if (user.Roles.Any(x => x.Id == _dataService.CompetitiveTesterRole.Id))
                    {
                        await user.RemoveRoleAsync(_dataService.CompetitiveTesterRole);
                        compStatus = 0;
                        description += "**Competitive Playtester Removed!**\n";
                    }
                    else
                    {
                        description += "**You need to have Competitive Playtester to remove it.**\n";
                        compStatus = 0;
                    }

                    break;

                case "csgo":
                    description += "**Toggled CSGO Subscription!**\n";
                    csgoStatus = await ToggleRole(_dataService.CSGOPlayTesterRole);
                    break;

                case "show":
                    embed.AddField("Playtester stats:",
                        $"CSGO: `{_dataService.CSGOPlayTesterRole.Members.Count()}` Members" +
                        $"\nTF2: `{_dataService.TF2PlayTesterRole.Members.Count()}` Members" +
                        $"\nCommunity: `{_dataService.CommunityTesterRole.Members.Count()}` Members" +
                        $"\nCompetitive: `{_dataService.CompetitiveTesterRole.Members.Count()}` Members");
                    break;

                default:
                    description += "**Unknown command.**\n" +
                                   "Please type `>help playtester`";
                    break;
            }

            //Refresh user object
            user = _dataService.GetSocketGuildUser(user.Id);

            if (csgoStatus == -1)
                csgoStatus = user.Roles.Any(x => x.Id == _dataService.CSGOPlayTesterRole.Id) ? 1 : 0;

            if (tf2Status == -1)
                tf2Status = user.Roles.Any(x => x.Id == _dataService.TF2PlayTesterRole.Id) ? 1 : 0;

            if (compStatus == -1)
                compStatus = user.Roles.Any(x => x.Id == _dataService.CompetitiveTesterRole.Id) ? 1 : 0;

            if (communityStatus == -1)
                communityStatus = user.Roles.Any(x => x.Id == _dataService.CommunityTesterRole.Id) ? 1 : 0;

            description += $"CSGO Playtesting: `{(csgoStatus == 1 ? "Subscribed" : "Unsubscribed")}`\n" +
                           $"TF2 Playtesting: `{(tf2Status == 1 ? "Subscribed" : "Unsubscribed")}`\n" +
                           $"Community Playtesting: `{(communityStatus == 1 ? "Subscribed" : "Unsubscribed")}`\n" +
                           $"Competitive Playtesting: `{(compStatus == 1 ? "Subscribed" : "Unsubscribed")}`";
            embed.WithFooter("Type >help playtester for more information");
            embed.WithDescription(description);

            await ReplyAsync(embed: embed.Build());

            async Task<int> ToggleRole(SocketRole role)
            {
                if (user.Roles.All(x => x.Id != role.Id))
                {
                    await user.AddRoleAsync(role);
                    return 1;
                }

                await user.RemoveRoleAsync(role);
                return 0;
            }
        }
    }
}