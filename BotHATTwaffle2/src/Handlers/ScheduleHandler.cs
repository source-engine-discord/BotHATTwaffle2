﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;
using BotHATTwaffle2.Services;
using BotHATTwaffle2.Services.Calendar;
using BotHATTwaffle2.Services.FaceIt;
using BotHATTwaffle2.Services.Playtesting;
using BotHATTwaffle2.Util;
using Discord;
using Discord.WebSocket;
using FluentScheduler;

namespace BotHATTwaffle2.Handlers
{
    public class ScheduleHandler
    {
        private const ConsoleColor LOG_COLOR = ConsoleColor.DarkYellow;
        private readonly GoogleCalendar _calendar;
        private readonly DiscordSocketClient _client;
        private readonly DataService _dataService;
        private readonly LogHandler _log;
        private readonly PlaytestService _playtestService;
        private readonly Random _random;
        private readonly ReservationService _reservationService;
        private readonly UserHandler _userHandler;
        private bool _allowPlayingCycle = true;
        private int _playtestCount;
        private string _lastBannerPath;
        const string PLAYERBASE_URL = @"https://www.tophattwaffle.com/demos/playerBase/fetchPlayers.php";

        public ScheduleHandler(DataService data, DiscordSocketClient client, LogHandler log,
            PlaytestService playtestService
            , UserHandler userHandler, Random random, ReservationService reservationService, GoogleCalendar calendar)
        {
            Console.WriteLine("Setting up ScheduleHandler...");
            _playtestService = playtestService;
            _log = log;
            _dataService = data;
            _client = client;
            _userHandler = userHandler;
            _random = random;
            _reservationService = reservationService;
            _calendar = calendar;

            //Fluent Scheduler init and events
            JobManager.Initialize(new Registry());

            JobManager.JobStart += FluentJobStart;
            JobManager.JobEnd += FluentJobEnd;
            JobManager.JobException += FluentJobException;
        }

