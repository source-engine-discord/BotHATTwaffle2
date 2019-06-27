using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Runtime.ConstrainedExecution;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using BotHATTwaffle2.Handlers;
using Discord.Commands;
using Discord.WebSocket;
using FluentScheduler;
using HtmlAgilityPack;
using Imgur.API.Authentication.Impl;
using Imgur.API.Endpoints.Impl;
using Newtonsoft.Json;
using RCONServerLib;

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
                                  $"I need this user to function properly. Please set the connect user in settings.json " +
                                  $"and restart.");
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
                var channel = await Commands.Readers.ChannelTypeReader<SocketTextChannel>.GetBestResultAsync(Guild, key);

                if (channel == null)
                    throw new InvalidOperationException($"The value of key '{key}' could not be parsed as a channel.");

                return channel;
            }
        }

        /// <summary>
        ///     Retrieves role socket entities from the IDs in the <see>
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

                if (user == null)
                {
                    _ = _log.LogMessage($"Error Setting SocketUser for string `{input}`");
                }
            }
            catch (Exception e)
            {
                _ = _log.LogMessage(e.ToString(), alert: true, color: LOG_COLOR);
            }

            return user;
        }

        public List<SocketUser> GetSocketUser(string input, char splitChar)
        {
            var users = new List<SocketUser>();
            var creators = input.Split(splitChar).Select(c => c.Trim()).ToArray();

            foreach (var c in creators) users.Add(GetSocketUser(c));

            return users;
        }

        /// <summary>
        ///     Validates a URI as good
        /// </summary>
        /// <param name="input">Input string</param>
        /// <returns>Returns URI object, or null.</returns>
        public Uri ValidateUri(string input)
        {
            try
            {
                if (Uri.IsWellFormedUriString(input, UriKind.Absolute))
                    return new Uri(input, UriKind.Absolute);

                throw new UriFormatException($"Unable to create URI for input {input}");
            }
            catch (UriFormatException e)
            {
                _ = _log.LogMessage(e.ToString(), alert: true, color: LOG_COLOR);
            }

            return null;
        }

        /// <summary>
        ///     Provides a list or URLs for each image in an imgur album, or null if not possible
        /// </summary>
        /// <param name="albumUrl">URL of imgur album</param>
        /// <returns>List or URLs, or null</returns>
        public List<string> GetImgurAlbum(string albumUrl)
        {
            try
            {
                var albumId = albumUrl.Replace(@"/gallery/", @"/a/").Substring(albumUrl.IndexOf(@"/a/", StringComparison.Ordinal) + 3);
                var client = new ImgurClient(RSettings.ProgramSettings.ImgurApi);
                var endpoint = new AlbumEndpoint(client);

                var images = endpoint.GetAlbumAsync(albumId).Result.Images.Select(i => i.Link).ToList();

                _ = _log.LogMessage("Getting Imgur Info from Imgur API" +
                                    $"\nAlbum URL: {albumUrl}" +
                                    $"\nAlbum ID: {albumId}" +
                                    $"\nClient Credits Remaining: {client.RateLimit.ClientRemaining} of {client.RateLimit.ClientLimit}" +
                                    $"\nImages Found:\n{string.Join("\n", images)}", false, color: LOG_COLOR);

                return images;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Sends a RCON command to a playtest server.
        /// </summary>
        /// <param name="serverId">ID of server to send command to</param>
        /// <param name="command">RCON Command to send</param>
        /// <returns></returns>
        public async Task<string> RconCommand(string serverId, string command)
        {
            string reply = null;
            
            var server = DatabaseHandler.GetTestServer(serverId);

            if (server == null)
                return null;

            IPHostEntry iPHostEntry = null;
            try
            {
                iPHostEntry = Dns.GetHostEntry(server.Address);

                if (RSettings.ProgramSettings.Debug)
                    _ = _log.LogMessage($"Server Address: {iPHostEntry.AddressList.FirstOrDefault()}", false, color: LOG_COLOR);
            }
            catch
            {
                //No address
            }

            int retryCount = 0;
            var client = new RemoteConClient();

            client.Connect(iPHostEntry.AddressList.FirstOrDefault().ToString(), 27015);

            //Delay until the client is connected, time out after 20 tries
            while (!client.Authenticated && client.Connected && retryCount < 20)
            {
                await Task.Delay(50);
                client.Authenticate(server.RconPassword);
                retryCount++;
                
                if (RSettings.ProgramSettings.Debug)
                    _ = _log.LogMessage($"Waiting for authentication from rcon server, tried: {retryCount} time.", false, color: LOG_COLOR);
            }

            //Are we connected and authenticated?
            if (client.Connected && client.Authenticated)
            {
                //Send command and and store the server's response in reply.
                //However for some reason it takes a while for the server to reply
                //As a result we will wait for a proper reply below.
                client.SendCommand(command, result => { reply = result; });

                await _log.LogMessage($"Sending RCON command:\n`{command}`\nTo server: `{server.Address}`", channel: true,
                    color: LOG_COLOR);

                retryCount = 0;

                //Delay until we have a proper reply from the server.
                while (reply == null && retryCount < 20)
                {
                    await Task.Delay(50);
                    retryCount++;

                    if (RSettings.ProgramSettings.Debug)
                        _ = _log.LogMessage($"Waiting for string from rcon server, tried: {retryCount} time.", false,
                            color: LOG_COLOR);
                }

                client.Disconnect();
            }
            else
                reply = $"Unable to connect or authenticate to RCON server with the ID of {serverId}.";

            string finalReply = FormatRconServerReply(reply);

            if (string.IsNullOrWhiteSpace(finalReply))
                return $"{command} was sent, but provided no reply.";

            return finalReply;
        }

        private string FormatRconServerReply(string input)
        {
            if (input == null)
                return "No response from server, but the command may still have been sent.";

            string[] replyArray = input.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
            input = string.Join("\n", replyArray.Where(x => !x.Trim().StartsWith("L ")));

            return input;
        }

        public async Task GetPlayCountFromServer(string serverId)
        {
            var returned = await RconCommand(serverId, "status");
            string[] replyArray = returned.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);

            //Only get the line with player count
            IEnumerable<string> results = replyArray.Where(l => l.StartsWith("players"));

            //Remove extra information from string
            var formatted = results.FirstOrDefault()?.Substring(10);

            PlayerCount = formatted?.Substring(0, formatted.IndexOf(" ", StringComparison.Ordinal));
        }

        public string GetServerCode(string fullServerAddress)
        {
            if (fullServerAddress.Contains('.'))
                return fullServerAddress.Substring(0, fullServerAddress.IndexOf(".", StringComparison.Ordinal));

            return fullServerAddress;
        }

        /// <summary>
        /// Gets the workshop ID from a FQDN workshop link
        /// </summary>
        /// <param name="workshopUrl">FQDN of workshop link</param>
        /// <returns>Workshop ID</returns>
        public string GetWorkshopIdFromFqdn(string workshopUrl)
        {
            return Regex.Match(workshopUrl, @"(\d+)").Value;
        }

        public async Task<bool> UnmuteUser(ulong userId)
        {
            var dbResult = DatabaseHandler.UnmuteUser(userId);
            if (dbResult)
            {
                try
                {
                    var guildUser = Guild.GetUser(userId);
                    await guildUser.RemoveRoleAsync(MuteRole);
                    await _log.LogMessage($"Removed mute from `{guildUser.Username}`.");
                    return true;
                }
                catch (Exception e)
                {
                    await _log.LogMessage($"Failed to unmute ID `{userId}`, they likely left the server.\n```{e.Message}```");
                    return false;
                }
            }
            await _log.LogMessage($"Failed to unmute ID `{userId}` because no mute was found in the database.");
            return false;
        }
    }
}