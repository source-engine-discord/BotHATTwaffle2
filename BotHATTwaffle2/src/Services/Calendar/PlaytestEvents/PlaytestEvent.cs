using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using BotHATTwaffle2.Handlers;
using BotHATTwaffle2.Models.LiteDB;
using BotHATTwaffle2.Services.Playtesting;
using BotHATTwaffle2.Services.SRCDS;
using BotHATTwaffle2.Util;
using Discord;
using Discord.WebSocket;
using FluentScheduler;
using Google.Apis.Calendar.v3.Data;

namespace BotHATTwaffle2.Services.Calendar.PlaytestEvents
{
    public class PlaytestEvent
    {
        public enum Games
        {
            CSGO = 0,
            TF2 = 1
        }

        protected const ConsoleColor LOG_COLOR = ConsoleColor.DarkGray;
        protected const string demoUrl = "http://demos.tophattwaffle.com";
        protected const string demoSiteUrlBase = @"https://www.tophattwaffle.com/demos/?demo=";
        protected readonly DataService _dataService;
        protected readonly LogHandler _log;

        //TODO: Set relevant permissions on variables
        public bool IsCasual;

        public bool PlaytestAlert = true;

        public PlaytestEvent(DataService data, LogHandler log, Event playtestEvent)
        {
            _log = log;
            _dataService = data;
            Creators = new List<SocketUser>();
            GalleryImages = new List<string>();

            //Set the information that requires no additional validation / API calls.
            //These are safe to do each calendar update.

            //Remove prefix from event
            Title = playtestEvent.Summary;
            StartDateTime = playtestEvent.Start.DateTime;
            EventEditTime = playtestEvent.Updated;
            ServerLocation = playtestEvent.Location;
            EndDateTime = playtestEvent.End.DateTime;
            Description = playtestEvent.Description;
            CleanedTitle = Title.Substring(Title.IndexOf('|') + 1).Trim();
        }

        public bool IsValid { get; private set; }
        public DateTime? EventEditTime { get; set; } //What is the last time the event was edited?
        public DateTime? StartDateTime { get; set; }
        public DateTime? EndDateTime { get; set; }
        public string Title { get; set; }
        public string CleanedTitle { get; }
        public List<SocketUser> Creators { get; set; }
        public Uri ImageGallery { get; set; }
        public Uri WorkshopLink { get; set; }
        public SocketUser Moderator { get; set; }
        public string Description { get; set; }
        public string ServerLocation { get; set; }
        public List<string> GalleryImages { get; set; }
        public bool CanUseGallery { get; private set; }
        public Games Game { get; protected set; }
        public PlaytestCommandInfo PlaytestCommandInfo { get; private set; }
        public bool PlaytestCommandRunning { get; set; }
        public SocketTextChannel AnnouncmentChannel { get; protected set; }
        public SocketTextChannel TestingChannel { get; protected set; }
        public IUserMessage AnnouncementMessage { get; private set; }
        public SocketRole TesterRole { get; protected set; }

        public bool Equals(PlaytestEvent playtestEvent)
        {
            if (playtestEvent?.EventEditTime == EventEditTime)
                return true;

            return false;
        }

        private void ParseEvent()
        {
            //Never re-parse a valid object
            if (IsValid)
                return;

            if (_dataService.RSettings.ProgramSettings.Debug)
                _ = _log.LogMessage($"Event description BEFORE stripping:\n{Description}\n", false,
                    color: LOG_COLOR);

            //Replace <br>s with \n for new line, replace &nbsp as well
            var strippedHtml = Description.Replace("<br>", "\n").Replace("&nbsp;", "");

            //Strip out HTML tags
            strippedHtml = Regex.Replace(strippedHtml, "<.*?>", string.Empty);

            if (_dataService.RSettings.ProgramSettings.Debug)
                _ = _log.LogMessage($"Event description AFTER stripping:\n{strippedHtml}\n", false, color: LOG_COLOR);

            // Splits description into lines and keeps only the part after the colon, if one exists.
            var description = strippedHtml.Trim().Split('\n')
                .Select(line => line.Substring(line.IndexOf(':') + 1).Trim())
                .ToImmutableArray();

            //Creators
            Creators = _dataService.GetSocketUsers(description.ElementAtOrDefault(0), ',');

            //Imgur Album
            ImageGallery = GeneralUtil.ValidateUri(description.ElementAtOrDefault(1));

            //Workshop URL
            WorkshopLink = GeneralUtil.ValidateUri(description.ElementAtOrDefault(2));

            if (ImageGallery == null || WorkshopLink == null)
            {
                _ = _log.LogMessage("Issue with Imgur Album or Workshop link when parsing test event.", alert:true);
                return;
            }

            //Game mode
            SetGameMode(description.ElementAtOrDefault(3));

            //Moderator
            Moderator = _dataService.GetSocketUser(description.ElementAtOrDefault(4));

            //Description
            Description = description.ElementAtOrDefault(5);

            //Gallery strings
            GalleryImages = GeneralUtil.GetImgurAlbum(ImageGallery.ToString());
        }

