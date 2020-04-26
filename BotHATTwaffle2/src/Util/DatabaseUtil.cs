using System;
using System.Collections.Generic;
using BotHATTwaffle2.Handlers;
using BotHATTwaffle2.Models.LiteDB;
using BotHATTwaffle2.Services;
using BotHATTwaffle2.Services.Calendar.PlaytestEvents;
using Discord;
using LiteDB;

namespace BotHATTwaffle2.Util
{
    internal static class DatabaseUtil
    {
        private const string DBPATH = @"MasterDB.db";
        private const string COLLECTION_ANNOUNCEMENT = "announcement";
        private const string COLLECTION_SERVERS = "servers";
        private const string COLLECTION_USER_JOIN = "userJoin";
        private const string COLLECTION_PLAYTEST_COMMAND = "ptCommandInfo";
        private const string COLLECTION_MUTES = "mutes";
        private const string COLLECTION_RESERVATIONS = "serverReservations";
        private const string COLLECTION_PTREQUESTS = "playtestRequests";
        private const string COLLECTION_COMP = "compPw";
        private const string COLLECTION_USERS_STEAMID = "usersSteamID";
        private const string COLLECTION_PREVIOUS_TEST = "previousTest";
        private const string COLLECTION_FACEIT_HUBS = "faceitHubs";
        private const string COLLECTION_FEEDBACK_FILE = "feedbackFile";
        private const string COLLECTION_TOOLS = "tools";
        private const ConsoleColor LOG_COLOR = ConsoleColor.Yellow;
        private static LogHandler _log;
        private static DataService _dataService;

        public static void SetHandlers(LogHandler log, DataService data)
        {
            _dataService = data;
            _log = log;
        }

        /// <summary>
        ///     Gets the stored CompPw from the DB.
        /// </summary>
        /// <returns>Found CompPw or null</returns>
        public static PreviousTest GetPreviousTest()
        {
            PreviousTest found = null;
            try
            {
                using (var db = new LiteDatabase(DBPATH))
                {
                    //Grab our collection
                    var col = db.GetCollection<PreviousTest>(COLLECTION_PREVIOUS_TEST);

                    found = col.FindOne(Query.EQ("_id", 1));

                    _ = _log.LogMessage($"Previous playtest event of {found.Title}");
                }
            }
            catch (Exception e)
            {
                _ = _log.LogMessage("Something happened getting PreviousTest\n" +
                                    $"{e}", false, color: ConsoleColor.Red);
                return found;
            }

            return found;
        }

        /// <summary>
        ///     Stores a competitive playtest password
        /// </summary>
        /// <param name="playtestEvent">Playtest event to store info</param>
        /// <returns>True if successful, false otherwise</returns>
        public static bool StorePreviousTest(PreviousTest previousTest)
        {
            try
            {
                using (var db = new LiteDatabase(DBPATH))
                {
                    //Grab our collection
                    var col = db.GetCollection<PreviousTest>(COLLECTION_PREVIOUS_TEST);

                    var found = col.FindOne(Query.EQ("_id", 1));

                    //If not null, we need to remove the old record first.
                    if (found != null)
                    {
                        if (_dataService.RSettings.ProgramSettings.Debug)
                            _ = _log.LogMessage("Old PreviousTest found, deleting", false, color: LOG_COLOR);

                        col.Delete(1);
                    }

                    if (_dataService.RSettings.ProgramSettings.Debug)
                        _ = _log.LogMessage($"Adding new record for previous test... {previousTest.Title}",
                            false, color: LOG_COLOR);

                    //Insert new entry with ID of 1, and our values.
                    col.Insert(previousTest);
                }
            }
            catch (Exception e)
            {
                _ = _log.LogMessage("Something happened storing PreviousTest\n" +
                                    $"{e}", false, color: ConsoleColor.Red);
                return false;
            }

            return true;
        }

