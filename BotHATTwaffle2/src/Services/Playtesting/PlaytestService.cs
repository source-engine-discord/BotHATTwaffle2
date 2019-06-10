using System;
using System.Threading.Tasks;
using BotHATTwaffle2.Services;
using BotHATTwaffle2.src.Handlers;
using BotHATTwaffle2.src.Services.Calendar;
using Discord;

namespace BotHATTwaffle2.src.Services.Playtesting
{
    public class PlaytestService
    {
        private const ConsoleColor logColor = ConsoleColor.DarkYellow;
        private static AnnouncementMessage _announcementMessage;
        private readonly GoogleCalendar _calendar;
        private readonly DataService _data;
        private readonly LogHandler _log;
        private readonly Random _random;

        public PlaytestService(DataService data, GoogleCalendar calendar, LogHandler log, Random random)
        {
            _data = data;
            _log = log;
            _calendar = calendar;
            _random = random;

            _announcementMessage = new AnnouncementMessage(_calendar, _data, _random, _log);
        }

        public IUserMessage PlaytestAnnouncementMessage { get; set; }

        public async Task PostAnnouncement()
        {
            if (_data.RootSettings.program_settings.debug)
                _ = _log.LogMessage("Posting playtesting announcement.", false, color: logColor);

            PlaytestAnnouncementMessage =
                await _data.AnnouncementChannel.SendMessageAsync(embed: _announcementMessage.CreatePlaytestEmbed());
        }
    }
}