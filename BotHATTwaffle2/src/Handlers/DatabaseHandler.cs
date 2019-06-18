using System;
using System.Collections.Generic;
using System.Linq;
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
        private const string CollectionServers = "servers";
        private const ConsoleColor LogColor = ConsoleColor.DarkYellow;
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
                        if (_data.RSettings.ProgramSettings.Debug)
                            _ = _log.LogMessage("Old record found, deleting", false, color: LogColor);

                        announcement.Delete(1);
                    }

                    if (_data.RSettings.ProgramSettings.Debug)
                        _ = _log.LogMessage("Adding new record..." +
                                            $"\n{message.Id} at {eventEditTime}", false, color: LogColor);

                    //Insert new entry with ID of 1, and our values.
                    announcement.Insert(new AnnounceMessage
                    {
                        Id = 1,
                        AnnouncementDateTime = eventEditTime,
                        AnnouncementId = message.Id
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

        /// <summary>
        /// Gets a specific test server from the database based on the ID.
        /// </summary>
        /// <param name="serverId">Server ID to get</param>
        /// <returns>Server object if found, null otherwise</returns>
        public static Server GetTestServer(string serverId)
        {
            Server foundServer = null;
            try
            {
                using (var db = new LiteDatabase(Dbpath))
                {
                    //Grab our collection
                    var servers = db.GetCollection<Server>(CollectionServers);

                    foundServer = servers.FindOne(Query.EQ("ServerId",serverId));
                }
                if (_data.RSettings.ProgramSettings.Debug && foundServer != null)
                    _ = _log.LogMessage(foundServer.ToString(), false, color: LogColor);
            }
            catch (Exception e)
            {
                //TODO: Don't actually know what exceptions can happen here, catch all for now.
                _ = _log.LogMessage("Something happened getting announcement message\n" +
                                    $"{e}", false, color: ConsoleColor.Red);
                return foundServer;
            }

            return foundServer;
        }

        /// <summary>
        /// Removes a server object from the database based on the ID.
        /// </summary>
        /// <param name="serverId">Server ID to remove</param>
        /// <returns>True if the server was removed, false otherwise.</returns>
        public static bool RemoveTestServer(string serverId)
        {
            var foundServer = GetTestServer(serverId);

            if (foundServer == null)
            {
                if (_data.RSettings.ProgramSettings.Debug)
                    _ = _log.LogMessage("No server found, so cannot remove anything", false, color: LogColor);
                return false;
            }

            try
            {
                using (var db = new LiteDatabase(Dbpath))
                {
                    //Grab our collection
                    var servers = db.GetCollection<Server>(CollectionServers);

                    servers.Delete(foundServer.Id);
                }
                if (_data.RSettings.ProgramSettings.Debug)
                    _ = _log.LogMessage(foundServer.ToString(), false, color: LogColor);
            }
            catch (Exception e)
            {
                //TODO: Don't actually know what exceptions can happen here, catch all for now.
                _ = _log.LogMessage("Something happened getting announcement message\n" +
                                    $"{e}", false, color: ConsoleColor.Red);
                return false;
            }

            return true;
        }

        /// <summary>
        /// Returns a IEnumerable of server objects containing all test servers in the database.
        /// </summary>
        /// <returns>IEnumerable of servers</returns>
        public static IEnumerable<Server> GetAllTestServers()
        {
            IEnumerable<Server> foundServers = null;
            try
            {
                using (var db = new LiteDatabase(Dbpath))
                {
                    //Grab our collection
                    var serverCol = db.GetCollection<Server>(CollectionServers);

                    foundServers = serverCol.FindAll();
                }
            }
            catch (Exception e)
            {
                //TODO: Don't actually know what exceptions can happen here, catch all for now.
                _ = _log.LogMessage("Something happened getting announcement message\n" +
                                    $"{e}", false, color: ConsoleColor.Red);
                return foundServers;
            }
            return foundServers;
        }

        /// <summary>
        /// Adds a server object to the database.
        /// </summary>
        /// <param name="server">Server to add to the database</param>
        /// <returns>True if server was added, false otherwise</returns>
        public static bool AddTestServer(Server server)
        {
            if (GetTestServer(server.ServerId) != null)
            {
                if (_data.RSettings.ProgramSettings.Debug)
                    _ = _log.LogMessage("Unable to add test server since one was found.", false, color: LogColor);
                //We found an entry under the same name as this server.
                return false;
            }

            try
            {
                using (var db = new LiteDatabase(Dbpath))
                {
                    
                    //Grab our collection
                    var servers = db.GetCollection<Server>(CollectionServers);

                    if (_data.RSettings.ProgramSettings.Debug)
                        _ = _log.LogMessage("Inserting new server into DB", false, color: LogColor);
                    servers.Insert(server);
                    servers.EnsureIndex(x => x.ServerId);
                }
            }
            catch (Exception e)
            {
                //TODO: Don't actually know what exceptions can happen here, catch all for now.
                _ = _log.LogMessage("Something happened getting announcement message\n" +
                                    $"{e}", false, color: ConsoleColor.Red);
            }

            return true;
        }
    }
}