        /// <summary>
        ///     Gets the stored CompPw from the DB.
        /// </summary>
        /// <returns>Found CompPw or null</returns>
        public static CompPw GetCompPw()
        {
            CompPw found = null;
            try
            {
                using (var db = new LiteDatabase(DBPATH))
                {
                    //Grab our collection
                    var col = db.GetCollection<CompPw>(COLLECTION_COMP);

                    found = col.FindOne(Query.EQ("_id", 1));
                }
            }
            catch (Exception e)
            {
                _ = _log.LogMessage("Something happened getting CompPw\n" +
                                    $"{e}", false, color: ConsoleColor.Red);
                return found;
            }

            return found;
        }

        /// <summary>
        ///     Stores a competitive playtest password
        /// </summary>
        /// <param name="playtestEvent">Playtest event to store info</param>
        /// <returns>True if successful, false otherwise</returns>
        public static bool StoreCompPw(PlaytestEvent playtestEvent)
        {
            try
            {
                using (var db = new LiteDatabase(DBPATH))
                {
                    //Grab our collection
                    var col = db.GetCollection<CompPw>(COLLECTION_COMP);

                    var found = col.FindOne(Query.EQ("_id", 1));

                    //If not null, we need to remove the old record first.
                    if (found != null)
                    {
                        if (_dataService.RSettings.ProgramSettings.Debug)
                            _ = _log.LogMessage("Old announcement CompPw found, deleting", false, color: LOG_COLOR);

                        col.Delete(1);
                    }

                    if (_dataService.RSettings.ProgramSettings.Debug)
                        _ = _log.LogMessage("Adding new record..." +
                                            $"\n{(playtestEvent as CsgoPlaytestEvent)?.CompPassword} at for event {playtestEvent.Title}",
                            false, color: LOG_COLOR);

                    //Insert new entry with ID of 1, and our values.
                    col.Insert(new CompPw
                    {
                        Id = 1,
                        Title = playtestEvent.Title,
                        CompPassword = (playtestEvent as CsgoPlaytestEvent)?.CompPassword
                    });
                }
            }
            catch (Exception e)
            {
                _ = _log.LogMessage("Something happened storing CompPw\n" +
                                    $"{e}", false, color: ConsoleColor.Red);
                return false;
            }

            return true;
        }

        /// <summary>
        ///     Get's all the faceit hubs currently in the DB
        /// </summary>
        /// <returns>A collection of the retrieved hubs.</returns>
        public static IEnumerable<FaceItHub> GetHubs()
        {
            try
            {
                using (var db = new LiteDatabase(DBPATH))
                {
                    //Grab our collection
                    var col = db.GetCollection<FaceItHub>(COLLECTION_FACEIT_HUBS);
                    return col.FindAll();
                }
            }
            catch (Exception e)
            {
                _ = _log.LogMessage("An error occurred while getting FACEIT Hubs\n" +
                                    $"{e}", false, color: ConsoleColor.Red);
                return null;
            }
        }

        /// <summary>
        ///     Insert a new FACEIT Hub.
        /// </summary>
        /// <param name="hub">Hub to add</param>
        /// <returns>true if successfully added; otherwise, false.</returns>
        public static bool InsertHub(FaceItHub hub)
        {
            try
            {
                using (var db = new LiteDatabase(DBPATH))
                {
                    //Grab our collection
                    var col = db.GetCollection<FaceItHub>(COLLECTION_FACEIT_HUBS);

                    if (_dataService.RSettings.ProgramSettings.Debug)
                        _ = _log.LogMessage("Storing FACEIT Hub...", false, color: LOG_COLOR);

                    col.Insert(hub);
                }
            }
            catch (Exception e)
            {
                _ = _log.LogMessage("An error occurred while inserting a FACEIT Hub\n" +
                                    $"{e}", false, color: ConsoleColor.Red);
                return false;
            }

            return true;
        }

        /// <summary>
        ///     Delete a FACEIT Hub tag.
        /// </summary>
        /// <param name="id">The id of the tag to delete.</param>
        /// <returns>true if successfully deleted; otherwise, false.</returns>
        public static bool DeleteHub(int id)
        {
            try
            {
                using (var db = new LiteDatabase(DBPATH))
                {
                    //Grab our collection
                    var col = db.GetCollection<FaceItHub>(COLLECTION_FACEIT_HUBS);

                    if (_dataService.RSettings.ProgramSettings.Debug)
                        _ = _log.LogMessage($"Deleting FACEIT Hub for ID {id}", false, color: LOG_COLOR);

                    return col.Delete(id);
                }
            }
            catch (Exception e)
            {
                _ = _log.LogMessage("An error occurred while deleting a FACEIT Hub\n" +
                                    $"{e}", false, color: ConsoleColor.Red);
                return false;
            }
        }