        /// <summary>
        ///     Adds required jobs on startup
        /// </summary>
        public void AddRequiredJobs()
        {
            //Read in the last playtest event from the DB
            _calendar.BootStorePreviousPlaytestEvent();

            _ = _log.LogMessage("Adding required scheduled jobs...", false, color: LOG_COLOR);

            //Ask Google API for new tests every 60 seconds.
            JobManager.AddJob(async () => await _calendar.UpdateTestEventCache(), s => s
                .WithName("[UpdatePlaytestEventCacheNow]").ToRunOnceIn(10).Seconds());

            JobManager.AddJob(async () => await _calendar.UpdateTestEventCache(), s => s
                .WithName("[UpdatePlaytestEventCache]").ToRunEvery(60).Seconds());

            //Delay the announcement updates so the calendar refreshes first.
            Task.Run(() =>
            {
                Thread.Sleep(10000);
                //Add schedule for playtest information
                JobManager.AddJob(async () => await _playtestService.PostOrUpdateAnnouncement("csgo"), s => s
                    .WithName("[PostOrUpdateAnnouncement_CSGO]").ToRunEvery(60).Seconds());


                JobManager.AddJob(async () => await _playtestService.PostOrUpdateAnnouncement("tf2"), s => s
                    .WithName("[PostOrUpdateAnnouncement_TF2]").ToRunEvery(60).Seconds());
            });

            //Early refresh on playtest announcements.
            JobManager.AddJob(async () => await _playtestService.PostOrUpdateAnnouncement("csgo"), s => s
                .WithName("[PostOrUpdateAnnouncementNow_CSGO]").ToRunOnceIn(20).Seconds());

            JobManager.AddJob(async () => await _playtestService.PostOrUpdateAnnouncement("tf2"), s => s
                .WithName("[PostOrUpdateAnnouncementNow_TF2]").ToRunOnceIn(20).Seconds());

            //Reattach to the old announcement message quickly
            JobManager.AddJob(async () => await _playtestService.TryAttachPreviousAnnounceMessage(), s => s
                .WithName("[TryAttachPreviousAnnounceMessage]").ToRunOnceIn(15).Seconds());

            //On start up schedule of playtest announcements
            JobManager.AddJob(() => _playtestService.ScheduleAllPlaytestAnnouncements(), s => s
                .WithName("[SchedulePlaytestAnnouncementsBoot]").ToRunOnceIn(20).Seconds());

            //Add schedule for playing information
            JobManager.AddJob(async () => await UpdatePlaying(), s => s
                .WithName("[PlayingUpdate]").ToRunEvery(20).Seconds());

            //Add schedule for playtest count update, will do every few hours, and now to seed the value.
            JobManager.AddJob(UpdatePlayTestCount, s => s
                .WithName("[PlaytestCountUpdate]").ToRunEvery(2).Hours());
            JobManager.AddJob(UpdatePlayTestCount, s => s
                .WithName("[PlaytestCountUpdateNow]").ToRunNow());

            //Update playerbase
            JobManager.AddJob(async () => await UpdatePlayerbase(), s => s
                .WithName("[PlayerbaseUpdate]").ToRunEvery(1).Days().At(0, 00));

            //Daily Faceit Demo Fetching
            JobManager.AddJob(async () => await DailyDemoRequests(), s => s
                .WithName("[FaceitDemoRequest]").ToRunEvery(1).Days().At(1, 00));
            
            //Banner update
            JobManager.AddJob(async () => await UpdateBanner(), s => s
                .WithName("[BannerUpdate]").ToRunEvery(1).Hours());
            JobManager.AddJob(async () => await UpdateBanner(), s => s
                .WithName("[BannerUpdateNow]").ToRunNow());

            //Daily FTP Test
            JobManager.AddJob(async () => await _playtestService.TestFtpAccess(), s => s
                .WithName("[FTP_Test]").ToRunEvery(1).Days().At(9, 00));

            //Re-add user mutes
            foreach (var user in DatabaseUtil.GetAllActiveUserMutes())
                //Send welcome message right away, or wait?
                if (DateTime.Now > user.MuteTime.AddMinutes(user.Duration))
                    //Timer expired, schedule now
                    JobManager.AddJob(async () => await _dataService.UnmuteUser(user.UserId), s => s
                        .WithName($"[UnmuteUser_{user.UserId}]").ToRunOnceIn(20).Seconds());
                else
                    //Not passed, scheduled ahead
                    JobManager.AddJob(async () => await _dataService.UnmuteUser(user.UserId), s => s
                        .WithName($"[UnmuteUser_{user.UserId}]").ToRunOnceAt(user.MuteTime.AddMinutes(user.Duration)));

            //Re-add server reservations.
            foreach (var reservation in DatabaseUtil.GetAllServerReservation())
            {
                string mention = null;

                try
                {
                    mention = _dataService.Guild.GetUser(reservation.UserId).Mention;
                }
                catch
                {
                    //Can't get user don't do a mention
                }

                //Send welcome message right away, or wait?
                if (DateTime.Now > reservation.StartTime.AddHours(3))
                    //Timer expired, schedule now
                    JobManager.AddJob(async () => await _dataService.BotChannel.SendMessageAsync($"{mention}",
                            embed: _reservationService.ReleaseServer(reservation.UserId,
                                "The reservation has expired.", _dataService.BotChannel)),
                        s => s.WithName(
                                $"[TSRelease_{GeneralUtil.GetServerCode(reservation.ServerId)}_{reservation.UserId}]")
                            .ToRunOnceIn(15).Seconds());
                else
                    //Not passed, scheduled ahead
                    JobManager.AddJob(async () => await _dataService.BotChannel.SendMessageAsync($"{mention}",
                            embed: _reservationService.ReleaseServer(reservation.UserId,
                                "The reservation has expired.", _dataService.BotChannel)),
                        s => s.WithName(
                                $"[TSRelease_{GeneralUtil.GetServerCode(reservation.ServerId)}_{reservation.UserId}]")
                            .ToRunOnceAt(reservation.StartTime.AddHours(3)));
            }

            DisplayScheduledJobs();
        }

        public void DisplayScheduledJobs()
        {
            //Display what jobs we have scheduled
            foreach (var allSchedule in JobManager.AllSchedules)
                _ = _log.LogMessage($"{allSchedule.Name} runs at: {allSchedule.NextRun}",
                    false, color: LOG_COLOR);
        }

        private void FluentJobStart(JobStartInfo info)
        {
            if (_dataService.RSettings.ProgramSettings.Debug)
                _ = _log.LogMessage($"FLUENT JOB STARTED:{info.Name}", false, color: LOG_COLOR);
        }

        private void FluentJobEnd(JobEndInfo info)
        {
            if (_dataService.RSettings.ProgramSettings.Debug)
                _ = _log.LogMessage($"FLUENT JOB ENDED:{info.Name}", false, color: LOG_COLOR);
        }