        protected virtual void SetGameMode(string input)
        {
            IsCasual = true;
        }

        /// <summary>
        ///     Checks the required values on the test event to see if it can be used
        /// </summary>
        /// <returns>True if valid, false otherwise.</returns>
        public bool TestValid()
        {
            //Parse the event along with relevant API calls.
            ParseEvent();

            if (Title != null && Creators.Count > 0 && ImageGallery != null && WorkshopLink != null &&
                Moderator != null &&
                Description != null && ServerLocation != null)
            {
                //Can we use the gallery images?
                if (GalleryImages.Count > 1)
                {
                    CanUseGallery = true;

                    if (_dataService.RSettings.ProgramSettings.Debug)
                        _ = _log.LogMessage("Can use image gallery for test event", false, color: LOG_COLOR);
                }

                if (_dataService.RSettings.ProgramSettings.Debug)
                    _ = _log.LogMessage($"Test event is valid!\n{ToString()}", false, color: LOG_COLOR);

                IsValid = true;
                return true;
            }

            _ = _log.LogMessage($"Test event is not valid!\n{ToString()}", false, color: LOG_COLOR);

            IsValid = false;
            return false;
        }

        public virtual async Task PlaytestCommandPre(bool replyInContext,
            LogReceiverService logReceiverService, RconService rconService)
        {
            if (_dataService.RSettings.ProgramSettings.Debug)
                _ = _log.LogMessage("Base class PlaytestCommandPre", false, color: LOG_COLOR);

            PlaytestCommandRunning = true;

            await _log.LogMessage("Running Playtest Pre Tasks!", color: LOG_COLOR);

            //Store test information for later use. Will be written to the DB.
            var gameMode = IsCasual ? "casual" : "comp";
            string mentions = null;

            Creators.ForEach(x => mentions += $"{x.Mention} ");
            PlaytestCommandInfo = new PlaytestCommandInfo
            {
                Id = 1, //Only storing 1 of these in the DB at a time, so hard code to 1.
                Mode = gameMode,
                DemoName = $"{StartDateTime:MM_dd_yyyy}" +
                           $"_{CleanedTitle.Substring(0, CleanedTitle.IndexOf(' ')).Trim()}" +
                           $"_{gameMode}",
                WorkshopId = GeneralUtil.GetWorkshopIdFromFqdn(WorkshopLink.ToString()),
                ServerAddress = ServerLocation,
                Title = CleanedTitle,
                ThumbNailImage = CanUseGallery ? GalleryImages[0] : _dataService.RSettings.General.FallbackTestImageUrl,
                ImageAlbum = ImageGallery.ToString(),
                CreatorMentions = mentions,
                StartDateTime = StartDateTime.GetValueOrDefault(),
                Game = Game.ToString()
            };


            //Start receiver if it isn't already
            logReceiverService.StartLogReceiver(PlaytestCommandInfo.ServerAddress);

            //Start feedback capture
            logReceiverService.EnableFeedback(PlaytestCommandInfo.DemoName);

            //Write to the DB so we can restore this info next boot
            DatabaseUtil.StorePlaytestCommandInfo(PlaytestCommandInfo);

            //Figure out where to send the no context message

            //No context to send these messages to - default them
            if (!replyInContext)
                await TestingChannel.SendMessageAsync(embed: new EmbedBuilder()
                    .WithAuthor($"Pre-start playtest of {CleanedTitle}")
                    .WithColor(new Color(55, 55, 165))
                    .WithDescription($"\nOn **{PlaytestCommandInfo.ServerAddress}**" +
                                     $"\nWith config of **{PlaytestCommandInfo.Mode}**" +
                                     $"\nWorkshop ID **{PlaytestCommandInfo.WorkshopId}**").Build());
        }