        /// <summary>
        ///     Stores the provided announce message in the database.
        ///     Creates if it does not exist.
        /// </summary>
        /// <param name="message">Message to store</param>
        /// <param name="eventEditTime">The last time the event was edited</param>
        /// <returns>True if successful, false otherwise</returns>
        public static bool StoreAnnouncement(IUserMessage message, string title, string game)
        {
            try
            {
                using (var db = new LiteDatabase(DBPATH))
                {
                    //Grab our collection
                    var announcement = db.GetCollection<AnnounceMessage>(COLLECTION_ANNOUNCEMENT);

                    //remove all old announcements for this game.
                    announcement.Delete(Query.EQ("Game", game));

                    if (_dataService.RSettings.ProgramSettings.Debug)
                        _ = _log.LogMessage("Adding new record..." +
                                            $"\n{message.Id} for {title} for game {game}", false, color: LOG_COLOR);

                    announcement.Insert(new AnnounceMessage
                    {
                        Title = title,
                        AnnouncementId = message.Id,
                        Game = game
                    });
                }
            }
            catch (Exception e)
            {
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
        public static AnnounceMessage GetAnnouncementMessage(PlaytestEvent.Games game)
        {
            AnnounceMessage foundMessage = null;
            try
            {
                using (var db = new LiteDatabase(DBPATH))
                {
                    //Grab our collection
                    var announcement = db.GetCollection<AnnounceMessage>(COLLECTION_ANNOUNCEMENT);

                    foundMessage = announcement.FindOne(Query.EQ("Game", game.ToString()));
                }
            }
            catch (Exception e)
            {
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
                        if (_dataService.RSettings.ProgramSettings.Debug)
                            _ = _log.LogMessage("Old playtest command info record found, deleting", false,
                                color: LOG_COLOR);

                        collection.Delete(1);
                    }

                    //Insert new entry with ID of 1, and our values.
                    collection.Insert(playtestCommandInfo);
                }
            }
            catch (Exception e)
            {
                _ = _log.LogMessage("Something happened storing announcement message\n" +
                                    $"{e}", false, color: ConsoleColor.Red);
                return false;
            }

            return true;
        }

        public static PlaytestCommandInfo GetPlaytestCommandInfo()
        {
            if (_dataService.RSettings.ProgramSettings.Debug)
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
            if (serverId == null)
                return null;

            //If the server ID contains a period, it can be assumed that it is a FQDN, and we should trim it down.
            serverId = GeneralUtil.GetServerCode(serverId);

            Server foundServer = null;
            try
            {
                using (var db = new LiteDatabase(DBPATH))
                {
                    //Grab our collection
                    var servers = db.GetCollection<Server>(COLLECTION_SERVERS);

                    foundServer = servers.FindOne(Query.EQ("ServerId", serverId));
                }

            }
            catch (Exception e)
            {
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
                if (_dataService.RSettings.ProgramSettings.Debug)
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
            }
            catch (Exception e)
            {
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
                _ = _log.LogMessage("Something happened getting all test servers\n" +
                                    $"{e}", false, color: ConsoleColor.Red);
                return foundServers;
            }

            return foundServers;
        }