        private void FluentJobException(JobExceptionInfo info)
        {
            if (_dataService.RSettings.ProgramSettings.Debug)
                _ = _log.LogMessage($"FLUENT JOB EXCEPTION:\n{info.Exception}", false, color: LOG_COLOR);
        }

        private async Task UpdateBanner()
        {
            string targetImage;
            var imageFiles = Directory.GetFiles(_dataService.RSettings.ProgramSettings.BannerPath, "*.*", SearchOption.AllDirectories)
                .Where(s => s.EndsWith(".png") || s.EndsWith(".jpg")).ToArray();

            //If only 1 image, use the one image.
            if (imageFiles.Length == 1)
                targetImage = imageFiles[0];
            //If more than 1 image, bounce between.
            else if (imageFiles.Length > 1 )
                do
                {
                    targetImage = imageFiles[_random.Next(0, imageFiles.Length)];
                } while (_lastBannerPath == targetImage);
            //No images, do nothing
            else
                return;

            await _log.LogMessage($"Applying new server banner!\n{targetImage}",false,color: LOG_COLOR);
            await _dataService.Guild.ModifyAsync(x => x.Banner = new Image(targetImage));
        }

        /// <summary>
        ///     Updated the playing line on the bot
        /// </summary>
        /// <returns></returns>
        private async Task UpdatePlaying()
        {
            //Allow us to turn this off.
            if (!_allowPlayingCycle)
                return;

            var playing =
                _dataService.RSettings.Lists.Playing[_random.Next(_dataService.RSettings.Lists.Playing.Count)];

            switch (playing)
            {
                case "[TestCount]":
                    playing = $"{_playtestCount} Playtests Run";
                    break;
                case "[CommandCount]":
                    playing = $"{_dataService.CommandCount} Commands Run";
                    break;
                case "[RunTime]":
                    playing =
                        $"Up For: {DateTime.Now.Subtract(_dataService.StartTime).ToString("d'd 'h'h 'm'm'").TrimStart(' ', 'd', 'h', 'm', '0')}";
                    break;
                case "[MessageCount]":
                    playing = $"{_dataService.MessageCount} Messages Read";
                    break;
            }

            await _client.SetGameAsync(playing);
        }

        //Holy shit did you really just declare a new class right here just for this shit? Unreal...
        private class TimeoutWebClient : WebClient
        {
            public int Timeout { get; set; }
            protected override WebRequest GetWebRequest(Uri uri)
            {
                WebRequest w = base.GetWebRequest(uri);
                w.Timeout = Timeout * 1000;
                return w;
            }
        }

        private async Task UpdatePlayerbase()
        {
            try
            {
                var webClient = new TimeoutWebClient { Timeout = 120};
                var response = webClient.DownloadString(PLAYERBASE_URL).Trim();

                await _log.LogMessage($"Got the following response when updating playerbase: `{response}`",
                    color: LOG_COLOR);
            }
            catch (Exception e)
            {
                await _log.LogMessage($"Failed to update playbase!\n{e}", false, color: LOG_COLOR);
            }
        }

        private async Task DailyDemoRequests()
        {
            await _log.LogMessage("Starting nightly demo grab from FaceIt!", false, color: LOG_COLOR);
            var fapi = new FaceItApi(_dataService, _log);

            //Asking for the past 7 days, and we check what we already have.
            //The faceit API is kinda garbage and does not always return recent games.
            await fapi.GetDemos(DateTime.Now.AddDays(-7), DateTime.Now);
        }

        public void DisablePlayingUpdate()
        {
            _allowPlayingCycle = false;
        }

        public void EnablePlayingUpdate()
        {
            _allowPlayingCycle = true;
        }

        /// <summary>
        ///     Updates the number of playtest files found on the local machine.
        /// </summary>
        private void UpdatePlayTestCount()
        {
            try
            {
                _playtestCount = Directory.GetFiles(_dataService.RSettings.ProgramSettings.PlaytestDemoPath, "*.dem",
                    SearchOption.AllDirectories).Length;
            }
            catch (Exception e)
            {
                _ = _log.LogMessage($"Cannot access path for getting playtest count\n{e.Message}", false);
                throw;
            }

            if (_dataService.RSettings.ProgramSettings.Debug)
                _ = _log.LogMessage($"{_playtestService} playtest files found!", false, color: LOG_COLOR);
        }
    }
}