using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using BotHATTwaffle2.Handlers;
using BotHATTwaffle2.Models.JSON;
using BotHATTwaffle2.Models.LiteDB;
using BotHATTwaffle2.Services;
using BotHATTwaffle2.Services.Calendar;
using BotHATTwaffle2.Services.Calendar.PlaytestEvents;
using BotHATTwaffle2.Services.Playtesting;
using BotHATTwaffle2.Services.SRCDS;
using BotHATTwaffle2.Util;
using Discord;
using Discord.Addons.Interactive;
using Discord.Commands;
using Discord.WebSocket;
using FluentScheduler;
using Newtonsoft.Json.Linq;

namespace BotHATTwaffle2.Commands
{
    public class ModerationModule : InteractiveBase
    {
        private const ConsoleColor LOG_COLOR = ConsoleColor.DarkGreen;
        private static readonly Dictionary<ulong, string> ServerDictionary = new Dictionary<ulong, string>();
        private readonly GoogleCalendar _calendar;
        private readonly DataService _dataService;
        private readonly InteractiveService _interactive;
        private readonly LogHandler _log;
        private readonly SrcdsLogService _srcdsLogService;
        private readonly PlaytestService _playtestService;
        private readonly Random _random;
        private readonly RconService _rconService;
        private readonly ReservationService _reservationService;
        private readonly ToolsService _toolsService;

        public ModerationModule(DataService data, LogHandler log, GoogleCalendar calendar,
            PlaytestService playtestService, InteractiveService interactive, ReservationService reservationService,
            Random random, RconService rconService, SrcdsLogService srcdsLogService, ToolsService toolsService)
        {
            _playtestService = playtestService;
            _calendar = calendar;
            _dataService = data;
            _log = log;
            _interactive = interactive;
            _reservationService = reservationService;
            _random = random;
            _rconService = rconService;
            _srcdsLogService = srcdsLogService;
            _toolsService = toolsService;
        }

//        [Command("Test")]
//        [Summary("Used to debug. This should not go live")]
//        [RequireUserPermission(GuildPermission.KickMembers)]
//        public async Task TestAsync()
//        {
//            var sapi = new SteamAPI(_dataService,_log);
//            var r = await sapi.GetWorkshopMapRadarFiles(@"C:\support\test", "1811247004");
//
//            foreach (var fileInfo in r)
//            {
//                await ReplyAsync(fileInfo.FullName);
//            }
//        }

        [Command("MatchMaking", RunMode = RunMode.Async)]
        [Alias("mm")]
        [RequireUserPermission(GuildPermission.KickMembers)]
        [Summary("Ido-MMR System")]
        public async Task MatchMakingAsync([Optional] string server)
        {
            if (Context.Channel.Id != _dataService.AdminBotsChannel.Id)
            {
                var embedError = new EmbedBuilder()
                    .WithColor(165, 55, 55)
                    .WithAuthor("You cannot use this command in this channel...");
                await ReplyAsync(embed: embedError.Build());
                return;
            }

            const string BASEURL = @"https://www.tophattwaffle.com/demos/playerBase/index.php?mode=idoMode&ids=";

            if (server == null)
                server = _calendar.GetNextPlaytestEvent(PlaytestEvent.Games.CSGO).ServerLocation;

            //Check again in-case there is no server on the calendar
            if (server == null)
            {
                var embedError = new EmbedBuilder()
                    .WithColor(165, 55, 55)
                    .WithAuthor("Error getting server...")
                    .WithDescription("The command will only ask CSGO servers for the players.");
                await ReplyAsync(embed: embedError.Build());
                return;
            }

            await Context.Channel.TriggerTypingAsync();

            var status = await _rconService.RconCommand(server, "status", false);

            var playerIds64 = new List<long>();

            var splitStatus = status.Split(new[] {"\r\n", "\r", "\n"}, StringSplitOptions.None);

            //Get the IDs, skip 0 which means no ID found for that line.
            foreach (var s in splitStatus)
            {
                var id = GeneralUtil.TranslateSteamIdToSteam64(s);

                if (id != 0)
                    playerIds64.Add(id);
            }

            //No users found
            if (playerIds64.Count == 0)
            {
                var embedError = new EmbedBuilder()
                    .WithColor(165, 55, 55)
                    .WithAuthor("No users found...");
                await ReplyAsync(embed: embedError.Build());
                return;
            }

            //Build the full URL
            var builtUrl = BASEURL + string.Join(",", playerIds64);

            //Lets get the JSON from the site
            var returnedJson = new WebClient().DownloadString(builtUrl).Trim();

            //This is all because WhaleMan code won't give us an array...
            var ratedPlayers = new List<RatedPlayer>();
            var jsonObject = JObject.Parse(returnedJson);
            foreach (var id in playerIds64)
            {
                var result = jsonObject.Property(id.ToString()).First;
                var player = result.ToObject<RatedPlayer>();
                ratedPlayers.Add(player);
            }

            //Reverse so best players are first
            var sortedPlayers = ratedPlayers.OrderBy(x => x.Rating).Reverse().ToList();

            var embed = new EmbedBuilder()
                .WithColor(55, 55, 165)
                .WithAuthor($"Player Ratings - {sortedPlayers.Count} Players Found", url: builtUrl);

            //Add fields to embed
            foreach (var sortedPlayer in sortedPlayers) embed.AddField(sortedPlayer.Name, sortedPlayer.Rating, true);

            await ReplyAsync(embed: embed.Build());
        }

        [Command("StartFeedback", RunMode = RunMode.Async)]
        [Alias("startfb")]
        [Summary("Starts server listening for in game feedback")]
        [Remarks("If all parameters are left empty, the next playtest event will be used.\nOtherwise specify" +
                 "a server and a name for the file to be created. When feedback is stopped, the file will be delivered.")]
        [RequireContext(ContextType.Guild)]
        [RequireUserPermission(GuildPermission.KickMembers)]
        public async Task StartServerFeedbackAsync(string serverId = null, string fileName = null)
        {
            string fName = fileName;
            var server = DatabaseUtil.GetTestServer(serverId);

            //If null, get the not PT event.
            if(serverId == null)
            {
                var ptEvent = _calendar.GetNextPlaytestEvent();
                fName = _calendar.GetNextPlaytestEvent().GetFeedbackFileName();
                server = DatabaseUtil.GetTestServer(ptEvent.ServerLocation);
            }

            if (server == null)
            {
                await ReplyAsync(embed: new EmbedBuilder()
                    .WithAuthor($"Server {serverId} not found!")
                    .WithColor(new Color(165, 55, 55)).Build());
                return;
            }

            if(fileName == null)
            {
                await ReplyAsync(embed: new EmbedBuilder()
                    .WithAuthor($"You must provide a file name if you provide a server.")
                    .WithColor(new Color(165, 55, 55)).Build());
                return;
            }

            //Should build the name inside the test event and just get that back instead of saving it each time.
            var result = _srcdsLogService.CreateFeedbackFile(server, fName);

            if (result)
                await ReplyAsync(embed: new EmbedBuilder()
                    .WithAuthor("Started new feedback listener")
                    .WithDescription(
                        $"`{server.Address}` is now listening for feedback in game.")
                    .WithColor(new Color(55, 165, 55)).Build());
            else
                await ReplyAsync(embed: new EmbedBuilder()
                    .WithAuthor("Unable to start feedback listening")
                    .WithDescription("The server may already be listening for feedback.")
                    .WithColor(new Color(165, 55, 55)).Build());
        }

