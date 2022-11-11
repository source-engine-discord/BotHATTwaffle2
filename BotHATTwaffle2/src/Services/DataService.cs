using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using BotHATTwaffle2.Commands.Readers;
using BotHATTwaffle2.Handlers;
using BotHATTwaffle2.Models.JSON;
using BotHATTwaffle2.Models.LiteDB;
using BotHATTwaffle2.Util;
using Discord;
using Discord.Net.Queue;
using Discord.WebSocket;
using FluentScheduler;
using Newtonsoft.Json;

namespace BotHATTwaffle2.Services
{
    public class DataService
    {
        private const ConsoleColor LOG_COLOR = ConsoleColor.Blue;

        private static bool _playtestStartAlert = true;
        private readonly DiscordSocketClient _client;
        private LogHandler _log;
        public int CommandCount = 0;

        public List<ulong> IgnoreListenList = new List<ulong>();
        public int MessageCount = 0;

        public DateTime StartTime;

        public DataService(DiscordSocketClient client)
        {
            StartTime = DateTime.Now.AddMinutes(-1);
            _client = client;
            // Some settings are needed before the client connects (e.g. token).
            ReadConfig();
        }

        public List<Blacklist> Blacklist { get; private set; }
        public RootSettings RSettings { get; set; }
        public SocketGuild Guild { get; set; }

        // Channels
        public SocketTextChannel GeneralChannel { get; private set; }
        public SocketTextChannel LogChannel { get; private set; }
        public SocketTextChannel WelcomeChannel { get; private set; }
        public SocketTextChannel CSGOAnnouncementChannel { get; private set; }
        public SocketTextChannel TF2AnnouncementChannel { get; private set; }
        public SocketTextChannel TF2TestingChannel { get; private set; }
        public SocketTextChannel CSGOTestingChannel { get; private set; }
        public SocketTextChannel CompetitiveTestingChannel { get; private set; }
        public SocketTextChannel WebhookChannel { get; private set; }
        public SocketTextChannel AdminChannel { get; private set; }
        public SocketTextChannel VoidChannel { get; private set; }
        public SocketTextChannel BotChannel { get; private set; }
        public SocketVoiceChannel LevelTestVoiceChannel { get; private set; }
        public SocketVoiceChannel AfkVoice { get; private set; }
        public SocketTextChannel AdminBotsChannel { get; private set; }
        public SocketTextChannel VerificationChannel { get; private set; }
        public SocketTextChannel VerificationRulesChannel { get; private set; }
        public SocketTextChannel CsgoPlaytestAdminChannel { get; private set; }


        // Roles
        public SocketRole CSGOPlayTesterRole { get; private set; }
        public SocketRole TF2PlayTesterRole { get; private set; }
        public SocketRole MuteRole { get; private set; }
        public SocketRole ModeratorRole { get; private set; }
        public SocketRole ActiveRole { get; private set; }
        public SocketRole PatreonsRole { get; private set; }
        public SocketRole CommunityTesterRole { get; private set; }
        public SocketRole BotsRole { get; private set; }
        public SocketRole AdminRole { get; private set; }
        public SocketRole CompetitiveTesterRole { get; private set; }
        public SocketRole ComptesterPlaytestCreator { get; private set; }
        public SocketUser AlertUser { get; private set; }
        public SocketRole CSGOPlaytestAdmin { get; private set; }
        public SocketRole TF2PlaytestAdmin { get; private set; }
        public SocketRole Verified { get; private set; }
        public static bool IncludePlayerCount { get; set; }
        public static string PlayerCount { get; set; }

        public bool GetStartAlertStatus()
        {
            return _playtestStartAlert;
        }

        public bool ToggleStartAlert()
        {
            _playtestStartAlert = !_playtestStartAlert;
            return _playtestStartAlert;
        }

        public void SetStartAlert(bool value)
        {
            _playtestStartAlert = value;
        }

        public bool GetIncludePlayerCount()
        {
            return IncludePlayerCount;
        }

        public string GetPlayerCount()
        {
            return PlayerCount;
        }

        public void SetPlayerCount(string playerCount)
        {
            PlayerCount = playerCount;
        }

        public void SetIncludePlayerCount(bool includeCount)
        {
            IncludePlayerCount = includeCount;
        }