        /// <summary>
        ///     Returns a IEnumerable of server objects containing all test servers with the specified game in the database.
        /// </summary>
        /// <returns>IEnumerable of servers</returns>
        public static IEnumerable<Server> GetAllTestServers(string game)
        {
            IEnumerable<Server> foundServers = null;
            try
            {
                using (var db = new LiteDatabase(DBPATH))
                {
                    //Grab our collection
                    var serverCol = db.GetCollection<Server>(COLLECTION_SERVERS);

                    foundServers = serverCol.Find(x => x.Game.Contains(game.ToLower()));
                }
            }
            catch (Exception e)
            {
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
                if (_dataService.RSettings.ProgramSettings.Debug)
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

                    servers.Insert(server);

                    if (_dataService.RSettings.ProgramSettings.Debug)
                        _ = _log.LogMessage("Inserting new server into DB", false, color: LOG_COLOR);
                }
            }
            catch (Exception e)
            {
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

                    if (_dataService.RSettings.ProgramSettings.Debug)
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

                    if (_dataService.RSettings.ProgramSettings.Debug)
                        _ = _log.LogMessage("Deleting new user join from DB", false, color: LOG_COLOR);

                    //Have to cast the user ID to a long
                    userJoins.Delete(Query.EQ("UserId", (long) userId));
                }
            }
            catch (Exception e)
            {
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
                _ = _log.LogMessage("Something happened getting all user joins\n" +
                                    $"{e}", false, color: ConsoleColor.Red);
                return foundUsers;
            }