        [Command("StopFeedback", RunMode = RunMode.Async)]
        [Alias("stopfb")]
        [Summary("Stops server listening for in game feedback")]
        [Remarks("If all parameters are left empty, the next playtest event will be used. Otherwise specify a server.")]
        [RequireContext(ContextType.Guild)]
        [RequireUserPermission(GuildPermission.KickMembers)]
        public async Task StopServerFeedbackAsync(string serverId = null)
        {
            var server = DatabaseUtil.GetTestServer(serverId);

            //If null, get the not PT event.
            if (serverId == null)
                server = DatabaseUtil.GetTestServer(_calendar.GetNextPlaytestEvent().ServerLocation);

            if (server == null)
            {
                await ReplyAsync(embed: new EmbedBuilder()
                    .WithAuthor($"Server {serverId} not found!")
                    .WithColor(new Color(165, 55, 55)).Build());
                return;
            }

            var fbf = _srcdsLogService.GetFeedbackFile(server);

            if (fbf == null)
            {
                await ReplyAsync(embed: new EmbedBuilder()
                    .WithAuthor($"Server {server.Address} did not have a feedback session!")
                    .WithColor(new Color(165, 55, 55)).Build());
                return;
            }

            var feedbackPath = fbf.FileName;

            //Should build the name inside the test event and just get that back instead of saving it each time.
            var result = _srcdsLogService.RemoveFeedbackFile(server);

            if (result)
            {
                await ReplyAsync(embed: new EmbedBuilder()
                    .WithAuthor("Stopped feedback listener")
                    .WithDescription(
                        $"`{server.Address}` is no longer collecting feedback in game.")
                    .WithColor(new Color(55, 165, 55)).Build());
                
                if(File.Exists(feedbackPath))
                {
                    await Context.Channel.SendFileAsync(feedbackPath);
                    File.Delete(feedbackPath);
                }
            }
            else
                await ReplyAsync(embed: new EmbedBuilder()
                    .WithAuthor("Unable to stop feedback listening")
                    .WithDescription("The server may not have been listening for feedback.")
                    .WithColor(new Color(165, 55, 55)).Build());
        }

        [Command("Mute")]
        [Alias("Banish", "Yeet")]
        [Summary("Mutes a user.")]
        [Remarks("Mutes a user for a specified reason and duration. When picking a duration" +
                 "you may leave off any unit of time. For example `>Mute [user] 1D5H [reason]` will mute for 1 day 5 hours. " +
                 "Alternatively, if you don't specify a unit of time, minutes is assumed. `>Mute [user] 120 [reason]` will mute for 2 hours.\n\n" +
                 "A mute may be extended on a currently muted user if you start the mute reason with `e`. For example `>Mute [user] 1D e User keeps being difficult` " +
                 "will mute the user for 1 additional day, on top of their existing mute.")]
        [RequireContext(ContextType.Guild)]
        [RequireUserPermission(GuildPermission.KickMembers)]
        public async Task MuteAsync([Summary("User to mute")] SocketGuildUser user,
            [Summary("Length to mute for, in `%D%H%M%S` format")]
            TimeSpan muteLength,
            [Summary("Reason the user has been muted")] [Remainder]
            string reason)
        {
            //Convert to total minutes, used later for mute extensions
            var duration = muteLength.TotalMinutes;

            //Variables used if we are extending a mute.
            double oldMuteTime = 0;
            var muteStartTime = DateTime.Now;
            var added = false;

            //Setup the embed for later.
            var embed = new EmbedBuilder();

            //This is all for a shitpost on mods trying to mute admins
            if (user.Roles.Any(x => x.Id == _dataService.AdminRole.Id) || user.IsBot)
            {
                user = Context.User as SocketGuildUser;
                muteLength = new TimeSpan(0, 69, 0);
                string modReason = null;
                for (var i = 0; i < reason.Length; i++)
                    modReason += _random.Next(2) == 1
                        ? reason[i].ToString().ToLower()
                        : reason[i].ToString().ToUpper();

                //Set the reason string
                reason = modReason;

                added = DatabaseUtil.AddMute(new Mute
                {
                    UserId = Context.User.Id,
                    Username = Context.User.Username,
                    Reason = reason,
                    Duration = muteLength.TotalMinutes,
                    MuteTime = muteStartTime,
                    ModeratorId = Context.User.Id,
                    Expired = false
                });

                embed.WithThumbnailUrl(@"https://content.tophattwaffle.com/BotHATTwaffle/reverse.png");
            }

            if (reason.StartsWith("e ", StringComparison.OrdinalIgnoreCase))
            {
                //Get the old mute, and make sure it exists before removing it. Also need some data from it.
                var oldMute = DatabaseUtil.GetActiveMute(user.Id);

                if (oldMute != null)
                {
                    //Set vars for next mute
                    oldMuteTime = oldMute.Duration;
                    muteStartTime = oldMute.MuteTime;

                    //Unmute inside the DB
                    var result = DatabaseUtil.UnmuteUser(user.Id);

                    //Remove old mute from job manager
                    JobManager.RemoveJob($"[UnmuteUser_{user.Id}]");

                    reason = "Extended from previous mute:" + reason.Substring(reason.IndexOf(' '));
                }
            }

            //If a mod mute didn't bring us here
            if (!added)
                added = DatabaseUtil.AddMute(new Mute
                {
                    UserId = user.Id,
                    Username = user.Username,
                    Reason = reason,
                    Duration = duration + oldMuteTime,
                    MuteTime = muteStartTime,
                    ModeratorId = Context.User.Id,
                    Expired = false
                });

            if (added)
            {
                try
                {
                    await user.AddRoleAsync(_dataService.MuteRole);

                    //disconnect user from voice
                    if(user.VoiceChannel != null)
                        await user.ModifyAsync(x => x.Channel = null);

                    JobManager.AddJob(async () => await _dataService.UnmuteUser(user.Id), s => s
                        .WithName($"[UnmuteUser_{user.Id}]")
                        .ToRunOnceAt(DateTime.Now.AddMinutes(duration + oldMuteTime)));
                }
                catch
                {
                    await ReplyAsync("Failed to apply mute role, did the user leave the server?");
                    return;
                }

                string formatted = null;

                if (muteLength.Days != 0)
                    formatted += muteLength.Days == 1 ? $"{muteLength.Days} Day," : $"{muteLength.Days} Days,";

                if (muteLength.Hours != 0)
                    formatted += muteLength.Hours == 1 ? $" {muteLength.Hours} Hour," : $" {muteLength.Hours} Hours,";

                if (muteLength.Minutes != 0)
                    formatted += muteLength.Minutes == 1
                        ? $" {muteLength.Minutes} Minute,"
                        : $" {muteLength.Minutes} Minutes,";

                if (muteLength.Seconds != 0)
                    formatted += muteLength.Seconds == 1
                        ? $" {muteLength.Seconds} Second"
                        : $" {muteLength.Seconds} Seconds";

                //hahaha funny number
                if (muteLength.TotalMinutes == 69)
                    formatted = "69 minutes";

                reason = _dataService.RemoveChannelMentionStrings(reason);

                await ReplyAsync(embed: embed
                    .WithAuthor($"{user.Username} Muted")
                    .WithDescription(
                        $"Muted for: `{formatted.Trim().TrimEnd(',')}`\nBecause: `{reason}`")
                    .WithColor(new Color(165, 55, 55))
                    .Build());

                await _log.LogMessage(
                    $"`{Context.User}` muted `{user}` `{user.Id}`\nFor: `{formatted.Trim().TrimEnd(',')}`\nBecause: `{reason}`",
                    color: LOG_COLOR);

                try
                {
                    await user.SendMessageAsync(embed: embed
                        .WithAuthor("You have been muted")
                        .WithDescription(
                            $"You have been muted for: `{formatted.Trim().TrimEnd(',')}`\nBecause: `{reason}`")
                        .WithColor(new Color(165, 55, 55))
                        .Build());
                }
                catch
                {
                    //Can't DM then, send in void instead
                    await _dataService.VoidChannel.SendMessageAsync(embed: embed
                        .WithAuthor("You have been muted")
                        .WithDescription(
                            $"You have been muted for: `{formatted.Trim().TrimEnd(',')}`\nBecause: `{reason}`")
                        .WithColor(new Color(165, 55, 55))
                        .Build());
                }
            }
            else
            {
                await ReplyAsync(embed: embed
                    .WithAuthor($"Unable to mute {user.Username}")
                    .WithDescription($"I could not mute `{user.Username}` `{user.Id}` because they are already muted.")
                    .WithColor(new Color(165, 55, 55))
                    .Build());
            }
        }

