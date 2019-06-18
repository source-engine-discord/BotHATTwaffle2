using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using BotHATTwaffle2.src.Handlers;
using Discord.Commands;
using Discord.WebSocket;
using Imgur.API.Authentication.Impl;
using Imgur.API.Endpoints.Impl;
using Newtonsoft.Json;
using RCONServerLib;

namespace BotHATTwaffle2.Services
{
    public class DataService
    {
        private const ConsoleColor LogColor = ConsoleColor.Cyan;
        private readonly DiscordSocketClient _client;
        private LogHandler _log;

        public DataService(DiscordSocketClient client)
        {
            _client = client;
            // Some settings are needed before the client connects (e.g. token).
            ReadConfig();
        }

        public RootSettings RootSettings { get; set; }

        public SocketGuild Guild { get; set; }

        // Channels
        public SocketTextChannel GeneralChannel { get; private set; }
        public SocketTextChannel LogChannel { get; private set; }
        public SocketTextChannel WelcomeChannel { get; private set; }
        public SocketTextChannel AnnouncementChannel { get; private set; }
        public SocketTextChannel TestingChannel { get; private set; }
        public SocketTextChannel CompetitiveTestingChannel { get; private set; }

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

        public bool includePlayerCount { get; set; }
        public string playerCount { get; set; }

        public async Task DeserializeConfig()
        {
            ReadConfig();
            await DeserializeChannels();
            GetRoles();

            includePlayerCount = false;
            playerCount = "0";

            AlertUser = _client.GetUser(RootSettings.ProgramSettings.AlertUser);

            Console.ForegroundColor = ConsoleColor.Magenta;
            Console.WriteLine("SETTINGS HAVE BEEN LOADED");
            Console.ForegroundColor = ConsoleColor.Red;
            if (RootSettings.ProgramSettings.Debug)
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

            RootSettings = JsonConvert.DeserializeObject<RootSettings>(File.ReadAllText(configPath));
        }


