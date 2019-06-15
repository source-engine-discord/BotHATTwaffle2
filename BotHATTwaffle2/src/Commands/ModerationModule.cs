using System;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using BotHATTwaffle2.Services;
using BotHATTwaffle2.Services.Calendar;
using BotHATTwaffle2.Services.Playtesting;
using BotHATTwaffle2.src.Handlers;
using BotHATTwaffle2.src.Models.LiteDB;
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
        private const ConsoleColor LogColor = ConsoleColor.DarkRed;

        public ModerationModule(DataService data, DiscordSocketClient client, LogHandler log, GoogleCalendar calendar,
            PlaytestService playtestService)
        {
            _playtestService = playtestService;
            _calendar = calendar;
            _data = data;
            _client = client;
            _log = log;
        }

        [Command("Kick")]
        [Summary("Kicks a user.")]
        [RequireUserPermission(GuildPermission
            .ManageChannels)] //Require Moderator Role, no reason for custom attribute.
        public async Task KickAsync(SocketGuildUser user, [Remainder] string reason = "No reason provided.")
        {
            try
            {
                await user.SendMessageAsync($"You have been kicked from {_client.Guilds.FirstOrDefault()?.Name} " +
                                            $"for: {reason}. Please take a few minutes to cool off before rejoining.");
            }
            catch (Exception) //User cannot be DM'd
            {
                await _log.LogMessage($"Attempted to DM {user.Nickname} about them being kicked for" +
                                      $"{reason}, but they don't allow DMs.");
            }

            await user.KickAsync(reason);
            await _log.LogMessage($"{user.Username} (ID: {user.Id}) has been kicked by " +
                                  $"{Context.User.Username} (ID: {Context.User.Id})");
        }

        [Command("test")]
        public async Task TestAsync()
        {

        }

        [Command("rcon", RunMode = RunMode.Async)]
        [Alias("r")]
        [RequireUserPermission(GuildPermission.KickMembers)]
        public async Task RconAsync(string input, [Remainder] string command)
        {
            await ReplyAsync($"```{await _data.RconCommand(input, command)}```");
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
                    FtpType = serverValues[7]
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

                await ReplyAsync("Server information contains passwords, as a result I have DM'd whoever is set " +
                                 "as the `Alert User` in my configuration.");
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
                    $"Current debug status is: `{_data.RootSettings.ProgramSettings.Debug}`");
            }
            else if (status.StartsWith("t", StringComparison.OrdinalIgnoreCase))
            {
                _data.RootSettings.ProgramSettings.Debug = true;
                await _data.UpdateRolesAndUsers();
                await Context.Channel.SendMessageAsync(
                    $"Changed debug status to: `{_data.RootSettings.ProgramSettings.Debug}`");
            }
            else if (status.StartsWith("f", StringComparison.OrdinalIgnoreCase))
            {
                _data.RootSettings.ProgramSettings.Debug = false;
                await _data.UpdateRolesAndUsers();
                await Context.Channel.SendMessageAsync(
                    $"Changed debug status to: `{_data.RootSettings.ProgramSettings.Debug}`");
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