        [Command("Unmute")]
        [Summary("Unmutes a user.")]
        [RequireContext(ContextType.Guild)]
        [RequireUserPermission(GuildPermission.KickMembers)]
        public async Task UnmuteAsync([Summary("User to unmute")] SocketGuildUser user)
        {
            var result = await _dataService.UnmuteUser(user.Id, $"{Context.User} removed the mute manually.");

            if (result)
            {
                await ReplyAsync(embed: new EmbedBuilder()
                    .WithAuthor($"{user.Username}")
                    .WithDescription($"`{user.Username}` `{user.Id}` has been unmuted by `{Context.User}`.")
                    .WithColor(new Color(165, 55, 55))
                    .Build());

                await _log.LogMessage($"`{user.Username}` `{user.Id}` has been unmuted by `{Context.User}`.");

                //Remove the scheduled job, because we are manually unmuting.
                JobManager.RemoveJob($"[UnmuteUser_{user.Id}]");
            }
            else
            {
                await ReplyAsync($"Failed to unmute `{user.Username}` `{user.Id}`");
            }
        }

        [Command("Mutes")]
        [Alias("MuteHistory")]
        [Summary("Shows active mutes or mute history for a specific user.")]
        [Remarks("If no parameters are provided, all active mutes for the server are shown." +
                 "\nIf a user is specific, the mute history for that user will be shown. A paged reply will be returned, " +
                 "along with a text file to let you see extended mute histories.")]
        [RequireContext(ContextType.Guild)]
        [RequireUserPermission(GuildPermission.KickMembers)]
        public async Task MutesAsync([Summary("User for which to get mute history")] [Optional]
            SocketGuildUser user)
        {
            var fullListing = "";

            var embed = new EmbedBuilder();
            //If null, get all the active mutes on the server.
            if (user == null)
            {
                embed.WithAuthor("Active Mutes in Server").WithColor(new Color(165, 55, 55));

                var allMutes = DatabaseUtil.GetAllActiveUserMutes();
                foreach (var mute in allMutes)
                {
                    var mod = _dataService.GetSocketUser(mute.ModeratorId);
                    var modString = mod == null ? $"{mute.ModeratorId}" : mod.ToString();
                    embed.AddField(mute.Username,
                        $"ID: `{mute.UserId}`\nMute Time: `{mute.MuteTime}`\nDuration: `{TimeSpan.FromMinutes(mute.Duration).ToString()}`\nReason: `{_dataService.RemoveChannelMentionStrings(mute.Reason)}`\nMuting Mod: `{modString}`");
                }

                if (allMutes.ToArray().Length == 0)
                {
                    embed.WithColor(55, 165, 55);
                    embed.AddField("No active mutes found", "I'm so proud of this community.");
                }
            }
            else
            {
                var allMutes = DatabaseUtil.GetAllUserMutes(user.Id);

                embed.WithAuthor($"All Mutes for {user.Username} - {user.Id}").WithColor(new Color(165, 55, 55));

                if (allMutes.Count() >= 5)
                {
                    //Create string to text file to send along with the embed
                    foreach (var muteFull in allMutes.Reverse())
                        fullListing += muteFull + "\n------------------------\n";

                    //Send the text file before the interactive embed
                    Directory.CreateDirectory("Mutes");
                    for (int i = 0; i < 4; i++)
                    {
                        try
                        {
                            File.WriteAllText($"Mutes\\AllMutes_{user.Id}.txt", fullListing);
                            break;
                        }
                        catch (Exception e)
                        {
                            Console.WriteLine(e);
                            await Task.Delay(5000);
                        }
                    }
                    await Context.Channel.SendFileAsync($"Mutes\\AllMutes_{user.Id}.txt");

                    //Paged reply
                    var lists = new List<string>();
                    var pageList = new PaginatedMessage
                    {
                        Title = $"All Mutes for {user.Username} - {user.Id}",
                        Color = new Color(165, 55, 55)
                    };
                    pageList.Options.DisplayInformationIcon = false;
                    pageList.Options.JumpDisplayOptions = JumpDisplayOptions.Never;

                    //Build the pages for the interactive embed
                    var counter = 0;
                    fullListing = null;
                    foreach (var mutePage in allMutes.Reverse())
                    {
                        var mod = _dataService.GetSocketUser(mutePage.ModeratorId);
                        var modString = mod == null ? $"{mutePage.ModeratorId}" : mod.ToString();

                        fullListing += $"**{mutePage.MuteTime.ToString()}**" +
                                       $"\nDuration: `{TimeSpan.FromMinutes(mutePage.Duration).ToString()}`" +
                                       $"\nReason: `{_dataService.RemoveChannelMentionStrings(mutePage.Reason)}`" +
                                       $"\nMuting Mod: `{modString}`\n\n";

                        counter++;
                        if (counter >= 5)
                        {
                            lists.Add(fullListing);
                            fullListing = null;
                            counter = 0;
                        }
                    }

                    //Add any left overs to the pages
                    lists.Add(fullListing);

                    //Send the page
                    pageList.Pages = lists;
                    await PagedReplyAsync(pageList);
                    return;
                }

                foreach (var mute in allMutes.Reverse())
                {
                    var mod = _dataService.GetSocketUser(mute.ModeratorId);
                    var modString = mod == null ? $"{mute.ModeratorId}" : mod.ToString();
                    fullListing += $"**{mute.MuteTime.ToString()}**" +
                                   $"\nDuration: `{TimeSpan.FromMinutes(mute.Duration).ToString()}`" +
                                   $"\nReason: `{_dataService.RemoveChannelMentionStrings(mute.Reason)}`" +
                                   $"\nMuting Mod: `{modString}`\n\n";
                }

                embed.WithDescription(fullListing);

                if (allMutes.ToArray().Length == 0)
                {
                    embed.WithColor(55, 165, 55);
                    embed.AddField($"No active mutes found for {user.Username}", "I'm so proud of this user.");
                }
            }

            await ReplyAsync(embed: embed.Build());
        }