        public virtual async Task PlaytestCommandStart(bool replyInContext, RconService rconService)
        {
            if (_dataService.RSettings.ProgramSettings.Debug)
                _ = _log.LogMessage("Base class PlaytestCommandStart", false, color: LOG_COLOR);

            PlaytestCommandRunning = true;

            await _log.LogMessage("Running Playtest Start Tasks!", color: LOG_COLOR);

            //No context to send these messages to - default them
            if (!replyInContext)
                await TestingChannel.SendMessageAsync(embed: new EmbedBuilder()
                    .WithAuthor($"Start playtest of {PlaytestCommandInfo.Title}")
                    .WithColor(new Color(55, 55, 165))
                    .WithDescription($"\nOn **{PlaytestCommandInfo.ServerAddress}**" +
                                     $"\nWith config of **{PlaytestCommandInfo.Mode}**" +
                                     $"\nWorkshop ID **{PlaytestCommandInfo.WorkshopId}**" +
                                     $"\nDemo Name **{PlaytestCommandInfo.DemoName}**").Build());
            _ = Task.Run(async () =>
            {
                await Task.Delay(25000);
                var patreonUsers = _dataService.PatreonsRole.Members.ToArray();
                GeneralUtil.Shuffle(patreonUsers);
                var thanks = "";
                foreach (var patreonsRoleMember in patreonUsers) thanks += $"{patreonsRoleMember.Username}, ";

                await rconService.RconCommand(PlaytestCommandInfo.ServerAddress,
                    $"say Thanks to these supporters: {thanks.TrimEnd(',', ' ')}");
                await Task.Delay(2000);
                await rconService.RconCommand(PlaytestCommandInfo.ServerAddress,
                    @"Say Become a supporter at www.patreon.com/tophattwaffle");
            });
        }

        public virtual async Task PlaytestCommandPost(bool replyInContext, LogReceiverService logReceiverService,
            RconService rconService)
        {
            if (_dataService.RSettings.ProgramSettings.Debug)
                _ = _log.LogMessage("Base class PlaytestCommandPost", false, color: LOG_COLOR);

            PlaytestCommandRunning = true;

            await _log.LogMessage("Running Playtest Post Tasks!", color: LOG_COLOR);
            //No context to send these messages to - default them
            if (!replyInContext)
                await _dataService.CSGOTestingChannel.SendMessageAsync(embed: new EmbedBuilder()
                    .WithAuthor($"Post playtest of {PlaytestCommandInfo.Title}")
                    .WithColor(new Color(55, 55, 165))
                    .WithDescription($"\nOn **{PlaytestCommandInfo.ServerAddress}**" +
                                     $"\nWorkshop ID **{PlaytestCommandInfo.WorkshopId}**" +
                                     $"\nDemo Name **{PlaytestCommandInfo.DemoName}**").Build());

            if (File.Exists(logReceiverService.GetFilePath()))
            {
                Directory.CreateDirectory(
                    $"{_dataService.RSettings.ProgramSettings.PlaytestDemoPath}\\{PlaytestCommandInfo.StartDateTime:yyyy}" +
                    $"\\{PlaytestCommandInfo.StartDateTime:MM} - {PlaytestCommandInfo.StartDateTime:MMMM}" +
                    $"\\{PlaytestCommandInfo.DemoName}");

                File.Copy(logReceiverService.GetFilePath(),
                    $"{_dataService.RSettings.ProgramSettings.PlaytestDemoPath}\\{PlaytestCommandInfo.StartDateTime:yyyy}" +
                    $"\\{PlaytestCommandInfo.StartDateTime:MM} - {PlaytestCommandInfo.StartDateTime:MMMM}" +
                    $"\\{PlaytestCommandInfo.DemoName}\\{PlaytestCommandInfo.DemoName}.txt"
                    , true);

                await AnnouncmentChannel.SendFileAsync(logReceiverService.GetFilePath(), "");
            }

            _ = Task.Run(async () =>
            {
                await Task.Delay(35000);
                var patreonUsers = _dataService.PatreonsRole.Members.ToArray();
                GeneralUtil.Shuffle(patreonUsers);
                var thanks = "";
                foreach (var patreonsRoleMember in patreonUsers) thanks += $"{patreonsRoleMember.Username}, ";

                await rconService.RconCommand(PlaytestCommandInfo.ServerAddress,
                    $"say Thanks to these supporters: {thanks.TrimEnd(',', ' ')}");
                await Task.Delay(2000);
                await rconService.RconCommand(PlaytestCommandInfo.ServerAddress,
                    @"Say Become a supporter at www.patreon.com/tophattwaffle");
            });

            //Stop getting more feedback
            logReceiverService.DisableFeedback();
            logReceiverService.SetNotActive();
        }

