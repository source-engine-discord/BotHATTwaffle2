using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using BotHATTwaffle2.Commands.Readers;
using BotHATTwaffle2.src.Handlers;
using Discord.WebSocket;
using Imgur.API.Authentication.Impl;
using Imgur.API.Endpoints.Impl;
using Newtonsoft.Json;

namespace BotHATTwaffle2.Services
{
    public class DataService
    {
        private const ConsoleColor logColor = ConsoleColor.Cyan;
        private readonly DiscordSocketClient _client;
        private LogHandler _log;

        public DataService(DiscordSocketClient client)
        {
            _client = client;
            // Some settings are needed before the client connects (e.g. token).
            ReadConfig();
        }

        public RootSettings RootSettings { get; set; }

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

        public async Task DeserialiseConfig()
        {
            ReadConfig();
            await DeserialiseChannels();
            GetRoles();

            AlertUser = _client.GetUser(RootSettings.program_settings.alertUser);

            Console.ForegroundColor = ConsoleColor.Magenta;
            Console.WriteLine("SETTINGS HAVE BEEN LOADED");
            Console.ForegroundColor = ConsoleColor.Red;
            if (RootSettings.program_settings.debug)
                Console.WriteLine("  _____  ______ ____  _    _  _____    ____  _   _ \r\n |  __ \\|  ____|  _ \\| |  | |/ ____|  / __ \\| \\ | |\r\n | |  | | |__  | |_) | |  | | |  __  | |  | |  \\| |\r\n | |  | |  __| |  _ <| |  | | | |_ | | |  | | . ` |\r\n | |__| | |____| |_) | |__| | |__| | | |__| | |\\  |\r\n |_____/|______|____/ \\____/ \\_____|  \\____/|_| \\_|\r\n                                                   \r\n                                                   ");
            Console.ResetColor();
        }

        public void SetLogHandler(LogHandler log)
        {
            _log = log;
        }

        private void ReadConfig()
        {
            const string CONFIG_PATH = "settings.json";

            if (!File.Exists(CONFIG_PATH))
            {
                Console.WriteLine("Settings file not found. Create settings file and try again.");
                Console.ReadLine();

                //Close program
                Environment.Exit(1);
            }

            RootSettings = JsonConvert.DeserializeObject<RootSettings>(File.ReadAllText(CONFIG_PATH));
        }


        /// <summary>
        ///     Deserialises channels from the configuration file.
        /// </summary>
        /// <exception cref="InvalidOperationException">Thrown when a channel can't be found.</exception>
        /// <returns>No object or value is returned by this method when it completes.</returns>
        private async Task DeserialiseChannels()
        {
            var guild = _client.Guilds.FirstOrDefault();

            Console.ForegroundColor = logColor;

            Console.WriteLine($"Active Guild: {guild.Name}");

            LogChannel = await ParseChannel(RootSettings.program_settings.logChannel);
            Console.WriteLine($"LogChannel ID:{LogChannel.Id} Discovered Name:{LogChannel.Name}");

            GeneralChannel = await ParseChannel(RootSettings.general.generalChannel);
            Console.WriteLine($"GeneralChannel ID:{GeneralChannel.Id} Discovered Name:{GeneralChannel.Name}");

            WelcomeChannel = await ParseChannel(RootSettings.general.welcomeChannel);
            Console.WriteLine($"WelcomeChannel ID:{WelcomeChannel.Id} Discovered Name:{WelcomeChannel.Name}");

            AnnouncementChannel = await ParseChannel(RootSettings.general.announcementChannel);
            Console.WriteLine(
                $"AnnouncementChannel ID:{AnnouncementChannel.Id} Discovered Name:{AnnouncementChannel.Name}");

            TestingChannel = await ParseChannel(RootSettings.general.testingChannel);
            Console.WriteLine($"TestingChannel ID:{TestingChannel.Id} Discovered Name:{TestingChannel.Name}");

            CompetitiveTestingChannel = await ParseChannel(RootSettings.general.competitiveTestingChannel);
            Console.WriteLine(
                $"CompetitiveTestingChannel ID:{CompetitiveTestingChannel.Id} Discovered Name:{CompetitiveTestingChannel.Name}");

            Console.ResetColor();

            async Task<SocketTextChannel> ParseChannel(string key)
            {
                var channel = await ChannelTypeReader<SocketTextChannel>.GetBestResultAsync(guild, key);

                if (channel == null)
                    throw new InvalidOperationException($"The value of key '{key}' could not be parsed as a channel.");

                return channel;
            }
        }