        [Command("Playtest", RunMode = RunMode.Async)]
        [Alias("p")]
        [Summary("Handles playtesting functions.")]
        [Remarks(
            "This command contains many sub commands for various playtesting functions. For this command to work, a playtest event must be active. " +
            "Command syntax is `>p [subcommand]`, for example `>p post`\n\n" +
            "`pre` / `prestart` - Pre-start the playtest. Required before a playtest can go live. Always run this before running `start`.\n" +
            "`start` - Starts the playtest, including recording the demo file.\n" +
            "`post` - Run when the gameplay portion of the playtest is complete. This will reload the map and get postgame " +
            "features enabled on the test server. It will also handle downloading the demo and giving it to the creators.\n" +
            "`p` / `pause` - Pauses a live test.\n" +
            "`u` / `unpause` - Unpauses a live test.\n" +
            "`s` / `scramble` - Scrambles teams on test server. This command will restart the test. Don't run it after running `start`\n" +
            "`k` / `kick` - Kicks a player from the playtest.\n" +
            "`end` - Officially ends a playtest which allows community server reservations.\n" +
            "`reset` - Resets the running flag. Really should not need to be used except edge cases.")]
        [RequireContext(ContextType.Guild)]
        [RequireUserPermission(GuildPermission.KickMembers)]
        public async Task PlaytestAsync([Summary("Playtesting Sub-command")] string command)
        {
            if (command.Equals("reset", StringComparison.OrdinalIgnoreCase))
            {
                _playtestService.ResetCommandRunningFlag();
                await ReplyAsync(embed: new EmbedBuilder()
                    .WithDescription("The running flag has been reset.")
                    .WithColor(55, 165, 55)
                    .Build());
                return;
            }

            //Not valid - abort
            if (!_calendar.GetNextPlaytestEvent().PlaytestCommandPreCheck())
            {
                await ReplyAsync(embed: new EmbedBuilder()
                    .WithDescription("There is already a playtest command running. Only 1 may be running at a time." +
                                     " To force a reset of the running flag, use `>p reset`. This only needs to be done if there" +
                                     "was some issue with the Discord API.\n\n" +
                                     "Or no valid test is found.")
                    .WithColor(165, 55, 55)
                    .Build());
                return;
            }

            //Setup a few variables we'll need later
            PlaytestCommandInfo playtestCommandInfo;
            switch (command.ToLower())
            {
                case "prestart":
                case "pre":
                    var preMessage = await ReplyAsync(embed: new EmbedBuilder()
                        .WithDescription("⏰ Running Playtest Pre-start...").WithColor(new Color(165, 55, 55)).Build());

                    playtestCommandInfo = await _playtestService.PlaytestCommandPre(true);
                    await ReplyAsync(embed: new EmbedBuilder()
                        .WithAuthor($"Pre-start playtest of {playtestCommandInfo.Title}")
                        .WithColor(new Color(55, 55, 165))
                        .WithDescription($"\nOn **{playtestCommandInfo.ServerAddress}**" +
                                         $"\nWith config of **{playtestCommandInfo.Mode}**" +
                                         $"\nWorkshop ID **{playtestCommandInfo.WorkshopId}**").Build());

                    await preMessage.DeleteAsync();
                    break;

                case "start":
                    var startMessage = await ReplyAsync(embed: new EmbedBuilder()
                        .WithDescription("⏰ Running Playtest Start...").WithColor(new Color(165, 55, 55)).Build());

                    playtestCommandInfo = await _playtestService.PlaytestCommandStart(true);
                    await ReplyAsync(embed: new EmbedBuilder()
                        .WithAuthor($"Start playtest of {playtestCommandInfo.Title}")
                        .WithColor(new Color(55, 55, 165))
                        .WithDescription($"\nOn **{playtestCommandInfo.ServerAddress}**" +
                                         $"\nWith config of **{playtestCommandInfo.Mode}**" +
                                         $"\nWorkshop ID **{playtestCommandInfo.WorkshopId}**" +
                                         $"\nDemo Name **{playtestCommandInfo.DemoName}**").Build());

                    await startMessage.DeleteAsync();
                    break;

                case "post":
                    var postMessage = await ReplyAsync(embed: new EmbedBuilder()
                        .WithDescription("⏰ Running Playtest post...").WithColor(new Color(165, 55, 55)).Build());

                    playtestCommandInfo = await _playtestService.PlaytestCommandPost(true);
                    await ReplyAsync(embed: new EmbedBuilder()
                        .WithAuthor($"Post playtest of {playtestCommandInfo.Title}")
                        .WithColor(new Color(55, 55, 165))
                        .WithDescription($"\nOn **{playtestCommandInfo.ServerAddress}**" +
                                         $"\nWorkshop ID **{playtestCommandInfo.WorkshopId}**" +
                                         $"\nDemo Name **{playtestCommandInfo.DemoName}**").Build());

                    await postMessage.DeleteAsync();
                    break;

                case "pause":
                case "p":
                    playtestCommandInfo = await _playtestService.PlaytestcommandGenericAction(true,
                        "mp_pause_match;say Pausing Match!;say Pausing Match!;say Pausing Match!;say Pausing Match!");

                    await ReplyAsync(embed: new EmbedBuilder()
                        .WithAuthor($"Pausing Playtest On {playtestCommandInfo.ServerAddress}")
                        .WithColor(new Color(55, 55, 165))
                        .Build());
                    break;

                case "unpause":
                case "u":
                    playtestCommandInfo = await _playtestService.PlaytestcommandGenericAction(true,
                        "mp_unpause_match;say Unpausing Match!;say Unpausing Match!;say Unpausing Match!;say Unpausing Match!");

                    await ReplyAsync(embed: new EmbedBuilder()
                        .WithAuthor($"Unpausing Playtest On {playtestCommandInfo.ServerAddress}")
                        .WithColor(new Color(55, 55, 165))
                        .Build());
                    break;

                case "scramble":
                case "s":
                    playtestCommandInfo = await _playtestService.PlaytestcommandGenericAction(true,
                        "mp_scrambleteams 1;say Scrambling Teams!;say Scrambling Teams!;say Scrambling Teams!;say Scrambling Teams!");

                    await ReplyAsync(embed: new EmbedBuilder()
                        .WithAuthor($"Scrambling teams on {playtestCommandInfo.ServerAddress}")
                        .WithColor(new Color(55, 55, 165))
                        .Build());
                    break;

                case "kick":
                case "k":
                    playtestCommandInfo = _calendar.GetNextPlaytestEvent().PlaytestCommandInfo;
                    var kick = new KickUserRcon(Context, _interactive, _rconService, _log);
                    await kick.KickPlaytestUser(playtestCommandInfo.ServerAddress);
                    _playtestService.ResetCommandRunningFlag();
                    break;

                case "end":
                    //Allow manual enabling of community reservations
                    _reservationService.AllowReservations();
                    await ReplyAsync("```Community servers may now be reserved.```");

                    break;
                default:
                    await ReplyAsync("Invalid action, please consult the help document for this command.");
                    break;
            }
        }

