using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using BotHATTwaffle2.Handlers;
using BotHATTwaffle2.Models.LiteDB;
using BotHATTwaffle2.Services;
using BotHATTwaffle2.Services.Calendar;
using BotHATTwaffle2.Services.Playtesting;
using BotHATTwaffle2.src.Handlers;
using Discord;
using Discord.Commands;
using Discord.WebSocket;

namespace BotHATTwaffle2.Commands
{
    public class ModerationModule : ModuleBase<SocketCommandContext>
    {
        private readonly GoogleCalendar _calendar;
        private readonly DiscordSocketClient _client;
        private readonly DataService _data;
        private readonly LogHandler _log;
        private readonly PlaytestService _playtestService;
        private const ConsoleColor LOG_COLOR = ConsoleColor.DarkRed;
        private static readonly Dictionary<ulong, string> ServerDictionary = new Dictionary<ulong, string>();
        private static PlaytestCommandInfo _playtestCommandInfo;

        public ModerationModule(DataService data, DiscordSocketClient client, LogHandler log, GoogleCalendar calendar,
            PlaytestService playtestService)
        {
            _playtestService = playtestService;
            _calendar = calendar;
            _data = data;
            _client = client;
            _log = log;
        }

        [Command("test")]
        public async Task TestAsync()
        {
            await _playtestService.PostOrUpdateAnnouncement();
        }

        [Command("Mute")]
        [Remarks(@"Format for duration is `%D%H%M%S` where any unit can be omitted")]
        [RequireContext(ContextType.Guild)]
        [RequireUserPermission(GuildPermission.KickMembers)]
        public async Task MuteAsync(SocketGuildUser user, string asd, [Remainder]string reason)
        {
            string muteLength = "5D12H30M15S";

            int days = 0;
            int hours = 0;
            int minutes = 0;
            int seconds = 0;

            string value = new string(muteLength.TakeWhile(char.IsDigit).ToArray());

            Console.WriteLine($"{value}\nD{days} H{hours} M{minutes} S{seconds}");

//            double duration = 0;
//                var added = DatabaseHandler.AddMute(new Mute
//                {
//                    UserId = user.Id,
//                    Username = user.Username,
//                    Reason = reason,
//                    Duration = duration,
//                    MuteTime = DateTime.Now,
//                    ModeratorId = Context.User.Id,
//                    Expired = false
//                });
        }