        /// <summary>
        /// Reloads the blacklist
        /// </summary>
        /// <returns>Returns true if successful</returns>
        public bool LoadBlacklist()
        {
            Blacklist = DatabaseUtil.GetAllBlacklist().ToList();

            if (Blacklist.Count == 0)
                return false;

            _ = _log.LogMessage($"Loaded {Blacklist.Count} entries into the blacklist!",color:LOG_COLOR);
            return true;
        }

        public async Task DeserializeConfig()
        {
            ReadConfig();
            await DeserializeChannels();
            GetRoles();
            IncludePlayerCount = false;
            PlayerCount = "0";
            try
            {
                AlertUser = GetSocketUser(RSettings.ProgramSettings.AlertUser);
                if (AlertUser == null)
                {
                    _ = Task.Run(async () =>
                    {
                        while(true)//for (int i = 0; i < 10; i++)
                        {
                            if (AlertUser != null)
                            {
                                return;
                            }

                            Console.WriteLine("Client did not return a user for AlertUser. Will try again in 5 seconds...");
                            await Task.Delay(3000);
                            AlertUser = GetSocketUser(RSettings.ProgramSettings.AlertUser);
                        }

                        throw new NullReferenceException("Alert user null!");
                    });
                }
            }
            catch
            {
                Console.WriteLine($"Unable to find a user with ID {RSettings.ProgramSettings.AlertUser}.\n" +
                                  "I need this user to function properly. Please set the alert user in settings.json " +
                                  "and restart.");
                Console.ReadLine();
                Environment.Exit(0);
            }

            Console.ForegroundColor = ConsoleColor.Magenta;
            Console.WriteLine("SETTINGS HAVE BEEN LOADED");
            Console.ForegroundColor = ConsoleColor.Red;
            if (RSettings.ProgramSettings.Debug)
                Console.WriteLine(
                    "  _____  ______ ____  _    _  _____    ____  _   _ \r\n |  __ \\|  ____|  _ \\| |  | |/ ____|  / __ \\| \\ | |\r\n | |  | | |__  | |_) | |  | | |  __  | |  | |  \\| |\r\n | |  | |  __| |  _ <| |  | | | |_ | | |  | | . ` |\r\n | |__| | |____| |_) | |__| | |__| | | |__| | |\\  |\r\n |_____/|______|____/ \\____/ \\_____|  \\____/|_| \\_|\r\n                                                   \r\n                                                   ");
            Console.ResetColor();
        }

        public async Task UpdateRolesAndUsers()
        {
            await DeserializeChannels();
            GetRoles();
        }

        public void SetLogHandler(LogHandler log)
        {
            _log = log;
        }

        private void ReadConfig()
        {
            const string configPath = "settings.json";

            if (!File.Exists(configPath))
            {
                Console.WriteLine("Settings file not found. Create settings file and try again.");
                Console.ReadLine();

                //Close program
                Environment.Exit(1);
            }

            RSettings = JsonConvert.DeserializeObject<RootSettings>(File.ReadAllText(configPath));
        }


