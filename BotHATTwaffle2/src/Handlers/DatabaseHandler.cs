using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using BotHATTwaffle2.Models.LiteDB;
using BotHATTwaffle2.Services;
using BotHATTwaffle2.src.Handlers;
using Discord;
using LiteDB;

namespace BotHATTwaffle2.Handlers
{
    internal class DatabaseHandler
    {
        private const string DBPATH = @"MasterDB.db";
        private const string COLLECTION_ANNOUNCEMENT = "announcement";
        private const string COLLECTION_SERVERS = "servers";
        private const string COLLECTION_USER_JOIN = "userJoin";
        private const string COLLECTION_PLAYTEST_COMMAND = "ptCommandInfo";
        private const string COLLECTION_MUTES = "mutes";
        private const ConsoleColor LOG_COLOR = ConsoleColor.DarkYellow;
        private static LogHandler _log;
        private static DataService _data;

        public static void SetHandlers(LogHandler log, DataService data)
        {
            _data = data;
            _log = log;
        }

        /// <summary>
        ///     Stores the provided announce message in the database.
        ///     Creates if it does not exist.
        /// </summary>
        /// <param name="message">Message to store</param>
        /// <returns>True if successful, false otherwise</returns>
        public static bool StoreAnnouncement(IUserMessage message, DateTime eventEditTime)
        {
            try
            {
                using (var db = new LiteDatabase(DBPATH))
                {
                    //Grab our collection
                    var announcement = db.GetCollection<AnnounceMessage>(COLLECTION_ANNOUNCEMENT);

                    var foundMessage = announcement.FindOne(Query.EQ("_id", 1));

                    //If not null, we need to remove the old record first.
                    if (foundMessage != null)
                    {
                        if (_data.RSettings.ProgramSettings.Debug)
                            _ = _log.LogMessage("Old announcement record found, deleting", false, color: LOG_COLOR);

                        announcement.Delete(1);
                    }

                    if (_data.RSettings.ProgramSettings.Debug)
                        _ = _log.LogMessage("Adding new record..." +
                                            $"\n{message.Id} at {eventEditTime}", false, color: LOG_COLOR);

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
        ///     Gets the stored announcement message from the DB.
        /// </summary>
        /// <returns>Found announcement message or null</returns>
        public static AnnounceMessage GetAnnouncementMessage()
        {
            AnnounceMessage foundMessage = null;
            try
            {
                using (var db = new LiteDatabase(DBPATH))
                {
                    //Grab our collection
                    var announcement = db.GetCollection<AnnounceMessage>(COLLECTION_ANNOUNCEMENT);

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

        public static bool StorePlaytestCommandInfo(PlaytestCommandInfo playtestCommandInfo)
        {
            try
            {
                using (var db = new LiteDatabase(DBPATH))
                {
                    //Grab our collection
                    var collection = db.GetCollection<PlaytestCommandInfo>(COLLECTION_PLAYTEST_COMMAND);

                    var commandInfo = collection.FindOne(Query.EQ("_id", 1));

                    //If not null, we need to remove the old record first.
                    if (commandInfo != null)
                    {
                        if (_data.RSettings.ProgramSettings.Debug)
                            _ = _log.LogMessage("Old playtest command info record found, deleting", false, color: LOG_COLOR);

                        collection.Delete(1);
                    }

                    //Insert new entry with ID of 1, and our values.
                    collection.Insert(playtestCommandInfo);
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

        public static PlaytestCommandInfo GetPlaytestCommandInfo()
        {
            if (_data.RSettings.ProgramSettings.Debug)
                _ = _log.LogMessage("Getting old playtest command information...", false, color: LOG_COLOR);

            PlaytestCommandInfo commandInfo = null;
            try
            {
                using (var db = new LiteDatabase(DBPATH))
                {
                    //Grab our collection
                    var collection = db.GetCollection<PlaytestCommandInfo>(COLLECTION_PLAYTEST_COMMAND);

                    commandInfo = collection.FindOne(Query.EQ("_id", 1));
                }
            }
            catch (Exception e)
            {
                //TODO: Don't actually know what exceptions can happen here, catch all for now.
                _ = _log.LogMessage("Something happened getting announcement message\n" +
                                    $"{e}", false, color: ConsoleColor.Red);
                return commandInfo;
            }

            return commandInfo;
        }

        /// <summary>
        ///     Gets a specific test server from the database based on the ID.
        /// </summary>
        /// <param name="serverId">Server ID to get</param>
        /// <returns>Server object if found, null otherwise</returns>
        public static Server GetTestServer(string serverId)
        {
            //If the server ID contains a period, it can be assumed that it is a FQDN, and we should trim it down.
            if (serverId.Contains('.'))
            {
                serverId = _data.GetServerCode(serverId);
            }

            Server foundServer = null;
            try
            {
                using (var db = new LiteDatabase(DBPATH))
                {
                    //Grab our collection
                    var servers = db.GetCollection<Server>(COLLECTION_SERVERS);

                    foundServer = servers.FindOne(Query.EQ("ServerId", serverId));
                }

                if (_data.RSettings.ProgramSettings.Debug && foundServer != null)
                    _ = _log.LogMessage(foundServer.ToString(), false, color: LOG_COLOR);
            }
            catch (Exception e)
            {
                //TODO: Don't actually know what exceptions can happen here, catch all for now.
                _ = _log.LogMessage("Something happened getting test server\n" +
                                    $"{e}", false, color: ConsoleColor.Red);
                return foundServer;
            }

            return foundServer;
        }

        /// <summary>
        ///     Removes a server object from the database based on the ID.
        /// </summary>
        /// <param name="serverId">Server ID to remove</param>
        /// <returns>True if the server was removed, false otherwise.</returns>
        public static bool RemoveTestServer(string serverId)
        {
            var foundServer = GetTestServer(serverId);

            if (foundServer == null)
            {
                if (_data.RSettings.ProgramSettings.Debug)
                    _ = _log.LogMessage("No server found, so cannot remove anything", false, color: LOG_COLOR);
                return false;
            }

            try
            {
                using (var db = new LiteDatabase(DBPATH))
                {
                    //Grab our collection
                    var servers = db.GetCollection<Server>(COLLECTION_SERVERS);

                    servers.Delete(foundServer.Id);
                }

                if (_data.RSettings.ProgramSettings.Debug)
                    _ = _log.LogMessage(foundServer.ToString(), false, color: LOG_COLOR);
            }
            catch (Exception e)
            {
                //TODO: Don't actually know what exceptions can happen here, catch all for now.
                _ = _log.LogMessage("Something happened removing test server\n" +
                                    $"{e}", false, color: ConsoleColor.Red);
                return false;
            }

            return true;
        }

        /// <summary>
        ///     Returns a IEnumerable of server objects containing all test servers in the database.
        /// </summary>
        /// <returns>IEnumerable of servers</returns>
        public static IEnumerable<Server> GetAllTestServers()
        {
            IEnumerable<Server> foundServers = null;
            try
            {
                using (var db = new LiteDatabase(DBPATH))
                {
                    //Grab our collection
                    var serverCol = db.GetCollection<Server>(COLLECTION_SERVERS);

                    foundServers = serverCol.FindAll();
                }
            }
            catch (Exception e)
            {
                //TODO: Don't actually know what exceptions can happen here, catch all for now.
                _ = _log.LogMessage("Something happened getting all test servers\n" +
                                    $"{e}", false, color: ConsoleColor.Red);
                return foundServers;
            }

            return foundServers;
        }

        /// <summary>
        ///     Adds a server object to the database.
        /// </summary>
        /// <param name="server">Server to add to the database</param>
        /// <returns>True if server was added, false otherwise</returns>
        public static bool AddTestServer(Server server)
        {
            if (GetTestServer(server.ServerId) != null)
            {
                if (_data.RSettings.ProgramSettings.Debug)
                    _ = _log.LogMessage("Unable to add test server since one was found.", false, color: LOG_COLOR);
                //We found an entry under the same name as this server.
                return false;
            }

            try
            {
                using (var db = new LiteDatabase(DBPATH))
                {
                    //Grab our collection
                    var servers = db.GetCollection<Server>(COLLECTION_SERVERS);
                    servers.EnsureIndex(x => x.ServerId);

                    if (_data.RSettings.ProgramSettings.Debug)
                        _ = _log.LogMessage("Inserting new server into DB", false, color: LOG_COLOR);
                }
            }
            catch (Exception e)
            {
                //TODO: Don't actually know what exceptions can happen here, catch all for now.
                _ = _log.LogMessage("Something happened adding test server\n" +
                                    $"{e}", false, color: ConsoleColor.Red);
            }

            return true;
        }

        /// <summary>
        ///     Adds a user join to the database to be processed once the bot reloads.
        /// </summary>
        /// <param name="userId">User ID to add</param>
        public static void AddJoinedUser(ulong userId)
        {
            try
            {
                using (var db = new LiteDatabase(DBPATH))
                {
                    var userJoins = db.GetCollection<UserJoinMessage>(COLLECTION_USER_JOIN);

                    userJoins.EnsureIndex(x => x.UserId);

                    if (_data.RSettings.ProgramSettings.Debug)
                        _ = _log.LogMessage("Inserting new user join into DB", false, color: LOG_COLOR);

                    userJoins.Insert(new UserJoinMessage
                    {
                        UserId = userId,
                        JoinTime = DateTime.Now
                    });
                }
            }
            catch (Exception e)
            {
                //TODO: Don't actually know what exceptions can happen here, catch all for now.
                _ = _log.LogMessage("Something happened adding user join\n" +
                                    $"{e}", false, color: ConsoleColor.Red);
            }
        }

        /// <summary>
        ///     Removes a user join from the database.
        /// </summary>
        /// <param name="userId">User ID to remove</param>
        public static void RemoveJoinedUser(ulong userId)
        {
            try
            {
                using (var db = new LiteDatabase(DBPATH))
                {
                    var userJoins = db.GetCollection<UserJoinMessage>(COLLECTION_USER_JOIN);
                    userJoins.EnsureIndex(x => x.UserId);

                    if (_data.RSettings.ProgramSettings.Debug)
                        _ = _log.LogMessage("Deleting new user join from DB", false, color: LOG_COLOR);

                    //Have to cast the user ID to a long
                    userJoins.Delete(Query.EQ("UserId", (long) userId));
                }
            }
            catch (Exception e)
            {
                //TODO: Don't actually know what exceptions can happen here, catch all for now.
                _ = _log.LogMessage("Something happened removing user join\n" +
                                    $"{e}", false, color: ConsoleColor.Red);
            }
        }

        /// <summary>
        ///     Gets all user joins from the database, used on restart.
        /// </summary>
        /// <returns></returns>
        public static IEnumerable<UserJoinMessage> GetAllUserJoins()
        {
            IEnumerable<UserJoinMessage> foundUsers = null;
            try
            {
                using (var db = new LiteDatabase(DBPATH))
                {
                    //Grab our collection
                    var userJoins = db.GetCollection<UserJoinMessage>(COLLECTION_USER_JOIN);

                    foundUsers = userJoins.FindAll();
                }
            }
            catch (Exception e)
            {
                //TODO: Don't actually know what exceptions can happen here, catch all for now.
                _ = _log.LogMessage("Something happened getting all user joins\n" +
                                    $"{e}", false, color: ConsoleColor.Red);
                return foundUsers;
            }

            return foundUsers;
        }

        /// <summary>
        /// Gets a single active mute for a user. There should only ever be 1 at a time.
        /// </summary>
        /// <param name="userId">UserId to get active mute for</param>
        /// <returns>Valid Mute object if found, null otherwise</returns>
        public static Mute GetActiveMute(ulong userId)
        {
            Mute foundMute = null;
            try
            {
                using (var db = new LiteDatabase(DBPATH))
                {
                    //Grab our collection
                    var collection = db.GetCollection<Mute>(COLLECTION_MUTES);

                    foundMute = collection.FindOne(Query.And(Query.EQ("UserId", (long)userId), Query.EQ("Expired",false)));
                }

                if (_data.RSettings.ProgramSettings.Debug && foundMute != null)
                    _ = _log.LogMessage(foundMute.ToString(), false, color: LOG_COLOR);
            }
            catch (Exception e)
            {
                //TODO: Don't actually know what exceptions can happen here, catch all for now.
                _ = _log.LogMessage("Something happened getting test server\n" +
                                    $"{e}", false, color: ConsoleColor.Red);
                return foundMute;
            }

            return foundMute;
        }

        /// <summary>
        /// Adds a mute to the database
        /// </summary>
        /// <param name="userMute">Mute object to add</param>
        /// <returns>True if added, false otherwise</returns>
        public static bool AddMute(Mute userMute)
        {
            //We found an active mute, don't add a second mute.
            if (GetActiveMute(userMute.UserId) != null)
                return false;

            try
            {
                using (var db = new LiteDatabase(DBPATH))
                {
                    var collection = db.GetCollection<Mute>(COLLECTION_MUTES);
                    
                    collection.EnsureIndex(x => x.UserId);

                    if (_data.RSettings.ProgramSettings.Debug)
                        _ = _log.LogMessage("Inserting new user mute into DB", false, color: LOG_COLOR);

                    collection.Insert(userMute);
                }

                return true;
            }
            catch (Exception e)
            {
                //TODO: Don't actually know what exceptions can happen here, catch all for now.
                _ = _log.LogMessage("Something happened adding user join\n" +
                                    $"{e}", false, color: ConsoleColor.Red);
            }

            return false;
        }

        /// <summary>
        /// Unmutes a user in the database based on userId
        /// </summary>
        /// <param name="userId">UserId to unmute</param>
        /// <returns>True if user was unmuted, false otherwise</returns>
        public static bool UnmuteUser(ulong userId)
        {
            var user = GetActiveMute(userId);
            if (user == null)
                return false;

            try
            {
                using (var db = new LiteDatabase(DBPATH))
                {
                    var collection = db.GetCollection<Mute>(COLLECTION_MUTES);

                    if (_data.RSettings.ProgramSettings.Debug)
                        _ = _log.LogMessage("Unmuting user from DB", false, color: LOG_COLOR);

                    user.Expired = true;

                    collection.Update(user);
                }

                return true;
            }
            catch (Exception e)
            {
                //TODO: Don't actually know what exceptions can happen here, catch all for now.
                _ = _log.LogMessage("Something happened adding user join\n" +
                                    $"{e}", false, color: ConsoleColor.Red);
            }

            return false;
        }

        /// <summary>
        /// Gets all currently active mutes on server.
        /// </summary>
        /// <returns>IEnumerable list of Mutes</returns>
        public static IEnumerable<Mute> GetAllActiveUserMutes()
        {
            IEnumerable<Mute> foundUsers = null;
            try
            {
                using (var db = new LiteDatabase(DBPATH))
                {
                    //Grab our collection
                    var collection = db.GetCollection<Mute>(COLLECTION_MUTES);

                    foundUsers = collection.Find(x => x.Expired == false);
                }
            }
            catch (Exception e)
            {
                //TODO: Don't actually know what exceptions can happen here, catch all for now.
                _ = _log.LogMessage("Something happened getting all user mutes\n" +
                                    $"{e}", false, color: ConsoleColor.Red);
                return foundUsers;
            }

            return foundUsers;
        }

        /// <summary>
        /// Gets all mutes based on a specific user ID
        /// </summary>
        /// <param name="userId">User ID to get mutes for</param>
        /// <returns>IEnumerable list of Mutes</returns>
        public static IEnumerable<Mute> GetAllUserMutes(ulong userId)
        {
            IEnumerable<Mute> foundMutes = null;
            try
            {
                using (var db = new LiteDatabase(DBPATH))
                {
                    //Grab our collection
                    var collection = db.GetCollection<Mute>(COLLECTION_MUTES);

                    foundMutes = collection.Find(Query.EQ("UserId", (long)userId));
                }
            }
            catch (Exception e)
            {
                //TODO: Don't actually know what exceptions can happen here, catch all for now.
                _ = _log.LogMessage($"Something happened getting all mutes for user ID {userId}\n" +
                                    $"{e}", false, color: ConsoleColor.Red);
                return foundMutes;
            }

            return foundMutes;
        }
    }
}