        public async Task PlaytestcommandGenericAction(bool replyInContext, string command, RconService rconService,
            string message = null)
        {
            if (!replyInContext)
                await _dataService.CSGOTestingChannel.SendMessageAsync(embed: new EmbedBuilder()
                    .WithAuthor(message)
                    .WithColor(new Color(55, 55, 165))
                    .Build());

            await rconService.RconCommand(PlaytestCommandInfo.ServerAddress, command);

            //Reset the flag as we are done running
            PlaytestCommandRunning = false;
        }

        public bool PlaytestCommandPreCheck()
        {
            //Stop executing if we are already running a command
            if (PlaytestCommandRunning) return false;

            //Make sure we have a valid event, if not, abort.
            if (!IsValid) return false;

            //Reload the last used playtest if the current event is null
            if (PlaytestCommandInfo == null)
                PlaytestCommandInfo = DatabaseUtil.GetPlaytestCommandInfo();

            //We are now running a command
            PlaytestCommandRunning = true;

            return true;
        }

        public void SetAnnouncementMessage(IUserMessage message)
        {
            AnnouncementMessage = message;
        }

        public virtual async Task PlaytestStartingInTask(RconService rconService, LogReceiverService logReceiverService,
            AnnouncementMessage announcementMessage)
        {
            if (_dataService.RSettings.ProgramSettings.Debug)
                _ = _log.LogMessage("Base class PlaytestStartingInTask", false, color: LOG_COLOR);

            //Ensure server is awake and RCON connection is established. Run other things while waking server
            _ = rconService.WakeRconServer(ServerLocation);

            //Start asking the server for player counts.
            _dataService.SetIncludePlayerCount(true);
            //Start asking for player counts
            JobManager.AddJob(
                async () => await rconService.GetPlayCountFromServer(ServerLocation),
                s => s.WithName("[QueryPlayerCount]").ToRunEvery(60).Seconds());

            //Figure out how long until the event starts
            var countdown = StartDateTime.GetValueOrDefault().Subtract(DateTime.Now);
            var countdownString =
                countdown.ToString("d'D 'h' Hour 'm' Minutes'").TrimStart(' ', 'D', 'H', 'o', 'u', 'r', '0')
                    .Replace(" 0 Minutes", "");

            await rconService.RconCommand(ServerLocation, "sv_cheats 0");
            var mentionRole = TesterRole;
            //Handle comp or casual
            if (IsCasual)
                await rconService.RconCommand(ServerLocation,
                    $"sv_password {_dataService.RSettings.General.CasualPassword}");
            else
                mentionRole = _dataService.CompetitiveTesterRole;

            //Skip the alert.
            if (!PlaytestAlert)
            {
                PlaytestAlert = true;
                return;
            }

            string unsubInfo = Game.ToString();
            if(!IsCasual)
                unsubInfo = "comp";

            await TesterRole.ModifyAsync(x => { x.Mentionable = true; });
            await TestingChannel.SendMessageAsync($"Heads up {mentionRole.Mention}! " +
                                                  $"There is a playtest starting in {countdownString}." +
                                                  $"\nType `>playtester {unsubInfo}` to stop getting {unsubInfo} playtest notifications.",
                embed: announcementMessage.CreatePlaytestEmbed(this, true, AnnouncementMessage.Id));
            await TesterRole.ModifyAsync(x => { x.Mentionable = false; });

            //DM users about their test
            foreach (var creator in Creators)
                try
                {
                    await creator.SendMessageAsync(
                        $"Don't forget that you have a playtest for __**{CleanedTitle}**__ in __**{countdownString}**__");
                }
                catch
                {
                    //Could not DM creator about their test.
                }
        }

