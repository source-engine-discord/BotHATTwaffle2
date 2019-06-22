using System;
using System.IO;
using System.Threading.Tasks;
using BotHATTwaffle2.Services;
using BotHATTwaffle2.Services.Playtesting;
using Discord;
using Discord.WebSocket;
using FluentScheduler;

namespace BotHATTwaffle2.Handlers
{
    internal class ScheduleHandler
    {
        private const ConsoleColor LOG_COLOR = ConsoleColor.DarkMagenta;
        private readonly DiscordSocketClient _client;
        private readonly DataService _data;
        private readonly LogHandler _log;
        private readonly PlaytestService _playtestService;
        private readonly UserHandler _userHandler;
        private readonly Random _random;
        private readonly ReservationService _reservationService;
        private int _playtestCount = 0;

        public ScheduleHandler(DataService data, DiscordSocketClient client, LogHandler log, PlaytestService playtestService
        , UserHandler userHandler, Random random, ReservationService reservationService)
        {
            Console.WriteLine("Setting up ScheduleHandler...");
            _playtestService = playtestService;
            _log = log;
            _data = data;
            _client = client;
            _userHandler = userHandler;
            _random = random;
            _reservationService = reservationService;

            //Fluent Scheduler init and events
            JobManager.Initialize(new Registry());

            JobManager.JobStart += FluentJobStart;
            JobManager.JobEnd += FluentJobEnd;
            JobManager.JobException += FluentJobException;
        }

        public void RemoveAllJobs()
        {
            _ = _log.LogMessage("Removing all scheduled jobs", false, color: ConsoleColor.Red);
            JobManager.RemoveAllJobs();
        }

        /// <summary>
        /// Adds required jobs on startup
        /// </summary>
        public void AddRequiredJobs()
        {
            _ = _log.LogMessage("Adding required scheduled jobs...", false, color: LOG_COLOR);

            //Add schedule for playtest information
            JobManager.AddJob(async () => await _playtestService.PostOrUpdateAnnouncement(), s => s
                .WithName("[PostOrUpdateAnnouncement]").ToRunEvery(60).Seconds());

            //Reattach to the old announcement message quickly
            JobManager.AddJob(async () => await _playtestService.TryAttachPreviousAnnounceMessage(), s => s
                .WithName("[TryAttachPreviousAnnounceMessage]").ToRunOnceIn(3).Seconds());

            //On start up schedule of playtest announcements
            JobManager.AddJob(() => _playtestService.SchedulePlaytestAnnouncements(), s => s
                .WithName("[SchedulePlaytestAnnouncementsBoot]").ToRunOnceIn(6).Seconds());

            //Add schedule for playing information
            JobManager.AddJob(async () => await UpdatePlaying(), s => s
                .WithName("[PlayingUpdate]").ToRunEvery(20).Seconds());

            //Add schedule for playtest count update, will do every few hours, and now to seed the value.
            JobManager.AddJob(UpdatePlayTestCount, s => s
                .WithName("[PlayingUpdate]").ToRunEvery(2).Hours());
            JobManager.AddJob(UpdatePlayTestCount, s => s
                .WithName("[PlayingUpdate]").ToRunNow());

            //Re-add joined users so they get welcome message and playtester role.
            //This would only happen if the bot restarts after someone joins, but didn't get the welcome message.
            foreach (var user in DatabaseHandler.GetAllUserJoins())
            {
                try
                {
                    //Test getting user in a try catch, if we can't they left the server.
                    var validUser = _data.Guild.GetUser(user.UserId);

                    //Send welcome message right away, or wait?
                    if (DateTime.Now > user.JoinTime.AddMinutes(10))
                    {
                        //Timer expired, schedule now
                        JobManager.AddJob(async () => await _userHandler.UserWelcomeMessage(validUser), s => s
                            .WithName($"[UserJoin_{validUser.Id}]").ToRunOnceIn(10).Seconds());
                    }
                    else
                    {
                        //Not passed, scheduled ahead
                        JobManager.AddJob(async () => await _userHandler.UserWelcomeMessage(validUser), s => s
                            .WithName($"[UserJoin_{validUser.Id}]").ToRunOnceAt(user.JoinTime.AddMinutes(10)));
                    }
                }
                catch
                {
                    //If we cannot get a user, that means that user left the server. So remove them.
                    if (_data.RSettings.ProgramSettings.Debug)
                        _ = _log.LogMessage($"Cannot re-add user join for ID {user.UserId}" +
                                            $"because they left the server.", false, color: LOG_COLOR);

                    DatabaseHandler.RemoveJoinedUser(user.UserId);
                }
            }

            //Re-add user mutes
            foreach (var user in DatabaseHandler.GetAllActiveUserMutes())
            {

                //Send welcome message right away, or wait?
                if (DateTime.Now > user.MuteTime.AddMinutes(user.Duration))
                {
                    //Timer expired, schedule now
                    JobManager.AddJob(async () => await _data.UnmuteUser(user.UserId), s => s
                        .WithName($"[UnmuteUser_{user.UserId}]").ToRunOnceIn(20).Seconds());
                }
                else
                {
                    //Not passed, scheduled ahead
                    JobManager.AddJob(async () => await _data.UnmuteUser(user.UserId), s => s
                        .WithName($"[UnmuteUser_{user.UserId}]").ToRunOnceAt(user.MuteTime.AddMinutes(user.Duration)));
                }
            }

            //Re-add user mutes
            foreach (var user in DatabaseHandler.GetAllActiveUserMutes())
            {

                //Send welcome message right away, or wait?
                if (DateTime.Now > user.MuteTime.AddMinutes(user.Duration))
                {
                    //Timer expired, schedule now
                    JobManager.AddJob(async () => await _data.UnmuteUser(user.UserId), s => s
                        .WithName($"[UnmuteUser_{user.UserId}]").ToRunOnceIn(20).Seconds());
                }
                else
                {
                    //Not passed, scheduled ahead
                    JobManager.AddJob(async () => await _data.UnmuteUser(user.UserId), s => s
                        .WithName($"[UnmuteUser_{user.UserId}]").ToRunOnceAt(user.MuteTime.AddMinutes(user.Duration)));
                }
            }

            //Re-add user mutes
            foreach (var reservation in DatabaseHandler.GetAllServerReservation())
            {
                string mention = null;

                try
                {
                    mention = _data.Guild.GetUser(reservation.UserId).Mention;
                }
                catch
                {
                    //Can't get user don't do a mention
                }

                //Send welcome message right away, or wait?
                if (DateTime.Now > reservation.StartTime.AddHours(2))
                {
                    //Timer expired, schedule now
                    JobManager.AddJob(async () => await _data.TestingChannel.SendMessageAsync($"{mention}",
                            embed: _reservationService.ReleaseServer(reservation.UserId, "The reservation has expired.")),
                        s => s.WithName($"[TSRelease_{_data.GetServerCode(reservation.ServerId)}_{reservation.UserId}]").ToRunOnceIn(15).Seconds());
                }
                else
                {
                    //Not passed, scheduled ahead
                    JobManager.AddJob(async () => await _data.TestingChannel.SendMessageAsync($"{mention}",
                            embed: _reservationService.ReleaseServer(reservation.UserId, "The reservation has expired.")),
                        s => s.WithName($"[TSRelease_{_data.GetServerCode(reservation.ServerId)}_{reservation.UserId}]").ToRunOnceAt(reservation.StartTime.AddHours(2)));
                }
            }

            //Display what jobs we have scheduled
            foreach (var allSchedule in JobManager.AllSchedules)
            {
                _ = _log.LogMessage($"{allSchedule.Name} runs at: {allSchedule.NextRun}", 
                    false, color: LOG_COLOR);
            }
        }

