using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using BotHATTwaffle2.Commands.Readers;
using BotHATTwaffle2.Handlers;
using BotHATTwaffle2.Util;
using Discord;
using Discord.WebSocket;
using Newtonsoft.Json;

namespace BotHATTwaffle2.Services
{
    public class DataService
    {
        private const ConsoleColor LOG_COLOR = ConsoleColor.Blue;
        private readonly DiscordSocketClient _client;
        private LogHandler _log;
        public int CommandCount = 0;

        public List<SocketUser> IgnoreListenList = new List<SocketUser>();
        public int MessageCount = 0;

        public DateTime StartTime;

        public DataService(DiscordSocketClient client)
        {
            StartTime = DateTime.Now;
            _client = client;
            // Some settings are needed before the client connects (e.g. token).
            ReadConfig();
        }

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
        public SocketTextChannel AdminBotsChannel { get; private set; }


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
        public SocketUser AlertUser { get; private set; }
        public SocketRole CSGOPlaytestAdmin { get; private set; }
        public SocketRole TF2PlaytestAdmin { get; private set; }
        public static bool IncludePlayerCount { get; set; }
        public static string PlayerCount { get; set; }

        public bool GetIncludePlayerCount() => IncludePlayerCount;
        public string GetPlayerCount() => PlayerCount;

        public void SetPlayerCount(string playerCount) => PlayerCount = playerCount;
        public void SetIncludePlayerCount(bool includeCount) => IncludePlayerCount = includeCount;

        public async Task DeserializeConfig()
        {
            ReadConfig();
            await DeserializeChannels();
            GetRoles();

            IncludePlayerCount = false;
            PlayerCount = "0";

            try
            {
                AlertUser = _client.GetUser(RSettings.ProgramSettings.AlertUser);
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

            CSGOPlaytestAdmin = Guild.GetRole(RSettings.UserRoles.CSGOPlaytestAdmin);
            Console.WriteLine($"CSGOPlaytestAdmin ID:{CSGOPlaytestAdmin.Id} Discovered Name:{CSGOPlaytestAdmin.Name}");

            TF2PlaytestAdmin = Guild.GetRole(RSettings.UserRoles.TF2PlaytestAdmin);
            Console.WriteLine($"TF2PlaytestAdmin ID:{TF2PlaytestAdmin.Id} Discovered Name:{TF2PlaytestAdmin.Name}");

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

                if (user == null) _ = _log.LogMessage($"Error Setting SocketGuildUser for string `{input}`");
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
    }
}