        public virtual async Task PlaytestTwentyMinuteTask(RconService rconService,
            LogReceiverService logReceiverService)
        {
            if (_dataService.RSettings.ProgramSettings.Debug)
                _ = _log.LogMessage("Base class PlaytestTwentyMinuteTask", false, color: LOG_COLOR);

            _dataService.SetIncludePlayerCount(true);

            //Ensure server is awake and RCON connection is established.
            await rconService.WakeRconServer(ServerLocation);

            await _log.LogMessage("Running playtesting starting in 20 minutes task...", true, color: LOG_COLOR);
        }

        public virtual async Task PlaytestFifteenMinuteTask(RconService rconService,
            LogReceiverService logReceiverService)
        {
            if (_dataService.RSettings.ProgramSettings.Debug)
                _ = _log.LogMessage("Base class PlaytestFifteenMinuteTask", false, color: LOG_COLOR);

            _dataService.SetIncludePlayerCount(true);

            //Ensure server is awake and RCON connection is established. Run other things while waking server
            _ = rconService.WakeRconServer(ServerLocation);

            //Setup the log receiver for this test.
            _ = Task.Run(async () =>
            {
                logReceiverService.StopLogReceiver();

                //Log receiver takes time to stop before it can be restarted.
                await Task.Delay(15000);
                logReceiverService.StartLogReceiver(ServerLocation);
            });
        }

        public virtual async Task PlaytestStartingTask(RconService rconService, LogReceiverService logReceiverService,
            AnnouncementMessage announcementMessage)
        {
            if (_dataService.RSettings.ProgramSettings.Debug)
                _ = _log.LogMessage("Base class PlaytestStartingTask", false, color: LOG_COLOR);

            _ = rconService.WakeRconServer(ServerLocation);

            logReceiverService.StartLogReceiver(ServerLocation);

            var mentionRole = TesterRole;
            //Handle comp or casual
            if (IsCasual)
                await rconService.RconCommand(ServerLocation,
                    $"sv_password {_dataService.RSettings.General.CasualPassword}");
            else
                mentionRole = _dataService.CompetitiveTesterRole;


            //Skip the alert.
            if (!PlaytestAlert)
            {
                PlaytestAlert = true;
                return;
            }

            string unsubInfo = Game.ToString();
            if (!IsCasual)
                unsubInfo = "comp";

            await TesterRole.ModifyAsync(x => { x.Mentionable = true; });
            await TestingChannel.SendMessageAsync($"Heads up {mentionRole.Mention}! " +
                                                  "There is a playtest starting __now__!" +
                                                  $"\nType `>playtester {unsubInfo}` to stop getting {unsubInfo} playtest notifications.",
                embed: announcementMessage.CreatePlaytestEmbed(this, true, AnnouncementMessage.Id));
            await TesterRole.ModifyAsync(x => { x.Mentionable = false; });
        }

        public override string ToString()
        {
            return "eventValid: " + IsValid
                                  + "\nGame: " + Game
                                  + "\neventEditTime: " + EventEditTime
                                  + "\ndateTime: " + StartDateTime
                                  + "\nEndDateTime: " + EndDateTime
                                  + "\ntitle: " + Title
                                  + "\nimageGallery: " + ImageGallery
                                  + "\nworkshopLink: " + WorkshopLink
                                  + "\nisCasual: " + IsCasual
                                  + "\nmoderator: " + Moderator
                                  + "\ndescription: " + Description
                                  + "\nserverLocation: " + ServerLocation
                                  + "\ncreators: " + string.Join(", ", Creators);
        }
    }
}