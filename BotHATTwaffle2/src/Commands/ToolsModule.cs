using System.Linq;
using System.Threading.Tasks;
using BotHATTwaffle2.Services;
using Discord;
using Discord.Commands;
using Discord.WebSocket;

namespace BotHATTwaffle2.Commands
{
    /// <summary>
    ///     Contains commands which provide links to various Source development tools.
    ///     TODO: Look into creating a generic class which can build these kinds of commands from JSON data.
    /// </summary>
    public class ToolsModule : ModuleBase<SocketCommandContext>
    {
        private readonly DiscordSocketClient _client;
        private readonly DataService _dataService;

        public ToolsModule(DiscordSocketClient client, DataService dataService)
        {
            _client = client;
            _dataService = dataService;
        }

        [Command("VTFEdit")]
        [Summary("Provides a download link to VTFEdit.")]
        [Alias("vtf")]
        public async Task VtfEditAsync()
        {
            var embed = new EmbedBuilder
            {
                Author = new EmbedAuthorBuilder
                {
                    Name = "Download VTFEdit",
                    IconUrl = _dataService.Guild.IconUrl
                },
                Title = "Click Here",
                Url = "https://www.tophattwaffle.com/downloads/vtfedit/",
                ThumbnailUrl = "https://content.tophattwaffle.com/BotHATTwaffle/vtfedit.png",
                Color = new Color(255, 206, 199),
                Description =
                    "VTFEdit is a lightweight tool used to convert images into Valve's proprietary format - VTF " +
                    "(Valve Texture Format). Because it has a GUI, it is substantially easier to use than Valve's " +
                    "own CLI tool, VTEX (Valve Texture Tool)."
            };

            await ReplyAsync(string.Empty, false, embed.Build());
        }

        [Command("GCFScape")]
        [Summary("Provides a download link to GCFScape.")]
        [Alias("gcf")]
        public async Task GcfScapeAsync()
        {
            var embed = new EmbedBuilder
            {
                Author = new EmbedAuthorBuilder
                {
                    Name = "Download GCFScape",
                    IconUrl = _dataService.Guild.IconUrl
                },
                Title = "Click Here",
                Url = "https://www.tophattwaffle.com/downloads/gcfscape/",
                ThumbnailUrl = "https://content.tophattwaffle.com/BotHATTwaffle/gcfscape.png",
                Color = new Color(63, 56, 156),
                Description =
                    "GCFScape is a tool for exploring, extracting, and managing content in various package formats " +
                    "used by Valve and Steam. Supported formats include VPK, GCF, PAK, BSP, and more."
            };

            await ReplyAsync(string.Empty, false, embed.Build());
        }

        [Command("Crowbar")]
        [Summary("Provides a download link to Crowbar.")]
        [Alias("cb")]
        public async Task CrowbarAsync()
        {
            var embed = new EmbedBuilder
            {
                Author = new EmbedAuthorBuilder
                {
                    Name = "Download Crowbar",
                    IconUrl = _dataService.Guild.IconUrl
                },
                Title = "Click Here",
                Url = "https://steamcommunity.com/groups/CrowbarTool",
                ThumbnailUrl =
                    "https://steamcdn-a.akamaihd.net/steamcommunity/public/images/avatars/ee/eedea029f691ffde0a0938c3048564555fcd5bac_full.jpg",
                Color = new Color(0, 128, 0),
                Description =
                    "Crowbar decompiles GoldSource and Source models (MDL and ANI file formats) and provides a " +
                    "It provides a convenient way to open HLMV (model viewer) with or without a selected model. " +
                    "It can search and unpack VPK, Tactical Intervention FPX, and Garry's Mod GMA packages."
            };

            await ReplyAsync(string.Empty, false, embed.Build());
        }

        [Command("VMTEditor")]
        [Summary("Provides a download link to VMT Editor.")]
        [Alias("vmt")]
        public async Task VmtEditorAsync()
        {
            var embed = new EmbedBuilder
            {
                Author = new EmbedAuthorBuilder
                {
                    Name = "Download VMT Editor",
                    IconUrl = _dataService.Guild.IconUrl
                },
                Title = "Click Here",
                Url = "https://gira-x.github.io/VMT-Editor/",
                ThumbnailUrl = "https://content.tophattwaffle.com/BotHATTwaffle/vmteditor.png",
                Color = new Color(50, 50, 50),
                Description =
                    "VMT Editor is, hands down, one of the best VMT (Valve Material Type) creation tools that " +
                    "exists for the Source engine. It quickly became a standard tool for most designers that " +
                    "regularly create VMT files. Created by Yanzl over at MapCore."
            };

            await ReplyAsync(string.Empty, false, embed.Build());
        }

        [Command("VIDE")]
        [Summary("Provides a download link to VIDE.")]
        public async Task VideAsync()
        {
            var embed = new EmbedBuilder
            {
                Author = new EmbedAuthorBuilder
                {
                    Name = "Download VIDE",
                    IconUrl = _dataService.Guild.IconUrl
                },
                Title = "Click Here",
                Url = "https://www.tophattwaffle.com/downloads/vide/",
                ThumbnailUrl = "https://content.tophattwaffle.com/BotHATTwaffle/vide.png",
                Color = new Color(50, 50, 50),
                Description =
                    "VIDE (Valve Integrated Development Environment) is a 3rd-party program which contains various " +
                    "tools. It is popular for its pakfile lump editor (packing assets into a level), but it can do " +
                    "so much more than that."
            };

            await ReplyAsync(string.Empty, false, embed.Build());
        }

