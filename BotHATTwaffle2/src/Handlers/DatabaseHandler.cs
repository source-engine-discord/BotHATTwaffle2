using System;
using BotHATTwaffle2.Services;
using BotHATTwaffle2.src.Models.LiteDB;
using Discord;
using LiteDB;

namespace BotHATTwaffle2.src.Handlers
{
    internal class DatabaseHandler
    {
        private const string Dbpath = @"MasterDB.db";
        private const string CollectionAnnouncement = "announcement";
        private const ConsoleColor logColor = ConsoleColor.DarkYellow;
        private static LogHandler _log;
        private static DataService _data;

        public static void SetHandlers(LogHandler log, DataService data)
        {
            _data = data;
            _log = log;
        }

        /// <summary>
        /// Stores the provided announce message in the database.
        /// Creates if it does not exist.
        /// </summary>
        /// <param name="message">Message to store</param>
        /// <returns>True if successful, false otherwise</returns>
        public static bool StoreAnnouncement(IUserMessage message, DateTime eventEditTime)
        {
            try
            {
                using (var db = new LiteDatabase(Dbpath))
                {
                    //Grab our collection
                    var announcement = db.GetCollection<AnnounceMessage>(CollectionAnnouncement);

                    var foundMessage = announcement.FindOne(Query.EQ("_id", 1));

                    //If not null, we need to remove the old record first.
                    if (foundMessage != null)
                    {
                        if (_data.RootSettings.program_settings.debug)
                            _ = _log.LogMessage("Old record found, deleting", false, color: logColor);

                        announcement.Delete(1);
                    }

                    if (_data.RootSettings.program_settings.debug)
                        _ = _log.LogMessage("Adding new record..." +
                                            $"\n{message.Id} at {eventEditTime}", false, color: logColor);

                    //Insert new entry with ID of 1, and our values.
                    announcement.Insert(new AnnounceMessage
                    {
                        id = 1,
                        AnnouncementDateTime = eventEditTime,
                        AnnouncementID = message.Id
                    });
                }
            }
            catch (Exception e)
            {
                //TODO: Don't actually know what exceptions can happen here, catch all for now.
                _ = _log.LogMessage("Something happened storing announcement message\n" +
                                    $"{e}", false, color: ConsoleColor.Red);
                return false;
            }
            return true;
        }

        /// <summary>
        /// Gets the stored announcement message from the DB.
        /// </summary>
        /// <returns>Found announcement message or null</returns>
        public static AnnounceMessage GetAnnouncementMessage()
        {
            AnnounceMessage foundMessage = null;
            try
            {
                using (var db = new LiteDatabase(Dbpath))
                {
                    //Grab our collection
                    var announcement = db.GetCollection<AnnounceMessage>(CollectionAnnouncement);

                    foundMessage = announcement.FindOne(Query.EQ("_id", 1));
                }
            }
            catch (Exception e)
            {
                //TODO: Don't actually know what exceptions can happen here, catch all for now.
                _ = _log.LogMessage("Something happened getting announcement message\n" +
                                    $"{e}", false, color: ConsoleColor.Red);
                return foundMessage;
            }

            return foundMessage;
        }
    }
}