        /// <summary>
        ///     Retrieves role socket entities from the IDs in the <see cref="Role" /> enum.
        /// </summary>
        /// <exception cref="InvalidOperationException">Thrown when a role can't be found.</exception>
        private void GetRoles()
        {
            var guild = _client.Guilds.FirstOrDefault();

            Console.ForegroundColor = logColor;

            ModeratorRole = guild.GetRole(RootSettings.userRoles.moderator);
            Console.WriteLine($"\nModerator ID:{ModeratorRole.Id} Discovered Name:{ModeratorRole.Name}");

            PlayTesterRole = guild.GetRole(RootSettings.userRoles.playtester);
            Console.WriteLine($"Playtester ID:{PlayTesterRole.Id} Discovered Name:{PlayTesterRole.Name}");

            MuteRole = guild.GetRole(RootSettings.userRoles.muted);
            Console.WriteLine($"Muted ID:{MuteRole.Id} Discovered Name:{MuteRole.Name}");

            ActiveRole = guild.GetRole(RootSettings.userRoles.active);
            Console.WriteLine($"Active ID:{ActiveRole.Id} Discovered Name:{ActiveRole.Name}");

            PatreonsRole = guild.GetRole(RootSettings.userRoles.patreons);
            Console.WriteLine($"Patreons ID:{PatreonsRole.Id} Discovered Name:{PatreonsRole.Name}");

            CommunityTesterRole = guild.GetRole(RootSettings.userRoles.communityTester);
            Console.WriteLine(
                $"CommunityTesterRole ID:{CommunityTesterRole.Id} Discovered Name:{CommunityTesterRole.Name}");

            BotsRole = guild.GetRole(RootSettings.userRoles.bots);
            Console.WriteLine($"BotsRole ID:{BotsRole.Id} Discovered Name:{BotsRole.Name}");

            AdminRole = guild.GetRole(RootSettings.userRoles.admin);
            Console.WriteLine($"AdminRole ID:{AdminRole.Id} Discovered Name:{AdminRole.Name}");

            CompetitiveTesterRole = guild.GetRole(RootSettings.userRoles.competitiveTester);
            Console.WriteLine(
                $"CompetitiveTesterRole ID:{CompetitiveTesterRole.Id} Discovered Name:{CompetitiveTesterRole.Name}");

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
                _ = _log.LogMessage(e.ToString(), alert: true, color: logColor);
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
                _ = _log.LogMessage(e.ToString(), alert: true, color: logColor);
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
            var images = new List<string>();
            try
            {
                albumUrl = "https://imgur.com/a/Ol6lhld";

                var albumId = albumUrl.Replace(@"/gallery/", @"/a/").Substring(albumUrl.IndexOf(@"/a/") + 3);
                var client = new ImgurClient(RootSettings.program_settings.imgurAPI);
                var endpoint = new AlbumEndpoint(client);

                images = endpoint.GetAlbumAsync(albumId).Result.Images.Select(i => i.Link).ToList();

                _ = _log.LogMessage("Getting Imgur Info from Imgur API" +
                                    $"\nAlbum URL: {albumUrl}" +
                                    $"\nAlbum ID: {albumId}" +
                                    $"\nClient Credits Remaining: {client.RateLimit.ClientRemaining} of {client.RateLimit.ClientLimit}" +
                                    $"\nImages Found:\n{string.Join("\n", images)}", false, color: logColor);

                return images;
            }
            catch
            {
                return null;
            }
        }
    }
}