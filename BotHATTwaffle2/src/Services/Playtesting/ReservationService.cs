using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using BotHATTwaffle2.Handlers;
using BotHATTwaffle2.Models.LiteDB;
using BotHATTwaffle2.Services.SRCDS;
using BotHATTwaffle2.Util;
using Discord;
using Discord.WebSocket;
using FluentScheduler;

namespace BotHATTwaffle2.Services.Playtesting
{
    public class ReservationService
    {
        private readonly DiscordSocketClient _client;
        private readonly DataService _dataService;
        private readonly LogHandler _log;
        private readonly SrcdsLogService _srcdsLogService;

        public ReservationService(DataService data, LogHandler log, Random random, DiscordSocketClient client, SrcdsLogService srcdsLogService)
        {
            _srcdsLogService = srcdsLogService;
            _dataService = data;
            _log = log;
            _client = client;

            CanReserve = true;
        }

        public bool CanReserve { get; private set; }

        /// <summary>
        ///     Releases all server reservations.
        ///     Alerts users of why their reservations were released.
        /// </summary>
        public async Task ClearAllServerReservations()
        {
            //Get all active reservations
            var allReservations = DatabaseUtil.GetAllServerReservation();

            foreach (var reservation in allReservations)
            {
                SocketGuildUser user = null;
                string mention = null;
                var userName = "" + reservation.UserId;
                try
                {
                    user = _dataService.Guild.GetUser(reservation.UserId);
                }
                catch
                {
                    //Can't get user, they likely left.
                }

                if (user != null)
                {
                    mention = user.Mention;
                    userName = user.ToString();
                }

                await _dataService.CSGOTestingChannel.SendMessageAsync(mention, embed: BuildServerReleaseEmbed(userName,
                    reservation,
                    "All server reservations have been cleared. This happens when a scheduled playtest starts soon."));
            }

            var jobs = JobManager.AllSchedules.Where(x => x.Name.StartsWith("[TSRelease_"));

            //Clear all jobs that are server releases
            foreach (var job in jobs) JobManager.RemoveJob(job.Name);

            //Lastly drop the collection to fully remove all reservations.
            DatabaseUtil.RemoveAllServerReservations();
        }

        /// <summary>
        ///     Builds the embed for releasing a server
        /// </summary>
        /// <param name="user">Username of reservation holder</param>
        /// <param name="reservation">Server reservation to build for</param>
        /// <param name="reason">Reason for release</param>
        /// <returns></returns>
        public Embed BuildServerReleaseEmbed(string user, ServerReservation reservation, string reason)
        {
            var server = DatabaseUtil.GetTestServerFromReservationUserId(reservation.UserId);

            var embed = new EmbedBuilder()
                .WithAuthor($"{user}'s reservation ended on {server.Address}")
                .WithColor(new Color(255, 100, 0))
                .WithDescription($"Your reservation on {server.Address} has ended because: `{reason}`" +
                                 "\n*Thanks for testing with us!*");

            return embed.Build();
        }

        /// <summary>
        ///     Releases a server reservation
        /// </summary>
        /// <param name="userId">userId of server to release</param>
        /// <param name="reason">Reason for release</param>
        /// <returns>A prebuilt embed message containing the reason</returns>
        public Embed ReleaseServer(ulong userId, string reason)
        {
            var reservation = DatabaseUtil.GetServerReservation(userId);
            var userName = "" + reservation.UserId;
            try
            {
                userName = _dataService.Guild.GetUser(reservation.UserId).ToString();
            }
            catch
            {
                //Can't get user, they likely left.
            }

            var embed = BuildServerReleaseEmbed(userName, reservation, reason);

            //Find the job that is a reservation, and has the user ID
            var job = JobManager.AllSchedules.SingleOrDefault(x =>
                x.Name.Contains($"{userId}") && x.Name.StartsWith("[TSRelease_"));

            //Remove it if not null
            if (job != null)
                JobManager.RemoveJob(job.Name);

            //If there is feedback running on this server, remove it. Also delete the file.
            var server = DatabaseUtil.GetTestServer(reservation.ServerId);

            var fbf = _srcdsLogService.GetFeedbackFile(server);
            if(fbf != null)
            {
                var filePath = fbf.FileName;
                if (File.Exists(filePath))
                    File.Delete(filePath);
            }

             _srcdsLogService.RemoveFeedbackFile(server);
            
            DatabaseUtil.RemoveServerReservation(userId);
            return embed;
        }

        /// <summary>
        ///     Prevents server reservations from being made
        ///     Clears any existing when called.
        /// </summary>
        public async Task DisableReservations()
        {
            CanReserve = false;
            await ClearAllServerReservations();
        }

        public void AllowReservations()
        {
            CanReserve = true;
        }
    }
}