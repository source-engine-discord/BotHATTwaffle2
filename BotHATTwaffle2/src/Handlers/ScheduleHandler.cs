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
        private const ConsoleColor LogColor = ConsoleColor.DarkMagenta;
        private readonly DiscordSocketClient _client;
        private readonly DataService _data;
        private readonly LogHandler _log;
        private readonly PlaytestService _playtestService;

        public ScheduleHandler(DataService data, DiscordSocketClient client, LogHandler log, PlaytestService playtestService)
        {
            Console.WriteLine("Setting up ScheduleHandler...");
            _playtestService = playtestService;
            _log = log;
            _data = data;
            _client = client;

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
            _ = _log.LogMessage("Adding required scheduled jobs...", false, color: LogColor);

            //Add schedule for playtest information
            JobManager.AddJob(async () => await _playtestService.PostOrUpdateAnnouncement(), s => s
                .WithName("[PostOrUpdateAnnouncement]").ToRunEvery(10).Seconds());

            //Reattach to the old announcement message quickly
            JobManager.AddJob(async () => await _playtestService.TryAttachPreviousAnnounceMessage(), s => s
                .WithName("[TryAttachPreviousAnnounceMessage]").ToRunOnceIn(3).Seconds());

            //On start up schedule of playtest announcements
            JobManager.AddJob(() => _playtestService.SchedulePlaytestAnnouncements(), s => s
                .WithName("[SchedulePlaytestAnnouncementsBoot]").ToRunOnceIn(5).Seconds());
            
            //Display what jobs we have scheduled
            foreach (var allSchedule in JobManager.AllSchedules)
            {
                _ = _log.LogMessage($"{allSchedule.Name} runs at: {allSchedule.NextRun}", 
                    false, color: LogColor);
            }
        }

        private void FluentJobStart(JobStartInfo info)
        {
            if (_data.RootSettings.ProgramSettings.Debug)
                _ = _log.LogMessage($"FLUENT JOB STARTED:{info.Name}", false, color: LogColor);
        }

        private void FluentJobEnd(JobEndInfo info)
        {
            if (_data.RootSettings.ProgramSettings.Debug)
                _ = _log.LogMessage($"FLUENT JOB ENDED:{info.Name}", false, color: LogColor);
        }

        private void FluentJobException(JobExceptionInfo info)
        {
            if (_data.RootSettings.ProgramSettings.Debug)
                _ = _log.LogMessage($"FLUENT JOB EXCEPTION:\n{info.Exception}", false, color: LogColor);
        }
    }
}