        /// <summary>
        ///     Deserialize channels from the configuration file.
        /// </summary>
        /// <exception cref="InvalidOperationException">Thrown when a channel can't be found.</exception>
        /// <returns>No object or value is returned by this method when it completes.</returns>
        private async Task DeserializeChannels()
        {
            Guild = _client.Guilds.FirstOrDefault();

            Console.ForegroundColor = LogColor;

            Console.WriteLine($"Active Guild: {Guild?.Name}\n");

            LogChannel = await ParseChannel(RootSettings.ProgramSettings.LogChannel);
            Console.WriteLine($"LogChannel ID:{LogChannel.Id} Discovered Name:{LogChannel.Name}");

            if (RootSettings.ProgramSettings.Debug)
            {
                Console.WriteLine("Setting channels based on debug values!!!");

                GeneralChannel = await ParseChannel(RootSettings.DebugValues.GeneralChannel);
                Console.WriteLine($"GeneralChannel ID:{GeneralChannel.Id} Discovered Name:{GeneralChannel.Name}");

                WelcomeChannel = await ParseChannel(RootSettings.DebugValues.WelcomeChannel);
                Console.WriteLine($"WelcomeChannel ID:{WelcomeChannel.Id} Discovered Name:{WelcomeChannel.Name}");

                AnnouncementChannel = await ParseChannel(RootSettings.DebugValues.AnnouncementChannel);
                Console.WriteLine(
                    $"AnnouncementChannel ID:{AnnouncementChannel.Id} Discovered Name:{AnnouncementChannel.Name}");

                TestingChannel = await ParseChannel(RootSettings.DebugValues.TestingChannel);
                Console.WriteLine($"TestingChannel ID:{TestingChannel.Id} Discovered Name:{TestingChannel.Name}");

                CompetitiveTestingChannel = await ParseChannel(RootSettings.DebugValues.CompetitiveTestingChannel);
                Console.WriteLine(
                    $"CompetitiveTestingChannel ID:{CompetitiveTestingChannel.Id} Discovered Name:{CompetitiveTestingChannel.Name}");
            }
            else
            {
                GeneralChannel = await ParseChannel(RootSettings.General.GeneralChannel);
                Console.WriteLine($"GeneralChannel ID:{GeneralChannel.Id} Discovered Name:{GeneralChannel.Name}");

                WelcomeChannel = await ParseChannel(RootSettings.General.WelcomeChannel);
                Console.WriteLine($"WelcomeChannel ID:{WelcomeChannel.Id} Discovered Name:{WelcomeChannel.Name}");

                AnnouncementChannel = await ParseChannel(RootSettings.General.AnnouncementChannel);
                Console.WriteLine(
                    $"AnnouncementChannel ID:{AnnouncementChannel.Id} Discovered Name:{AnnouncementChannel.Name}");

                TestingChannel = await ParseChannel(RootSettings.General.TestingChannel);
                Console.WriteLine($"TestingChannel ID:{TestingChannel.Id} Discovered Name:{TestingChannel.Name}");

                CompetitiveTestingChannel = await ParseChannel(RootSettings.General.CompetitiveTestingChannel);
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

            Console.ForegroundColor = LogColor;

            if (RootSettings.ProgramSettings.Debug)
            {
                Console.WriteLine("\nSetting roles based on debug values!!!");

                ModeratorRole = guild.GetRole(RootSettings.DebugValues.Moderator);
                Console.WriteLine($"Moderator ID:{ModeratorRole.Id} Discovered Name:{ModeratorRole.Name}");

                PlayTesterRole = guild.GetRole(RootSettings.DebugValues.Playtester);
                Console.WriteLine($"Playtester ID:{PlayTesterRole.Id} Discovered Name:{PlayTesterRole.Name}");

                MuteRole = guild.GetRole(RootSettings.DebugValues.Muted);
                Console.WriteLine($"Muted ID:{MuteRole.Id} Discovered Name:{MuteRole.Name}");

                ActiveRole = guild.GetRole(RootSettings.DebugValues.Active);
                Console.WriteLine($"Active ID:{ActiveRole.Id} Discovered Name:{ActiveRole.Name}");

                PatreonsRole = guild.GetRole(RootSettings.DebugValues.Patreons);
                Console.WriteLine($"Patreons ID:{PatreonsRole.Id} Discovered Name:{PatreonsRole.Name}");

                CommunityTesterRole = guild.GetRole(RootSettings.DebugValues.CommunityTester);
                Console.WriteLine(
                    $"CommunityTesterRole ID:{CommunityTesterRole.Id} Discovered Name:{CommunityTesterRole.Name}");

                BotsRole = guild.GetRole(RootSettings.DebugValues.Bots);
                Console.WriteLine($"BotsRole ID:{BotsRole.Id} Discovered Name:{BotsRole.Name}");

                AdminRole = guild.GetRole(RootSettings.DebugValues.Admin);
                Console.WriteLine($"AdminRole ID:{AdminRole.Id} Discovered Name:{AdminRole.Name}");

                CompetitiveTesterRole = guild.GetRole(RootSettings.DebugValues.CompetitiveTester);
                Console.WriteLine(
                    $"CompetitiveTesterRole ID:{CompetitiveTesterRole.Id} Discovered Name:{CompetitiveTesterRole.Name}");
            }
            else
            {
                ModeratorRole = guild.GetRole(RootSettings.UserRoles.Moderator);
                Console.WriteLine($"\nModerator ID:{ModeratorRole.Id} Discovered Name:{ModeratorRole.Name}");

                PlayTesterRole = guild.GetRole(RootSettings.UserRoles.Playtester);
                Console.WriteLine($"Playtester ID:{PlayTesterRole.Id} Discovered Name:{PlayTesterRole.Name}");

                MuteRole = guild.GetRole(RootSettings.UserRoles.Muted);
                Console.WriteLine($"Muted ID:{MuteRole.Id} Discovered Name:{MuteRole.Name}");

                ActiveRole = guild.GetRole(RootSettings.UserRoles.Active);
                Console.WriteLine($"Active ID:{ActiveRole.Id} Discovered Name:{ActiveRole.Name}");

                PatreonsRole = guild.GetRole(RootSettings.UserRoles.Patreons);
                Console.WriteLine($"Patreons ID:{PatreonsRole.Id} Discovered Name:{PatreonsRole.Name}");

                CommunityTesterRole = guild.GetRole(RootSettings.UserRoles.CommunityTester);
                Console.WriteLine(
                    $"CommunityTesterRole ID:{CommunityTesterRole.Id} Discovered Name:{CommunityTesterRole.Name}");

                BotsRole = guild.GetRole(RootSettings.UserRoles.Bots);
                Console.WriteLine($"BotsRole ID:{BotsRole.Id} Discovered Name:{BotsRole.Name}");

                AdminRole = guild.GetRole(RootSettings.UserRoles.Admin);
                Console.WriteLine($"AdminRole ID:{AdminRole.Id} Discovered Name:{AdminRole.Name}");

                CompetitiveTesterRole = guild.GetRole(RootSettings.UserRoles.CompetitiveTester);
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
                    throw new InvalidOperationException($"Error Setting SocketUser for string {input}");
            }
            catch (Exception e)
            {
                _ = _log.LogMessage(e.ToString(), alert: true, color: LogColor);
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
                _ = _log.LogMessage(e.ToString(), alert: true, color: LogColor);
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
                var client = new ImgurClient(RootSettings.ProgramSettings.ImgurApi);
                var endpoint = new AlbumEndpoint(client);

                var images = endpoint.GetAlbumAsync(albumId).Result.Images.Select(i => i.Link).ToList();

                _ = _log.LogMessage("Getting Imgur Info from Imgur API" +
                                    $"\nAlbum URL: {albumUrl}" +
                                    $"\nAlbum ID: {albumId}" +
                                    $"\nClient Credits Remaining: {client.RateLimit.ClientRemaining} of {client.RateLimit.ClientLimit}" +
                                    $"\nImages Found:\n{string.Join("\n", images)}", false, color: LogColor);

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

            IPHostEntry iPHostEntry = null;
            try
            {
                iPHostEntry = Dns.GetHostEntry(server.Address);

                if (RootSettings.ProgramSettings.Debug)
                    _ = _log.LogMessage($"Server Address: {iPHostEntry.AddressList.FirstOrDefault()}", false, color: LogColor);
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
                
                if (RootSettings.ProgramSettings.Debug)
                    _ = _log.LogMessage($"Waiting for authentication from rcon server, tried: {retryCount} time.", false, color: LogColor);
            }

            //Are we connected and authenticated?
            if (client.Connected && client.Authenticated)
            {
                //Send command and and store the server's response in reply.
                //However for some reason it takes a while for the server to reply
                //As a result we will wait for a proper reply below.
                client.SendCommand(command, result => { reply = result; });

                await _log.LogMessage($"Sending RCON command:\n{command}\nTo server: {server.Address}", channel: false,
                    color: LogColor);

                retryCount = 0;

                //Delay until we have a proper reply from the server.
                while (reply == null && retryCount < 20)
                {
                    await Task.Delay(50);
                    retryCount++;

                    if (RootSettings.ProgramSettings.Debug)
                        _ = _log.LogMessage($"Waiting for string from rcon server, tried: {retryCount} time.", false,
                            color: LogColor);
                }
            }
            else
                reply = $"Unable to connect or authenticate to RCON server with the ID of {serverId}.";

            return FormatRconServerReply(reply);
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

            playerCount = formatted?.Substring(0, formatted.IndexOf(" ", StringComparison.Ordinal));
        }

        public string GetServerCode(string fullServerAddress)
        {
            return fullServerAddress.Substring(0, fullServerAddress.IndexOf(".", StringComparison.Ordinal));
        }
    }
}