        [Command("Playtest", RunMode = RunMode.Async)]
        [Alias("p")]
        [RequireContext(ContextType.Guild)]
        [RequireUserPermission(GuildPermission.KickMembers)]
        public async Task PlaytestAsync(string command)
        {
            //Reload the last used playtest if the current event is null
            if (_playtestCommandInfo == null)
                _playtestCommandInfo = DatabaseHandler.GetPlaytestCommandInfo();


            //Make sure we have a valid event, if not, abort.
            if (!_calendar.GetTestEventNoUpdate().IsValid)
            {
                await ReplyAsync("This command requires a valid playtest event.");
                return;
            }

            //Setup a few variables we'll need later
            string config = _calendar.GetTestEventNoUpdate().IsCasual
                ? _data.RSettings.General.CasualConfig
                : _data.RSettings.General.CompConfig;

            switch (command.ToLower())
            {
                case "prestart":
                case "pre":

                    //Store test information for later use. Will be written to the DB.
                    string gameMode = _calendar.GetTestEventNoUpdate().IsCasual ? "casual" : "comp";
                    string mentions = null;
                    _calendar.GetTestEventNoUpdate().Creators.ForEach(x => mentions += $"{x.Mention} ");
                    _playtestCommandInfo = new PlaytestCommandInfo
                    {
                        Id = 1, //Only storing 1 of these in the DB at a time, so hard code to 1.
                        Mode = gameMode,
                        DemoName = $"{_calendar.GetTestEventNoUpdate().StartDateTime:MM_dd_yyyy}" +
                                   $"_{_calendar.GetTestEventNoUpdate().Title.Substring(0, _calendar.GetTestEventNoUpdate().Title.IndexOf(' '))}" +
                                   $"_{gameMode}",
                        WorkshopId = _data.GetWorkshopIdFromFqdn(_calendar.GetTestEventNoUpdate().WorkshopLink.ToString()),
                        ServerAddress = _calendar.GetTestEventNoUpdate().ServerLocation,
                        Title = _calendar.GetTestEventNoUpdate().Title,
                        ThumbNailImage = _calendar.GetTestEventNoUpdate().CanUseGallery ? _calendar.GetTestEventNoUpdate().GalleryImages[0] : _data.RSettings.General.FallbackTestImageUrl,
                        ImageAlbum = _calendar.GetTestEventNoUpdate().ImageGallery.ToString(),
                        CreatorMentions = mentions,
                        StartDateTime = _calendar.GetTestEventNoUpdate().StartDateTime.Value
                    };

                    //Write to the DB so we can restore this info next boot
                    DatabaseHandler.StorePlaytestCommandInfo(_playtestCommandInfo);

                    await ReplyAsync($"Pre-start playtest of **{_playtestCommandInfo.Title}**" +
                                     $"\nOn **{_playtestCommandInfo.ServerAddress}**" +
                                     $"\nWith config of **{config}**" +
                                     $"\nWorkshop ID **{_playtestCommandInfo.WorkshopId}**");

                    await _data.RconCommand(_playtestCommandInfo.ServerAddress, $"exec {config}");
                    await Task.Delay(1000);
                    await _data.RconCommand(_playtestCommandInfo.ServerAddress, $"host_workshop_map {_playtestCommandInfo.WorkshopId}");
                    break;

                case "start":
                    await ReplyAsync($"Start playtest of **{_playtestCommandInfo.Title}**" +
                                     $"\nOn **{_playtestCommandInfo.ServerAddress}**" +
                                     $"\nWith config of **{config}**" +
                                     $"\nWorkshop ID **{_playtestCommandInfo.WorkshopId}**" +
                                     $"\nDemo Name **{_playtestCommandInfo.DemoName}**");

                    await _data.RconCommand(_playtestCommandInfo.ServerAddress, $"exec {config}");
                    await Task.Delay(3000);
                    await _data.RconCommand(_playtestCommandInfo.ServerAddress, $"tv_record {_playtestCommandInfo.DemoName}; say Recording {_playtestCommandInfo.DemoName}");
                    await Task.Delay(1000);
                    await _data.RconCommand(_playtestCommandInfo.ServerAddress, $"say Playtest of {_playtestCommandInfo.Title} is live! Be respectful and GLHF!");
                    await Task.Delay(1000);
                    await _data.RconCommand(_playtestCommandInfo.ServerAddress, $"say Playtest of {_playtestCommandInfo.Title} is live! Be respectful and GLHF!");
                    await Task.Delay(1000);
                    await _data.RconCommand(_playtestCommandInfo.ServerAddress, $"say Playtest of {_playtestCommandInfo.Title} is live! Be respectful and GLHF!");
                    break;

                case "post":
                    //This is fired and forgotten. All error handling will be done in the method itself.
                    await ReplyAsync($"Post playtest of **{_playtestCommandInfo.Title}**" +
                                     $"\nOn **{_playtestCommandInfo.ServerAddress}**" +
                                     $"\nWorkshop ID **{_playtestCommandInfo.WorkshopId}**" +
                                     $"\nDemo Name **{_playtestCommandInfo.DemoName}**");

                    PlaytestPostTasks(_playtestCommandInfo);
                    break;

                case "pause":
                case "p":
                    await _data.RconCommand(_playtestCommandInfo.ServerAddress,
                        @"mp_pause_match;say Pausing Match!;say Pausing Match!;say Pausing Match!;say Pausing Match!");
                    await ReplyAsync($"```Pausing playtest on {_playtestCommandInfo.ServerAddress}!```");
                    break;

                case "unpause":
                case "u":
                    await _data.RconCommand(_playtestCommandInfo.ServerAddress,
                        @"mp_unpause_match;say Unpausing Match!;say Unpausing Match!;say Unpausing Match!;say Unpausing Match!");
                    await ReplyAsync($"```Unpausing playtest on {_playtestCommandInfo.ServerAddress}!```");
                    break;

                case "scramble":
                case "s":
                    await _data.RconCommand(_playtestCommandInfo.ServerAddress, "mp_scrambleteams 1" +
                                                                           ";say Scrambling Teams!;say Scrambling Teams!;say Scrambling Teams!;say Scrambling Teams!");
                    await ReplyAsync($"```Scrambling teams on {_playtestCommandInfo.ServerAddress}!```");
                    break;

                default:
                    await ReplyAsync("Invalid action, please consult the help document for this command.");
                    break;
            }
        }

