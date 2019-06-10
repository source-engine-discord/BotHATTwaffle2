using System;
using System.Linq;
using System.Threading.Tasks;
using BotHATTwaffle2.Services;
using BotHATTwaffle2.src.Handlers;
using BotHATTwaffle2.src.Services.Calendar;
using BotHATTwaffle2.src.Services.Playtesting;
using Discord;
using Discord.Commands;
using Discord.WebSocket;
using FluentScheduler;

namespace BotHATTwaffle2.src.Commands
{
    public class ModerationModule : ModuleBase<SocketCommandContext>
    {
        private readonly GoogleCalendar _calendar;
        private readonly DiscordSocketClient _client;
        private readonly DataService _data;
        private readonly LogHandler _log;
        private readonly PlaytestService _playtestService;

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

        [Command("cal")]
        public async Task CalAsync()
        {
            Console.WriteLine("ABOUT TO SCHEDULE");
            JobManager.AddJob(() => Console.WriteLine("I was scheduled!"), s => s.ToRunOnceIn(5).Seconds());
            //await _playtestService.PostAnnouncement();
        }
    }
}