        /// <summary>
        ///     Deserialize channels from the configuration file.
        /// </summary>
        /// <exception cref="InvalidOperationException">Thrown when a channel can't be found.</exception>
        /// <returns>No object or value is returned by this method when it completes.</returns>
        private async Task DeserializeChannels()
        {
            Guild = _client.Guilds.FirstOrDefault();

            Console.ForegroundColor = LOG_COLOR;

            Console.WriteLine($"Active Guild: {Guild?.Name}\n");

            LevelTestVoiceChannel = Guild.GetVoiceChannel(RSettings.General.LevelTestingVoice);
            Console.WriteLine(
                $"LevelTestVoiceChannel ID:{LevelTestVoiceChannel.Id} Discovered Name:{LevelTestVoiceChannel.Name}");

            AfkVoice = Guild.GetVoiceChannel(RSettings.General.AfkVoice);
            Console.WriteLine(
                $"LevelTestVoiceChannel ID:{AfkVoice.Id} Discovered Name:{AfkVoice.Name}");

            LogChannel = await ParseChannel(RSettings.ProgramSettings.LogChannel);
            Console.WriteLine($"LogChannel ID:{LogChannel.Id} Discovered Name:{LogChannel.Name}");

            AdminChannel = await ParseChannel(RSettings.General.AdminChannel);
            Console.WriteLine($"AdminChannel ID:{AdminChannel.Id} Discovered Name:{AdminChannel.Name}");

            VoidChannel = await ParseChannel(RSettings.General.VoidChannel);
            Console.WriteLine($"VoidChannel ID:{VoidChannel.Id} Discovered Name:{VoidChannel.Name}");

            BotChannel = await ParseChannel(RSettings.General.BotChannel);
            Console.WriteLine($"BotChannel ID:{BotChannel.Id} Discovered Name:{BotChannel.Name}");

            AdminBotsChannel = await ParseChannel(RSettings.General.AdminBotsChannel);
            Console.WriteLine($"AdminBotChannel ID:{AdminBotsChannel.Id} Discovered Name:{AdminBotsChannel.Name}");

            GeneralChannel = await ParseChannel(RSettings.General.GeneralChannel);
            Console.WriteLine($"GeneralChannel ID:{GeneralChannel.Id} Discovered Name:{GeneralChannel.Name}");

            WelcomeChannel = await ParseChannel(RSettings.General.WelcomeChannel);
            Console.WriteLine($"WelcomeChannel ID:{WelcomeChannel.Id} Discovered Name:{WelcomeChannel.Name}");

            CSGOAnnouncementChannel = await ParseChannel(RSettings.General.CSGOAnnouncementChannel);
            Console.WriteLine(
                $"CSGO AnnouncementChannel ID:{CSGOAnnouncementChannel.Id} Discovered Name:{CSGOAnnouncementChannel.Name}");

            TF2AnnouncementChannel = await ParseChannel(RSettings.General.TF2AnnouncementChannel);
            Console.WriteLine(
                $"TF2 AnnouncementChannel ID:{TF2AnnouncementChannel.Id} Discovered Name:{TF2AnnouncementChannel.Name}");

            CSGOTestingChannel = await ParseChannel(RSettings.General.CSGOTestingChannel);
            Console.WriteLine(
                $"CSGO TestingChannel ID:{CSGOTestingChannel.Id} Discovered Name:{CSGOTestingChannel.Name}");

            TF2TestingChannel = await ParseChannel(RSettings.General.TF2TestingChannel);
            Console.WriteLine($"TF2 TestingChannel ID:{TF2TestingChannel.Id} Discovered Name:{TF2TestingChannel.Name}");

            WebhookChannel = await ParseChannel(RSettings.General.WebhookChannel);
            Console.WriteLine($"WebhookChannel ID:{WebhookChannel.Id} Discovered Name:{WebhookChannel.Name}");

            CompetitiveTestingChannel = await ParseChannel(RSettings.General.CompetitiveTestingChannel);
            Console.WriteLine(
                $"CompetitiveTestingChannel ID:{CompetitiveTestingChannel.Id} Discovered Name:{CompetitiveTestingChannel.Name}");

            VerificationChannel = await ParseChannel(RSettings.General.VerificationChannel);
            Console.WriteLine($"VerificationChannel ID:{VerificationChannel.Id} Discovered Name:{VerificationChannel.Name}");

            VerificationRulesChannel = await ParseChannel(RSettings.General.VerificationRulesChannel);
            Console.WriteLine($"VerificationRulesChannel ID:{VerificationRulesChannel.Id} Discovered Name:{VerificationRulesChannel.Name}");

            CsgoPlaytestAdminChannel = await ParseChannel(RSettings.General.CsgoPlaytestAdminChannel);
            Console.WriteLine($"CsgoPlaytestAdminChannel ID:{CsgoPlaytestAdminChannel.Id} Discovered Name:{CsgoPlaytestAdminChannel.Name}");

            Console.ResetColor();

            async Task<SocketTextChannel> ParseChannel(string key)
            {
                var channel = await ChannelTypeReader<SocketTextChannel>.GetBestResultAsync(Guild, key);

                if (channel == null)
                    throw new InvalidOperationException($"The value of key '{key}' could not be parsed as a channel.");

                return channel;
            }
        }