        [Command("Active")]
        [Summary("Grants a user the Active Memeber role.")]
        [RequireContext(ContextType.Guild)]
        [RequireUserPermission(GuildPermission.KickMembers)]
        public async Task ActiveAsync([Summary("User to give role to.")] SocketGuildUser user)
        {
            if (user.Roles.Any(x => x.Id == _dataService.ActiveRole.Id))
            {
                await Context.Message.DeleteAsync();
                await user.RemoveRoleAsync(_dataService.ActiveRole);
                await _log.LogMessage($"{user} has {_dataService.ActiveRole} removed by {Context.User}");
            }
            else
            {
                await _log.LogMessage($"{user} has been given {_dataService.ActiveRole.Mention} by {Context.User}");
                await ReplyAsync(embed: new EmbedBuilder()
                    .WithAuthor($"{user.Username} is now an active member!")
                    .WithDescription(
                        $"The {_dataService.ActiveRole.Mention} is given to users who are active and helpful in our community. " +
                        "Thanks for contributing!")
                    .WithColor(new Color(241, 196, 15))
                    .Build());
                await user.AddRoleAsync(_dataService.ActiveRole);
            }
        }

        [Command("CompetitiveTester")]
        [Summary("Grants a user the Competitive Tester role.")]
        [Alias("comp")]
        [RequireContext(ContextType.Guild)]
        [RequireUserPermission(GuildPermission.KickMembers)]
        public async Task CompetitiveTesterAsync([Summary("User to give role to")] SocketGuildUser user)
        {
            if (user.Roles.Any(x => x.Id == _dataService.CompetitiveTesterRole.Id))
            {
                await Context.Message.DeleteAsync();
                await user.RemoveRoleAsync(_dataService.CompetitiveTesterRole);
                await _log.LogMessage($"{user} has {_dataService.CompetitiveTesterRole} removed by {Context.User}");
            }
            else
            {
                await ReplyAsync(embed: new EmbedBuilder()
                    .WithAuthor($"{user.Username} is now a Competitive Tester!")
                    .WithDescription(
                        $"The {_dataService.CompetitiveTesterRole.Mention} is given to users who contribute positively to the playtesting service. " +
                        "Such as attending tests, giving valid feedback, and making smart plays.")
                    .WithColor(new Color(52, 152, 219))
                    .Build());

                await user.AddRoleAsync(_dataService.CompetitiveTesterRole);
                await _log.LogMessage($"{user} has been given {_dataService.CompetitiveTesterRole} by {Context.User}");
            }
        }

        [Command("Invite")]
        [Summary("Invites a user to a competitive level test.")]
        [RequireContext(ContextType.Guild)]
        [RequireUserPermission(GuildPermission.KickMembers)]
        public async Task CompInviteAsync([Summary("User to invite")] SocketGuildUser user)
        {
            await Context.Message.DeleteAsync();

            //Get the next CSGO test
            var nextTest = _calendar.GetNextPlaytestEvent(PlaytestEvent.Games.CSGO) as CsgoPlaytestEvent;

            //Do nothing if a test is not valid.
            if (!nextTest.IsValid || nextTest.IsCasual)
            {
                await ReplyAsync("There is no valid test that I can invite that user to.");
                return;
            }

            await _log.LogMessage(
                $"`{user}` has been invited to the competitive test of `{nextTest.Title}` by `{Context.User}`");

            try
            {
                await user.SendMessageAsync(
                    $"You've been invited to join __**{nextTest.Title}**__!\n" +
                    "Open Counter-Strike Global Offensive and paste the following into console to join:" +
                    $"```connect {nextTest.ServerLocation}; password {nextTest.CompPassword}```");
            }
            catch
            {
                await ReplyAsync("I attempted to DM that user connection information, but they don't allow DMs.");
            }
        }

