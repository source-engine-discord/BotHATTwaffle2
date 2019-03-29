using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Discord.WebSocket;
using Newtonsoft.Json;
using BotHATTwaffle2.Commands.Readers;

namespace BotHATTwaffle2.Services
{
    public class DataService
    {
        private readonly DiscordSocketClient _client;

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

        public DataService(DiscordSocketClient client)
        {
            _client = client;

            // Some settings are needed before the client connects (e.g. token).
            ReadConfig();
        }

        public async Task DeserialiseConfig()
        {
            ReadConfig();
            await DeserialiseChannels();
            GetRoles();

            Console.ForegroundColor = ConsoleColor.Magenta;
            Console.WriteLine("SETTINGS HAVE BEEN LOADED");
            Console.ResetColor();
        }

        private void ReadConfig()
        {
            const string CONFIG_PATH = "settings.json";

            if (!File.Exists(CONFIG_PATH))
            {
                Console.WriteLine("Settings file not found. Create settings file and try again.");
                Console.ReadLine();

                //Close program
                System.Environment.Exit(1);
            }

            RootSettings = JsonConvert.DeserializeObject<RootSettings>(File.ReadAllText(CONFIG_PATH));
		}


        /// <summary>
        /// Deserialises channels from the configuration file.
        /// </summary>
        /// <exception cref="InvalidOperationException">Thrown when a channel can't be found.</exception>
        /// <returns>No object or value is returned by this method when it completes.</returns>
        private async Task DeserialiseChannels()
        {
            SocketGuild guild = _client.Guilds.FirstOrDefault();

            Console.ForegroundColor = ConsoleColor.Red;

            Console.WriteLine($"Active Guild: {guild.Name}");

            Console.ForegroundColor = ConsoleColor.Blue;

            LogChannel = await ParseChannel(RootSettings.program_settings.logChannel);
            Console.WriteLine($"LogChannel ID:{LogChannel.Id} Discovered Name:{LogChannel.Name}");

            GeneralChannel = await ParseChannel(RootSettings.general.generalChannel);
            Console.WriteLine($"GeneralChannel ID:{GeneralChannel.Id} Discovered Name:{GeneralChannel.Name}");

            WelcomeChannel = await ParseChannel(RootSettings.general.welcomeChannel);
            Console.WriteLine($"WelcomeChannel ID:{WelcomeChannel.Id} Discovered Name:{WelcomeChannel.Name}");

            AnnouncementChannel = await ParseChannel(RootSettings.general.announcementChannel);
            Console.WriteLine($"AnnouncementChannel ID:{AnnouncementChannel.Id} Discovered Name:{AnnouncementChannel.Name}");

            TestingChannel = await ParseChannel(RootSettings.general.testingChannel);
            Console.WriteLine($"TestingChannel ID:{TestingChannel.Id} Discovered Name:{TestingChannel.Name}");

            CompetitiveTestingChannel = await ParseChannel(RootSettings.general.compeitiveTestingChannel);
            Console.WriteLine($"CompetitiveTestingChannel ID:{CompetitiveTestingChannel.Id} Discovered Name:{CompetitiveTestingChannel.Name}");

            Console.ResetColor();

            async Task<SocketTextChannel> ParseChannel(string key)
            {
                SocketTextChannel channel = await ChannelTypeReader<SocketTextChannel>.GetBestResultAsync(guild, key);
                
                if (channel == null)
                    throw new InvalidOperationException($"The value of key '{key}' could not be parsed as a channel.");

                return channel;
            }


        }

        /// <summary>
        /// Retrieves role socket entities from the IDs in the <see cref="Role"/> enum.
        /// </summary>
        /// <exception cref="InvalidOperationException">Thrown when a role can't be found.</exception>
        private void GetRoles()
        {
            SocketGuild guild = _client.Guilds.FirstOrDefault();

            Console.ForegroundColor = ConsoleColor.Green;

            ModeratorRole = guild.GetRole(RootSettings.userRoles.moderator);
            Console.WriteLine($"Moderator ID:{ModeratorRole.Id} Discovered Name:{ModeratorRole.Name}");

            PlayTesterRole = guild.GetRole(RootSettings.userRoles.playtester);
            Console.WriteLine($"Playester ID:{PlayTesterRole.Id} Discovered Name:{PlayTesterRole.Name}");

            MuteRole = guild.GetRole(RootSettings.userRoles.muted);
            Console.WriteLine($"Muted ID:{MuteRole.Id} Discovered Name:{MuteRole.Name}");

            ActiveRole = guild.GetRole(RootSettings.userRoles.active);
            Console.WriteLine($"Active ID:{ActiveRole.Id} Discovered Name:{ActiveRole.Name}");

            PatreonsRole = guild.GetRole(RootSettings.userRoles.patreons);
            Console.WriteLine($"Patreons ID:{PatreonsRole.Id} Discovered Name:{PatreonsRole.Name}");

            CommunityTesterRole = guild.GetRole(RootSettings.userRoles.communityTester);
            Console.WriteLine($"CommunityTesterRole ID:{CommunityTesterRole.Id} Discovered Name:{CommunityTesterRole.Name}");

            BotsRole = guild.GetRole(RootSettings.userRoles.bots);
            Console.WriteLine($"BotsRole ID:{BotsRole.Id} Discovered Name:{BotsRole.Name}");

            AdminRole = guild.GetRole(RootSettings.userRoles.admin);
            Console.WriteLine($"AdminRole ID:{AdminRole.Id} Discovered Name:{AdminRole.Name}");

            CompetitiveTesterRole = guild.GetRole(RootSettings.userRoles.competitiveTester);
            Console.WriteLine($"CompetitiveTesterRole ID:{CompetitiveTesterRole.Id} Discovered Name:{CompetitiveTesterRole.Name}");

            Console.ResetColor();
        }
    }
}