        internal async void PlaytestPostTasks(PlaytestCommandInfo playtestCommandInfo)
        {
            await _data.RconCommand(playtestCommandInfo.ServerAddress, $"host_workshop_map {playtestCommandInfo.WorkshopId}");
            await Task.Delay(15000); //Wait for map to change
            await _data.RconCommand(playtestCommandInfo.ServerAddress,
                $"sv_cheats 1; bot_stop 1;sv_voiceenable 0;exec {_data.RSettings.General.PostgameConfig};" +
                $"say Please join the level testing voice channel for feedback!;" +
                $"say Please join the level testing voice channel for feedback!;" +
                $"say Please join the level testing voice channel for feedback!;" +
                $"say Please join the level testing voice channel for feedback!;" +
                $"say Please join the level testing voice channel for feedback!");

            DownloadHandler.DownloadPlaytestDemo(playtestCommandInfo);

            const string demoUrl = "http://demos.tophattwaffle.com";

            var embed = new EmbedBuilder()
                .WithAuthor($"Download playtest demo for {playtestCommandInfo.Title}",_data.Guild.IconUrl, demoUrl)
                .WithThumbnailUrl(playtestCommandInfo.ThumbNailImage)
                .WithColor(new Color(243,128,72))
                .WithDescription($"[Download Demo Here]({demoUrl}) | [Map Images]({playtestCommandInfo.ImageAlbum}) | [Playtesting Information](https://www.tophattwaffle.com/playtesting/)");

            await _data.TestingChannel.SendMessageAsync(playtestCommandInfo.CreatorMentions, embed: embed.Build());
        }

        [Command("Active")]
        [Summary("Grants a user the Active Memeber role")]
        [RequireContext(ContextType.Guild)]
        [RequireUserPermission(GuildPermission.KickMembers)]
        public async Task ActiveAsync([Summary("User to give role to.")]SocketGuildUser user)
        {
            await _log.LogMessage($"{user} has been given {_data.ActiveRole.Mention} by {Context.User}");
            await ReplyAsync($"{user.Mention} has been given {_data.ActiveRole.Mention}!\n\nThanks for contributing to our playtest!");
            await user.AddRoleAsync(_data.ActiveRole);
        }

        [Command("CompetitiveTester")]
        [Summary("Grants a user the Competitive Tester role")]
        [Alias("comp")]
        [RequireContext(ContextType.Guild)]
        [RequireUserPermission(GuildPermission.KickMembers)]
        public async Task CompetitiveTesterAsync([Summary("User to give role to.")]SocketGuildUser user)
        {

            if (((SocketGuildUser)Context.User).Roles.Contains(_data.CompetitiveTesterRole))
            {
                await Context.Message.DeleteAsync();
                await user.RemoveRoleAsync(_data.CompetitiveTesterRole);
                await _log.LogMessage($"{user} has {_data.CompetitiveTesterRole} removed by {Context.User}");
            }
            else
            {
                await ReplyAsync($"{Context.User.Mention} has been added to Competitive Testers!");
                await user.AddRoleAsync(_data.CompetitiveTesterRole);
                await _log.LogMessage($"{user} has been given {_data.CompetitiveTesterRole} by {Context.User}");
            }
        }

        [Command("Invite")]
        [Summary("Invites a user to a competitive level test")]
        [RequireContext(ContextType.Guild)]
        [RequireUserPermission(GuildPermission.KickMembers)]
        public async Task CompInviteAsync([Summary("User to invite.")]SocketGuildUser user)
        {
            await Context.Message.DeleteAsync();

            //Do nothing if a test is not valid.
            if (!_calendar.GetTestEventNoUpdate().IsValid)
            {
                await ReplyAsync("There is no valid test that I can invite that user to.");
                return;
            }

            await _log.LogMessage($"{user} has been invite to the competitive test of {_calendar.GetTestEventNoUpdate().Title} by {Context.User}");

            try
            {
                await user.SendMessageAsync($"You've been invited to join __**{_calendar.GetTestEventNoUpdate().Title}**__!\n" +
                                            $"Open Counter-Strike Global Offensive and paste the following into console to join:" +
                                            $"```connect {_calendar.GetTestEventNoUpdate().ServerLocation}; password {_calendar.GetTestEventNoUpdate().CompPassword}```");
            }
            catch
            {
                await ReplyAsync("I attempted to DM that user connection information, but they don't allow DMs.");
            }
        }