        [Command("rcon", RunMode = RunMode.Async)]
        [Alias("r")]
        [RequireUserPermission(GuildPermission.KickMembers)]
        [RequireContext(ContextType.Guild)]
        [Summary("Sends RCON commands to a server.")]
        [Remarks("Sends RCON commands to a test server.\n" +
                 "You can use `>rcon auto` to automatically use the next playtest server.\n" +
                 "You can specify a server be specified before commands are sent.\n" +
                 "Set a server using `>rcon set [serverId]\n" +
                 "Then commands can be sent as normal without a server ID:\n" +
                 "Example: `>r sv_cheats 1`\n" +
                 "Provide no parameters to see what server you're current sending to.")]
        public async Task RconAsync([Summary("Rcon command to send")] [Remainder] [Optional]
            string command)
        {
            var testEvent = _calendar.GetNextPlaytestEvent();
            string targetServer = null;
            if (command == null)
            {
                if (ServerDictionary.ContainsKey(Context.User.Id))
                {
                    targetServer = ServerDictionary[Context.User.Id];
                }
                else
                {
                    targetServer = "No playtest server found";
                    if (testEvent != null && testEvent.IsValid)
                    {
                        //There is a playtest event, get the server ID from the test event
                        var serverAddress = testEvent.ServerLocation;
                        targetServer = serverAddress.Substring(0, serverAddress.IndexOf('.'));
                    }

                    await ReplyAsync(embed: new EmbedBuilder()
                        .WithAuthor($"RCON commands sent by {Context.User}", _dataService.Guild.IconUrl)
                        .WithDescription(
                            "Will be sent using `Auto mode`. Which is the active playtest server, if there is one.\n" +
                            $"Current test server: `{targetServer}`")
                        .WithColor(new Color(55, 165, 55)).Build());
                    return;
                }

                await ReplyAsync(embed: new EmbedBuilder()
                    .WithAuthor($"RCON commands sent by {Context.User}", _dataService.Guild.IconUrl)
                    .WithDescription($"will be sent to `{targetServer}`")
                    .WithColor(new Color(55, 165, 55)).Build());
                return;
            }

            //Set server mode
            if (command.StartsWith("set", StringComparison.OrdinalIgnoreCase))
            {
                //Set user's mode to Auto, which is really just removing a user from the dictionary
                if (command.Substring(3).Trim().StartsWith("auto", StringComparison.OrdinalIgnoreCase))
                {
                    if (ServerDictionary.ContainsKey(Context.User.Id)) ServerDictionary.Remove(Context.User.Id);
                    await ReplyAsync(embed: new EmbedBuilder()
                        .WithAuthor($"RCON commands sent by {Context.User}", _dataService.Guild.IconUrl)
                        .WithDescription(
                            "will be sent using `Auto mode`. Which is the active playtest server, if there is one.")
                        .WithColor(new Color(55, 165, 55)).Build());
                    return;
                }

                var server = DatabaseUtil.GetTestServer(command.Substring(3).Trim());

                if (server == null)
                {
                    await ReplyAsync(embed: new EmbedBuilder()
                        .WithAuthor("Cannot set RCON server", _dataService.Guild.IconUrl)
                        .WithDescription($"No server found with the name {command.Substring(3).Trim()}")
                        .WithColor(new Color(165, 55, 55)).Build());
                    return;
                }

                //Dictionary contains user already, remove them.
                if (ServerDictionary.ContainsKey(Context.User.Id)) ServerDictionary.Remove(Context.User.Id);
                ServerDictionary.Add(Context.User.Id, command.Substring(3).Trim());
                await ReplyAsync(embed: new EmbedBuilder()
                    .WithAuthor($"RCON commands sent by {Context.User}", _dataService.Guild.IconUrl)
                    .WithDescription($"will be sent to `{ServerDictionary[Context.User.Id]}`")
                    .WithColor(new Color(55, 165, 55)).Build());
                return;
            }

            //In auto mode
            if (!ServerDictionary.ContainsKey(Context.User.Id))
            {
                if (testEvent != null && testEvent.IsValid)
                {
                    //There is a playtest event, get the server ID from the test event
                    var serverAddress = testEvent.ServerLocation;
                    targetServer = serverAddress.Substring(0, serverAddress.IndexOf('.'));
                }
                else
                {
                    //No playtest event, we cannot do anything.
                    await ReplyAsync(embed: new EmbedBuilder()
                        .WithAuthor("No playtest server found.", _dataService.Guild.IconUrl)
                        .WithDescription("Set your target server using `>rcon set [serverId]`.")
                        .WithColor(new Color(55, 165, 55)).Build());
                    return;
                }
            }
            else
                //User has a server set manually.
            {
                targetServer = ServerDictionary[Context.User.Id];
            }

            //Quick kick feature
            if (command.StartsWith("kick", StringComparison.OrdinalIgnoreCase))
            {
                var kick = new KickUserRcon(Context, _interactive, _rconService, _log);
                await kick.KickPlaytestUser(targetServer);
                return;
            }

            if (Context.User.Id != _dataService.AlertUser.Id &&
                (command.Contains("exit", StringComparison.OrdinalIgnoreCase) ||
                 command.Contains("quit", StringComparison.OrdinalIgnoreCase)))
            {
                await ReplyAsync(embed: new EmbedBuilder()
                    .WithAuthor("Quit and Exit not allowed!", _dataService.Guild.IconUrl)
                    .WithColor(new Color(165, 55, 55)).Build());
                return;
            }

            await Context.Channel.TriggerTypingAsync();
            string reply;
            IUserMessage delayed = null;
            var rconCommand = _rconService.RconCommand(targetServer, command);
            var waiting = Task.Delay(4000);
            if (rconCommand == await Task.WhenAny(rconCommand, waiting))
            {
                reply = await rconCommand;
            }
            else
            {
                delayed = await ReplyAsync(embed: new EmbedBuilder()
                    .WithDescription(
                        $"⏰RCON command to `{targetServer}` is taking longer than normal...\nSit tight while I'll " +
                        "try a few more times.")
                    .WithColor(new Color(165, 55, 55)).Build());
                reply = await rconCommand;
            }

            if (reply.Length > 1900)
                reply = reply.Substring(0, 1900);
            
            await ReplyAsync(embed: new EmbedBuilder()
                .WithAuthor($"Command sent to {targetServer}", _dataService.Guild.IconUrl)
                .WithDescription($"```{reply}```")
                .WithColor(new Color(55, 165, 55)).Build());

            if (delayed != null)
                await delayed.DeleteAsync();
        }