        /// <summary>
        ///     Retrieves role socket entities from the IDs in the
        ///     <see>
        ///         <cref>Role</cref>
        ///     </see>
        ///     enum.
        /// </summary>
        /// <exception cref="InvalidOperationException">Thrown when a role can't be found.</exception>
        private void GetRoles()
        {
            Console.ForegroundColor = LOG_COLOR;

            CSGOPlayTesterRole = Guild.GetRole(RSettings.UserRoles.CSGOPlaytester);
            Console.WriteLine($"CSGO Playtester ID:{CSGOPlayTesterRole.Id} Discovered Name:{CSGOPlayTesterRole.Name}");

            TF2PlayTesterRole = Guild.GetRole(RSettings.UserRoles.TF2Playtester);
            Console.WriteLine($"TF2 Playtester ID:{CSGOPlayTesterRole.Id} Discovered Name:{TF2PlayTesterRole.Name}");

            ModeratorRole = Guild.GetRole(RSettings.UserRoles.Moderator);
            Console.WriteLine($"\nModerator ID:{ModeratorRole.Id} Discovered Name:{ModeratorRole.Name}");

            MuteRole = Guild.GetRole(RSettings.UserRoles.Muted);
            Console.WriteLine($"Muted ID:{MuteRole.Id} Discovered Name:{MuteRole.Name}");

            ActiveRole = Guild.GetRole(RSettings.UserRoles.Active);
            Console.WriteLine($"Active ID:{ActiveRole.Id} Discovered Name:{ActiveRole.Name}");

            PatreonsRole = Guild.GetRole(RSettings.UserRoles.Patreons);
            Console.WriteLine($"Patreons ID:{PatreonsRole.Id} Discovered Name:{PatreonsRole.Name}");

            CommunityTesterRole = Guild.GetRole(RSettings.UserRoles.CommunityTester);
            Console.WriteLine(
                $"CommunityTesterRole ID:{CommunityTesterRole.Id} Discovered Name:{CommunityTesterRole.Name}");

            BotsRole = Guild.GetRole(RSettings.UserRoles.Bots);
            Console.WriteLine($"BotsRole ID:{BotsRole.Id} Discovered Name:{BotsRole.Name}");

            AdminRole = Guild.GetRole(RSettings.UserRoles.Admin);
            Console.WriteLine($"AdminRole ID:{AdminRole.Id} Discovered Name:{AdminRole.Name}");

            CompetitiveTesterRole = Guild.GetRole(RSettings.UserRoles.CompetitiveTester);
            Console.WriteLine(
                $"CompetitiveTesterRole ID:{CompetitiveTesterRole.Id} Discovered Name:{CompetitiveTesterRole.Name}");

            ComptesterPlaytestCreator = Guild.GetRole(RSettings.UserRoles.ComptesterPlaytestCreator);
            Console.WriteLine(
                $"CompetitiveTesterCreator ID:{ComptesterPlaytestCreator.Id} Discovered Name:{ComptesterPlaytestCreator.Name}");

            CSGOPlaytestAdmin = Guild.GetRole(RSettings.UserRoles.CSGOPlaytestAdmin);
            Console.WriteLine($"CSGOPlaytestAdmin ID:{CSGOPlaytestAdmin.Id} Discovered Name:{CSGOPlaytestAdmin.Name}");

            TF2PlaytestAdmin = Guild.GetRole(RSettings.UserRoles.TF2PlaytestAdmin);
            Console.WriteLine($"TF2PlaytestAdmin ID:{TF2PlaytestAdmin.Id} Discovered Name:{TF2PlaytestAdmin.Name}");

            Verified = Guild.GetRole(RSettings.UserRoles.Verified);
            Console.WriteLine($"Unverified ID:{Verified.Id} Discovered Name:{Verified.Name}");

            Console.ResetColor();
        }

        /// <summary>
        ///     Checks if a provided string is a valid user
        /// </summary>
        /// <param name="input">String with of username with abcd#1234 or ulong ID</param>
        /// <returns>Valid SocketUser</returns>
        public SocketUser GetSocketUser(string input)
        {
            SocketUser user = null;
            if (input == null)
                return null;
            try
            {
                //Check if username#1234 was provided
                if (input.Contains('#'))
                {
                    var split = input.Split('#');
                    user = _client.GetUser(split[0], split[1]);
                }
                //Check if ID was provided instead
                else
                {
                    if (ulong.TryParse(input, out var id))
                        user = _client.GetUser(id);
                }

                if (user == null) _ = _log.LogMessage($"Error Setting SocketUser for string `{input}`");
            }
            catch (Exception e)
            {
                _ = _log.LogMessage(e.ToString(), color: LOG_COLOR);
            }

            return user;
        }

