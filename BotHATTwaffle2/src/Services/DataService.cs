using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using BotHATTwaffle2.Commands.Readers;
using BotHATTwaffle2.Handlers;
using BotHATTwaffle2.Util;
using Discord.WebSocket;
using Newtonsoft.Json;

namespace BotHATTwaffle2.Services
{
    public class DataService
    {
        private const ConsoleColor LOG_COLOR = ConsoleColor.Cyan;
        private readonly DiscordSocketClient _client;
        private LogHandler _log;

        public DataService(DiscordSocketClient client)
        {
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
        public SocketTextChannel AnnouncementChannel { get; private set; }
        public SocketTextChannel TestingChannel { get; private set; }
        public SocketTextChannel CompetitiveTestingChannel { get; private set; }
        public SocketTextChannel WebhookChannel { get; private set; }

        // Roles
        public SocketRole PlayTesterRole { get; private set; }
        public SocketRole MuteRole { get; private set; }
        public SocketRole ModeratorRole { get; private set; }
        public SocketRole ActiveRole { get; private set; }
        public SocketRole PatreonsRole { get; private set; }
        public SocketRole CommunityTesterRole { get; private set; }
        public SocketRole BotsRole { get; private set; }
        public SocketRole AdminRole { get; private set; }
        public SocketRole CompetitiveTesterRole { get; private set; }
        public SocketUser AlertUser { get; private set; }

        public bool IncludePlayerCount { get; set; }
        public string PlayerCount { get; set; }

        public List<SocketUser> IgnoreListenList = new List<SocketUser>();

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
                                  "I need this user to function properly. Please set the connect user in settings.json " +
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

            LogChannel = await ParseChannel(RSettings.ProgramSettings.LogChannel);
            Console.WriteLine($"LogChannel ID:{LogChannel.Id} Discovered Name:{LogChannel.Name}");

            if (RSettings.ProgramSettings.Debug)
            {
                Console.WriteLine("Setting channels based on debug values!!!");

                GeneralChannel = await ParseChannel(RSettings.DebugValues.GeneralChannel);
                Console.WriteLine($"GeneralChannel ID:{GeneralChannel.Id} Discovered Name:{GeneralChannel.Name}");

                WelcomeChannel = await ParseChannel(RSettings.DebugValues.WelcomeChannel);
                Console.WriteLine($"WelcomeChannel ID:{WelcomeChannel.Id} Discovered Name:{WelcomeChannel.Name}");

                AnnouncementChannel = await ParseChannel(RSettings.DebugValues.AnnouncementChannel);
                Console.WriteLine(
                    $"AnnouncementChannel ID:{AnnouncementChannel.Id} Discovered Name:{AnnouncementChannel.Name}");

                WebhookChannel = await ParseChannel(RSettings.DebugValues.WebhookChannel);
                Console.WriteLine($"WebhookChannel ID:{WebhookChannel.Id} Discovered Name:{WebhookChannel.Name}");

                TestingChannel = await ParseChannel(RSettings.DebugValues.TestingChannel);
                Console.WriteLine($"TestingChannel ID:{TestingChannel.Id} Discovered Name:{TestingChannel.Name}");

                CompetitiveTestingChannel = await ParseChannel(RSettings.DebugValues.CompetitiveTestingChannel);
                Console.WriteLine(
                    $"CompetitiveTestingChannel ID:{CompetitiveTestingChannel.Id} Discovered Name:{CompetitiveTestingChannel.Name}");
            }
            else
            {
                GeneralChannel = await ParseChannel(RSettings.General.GeneralChannel);
                Console.WriteLine($"GeneralChannel ID:{GeneralChannel.Id} Discovered Name:{GeneralChannel.Name}");

                WelcomeChannel = await ParseChannel(RSettings.General.WelcomeChannel);
                Console.WriteLine($"WelcomeChannel ID:{WelcomeChannel.Id} Discovered Name:{WelcomeChannel.Name}");

                AnnouncementChannel = await ParseChannel(RSettings.General.AnnouncementChannel);
                Console.WriteLine(
                    $"AnnouncementChannel ID:{AnnouncementChannel.Id} Discovered Name:{AnnouncementChannel.Name}");

                TestingChannel = await ParseChannel(RSettings.General.TestingChannel);
                Console.WriteLine($"TestingChannel ID:{TestingChannel.Id} Discovered Name:{TestingChannel.Name}");

                WebhookChannel = await ParseChannel(RSettings.General.WebhookChannel);
                Console.WriteLine($"WebhookChannel ID:{WebhookChannel.Id} Discovered Name:{WebhookChannel.Name}");

                CompetitiveTestingChannel = await ParseChannel(RSettings.General.CompetitiveTestingChannel);
                Console.WriteLine(
                    $"CompetitiveTestingChannel ID:{CompetitiveTestingChannel.Id} Discovered Name:{CompetitiveTestingChannel.Name}");
            }

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
            var guild = _client.Guilds.FirstOrDefault();

            Console.ForegroundColor = LOG_COLOR;

            if (RSettings.ProgramSettings.Debug)
            {
                Console.WriteLine("\nSetting roles based on debug values!!!");

                ModeratorRole = guild.GetRole(RSettings.DebugValues.Moderator);
                Console.WriteLine($"Moderator ID:{ModeratorRole.Id} Discovered Name:{ModeratorRole.Name}");

                PlayTesterRole = guild.GetRole(RSettings.DebugValues.Playtester);
                Console.WriteLine($"Playtester ID:{PlayTesterRole.Id} Discovered Name:{PlayTesterRole.Name}");

                MuteRole = guild.GetRole(RSettings.DebugValues.Muted);
                Console.WriteLine($"Muted ID:{MuteRole.Id} Discovered Name:{MuteRole.Name}");

                ActiveRole = guild.GetRole(RSettings.DebugValues.Active);
                Console.WriteLine($"Active ID:{ActiveRole.Id} Discovered Name:{ActiveRole.Name}");

                PatreonsRole = guild.GetRole(RSettings.DebugValues.Patreons);
                Console.WriteLine($"Patreons ID:{PatreonsRole.Id} Discovered Name:{PatreonsRole.Name}");

                CommunityTesterRole = guild.GetRole(RSettings.DebugValues.CommunityTester);
                Console.WriteLine(
                    $"CommunityTesterRole ID:{CommunityTesterRole.Id} Discovered Name:{CommunityTesterRole.Name}");

                BotsRole = guild.GetRole(RSettings.DebugValues.Bots);
                Console.WriteLine($"BotsRole ID:{BotsRole.Id} Discovered Name:{BotsRole.Name}");

                AdminRole = guild.GetRole(RSettings.DebugValues.Admin);
                Console.WriteLine($"AdminRole ID:{AdminRole.Id} Discovered Name:{AdminRole.Name}");

                CompetitiveTesterRole = guild.GetRole(RSettings.DebugValues.CompetitiveTester);
                Console.WriteLine(
                    $"CompetitiveTesterRole ID:{CompetitiveTesterRole.Id} Discovered Name:{CompetitiveTesterRole.Name}");
            }
            else
            {
                ModeratorRole = guild.GetRole(RSettings.UserRoles.Moderator);
                Console.WriteLine($"\nModerator ID:{ModeratorRole.Id} Discovered Name:{ModeratorRole.Name}");

                PlayTesterRole = guild.GetRole(RSettings.UserRoles.Playtester);
                Console.WriteLine($"Playtester ID:{PlayTesterRole.Id} Discovered Name:{PlayTesterRole.Name}");

                MuteRole = guild.GetRole(RSettings.UserRoles.Muted);
                Console.WriteLine($"Muted ID:{MuteRole.Id} Discovered Name:{MuteRole.Name}");

                ActiveRole = guild.GetRole(RSettings.UserRoles.Active);
                Console.WriteLine($"Active ID:{ActiveRole.Id} Discovered Name:{ActiveRole.Name}");

                PatreonsRole = guild.GetRole(RSettings.UserRoles.Patreons);
                Console.WriteLine($"Patreons ID:{PatreonsRole.Id} Discovered Name:{PatreonsRole.Name}");

                CommunityTesterRole = guild.GetRole(RSettings.UserRoles.CommunityTester);
                Console.WriteLine(
                    $"CommunityTesterRole ID:{CommunityTesterRole.Id} Discovered Name:{CommunityTesterRole.Name}");

                BotsRole = guild.GetRole(RSettings.UserRoles.Bots);
                Console.WriteLine($"BotsRole ID:{BotsRole.Id} Discovered Name:{BotsRole.Name}");

                AdminRole = guild.GetRole(RSettings.UserRoles.Admin);
                Console.WriteLine($"AdminRole ID:{AdminRole.Id} Discovered Name:{AdminRole.Name}");

                CompetitiveTesterRole = guild.GetRole(RSettings.UserRoles.CompetitiveTester);
                Console.WriteLine(
                    $"CompetitiveTesterRole ID:{CompetitiveTesterRole.Id} Discovered Name:{CompetitiveTesterRole.Name}");
            }

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

        /// <summary>
        /// Converts a string of users to a list of socket users
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
        /// Finds a user in the Guild. If the input type is unknown, this method can be used.
        /// </summary>
        /// <param name="input">String with user#1234 or ID</param>
        /// <returns>SocketGuildUser that was found</returns>
        public SocketGuildUser GetSocketGuildUser(string input)
        {
            SocketGuildUser user = null;
            try
            {
                //Check if username#1234 was provided
                if (input.Contains('#'))
                {
                    var split = input.Split('#');
                    ushort.TryParse(split[1], out var disc);
                    user = Guild.Users.FirstOrDefault(x => x.Username.Equals(split[0],StringComparison.OrdinalIgnoreCase)
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
        /// Gets a socketGuildUser based on ID. Useful for converting a user from SocketUser to SocketGuildUser
        /// for use outside of the Guild.
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
        /// Takes a user ID and attempts to unmute them
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

                    await _log.LogMessage($"Removed mute from `{guildUser.Username}` because {reason}.", color: LOG_COLOR);
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