        [Command("Reservation")]
        [Alias("EditReservation", "er")]
        [Summary("Edits server reservations.")]
        [Remarks("`>er` Clears all reservations." +
                 "\n`>er [ServerId]` clears a specific reservation." +
                 "\n`>er [on/enable]` allows server reservations." +
                 "\n`>er [off/disable]` disables server reservations.")]
        [RequireContext(ContextType.Guild)]
        [RequireUserPermission(GuildPermission.KickMembers)]
        public async Task ClearReservationAsync([Summary("ID of test server to clear")] [Optional] [Remainder]
            string command)
        {
            if (command == null)
                command = "clear";
            switch (command.ToLower())
            {
                case "off":
                case "disable":
                    await _reservationService.DisableReservations();
                    await ReplyAsync(embed: new EmbedBuilder()
                        .WithAuthor("Server reservations disabled", _dataService.Guild.IconUrl)
                        .WithColor(new Color(165, 55, 55)).Build());
                    break;
                case "on":
                case "enable":
                    _reservationService.AllowReservations();
                    await ReplyAsync(embed: new EmbedBuilder()
                        .WithAuthor("Allowing server reservations", _dataService.Guild.IconUrl)
                        .WithColor(new Color(55, 165, 55)).Build());
                    break;

                default:
                    if (command != "clear")
                    {
                        var reservation = DatabaseUtil.GetServerReservation(command);

                        if (reservation != null)
                        {
                            await _dataService.BotChannel.SendMessageAsync($"{_dataService.GetSocketGuildUser(reservation.UserId).Mention}"
                                ,embed: _reservationService.ReleaseServer(reservation.UserId,
                                "A moderator has cleared your reservation.", _dataService.BotChannel));

                            await ReplyAsync(embed: new EmbedBuilder()
                                .WithAuthor($"{DatabaseUtil.GetTestServer(command).Address} has been released.",
                                    _dataService.Guild.IconUrl)
                                .WithColor(new Color(55, 165, 55)).Build());
                            return;
                        }

                        await ReplyAsync(embed: new EmbedBuilder()
                            .WithAuthor("No server reservation found to release", _dataService.Guild.IconUrl)
                            .WithColor(new Color(165, 55, 55)).Build());
                    }

                    await ReplyAsync(embed: new EmbedBuilder()
                        .WithAuthor("Clearing all reservations", _dataService.Guild.IconUrl)
                        .WithColor(new Color(165, 55, 55)).Build());

                    await _reservationService.ClearAllServerReservations();
                    break;
            }
        }

        [Command("TestServer")]
        [Alias("ts")]
        [RequireUserPermission(GuildPermission.Administrator)]
        [Summary("Command for manipulating test servers.")]
        [Remarks("`>TestServer get [ServerCode / all]`\n" +
                 "`>TestServer remove [ServerCode]`\n\n" +
                 "Adding a server requires the information provided with each variable on a new line after the invoking command." +
                 "`>TestServer add\n" +
                 "[ServerId]\n" +
                 "[Description]\n" +
                 "[Address]\n" +
                 "[RconPassword]\n" +
                 "[FtpUser]\n" +
                 "[FtpPassword]\n" +
                 "[FtpPath]\n" +
                 "[FtpType]\n" +
                 "[GameType]`\n\n" +
                 "Getting a single test server will reply with the required information to re-add the server into the database. " +
                 "This is useful when editing servers.")]
        public async Task TestServerAsync(string action, [Remainder] string values = null)
        {
            //Add server
            if (action.StartsWith("a", StringComparison.OrdinalIgnoreCase))
            {
                //Need command values, abort if we don't have them.
                if (values == null)
                {
                    await ReplyAsync("No command provided");
                    return;
                }

                var serverValues = values.Split("\n");

                //Make sure all the data is present, as all values are required
                if (serverValues.Length != 9)
                {
                    await ReplyAsync("Adding a server requires all 9 server values.");
                    await Context.Message.DeleteAsync();
                    return;
                }

                //Validate FTP type before entry
                switch (serverValues[7])
                {
                    case "ftp":
                        break;
                    case "sftp":
                        break;
                    case "ftps":
                        break;
                    default:
                        await ReplyAsync("Invalid FTP type. Please provide `ftp`, `ftps`, or `sftp` and try again." +
                                         "\nYour message was deleted as it may have contained a password.");
                        await Context.Message.DeleteAsync();
                        return;
                }

                if (DatabaseUtil.AddTestServer(new Server
                {
                    ServerId = serverValues[0],
                    Description = serverValues[1],
                    Address = serverValues[2],
                    RconPassword = serverValues[3],
                    FtpUser = serverValues[4],
                    FtpPassword = serverValues[5],
                    FtpPath = serverValues[6],
                    FtpType = serverValues[7].ToLower(),
                    Game = serverValues[8]
                }))
                {
                    await ReplyAsync("Server added!\nI deleted your message since it had passwords in it.");
                    await Context.Message.DeleteAsync();
                    return;
                }

                await ReplyAsync(
                    "Issue adding server, does it already exist?\nI deleted your message since it had passwords in it.");
                await Context.Message.DeleteAsync();
            }
            //Get server
            else if (action.StartsWith("g"))
            {
                var reply = $"No server found with server code {values}";
                if (values != null && !values.Equals("all", StringComparison.OrdinalIgnoreCase))
                {
                    var testServer = DatabaseUtil.GetTestServer(values);

                    if (testServer != null)
                        reply = $"Found the following server:\n{testServer}\n\n" +
                                "Use the following command to re-add this server to the database.\n" +
                                "```" +
                                ">TestServer add" +
                                $"\n{testServer.ServerId}" +
                                $"\n{testServer.Description}" +
                                $"\n{testServer.Address}" +
                                $"\n{testServer.RconPassword}" +
                                $"\n{testServer.FtpUser}" +
                                $"\n{testServer.FtpPassword}" +
                                $"\n{testServer.FtpPath}" +
                                $"\n{testServer.FtpType}" +
                                $"\n{testServer.Game}" +
                                "```";

                    await _dataService.AlertUser.SendMessageAsync(reply);
                }
                //Get all servers instead
                else
                {
                    var testServers = DatabaseUtil.GetAllTestServers();

                    if (testServers != null)
                    {
                        reply = null;
                        foreach (var testServer in testServers) reply += "```" + testServer + "```";
                    }
                    else
                    {
                        reply = "Could not get all servers because the request returned null.";
                    }

                    await _dataService.AlertUser.SendMessageAsync(reply);
                }

                await ReplyAsync(
                    $"Server information contains passwords, as a result I have DM'd it to {_dataService.AlertUser}.");
            }
            //Remove server
            else if (action.StartsWith("r"))
            {
                if (DatabaseUtil.RemoveTestServer(values))
                    await ReplyAsync($"Server with the ID: `{values}` was removed.");
                else
                    await ReplyAsync(
                        $"Could not remove a server with the ID of: `{values}`. It likely does not exist in the DB.");
            }
        }

        [Command("ForceAnnounce")]
        [Alias("fa")]
        [RequireUserPermission(GuildPermission.KickMembers)]
        [Summary("Allows manual announcing of a playtest. This command mentions the playtester role.")]
        public async Task ForceAnnounceAsync()
        {
            await _playtestService.PlaytestStartingInTask(_calendar.GetNextPlaytestEvent());
        }

        [Command("CallAllTesters")]
        [Alias("cat")]
        [RequireUserPermission(GuildPermission.KickMembers)]
        [Summary("Mentions the normal playtest role for a competitive test.")]
        public async Task CallAllTestersAsync(int neededPlayers)
        {
            await _playtestService.CallNormalTesters(neededPlayers);
        }