        [Command("rcon")]
        [Alias("r")]
        [RequireUserPermission(GuildPermission.KickMembers)]
        [RequireContext(ContextType.Guild)]
        [Remarks("Sends RCON commands to a test server.\n" +
                 "You can use `>rcon auto` to automatically use the next playtest server.\n" +
                 "You can specify a server be specified before commands are sent.\n" +
                 "Set a server using `>rcon set [serverId]\n" +
                 "Then commands can be sent as normal without a server ID:\n" +
                 "Example: `>r sv_cheats 1`\n" +
                 "Provide no parameters to see what server you're current sending to.")]
        public async Task RconAsync([Remainder] string input = null)
        {
            string targetServer = null;
            if (input == null && ServerDictionary.ContainsKey(Context.User.Id))
            {
                targetServer = ServerDictionary[Context.User.Id];
                await ReplyAsync($"RCON commands sent by {Context.User} will be sent to `{targetServer}`");
                return;
            }

            //Set server mode
            if (!string.IsNullOrWhiteSpace(input) && input.StartsWith("set", StringComparison.OrdinalIgnoreCase))
            {
                //Dictionary contains user already, remove them.
                if(ServerDictionary.ContainsKey(Context.User.Id))
                {
                    ServerDictionary.Remove(Context.User.Id);
                }
                ServerDictionary.Add(Context.User.Id, input.Substring(3).Trim());
                await ReplyAsync($"RCON commands sent by {Context.User} will be sent to `{ServerDictionary[Context.User.Id]}`");
                return;
            }

            //Set user's mode to Auto, which is really just removing a user from the dictionary
            if (!string.IsNullOrWhiteSpace(input) && input.StartsWith("auto", StringComparison.OrdinalIgnoreCase))
            {
                if (ServerDictionary.ContainsKey(Context.User.Id))
                {
                    ServerDictionary.Remove(Context.User.Id);
                }
                await ReplyAsync($"RCON commands sent by {Context.User} will be sent using Auto mode. Which is the active playtest server, if there is one.");
                return;
            }

            //In auto mode
            if (!ServerDictionary.ContainsKey(Context.User.Id))
            {
                if (_calendar.GetTestEventNoUpdate().IsValid)
                {
                    //There is a playtest event, get the server ID from the test event
                    string serverAddress = _calendar.GetTestEventNoUpdate().ServerLocation;
                    targetServer = serverAddress.Substring(0, serverAddress.IndexOf('.'));
                }
                else
                {
                    //No playtest event, we cannot do anything.
                    await ReplyAsync("No playtest server found. Set your target server using `>rcon set [serverId]`.");
                    return;
                }
            }
            else
                //User has a server set manually.
                targetServer = ServerDictionary[Context.User.Id];


            await ReplyAsync($"```{await _data.RconCommand(targetServer, input)}```");
        }

