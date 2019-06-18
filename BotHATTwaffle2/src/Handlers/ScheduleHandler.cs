using System;
using BotHATTwaffle2.Services;
using BotHATTwaffle2.Services.Playtesting;
using BotHATTwaffle2.src.Handlers;
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

        public ScheduleHandler(DataService data, DiscordSocketClient client, LogHandler log, PlaytestService playtestService
        , UserHandler userHandler)
        {
            Console.WriteLine("Setting up ScheduleHandler...");
            _playtestService = playtestService;
            _log = log;
            _data = data;
            _client = client;
            _userHandler = userHandler;

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
    }
}