        [Command("SkipAnnounce")]
        [Alias("sa")]
        [RequireUserPermission(GuildPermission.KickMembers)]
        [Summary("Toggles the next playtest announcement.")]
        [Remarks(
            "Toggles if the next playtest announcement happens. This will allow you to prevent the 1 hour, or starting " +
            "playtest announcement messages from happening. Server setup tasks are still preformed, just the message is skipped. " +
            "After server setup tasks run, the flag is reset. Meaning if you disable the 1 hour announcement, the starting announcement " +
            "will still go out unless you disable it after the 1 hour announcement would have gone out.")]
        public async Task SkipAnnounceAsync()
        {
            //Toggle the announcement state
            var status = _dataService.ToggleStartAlert();

            await ReplyAsync(embed: new EmbedBuilder()
                .WithAuthor($"Next Playtest Alert is: {status}")
                .WithColor(status ? new Color(55, 165, 55) : new Color(165, 55, 55))
                .Build());
        }

        [Command("Debug")]
        [RequireUserPermission(GuildPermission.Administrator)]
        [Summary("Debug settings.")]
        [Remarks("View or change the debug flag." +
                 "\n`>debug [true/false/reload]` to set the flag, or reload settings from the settings file.")]
        public async Task DebugAsync(string status = null)
        {
            if (status == null)
            {
                await Context.Channel.SendMessageAsync(
                    $"Current debug status is: `{_dataService.RSettings.ProgramSettings.Debug}`");
            }
            else if (status.StartsWith("t", StringComparison.OrdinalIgnoreCase))
            {
                _dataService.RSettings.ProgramSettings.Debug = true;
                await _dataService.UpdateRolesAndUsers();
                await Context.Channel.SendMessageAsync(
                    $"Changed debug status to: `{_dataService.RSettings.ProgramSettings.Debug}`");
            }
            else if (status.StartsWith("f", StringComparison.OrdinalIgnoreCase))
            {
                _dataService.RSettings.ProgramSettings.Debug = false;
                await _dataService.UpdateRolesAndUsers();
                await Context.Channel.SendMessageAsync(
                    $"Changed debug status to: `{_dataService.RSettings.ProgramSettings.Debug}`");
            }
            else if (status.StartsWith("r", StringComparison.OrdinalIgnoreCase))
            {
                await _dataService.DeserializeConfig();
                await Context.Channel.SendMessageAsync(
                    "Deserializing configuration...");
            }
        }

        [Command("ModifyTools")]
        [Alias("mt")]
        [RequireUserPermission(GuildPermission.KickMembers)]
        [Summary("Command for manipulating tool prompts.")]
        [Remarks("`>mt delete [toolCommand]` removes a tool from the DB." +
                 "\n`>mt get [toolCommand]` gets the back end info for the tool. Useful for re-adding or seeing what" +
                 "is currently set for that tool. Can also be used as an example of expected input." +
                 "\n`>mt add [command]`" +
                 "\n`[toolName]`" +
                 "\n`[url]`" +
                 "\n`[ThumbnailUrl]`" +
                 "\n`[Color]`" +
                 "\n`[Description]`" +
                 "\n\nWhen adding new tools, the command is splitting on new line. So the description cannot have any " +
                 "line breaks. To see what you should provide type `>mt get [existingTool]`")]
        public async Task ModifyToolsAsync(string action, [Remainder] string values = null)
        {
            //Make sure we at least have values in the params
            if (values == null)
            {
                await ReplyAsync("You must provide a Tool Command. Check `>help mt`");
                return;
            }

            if (action.StartsWith("d", StringComparison.OrdinalIgnoreCase))
            {
                var result = DatabaseUtil.RemoveTool(values);

                if (result)
                {
                    await ReplyAsync($"I've removed the Tool `{values}`");
                    //Reload the tools since we have changed something.
                    _toolsService.LoadTools();
                }
                else
                {
                    await ReplyAsync($"Could not remove `{values}`, does it even exist?");
                }
            }
            else if (action.StartsWith("g", StringComparison.OrdinalIgnoreCase))
            {
                var tool = _toolsService.GetTool(values);

                if (tool == null)
                {
                    await ReplyAsync($"Could not find `{values}`, does it even exist?");
                    return;
                }

                string backendText = $"{tool.Command}" +
                                     $"\n{tool.AuthorName}" +
                                     $"\n{tool.Url}" +
                                     $"\n{tool.ThumbnailUrl}" +
                                     $"\n{tool.Color}" +
                                     $"\n{tool.Description}";

                await ReplyAsync($"The backend for this tool is in the code block below. This is what you'd " +
                                 $"give me to add the same tool into the DB." +
                                 $"```>mt add {backendText}```");
            }
            else if (action.StartsWith("a", StringComparison.OrdinalIgnoreCase))
            {
                var toolValues = values.Split("\n");

                //Make sure all the data is present, as all values are required
                if (toolValues.Length != 6)
                {
                    await ReplyAsync("Adding a tool requires all 6 tool values.");
                    return;
                }

                //Data validate
                var uri = GeneralUtil.ValidateUri(toolValues[2]);
                if (uri == null)
                {
                    await ReplyAsync($"`{toolValues[2]}` is not a valid URL.");
                    return;
                }
                uri = GeneralUtil.ValidateUri(toolValues[3]);
                if (uri == null)
                {
                    await ReplyAsync($"`{toolValues[3]}` is not a valid URL.");
                    return;
                }

                var colorSplit = toolValues[4].Split(" ");
                if (colorSplit.Length != 3)
                {
                    await ReplyAsync($"{toolValues[4]} is not a valid color format. Format as `R G B` Ex: `128 50 20`");
                    return;
                }

                foreach (var s in colorSplit)
                {
                    int i;
                    var result = int.TryParse(s, out i);

                    if (!result || i < 0 || i > 255)
                    {
                        await ReplyAsync($"`{s}` in color is not an integer between 0 and 255.");
                        return;
                    }
                }

                if (DatabaseUtil.AddTool(new Tool
                {
                    Command = toolValues[0],
                    AuthorName = toolValues[1],
                    Url = toolValues[2],
                    ThumbnailUrl = toolValues[3],
                    Color = toolValues[4],
                    Description = toolValues[5]
                }))
                {
                    await ReplyAsync("Tool added!");
                    //Reload the tools since we have changed something.
                    _toolsService.LoadTools();
                }
                else
                {
                    await ReplyAsync("Failed adding tool. A tool with that command may already exist.");
                }

            }
            else
            {
                await ReplyAsync("Unknown parameters. Check `>help mt`");
            }
        }
    }
}