        /// <summary>
        ///     Checks if a provided ulong is a valid user
        /// </summary>
        /// <param name="input">ulong ID of user</param>
        /// <returns>Valid SocketUser</returns>
        public SocketUser GetSocketUser(ulong input)
        {
            SocketUser user = null;
            try
            {
                user = _client.GetUser(input);

                if (user == null) _ = _log.LogMessage($"Error Setting SocketUser for string `{input}`");
            }
            catch (Exception e)
            {
                _ = _log.LogMessage(e.ToString(), color: LOG_COLOR);
            }

            return user;
        }

        public SocketGuildUser GetSocketGuildUserFromSteamId(string steamId)
        {
            var foundUser = DatabaseUtil.GetUserSteamID(steamId: steamId);

            if (foundUser == null)
                return null;

            return GetSocketGuildUser(foundUser.UserId);
        }

        /// <summary>
        ///     Takes a string that may contain channel mention strings, and replaces them with the channel name.
        /// </summary>
        /// <param name="input">String to modify</param>
        /// <returns></returns>
        public string RemoveChannelMentionStrings(string input)
        {
            var currentString = input;

            //Discord channel mention string pattern
            var channelRegex = new Regex("<#\\d+>");

            //All matches
            var matches = channelRegex.Matches(currentString);

            foreach (Match match in matches)
            {
                var idParse = ulong.TryParse(Regex.Match(match.Value, @"\d+").Value, out var channelId);

                //Did we actually parse a valid ID?
                if (idParse)
                {
                    var channel = Guild.GetChannel(channelId);

                    //Channel exists, replace
                    if (channel != null)
                        currentString = currentString.Replace(match.Value, channel.Name);
                }
            }

            return currentString;
        }

        /// <summary>
        ///     Converts a string of users to a list of socket users
        /// </summary>
        /// <param name="input">String containing users</param>
        /// <param name="splitChar">Char to split with</param>
        /// <returns>List of SocketUsers</returns>
        public List<SocketUser> GetSocketUsers(string input, char splitChar)
        {
            var users = new List<SocketUser>();
            var creators = input.Split(splitChar).Select(c => c.Trim()).ToArray();

            foreach (var c in creators) users.Add(GetSocketUser(c.Trim()));

            return users;
        }

        public List<SocketGuildUser> GetSocketGuildUsers(string input, char splitChar)
        {
            var users = new List<SocketGuildUser>();
            var creators = input.Split(splitChar).Select(c => c.Trim()).ToArray();

            foreach (var c in creators) users.Add(GetSocketGuildUser(c));

            return users;
        }

        /// <summary>
        ///     Finds a user in the Guild. If the input type is unknown, this method can be used.
        /// </summary>
        /// <param name="input">String with user#1234 or ID</param>
        /// <returns>SocketGuildUser that was found</returns>
        public SocketGuildUser GetSocketGuildUser(string input)
        {
            SocketGuildUser user = null;
            try
            {
                if (input.StartsWith("<@") && input.EndsWith(">"))
                {
                    var numbersOnly = new Regex(@"[0-9]+");
                    input = numbersOnly.Match(input).Value;

                    if (ulong.TryParse(input, out var id))
                        user = Guild.GetUser(id);
                }
                else if (input.Contains('#')) //Check if username#1234 was provided
                {
                    var split = input.Split('#');
                    ushort.TryParse(split[1], out var disc);
                    user = Guild.Users.FirstOrDefault(x =>
                        x.Username.Equals(split[0], StringComparison.OrdinalIgnoreCase)
                        && x.DiscriminatorValue == disc);
                }
                //Check if ID was provided instead
                else
                {
                    if (ulong.TryParse(input, out var id))
                        user = Guild.GetUser(id);
                }

                if (user == null) _ = _log.LogMessage($"Error Setting SocketGuildUser for string `{input}`");
            }
            catch (Exception e)
            {
                _ = _log.LogMessage(e.ToString(), alert: false, color: LOG_COLOR);
            }

            return user;
        }

        /// <summary>
        ///     Gets a socketGuildUser based on ID. Useful for converting a user from SocketUser to SocketGuildUser
        ///     for use outside of the Guild.
        /// </summary>
        /// <param name="input">user ID</param>
        /// <returns>SocketGuildUser that was found</returns>
        public SocketGuildUser GetSocketGuildUser(ulong input)
        {
            SocketGuildUser user = null;
            try
            {
                user = Guild.GetUser(input);

                if (user == null) _ = _log.LogMessage($"Error Getting SocketGuildUser for string `{input}`");
            }
            catch (Exception e)
            {
                _ = _log.LogMessage(e.ToString(), alert: false, color: LOG_COLOR);
            }

            return user;
        }