        private void FluentJobStart(JobStartInfo info)
        {
            if (_data.RSettings.ProgramSettings.Debug)
                _ = _log.LogMessage($"FLUENT JOB STARTED:{info.Name}", false, color: LOG_COLOR);
        }

        private void FluentJobEnd(JobEndInfo info)
        {
            if (_data.RSettings.ProgramSettings.Debug)
                _ = _log.LogMessage($"FLUENT JOB ENDED:{info.Name}", false, color: LOG_COLOR);
        }

        private void FluentJobException(JobExceptionInfo info)
        {
            if (_data.RSettings.ProgramSettings.Debug)
                _ = _log.LogMessage($"FLUENT JOB EXCEPTION:\n{info.Exception}", false, color: LOG_COLOR);
        }

        /// <summary>
        /// Updated the playing line on the bot
        /// </summary>
        /// <returns></returns>
        private async Task UpdatePlaying()
        {
            string playing = _data.RSettings.Lists.Playing[_random.Next(_data.RSettings.Lists.Playing.Count)];

            if (playing == "[TestCount]")
                playing = $"{_playtestCount} Playtests Run";

            await _client.SetGameAsync(playing);
        }

        /// <summary>
        /// Updates the number of playtest files found on the local machine.
        /// </summary>
        private void UpdatePlayTestCount()
        {
            try
            {
                _playtestCount = Directory.GetFiles(_data.RSettings.ProgramSettings.PlaytestDemoPath, "*.dem", SearchOption.AllDirectories).Length;
            }
            catch (Exception e)
            {
                _ = _log.LogMessage($"Cannot access path for getting playtest count\n{e.Message}",channel:false);
                throw;
            }

            if (_data.RSettings.ProgramSettings.Debug)
                _ = _log.LogMessage($"{_playtestService} playtest files found!", false, color: LOG_COLOR);
        }
    }
}