        [Command("CompilePal")]
        [Summary("Provides a download link to CompilePal.")]
        [Alias("cpal")]
        public async Task CompilePalAsync()
        {
            var embed = new EmbedBuilder
            {
                Author = new EmbedAuthorBuilder
                {
                    Name = "Download CompilePal",
                    IconUrl = _dataService.Guild.IconUrl
                },
                Title = "Click Here",
                Url = "https://compilepal.ruar.ai//",
                ThumbnailUrl = "https://content.tophattwaffle.com/BotHATTwaffle/compilepal.jpg",
                Color = new Color(224, 29, 48),
                Description = "Compile Pal is an easy to use wrapper for the Source map compiling tools."
            };

            await ReplyAsync(string.Empty, false, embed.Build());
        }

        [Command("WallWorm")]
        [Summary("Provides a link to Wall Worm's website.")]
        [Alias("wwmt")]
        public async Task WallWormAsync()
        {
            var embed = new EmbedBuilder
            {
                Author = new EmbedAuthorBuilder
                {
                    Name = "Check out Wall Worm",
                    IconUrl = _dataService.Guild.IconUrl
                },
                Title = "Click Here",
                Url = "https://dev.wallworm.com/",
                ThumbnailUrl = "https://content.tophattwaffle.com/BotHATTwaffle/worm_logo.png",
                Color = new Color(21, 21, 21),
                Description =
                    "Wall Worm tools enable developers to design assets and level in Autodesk's 3ds Max and export " +
                    "them into the Source Engine. It's the best thing to ever happen to Source Engine modelling."
            };

            await ReplyAsync(string.Empty, false, embed.Build());
        }

        [Command("BSPSource")]
        [Summary("Provides a download link to BSPSource.")]
        [Alias("bsp")]
        public async Task BspSourceAsync()
        {
            var embed = new EmbedBuilder
            {
                Author = new EmbedAuthorBuilder
                {
                    Name = "Download BSPSource",
                    IconUrl = _dataService.Guild.IconUrl
                },
                Title = "Click Here",
                Url = "https://www.tophattwaffle.com/downloads/bspsource/",
                ThumbnailUrl = "https://content.tophattwaffle.com/BotHATTwaffle/BSPSource_icon.png",
                Color = new Color(84, 137, 71),
                Description =
                    "BSPSource is a tool for decompiling Source's BSP (Binary Space Partition) files into VMF " +
                    "(Valve Map Format) files that can be opened with Hammer. It is a great tool to see how things " +
                    "are done in a map. It should not be used to steal content."
            };

            await ReplyAsync(string.Empty, false, embed.Build());
        }

        [Command("AutoRadar")]
        [Summary("Provides a download link to Terri's Auto Radar.")]
        [Alias("tar")]
        public async Task TarAsync()
        {
            var embed = new EmbedBuilder
            {
                Author = new EmbedAuthorBuilder
                {
                    Name = "Download Terri's Auto Radar",
                    IconUrl = _dataService.Guild.IconUrl
                },
                Title = "Click Here",
                Url = "https://github.com/Terri00/CS-GO-Auto-Radar/blob/tavr/README.md",
                ThumbnailUrl =
                    "https://camo.githubusercontent.com/a13fe7791b6752a3b8db0e89cd758f07966a198b/68747470733a2f2f692e696d6775722e636f6d2f6a4e57554c56302e706e67",
                Color = new Color(50, 50, 50),
                Description = "Automatically make a radar with every compile of a map you do. Specify the layout in " +
                              "hammer by adding brushes to a visgroup named 'tar_layout', and Auto Radar will do the rest."
            };

            await ReplyAsync(string.Empty, false, embed.Build());
        }

        [Command("BlenderSourceTools")]
        [Summary("Provides a download link to the latest version of Blender Source Tools.")]
        [Alias("bst")]
        public async Task BstAsync()
        {
            var embed = new EmbedBuilder
            {
                Author = new EmbedAuthorBuilder
                {
                    Name = "Download Blender Source Tools",
                    IconUrl = _client.Guilds.FirstOrDefault()?.IconUrl
                },
                Title = "Click Here",
                Url = "https://github.com/Artfunkel/BlenderSourceTools",
                ThumbnailUrl =
                    "https://www.blendswap.com/files/images/2017/07/23/Blend/89002/dffa3371bf3bd129701856cef1d37ed1.jpg",
                Color = new Color(50, 50, 50),
                Description =
                    "Blender Source Tools allow Blender to import and export Studiomdl Data and DMX model files,  " +
                    "as well as automatically generating .qc files."
            };

            await ReplyAsync(string.Empty, false, embed.Build());
        }

        [Command("Log")]
        [Summary("Provides a link to the compile log checker on Interlopers.")]
        [Alias("l")]
        public async Task LogCheckerAsync()
        {
            var embed = new EmbedBuilder
            {
                Author = new EmbedAuthorBuilder
                {
                    Name = "Interlopers Compile Log Checker",
                    IconUrl = _dataService.Guild.IconUrl
                },
                Title = "Click Here",
                Url = "http://www.interlopers.net/errors",
                ThumbnailUrl = "https://www.tophattwaffle.com/wp-content/uploads/2017/12/selectall.jpg",
                Color = new Color(84, 137, 71),
                Description =
                    "The compile log checker on Interlopers is a web tool which analyzes compile logs of maps to " +
                    "detect compilation issues and propose solutions. Simply copy and paste an entire log or a " +
                    "section with an error and click the button to have the log checked."
            };

            await ReplyAsync(string.Empty, false, embed.Build());
        }
    }
}