        /// <summary>
        ///     Gets a message object
        /// </summary>
        /// <param name="channel">Channel to look in</param>
        /// <param name="messageId">ID of message</param>
        /// <returns></returns>
        public async Task<IMessage> GetSocketMessage(ISocketMessageChannel channel, ulong messageId)
        {
            var msg = await channel.GetMessageAsync(messageId);
            return msg;
        }

        /// <summary>
        ///     Takes a user ID and attempts to unmute them
        /// </summary>
        /// <param name="userId">UserID to unmute</param>
        /// <param name="reason">Reason for unmute</param>
        /// <returns>True if unmuted, false otherwise</returns>
        public async Task<bool> UnmuteUser(ulong userId, string reason = null)
        {
            var dbResult = DatabaseUtil.UnmuteUser(userId);
            if (dbResult)
                try
                {
                    var guildUser = Guild.GetUser(userId);
                    await guildUser.RemoveRoleAsync(MuteRole);
                    await guildUser.AddRoleAsync(Verified);

                    //If null, mute timed out
                    reason = reason ?? "The mute timed out.";

                    await _log.LogMessage($"Removed mute from `{guildUser.Username}` because {reason}.",
                        color: LOG_COLOR);
                    return true;
                }
                catch (Exception e)
                {
                    await _log.LogMessage(
                        $"Failed to unmute ID `{userId}`, they likely left the server.\n```{e.Message}```");
                    return false;
                }

            await _log.LogMessage($"Failed to unmute ID `{userId}` because no mute was found in the database.");
            return false;
        }

        public async Task MuteUser(SocketGuildUser user, TimeSpan muteLength, string reason, SocketMessage message)
        {
            //Convert to total minutes, used later for mute extensions
            var duration = muteLength.TotalMinutes;

            //Variables used if we are extending a mute.
            double oldMuteTime = 0;
            var muteStartTime = DateTime.Now;

            //Setup the embed for later.
            var embed = new EmbedBuilder();

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

            var added = DatabaseUtil.AddMute(new Mute
            {
                UserId = user.Id,
                Username = user.Username,
                Reason = reason,
                Duration = duration + oldMuteTime,
                MuteTime = muteStartTime,
                ModeratorId = message.Author.Id,
                Expired = false
            });

            if (added)
            {
                try
                {
                    await user.AddRoleAsync(MuteRole);
                    await user.RemoveRoleAsync(Verified);

                    //disconnect user from voice
                    if (user.VoiceChannel != null)
                        await user.ModifyAsync(x => x.Channel = null);

                    JobManager.AddJob(async () => await UnmuteUser(user.Id), s => s
                        .WithName($"[UnmuteUser_{user.Id}]")
                        .ToRunOnceAt(DateTime.Now.AddMinutes(duration + oldMuteTime)));
                }
                catch
                {
                    await message.Channel.SendMessageAsync("Failed to apply mute role, did the user leave the server?");
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

                reason = RemoveChannelMentionStrings(reason);

                //Do not display this message for blacklist violations.
                //When a blacklist mute happens, the user "muted" themselves.
                if(user.Id != message.Author.Id)
                    await message.Channel.SendMessageAsync(embed: embed
                        .WithAuthor($"{user.Username} Muted")
                        .WithDescription(
                            $"Muted for: `{formatted.Trim().TrimEnd(',')}`\nBecause: `{reason}`")
                        .WithColor(new Color(165, 55, 55))
                        .Build());

                await _log.LogMessage(
                    $"`{message.Author}` muted `{user}` `{user.Id}`\nFor: `{formatted.Trim().TrimEnd(',')}`\nBecause: `{reason}`",
                    color: ConsoleColor.Red);

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
                    await VoidChannel.SendMessageAsync(embed: embed
                        .WithAuthor("You have been muted")
                        .WithDescription(
                            $"You have been muted for: `{formatted.Trim().TrimEnd(',')}`\nBecause: `{reason}`")
                        .WithColor(new Color(165, 55, 55))
                        .Build());
                }
            }
            else
            {
                await message.Channel.SendMessageAsync(embed: embed
                    .WithAuthor($"Unable to mute {user.Username}")
                    .WithDescription($"I could not mute `{user.Username}` `{user.Id}` because they are already muted.")
                    .WithColor(new Color(165, 55, 55))
                    .Build());
            }
        }
    }
}