        [Command("TestServer")]
        [Alias("ts")]
        [RequireUserPermission(GuildPermission.Administrator)]
        [Summary("Command for manipulating test servers. See command help for more information.")]
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
        "[FtpType]\n`")]
        public async Task TestServerAsync(string action, [Remainder]string values = null)
        {
            //Add server
            if (action.StartsWith("a", StringComparison.OrdinalIgnoreCase))
            {
                //Need input values, abort if we don't have them.
                if (values == null)
                {
                    await ReplyAsync("No input provided");
                    return;
                }

                string[] serverValues = values.Split("\n");

                //Make sure all the data is present, as all values are required
                if (serverValues.Length != 8)
                {
                    await ReplyAsync("Adding a server requires all 8 server values.");
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

                if (DatabaseHandler.AddTestServer(new Server()
                {
                    ServerId = serverValues[0],
                    Description = serverValues[1],
                    Address = serverValues[2],
                    RconPassword = serverValues[3],
                    FtpUser = serverValues[4],
                    FtpPassword = serverValues[5],
                    FtpPath = serverValues[6],
                    FtpType = serverValues[7].ToLower()
                }))
                {
                    await ReplyAsync("Server added!\nI deleted your message since it had passwords in it.");
                    await Context.Message.DeleteAsync();
                    return;
                }

                await ReplyAsync("Issue adding server, does it already exist?\nI deleted your message since it had passwords in it.");
                await Context.Message.DeleteAsync();
            }
            //Get server
            else if (action.StartsWith("g"))
            {
                string reply = $"No server found with server code {values}";
                if (values != null && !values.Equals("all", StringComparison.OrdinalIgnoreCase))
                {
                    var testServer = DatabaseHandler.GetTestServer(values);

                    if (testServer != null)
                        reply = $"Found the following server:\n{testServer.ToString()}\n\n" +
                                $"Use the following command to re-add this server to the database.\n" +
                                $"```" +
                                $">TestServer add" +
                                $"\n{testServer.ServerId}" +
                                $"\n{testServer.Description}" +
                                $"\n{testServer.Address}" +
                                $"\n{testServer.RconPassword}" +
                                $"\n{testServer.FtpUser}" +
                                $"\n{testServer.FtpPassword}" +
                                $"\n{testServer.FtpPath}" +
                                $"\n{testServer.FtpType}" +
                                $"```";

                    await _data.AlertUser.SendMessageAsync(reply);
                    
                }
                //Get all servers instead
                else
                {
                    var testServers = DatabaseHandler.GetAllTestServers();

                    if (testServers != null)
                    {
                        reply = null;
                        foreach (var testServer in testServers)
                        {
                            reply += "```" + testServer + "```";
                        }
                    }
                    else
                        reply = "Could not get all servers because the request returned null.";

                    await _data.AlertUser.SendMessageAsync(reply);
                }

                await ReplyAsync($"Server information contains passwords, as a result I have DM'd it to {_data.AlertUser}.");
            }
            //Remove server
            else if (action.StartsWith("r"))
            {
                if (DatabaseHandler.RemoveTestServer(values))
                {
                    await ReplyAsync($"Server with the ID: `{values}` was removed.");
                }
                else
                {
                    await ReplyAsync($"Could not remove a server with the ID of: `{values}`. It likely does not exist in the DB.");
                }
            }
        }

        [Command("ForceAnnounce")]
        [Alias("fa")]
        [RequireUserPermission(GuildPermission.KickMembers)]
        [Summary("Allows manual announcing of a playtest. This command mentions the playtester role.")]
        public async Task ForceAnnounceAsync()
        {
            await _playtestService.PlaytestStartingInTask();
        }

        [Command("Debug")]
        [RequireUserPermission(GuildPermission.Administrator)]
        [Summary("View or change the debug flag." +
                 "\n`>debug [true/false/reload]` to set the flag, or reload settings from the settings file.")]
        public async Task DebugAsync(string status = null)
        {
            if (status == null)
            {
                await Context.Channel.SendMessageAsync(
                    $"Current debug status is: `{_data.RSettings.ProgramSettings.Debug}`");
            }
            else if (status.StartsWith("t", StringComparison.OrdinalIgnoreCase))
            {
                _data.RSettings.ProgramSettings.Debug = true;
                await _data.UpdateRolesAndUsers();
                await Context.Channel.SendMessageAsync(
                    $"Changed debug status to: `{_data.RSettings.ProgramSettings.Debug}`");
            }
            else if (status.StartsWith("f", StringComparison.OrdinalIgnoreCase))
            {
                _data.RSettings.ProgramSettings.Debug = false;
                await _data.UpdateRolesAndUsers();
                await Context.Channel.SendMessageAsync(
                    $"Changed debug status to: `{_data.RSettings.ProgramSettings.Debug}`");
            }
            else if (status.StartsWith("r", StringComparison.OrdinalIgnoreCase))
            {
                await _data.DeserializeConfig();
                await Context.Channel.SendMessageAsync(
                    $"Deserializing configuration...");
            }
        }
    }
}