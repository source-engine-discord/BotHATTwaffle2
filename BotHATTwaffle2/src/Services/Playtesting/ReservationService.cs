using System;
using System.Linq;
using System.Threading.Tasks;
using BotHATTwaffle2.Handlers;
using BotHATTwaffle2.Models.LiteDB;
using BotHATTwaffle2.Services.Calendar;
using Discord;
using Discord.WebSocket;
using FluentScheduler;

namespace BotHATTwaffle2.Services.Playtesting
{
    public class ReservationService
    {
        private readonly DataService _data;
        private readonly LogHandler _log;
        private readonly DiscordSocketClient _client;
        public bool CanReserve { get; private set; }

        public ReservationService(DataService data, LogHandler log, Random random, DiscordSocketClient client)
        {
            _data = data;
            _log = log;
            _client = client;

            CanReserve = true;
        }

        /// <summary>
        /// Releases all server reservations.
        /// Alerts users of why their reservations were released.
        /// </summary>
        public async Task ClearAllServerReservations()
        {
            //Get all active reservations
            var allReservations = DatabaseHandler.GetAllServerReservation();
            
            foreach (var reservation in allReservations)
            {
                SocketGuildUser user = null;
                string mention = null;
                string userName = "" + reservation.UserId;
                try
                {
                    user = _data.Guild.GetUser(reservation.UserId);
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

                await _data.TestingChannel.SendMessageAsync(mention, embed: BuildServerReleaseEmbed(userName, reservation,
                    "All server reservations have been cleared. This happens when a scheduled playtest starts soon."));
            }

            //Lastly drop the collection to fully remove all reservations.
            DatabaseHandler.RemoveAllServerReservations();
        }

        /// <summary>
        /// Builds the embed for releasing a server
        /// </summary>
        /// <param name="user">Username of reservation holder</param>
        /// <param name="reservation">Server reservation to build for</param>
        /// <param name="reason">Reason for release</param>
        /// <returns></returns>
        public Embed BuildServerReleaseEmbed(string user, ServerReservation reservation, string reason)
        {
            var server = DatabaseHandler.GetTestServerFromReservationUserId(reservation.UserId);

            var embed = new EmbedBuilder()
                .WithAuthor($"{user}'s reservation ended on {server.Address}")
                .WithColor(new Color(255, 100, 0))
                .WithDescription($"Your reservation on {server.Address} has ended because: `{reason}`" +
                                 $"\n*Thanks for testing with us!*");

            return embed.Build();
        }

        /// <summary>
        /// Releases a server reservation
        /// </summary>
        /// <param name="userId">userId of server to release</param>
        /// <param name="reason">Reason for release</param>
        /// <returns>A prebuilt embed message containing the reason</returns>
        public Embed ReleaseServer(ulong userId, string reason)
        {
            var reservation = DatabaseHandler.GetServerReservation(userId);
            string userName = "" + reservation.UserId;
            try
            {
                userName = _data.Guild.GetUser(reservation.UserId).ToString();
            }
            catch
            {
                //Can't get user, they likely left.
            }
            var embed = BuildServerReleaseEmbed(userName, reservation, reason);

            //Find the job that is a reservation, and has the user ID
            var job = JobManager.AllSchedules.SingleOrDefault(x => x.Name.Contains($"{userId}") && x.Name.StartsWith("[TSRelease_"));

            //Remove it if not null
            if (job != null)
                JobManager.RemoveJob(job.Name);

            DatabaseHandler.RemoveServerReservation(userId);
            return embed;
        }

        /// <summary>
        /// Prevents server reservations from being made
        /// Clears any existing when called.
        /// </summary>
        public async Task DisableReservations()
        {
            CanReserve = false;
            await ClearAllServerReservations();
            var jobs = JobManager.AllSchedules.Where(x => x.Name.StartsWith("[TSRelease_"));

            //Clear all jobs that are server releases
            foreach (var job in jobs)
            {
                JobManager.RemoveJob(job.Name);
            }
        }

        public void AllowReservations()
        {
            CanReserve = true;
        }
    }
}
