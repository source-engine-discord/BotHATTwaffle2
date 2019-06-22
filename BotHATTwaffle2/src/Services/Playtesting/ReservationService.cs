using System;
using System.Linq;
using System.Threading.Tasks;
using BotHATTwaffle2.Handlers;
using BotHATTwaffle2.Models.LiteDB;
using BotHATTwaffle2.Services.Calendar;
using BotHATTwaffle2.src.Handlers;
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

        private async Task ClearAllServerReservations()
        {
            //Get all active reservations
            var allReservations = DatabaseHandler.GetAllServerReservation();
            
            foreach (var reservation in allReservations)
            {
                Console.WriteLine("RUNNING\nRUNNING\nRUNNING\nRUNNING\nRUNNING\nRUNNING\nRUNNING\nRUNNING\nRUNNING\nRUNNING\nRUNNING\n");
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

        public Embed BuildServerReleaseEmbed(string user, ServerReservation reservation, string reason = null)
        {
            var server = DatabaseHandler.GetTestServerFromReservationUserId(reservation.UserId);

            var embed = new EmbedBuilder()
                .WithAuthor($"{user}'s reservation ended on {server.Address}")
                .WithColor(new Color(255, 100, 0))
                .WithDescription($"Your reservation on {server.Address} has ended because: `{reason}`" +
                                 $"\n*Thanks for testing with us!*");

            return embed.Build();
        }

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
            DatabaseHandler.RemoveServerReservation(userId);
            return embed;
        }

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