            return foundUsers;
        }

        /// <summary>
        ///     Gets a single active mute for a user. There should only ever be 1 at a time.
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

                    foundMute = collection.FindOne(Query.And(Query.EQ("UserId", (long) userId),
                        Query.EQ("Expired", false)));
                }

                if (_dataService.RSettings.ProgramSettings.Debug && foundMute != null)
                    _ = _log.LogMessage(foundMute.ToString(), false, color: LOG_COLOR);
            }
            catch (Exception e)
            {
                _ = _log.LogMessage("Something happened getting test server\n" +
                                    $"{e}", false, color: ConsoleColor.Red);
                return foundMute;
            }

            return foundMute;
        }

        /// <summary>
        ///     Adds a mute to the database
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

                    if (_dataService.RSettings.ProgramSettings.Debug)
                        _ = _log.LogMessage("Inserting new user mute into DB", false, color: LOG_COLOR);

                    collection.Insert(userMute);
                }

                return true;
            }
            catch (Exception e)
            {
                _ = _log.LogMessage("Something happened adding user join\n" +
                                    $"{e}", false, color: ConsoleColor.Red);
            }

            return false;
        }

        /// <summary>
        ///     Unmutes a user in the database based on userId
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

                    if (_dataService.RSettings.ProgramSettings.Debug)
                        _ = _log.LogMessage("Unmuting user from DB", false, color: LOG_COLOR);

                    user.Expired = true;

                    collection.Update(user);
                }

                return true;
            }
            catch (Exception e)
            {
                _ = _log.LogMessage("Something happened adding user join\n" +
                                    $"{e}", false, color: ConsoleColor.Red);
            }

            return false;
        }

        /// <summary>
        ///     Gets all currently active mutes on server.
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
                _ = _log.LogMessage("Something happened getting all user mutes\n" +
                                    $"{e}", false, color: ConsoleColor.Red);
                return foundUsers;
            }

            return foundUsers;
        }

        /// <summary>
        ///     Gets all mutes based on a specific user ID
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

                    foundMutes = collection.Find(Query.EQ("UserId", (long) userId));
                }
            }
            catch (Exception e)
            {
                _ = _log.LogMessage($"Something happened getting all mutes for user ID {userId}\n" +
                                    $"{e}", false, color: ConsoleColor.Red);
                return foundMutes;
            }

            return foundMutes;
        }

        /// <summary>
        ///     Adds a new server reservations
        /// </summary>
        /// <param name="serverReservation">Server reservation to add</param>
        /// <returns>True if reservation could be added, false otherwise</returns>
        public static bool AddServerReservation(ServerReservation serverReservation)
        {
            //Need to check if a valid reservation exists first as a safety check
            if (GetServerReservation(serverReservation.ServerId) != null)
                return false;

            //That server ID does not exist
            if (GetTestServer(serverReservation.ServerId) == null)
                return false;

            try
            {
                using (var db = new LiteDatabase(DBPATH))
                {
                    var collection = db.GetCollection<ServerReservation>(COLLECTION_RESERVATIONS);

                    collection.EnsureIndex(x => x.UserId);

                    if (_dataService.RSettings.ProgramSettings.Debug)
                        _ = _log.LogMessage("Inserting new server reservation into DB", false, color: LOG_COLOR);

                    collection.Insert(serverReservation);
                }

                return true;
            }
            catch (Exception e)
            {
                _ = _log.LogMessage("Something happened adding server reservations\n" +
                                    $"{e}", false, color: ConsoleColor.Red);
            }

            return false;
        }

        /// <summary>
        ///     Gets a server reservation
        /// </summary>
        /// <param name="server">server ID to get reservation for</param>
        /// <returns>ServerReservation object if found, null otherwiser</returns>
        public static ServerReservation GetServerReservation(string server)
        {
            ServerReservation serverReservation = null;
            try
            {
                using (var db = new LiteDatabase(DBPATH))
                {
                    //Grab our collection
                    var collection = db.GetCollection<ServerReservation>(COLLECTION_RESERVATIONS);

                    serverReservation = collection.FindOne(x => x.ServerId == GeneralUtil.GetServerCode(server));
                }

                if (_dataService.RSettings.ProgramSettings.Debug && serverReservation != null)
                    _ = _log.LogMessage(serverReservation.ToString(), false, color: LOG_COLOR);
            }
            catch (Exception e)
            {
                _ = _log.LogMessage("Something happened getting server reservations\n" +
                                    $"{e}", false, color: ConsoleColor.Red);
                return serverReservation;
            }

            return serverReservation;
        }

        public static ServerReservation GetServerReservation(ulong userId)
        {
            ServerReservation serverReservation = null;
            try
            {
                using (var db = new LiteDatabase(DBPATH))
                {
                    //Grab our collection
                    var collection = db.GetCollection<ServerReservation>(COLLECTION_RESERVATIONS);

                    serverReservation = collection.FindOne(Query.EQ("UserId", (long) userId));
                }

                if (_dataService.RSettings.ProgramSettings.Debug && serverReservation != null)
                    _ = _log.LogMessage(serverReservation.ToString(), false, color: LOG_COLOR);
            }
            catch (Exception e)
            {
                _ = _log.LogMessage("Something happened getting server reservations\n" +
                                    $"{e}", false, color: ConsoleColor.Red);
                return serverReservation;
            }

            return serverReservation;
        }

        /// <summary>
        ///     Gets server object on an active reservation based on a user ID
        /// </summary>
        /// <param name="userId">UserID to locate server from</param>
        /// <returns>Server object if reservation found, null otherwise</returns>
        public static Server GetTestServerFromReservationUserId(ulong userId)
        {
            Server foundServer = null;
            try
            {
                using (var db = new LiteDatabase(DBPATH))
                {
                    //Grab our collection
                    var collection = db.GetCollection<ServerReservation>(COLLECTION_RESERVATIONS);

                    var serverReservation = collection.FindOne(Query.EQ("UserId", (long) userId));

                    if (serverReservation != null) foundServer = GetTestServer(serverReservation.ServerId);
                }
            }
            catch (Exception e)
            {
                _ = _log.LogMessage("Something happened getting test server reservations\n" +
                                    $"{e}", false, color: ConsoleColor.Red);
                return foundServer;
            }

            return foundServer;
        }

        /// <summary>
        ///     Gets all active server reservations
        /// </summary>
        /// <returns>IEnumerable of ServerReservation objects of active reservations, or null if none.</returns>
        public static IEnumerable<ServerReservation> GetAllServerReservation()
        {
            IEnumerable<ServerReservation> serverReservations = null;
            try
            {
                using (var db = new LiteDatabase(DBPATH))
                {
                    //Grab our collection
                    var collection = db.GetCollection<ServerReservation>(COLLECTION_RESERVATIONS);

                    serverReservations = collection.FindAll();
                }
            }
            catch (Exception e)
            {
                _ = _log.LogMessage("Something happened getting all server reservations\n" +
                                    $"{e}", false, color: ConsoleColor.Red);
                return serverReservations;
            }

            return serverReservations;
        }

        /// <summary>
        ///     Removes all server reservations by dropping the collection
        /// </summary>
        /// <returns>True if successful, false otherwise</returns>
        public static bool RemoveAllServerReservations()
        {
            try
            {
                using (var db = new LiteDatabase(DBPATH))
                {
                    return db.DropCollection(COLLECTION_RESERVATIONS);
                }
            }
            catch (Exception e)
            {
                _ = _log.LogMessage("Something happened getting all server reservations\n" +
                                    $"{e}", false, color: ConsoleColor.Red);
                return false;
            }
        }

        /// <summary>
        ///     Removes a server reservation based on user ID
        /// </summary>
        /// <param name="userId">User ID to remove server reservation</param>
        /// <returns>True if removed, false otherwise</returns>
        public static bool RemoveServerReservation(ulong userId)
        {
            try
            {
                using (var db = new LiteDatabase(DBPATH))
                {
                    var collection = db.GetCollection<ServerReservation>(COLLECTION_RESERVATIONS);
                    var reservation = collection.FindOne(Query.EQ("UserId", (long) userId));

                    if (reservation != null)
                    {
                        collection.Delete(reservation.Id);
                        return true;
                    }
                }
            }
            catch (Exception e)
            {
                _ = _log.LogMessage("Something happened releasing reservations\n" +
                                    $"{e}", false, color: ConsoleColor.Red);
                return false;
            }

            return false;
        }

        /// <summary>
        ///     Changes the announced flag to true on a server reservation based on user ID
        /// </summary>
        /// <param name="userId">User ID of reservation</param>
        /// <returns>True if updated, false otherwise</returns>
        public static bool UpdateAnnouncedServerReservation(ulong userId)
        {
            try
            {
                using (var db = new LiteDatabase(DBPATH))
                {
                    var collection = db.GetCollection<ServerReservation>(COLLECTION_RESERVATIONS);
                    var reservation = collection.FindOne(Query.EQ("UserId", (long) userId));

                    if (reservation != null)
                    {
                        reservation.Announced = true;
                        return collection.Update(reservation);
                    }
                }
            }
            catch (Exception e)
            {
                _ = _log.LogMessage("Something happened releasing reservations\n" +
                                    $"{e}", false, color: ConsoleColor.Red);
                return false;
            }

            return false;
        }

        /// <summary>
        ///     Adds a playtest request to the database
        /// </summary>
        /// <param name="playtestRequest">PlaytestRequest to add</param>
        /// <returns>True if added, false otherwise</returns>
        public static bool AddPlaytestRequests(PlaytestRequest playtestRequest)
        {
            try
            {
                using (var db = new LiteDatabase(DBPATH))
                {
                    //Grab our collection
                    var collection = db.GetCollection<PlaytestRequest>(COLLECTION_PTREQUESTS);
                    collection.EnsureIndex(x => x.Timestamp);

                    //Stamp the current time on the insert
                    playtestRequest.Timestamp = DateTime.Now;

                    collection.Insert(playtestRequest);
                }
            }
            catch (Exception e)
            {
                _ = _log.LogMessage("Something happened storing playtest request\n" +
                                    $"{e}", false, color: ConsoleColor.Red);
                return false;
            }

            return true;
        }

        public static bool UpdatePlaytestRequests(PlaytestRequest playtestRequest)
        {
            try
            {
                using (var db = new LiteDatabase(DBPATH))
                {
                    //Grab our collection
                    var collection = db.GetCollection<PlaytestRequest>(COLLECTION_PTREQUESTS);

                    collection.Update(playtestRequest);
                }
            }
            catch (Exception e)
            {
                _ = _log.LogMessage("Something happened storing playtest request\n" +
                                    $"{e}", false, color: ConsoleColor.Red);
                return false;
            }

            return true;
        }

        /// <summary>
        ///     Gets all playtest requests from the DB
        /// </summary>
        /// <returns>IEnumerable of PlaytestRequests</returns>
        public static IEnumerable<PlaytestRequest> GetAllPlaytestRequests()
        {
            IEnumerable<PlaytestRequest> foundRequests = null;
            try
            {
                using (var db = new LiteDatabase(DBPATH))
                {
                    //Grab our collection
                    var collection = db.GetCollection<PlaytestRequest>(COLLECTION_PTREQUESTS);

                    foundRequests = collection.FindAll();
                }
            }
            catch (Exception e)
            {
                _ = _log.LogMessage("Something happened getting all playtest requests\n" +
                                    $"{e}", false, color: ConsoleColor.Red);
                return foundRequests;
            }

            return foundRequests;
        }

        /// <summary>
        ///     Removes a PlaytestRequest from the Database
        /// </summary>
        /// <param name="playtestRequest">PlaytestRequest to remove</param>
        /// <returns>True if removed, false otherwise</returns>
        public static bool RemovePlaytestRequest(PlaytestRequest playtestRequest)
        {
            try
            {
                using (var db = new LiteDatabase(DBPATH))
                {
                    //Grab our collection
                    var collection = db.GetCollection<PlaytestRequest>(COLLECTION_PTREQUESTS);

                    collection.Delete(playtestRequest.Id);
                }
            }
            catch (Exception e)
            {
                _ = _log.LogMessage("Something happened getting all playtest requests\n" +
                                    $"{e}", false, color: ConsoleColor.Red);
                return false;
            }

            return true;
        }

        /// <summary>
        ///     Adds a users SteamID to the database
        /// </summary>
        /// <param name="playtestRequest">PlaytestRequest to add</param>
        /// <returns>True if added, false otherwise</returns>
        public static bool AddUserSteamID(UserSteamID input)
        {
            if (GetUserSteamID(input.UserId) != null)
                return false;

            try
            {
                using (var db = new LiteDatabase(DBPATH))
                {
                    //Grab our collection
                    var collection = db.GetCollection<UserSteamID>(COLLECTION_USERS_STEAMID);
                    collection.EnsureIndex(x => x.SteamID);

                    collection.Insert(input);
                }
            }
            catch (Exception e)
            {
                _ = _log.LogMessage("Something happened storing SteamID\n" +
                                    $"{e}", false, color: ConsoleColor.Red);
                return false;
            }

            return true;
        }

        public static bool DeleteUserSteamID(UserSteamID input)
        {
            try
            {
                using (var db = new LiteDatabase(DBPATH))
                {
                    //Grab our collection
                    var collection = db.GetCollection<UserSteamID>(COLLECTION_USERS_STEAMID);

                    collection.Delete(input.Id);
                }
            }
            catch (Exception e)
            {
                _ = _log.LogMessage("Something happened getting all playtest requests\n" +
                                    $"{e}", false, color: ConsoleColor.Red);
                return false;
            }

            return true;
        }

        public static UserSteamID GetUserSteamID(ulong userId = 0, string steamId = null)
        {
            if (userId == 0 && steamId == null)
                return null;

            try
            {
                using (var db = new LiteDatabase(DBPATH))
                {
                    //Grab our collection
                    var collection = db.GetCollection<UserSteamID>(COLLECTION_USERS_STEAMID);

                    if (userId != 0) return collection.FindOne(Query.EQ("UserId", (long) userId));

                    if (steamId != null) return collection.FindOne(Query.EQ("SteamID", steamId));
                }
            }
            catch (Exception e)
            {
                _ = _log.LogMessage("Something happened getting test server reservations\n" +
                                    $"{e}", false, color: ConsoleColor.Red);
                return null;
            }

            return null;
        }

        public static bool AddFeedbackFile(FeedbackFileStore feedbackFileStore)
        {
            try
            {
                using (var db = new LiteDatabase(DBPATH))
                {
                    //Grab our collection
                    var feedBackFile = db.GetCollection<FeedbackFileStore>(COLLECTION_FEEDBACK_FILE);
                    feedBackFile.EnsureIndex(x => x.ServerAddress);

                    feedBackFile.Insert(feedbackFileStore);

                    if (_dataService.RSettings.ProgramSettings.Debug)
                        _ = _log.LogMessage("Inserting new FeedbackFile into DB", false, color: LOG_COLOR);
                }
            }
            catch (Exception e)
            {
                _ = _log.LogMessage("Something happened adding FeedbackFile\n" +
                                    $"{e}", false, color: ConsoleColor.Red);
            }

            return true;
        }

        public static IEnumerable<FeedbackFileStore> GetAllFeedbackFiles()
        {
            IEnumerable<FeedbackFileStore> feedbackFiles = null;
            try
            {
                using (var db = new LiteDatabase(DBPATH))
                {
                    //Grab our collection
                    var feedbackCol = db.GetCollection<FeedbackFileStore>(COLLECTION_FEEDBACK_FILE);

                    feedbackFiles = feedbackCol.FindAll();
                }
            }
            catch (Exception e)
            {
                _ = _log.LogMessage("Something happened getting all test servers\n" +
                                    $"{e}", false, color: ConsoleColor.Red);
                return feedbackFiles;
            }

            return feedbackFiles;
        }

        public static bool RemoveFeedbackFile(string serverId)
        {
            var foundServer = GetTestServer(serverId);

            if (foundServer == null)
            {
                if (_dataService.RSettings.ProgramSettings.Debug)
                    _ = _log.LogMessage("No server found, so cannot remove anything", false, color: LOG_COLOR);
                return false;
            }

            try
            {
                using (var db = new LiteDatabase(DBPATH))
                {
                    //Grab our collection
                    var feedbackFile = db.GetCollection<FeedbackFileStore>(COLLECTION_FEEDBACK_FILE);

                    feedbackFile.Delete(x => x.ServerAddress == foundServer.Address);
                }

                if (_dataService.RSettings.ProgramSettings.Debug)
                    _ = _log.LogMessage(foundServer.ToString(), false, color: LOG_COLOR);
            }
            catch (Exception e)
            {
                _ = _log.LogMessage("Something happened removing FeedbackFile\n" +
                                    $"{e}", false, color: ConsoleColor.Red);
                return false;
            }

            return true;
        }

        public static bool AddTool(Tool tool)
        {
            try
            {
                using (var db = new LiteDatabase(DBPATH))
                {
                    //Grab our collection
                    var toolCol = db.GetCollection<Tool>(COLLECTION_TOOLS);
                    toolCol.EnsureIndex(x => x.Command);

                    //Make sure we don't have a tool with the same command already.
                    var foundTool = toolCol.FindOne(x => x.Command == tool.Command);
                    if (foundTool != null)
                    {
                        _ = _log.LogMessage($"Cannot insert {tool.Command} as I already " +
                                            $"have a tool with that command.", false, color: LOG_COLOR);
                        return false;
                    }

                    toolCol.Insert(tool);

                    if (_dataService.RSettings.ProgramSettings.Debug)
                        _ = _log.LogMessage("Inserting new Tool into DB", false, color: LOG_COLOR);
                }
            }
            catch (Exception e)
            {
                _ = _log.LogMessage("Something happened adding Tool\n" +
                                    $"{e}", false, color: ConsoleColor.Red);
            }

            return true;
        }

        public static IEnumerable<Tool> GetAllTools()
        {
            IEnumerable<Tool> tools = null;
            try
            {
                using (var db = new LiteDatabase(DBPATH))
                {
                    //Grab our collection
                    var toolCol = db.GetCollection<Tool>(COLLECTION_TOOLS);
                    tools = toolCol.FindAll();
                }
            }
            catch (Exception e)
            {
                _ = _log.LogMessage("Something happened getting all tools\n" +
                                    $"{e}", false, color: ConsoleColor.Red);
                return tools;
            }

            return tools;
        }

        public static bool RemoveTool(string command)
        {
            try
            {
                using (var db = new LiteDatabase(DBPATH))
                {
                    //Grab our collection
                    var toolsCol = db.GetCollection<Tool>(COLLECTION_TOOLS);

                    var foundTool = toolsCol.FindOne(x => x.Command == command);

                    if (foundTool == null)
                    {
                        if (_dataService.RSettings.ProgramSettings.Debug)
                            _ = _log.LogMessage("No tool found, so cannot remove it.", false, color: LOG_COLOR);
                        return false;
                    }

                    toolsCol.Delete(x => x.Command == foundTool.Command);

                    if (_dataService.RSettings.ProgramSettings.Debug)
                        _ = _log.LogMessage(foundTool + " removed from the DB.", false, color: LOG_COLOR);
                }
            }
            catch (Exception e)
            {
                _ = _log.LogMessage("Something happened removing Tool\n" +
                                    $"{e}", false, color: ConsoleColor.Red);
                return false;
            }

            return true;
        }
    }
}