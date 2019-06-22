using System;
using System.Collections.Generic;
using System.Linq;
using BotHATTwaffle2.Handlers;
using Discord.WebSocket;

namespace BotHATTwaffle2.Services.Calendar
{
    public class PlaytestEvent
    {
        private const ConsoleColor LOG_COLOR = ConsoleColor.Magenta;
        private readonly DataService _data;
        private readonly LogHandler _log;

        public bool IsCasual;

        public PlaytestEvent(DataService data, LogHandler log)
        {
            _log = log;
            _data = data;
            Creators = new List<SocketUser>();
            GalleryImages = new List<string>();
            VoidEvent();
        }

        public bool IsValid { get; private set; }
        public DateTime? EventEditTime { get; set; } //What is the last time the event was edited?
        public DateTime? StartDateTime { get; set; }
        public DateTime? EndDateTime { get; set; }
        public string Title { get; set; }
        public List<SocketUser> Creators { get; set; }
        public Uri ImageGallery { get; set; }
        public Uri WorkshopLink { get; set; }
        public SocketUser Moderator { get; set; }
        public string Description { get; set; }
        public string ServerLocation { get; set; }
        public List<string> GalleryImages { get; set; }
        public bool CanUseGallery { get; private set; }
        public DateTime? LastEditTime { get; set; }
        public string CompPassword { get; set; }
        public string CompCasualServer { get; set; }

        public void SetGameMode(string input)
        {
            if (input.Contains("comp", StringComparison.OrdinalIgnoreCase))
            {
                IsCasual = false;
                var i = new Random().Next(_data.RSettings.General.CompPasswords.Length);
                CompPassword = _data.RSettings.General.CompPasswords[i];

                _ = _log.LogMessage($"Competitive password for `{Title}` is: `{CompPassword}`");
            }
            else
            {
                IsCasual = true;
            }
        }

        /// <summary>
        /// Finds a free server to load the comp level onto
        /// </summary>
        private void SetCompCasualServer()
        {
            var destServer = DatabaseHandler.GetTestServer(_data.GetServerCode(ServerLocation));
            var allServers = DatabaseHandler.GetAllTestServers();

            //If for some reason the DB does not contain servers, abort.
            if (destServer == null || allServers == null)
                return;

            while (true)
            {
                var selectedServer = allServers.ToArray()[new Random().Next(allServers.Count())];
                if (destServer.Id != selectedServer.Id)
                {
                    CompCasualServer = selectedServer.Address;

                    if (_data.RSettings.ProgramSettings.Debug)
                        _ = _log.LogMessage($"Casual server to mirror competitive is {CompCasualServer}", false, color: LOG_COLOR);

                    return;
                }
            }
        }

        /// <summary>
        ///     Checks the required values on the test event to see if it can be used
        /// </summary>
        /// <returns>True if valid, false otherwise.</returns>
        public bool TestValid()
        {
            if (EventEditTime != null && StartDateTime != null && EndDateTime != null && Title != null &&
                Creators.Count > 0 && ImageGallery != null && WorkshopLink != null && Moderator != null &&
                Description != null && ServerLocation != null)
            {
                //Can we use the gallery images?
                if (GalleryImages.Count > 1)
                {
                    CanUseGallery = true;

                    if (_data.RSettings.ProgramSettings.Debug)
                        _ = _log.LogMessage("Can use image gallery for test event", false, color: LOG_COLOR);
                }

                if (_data.RSettings.ProgramSettings.Debug)
                    _ = _log.LogMessage($"Test event is valid!\n{ToString()}", false, color: LOG_COLOR);

                SetCompCasualServer();

                IsValid = true;

                return true;
            }

            if (_data.RSettings.ProgramSettings.Debug)
                _ = _log.LogMessage($"Test even is not valid!\n{ToString()}", false, color: LOG_COLOR);

            IsValid = false;

            return false;
        }

        /// <summary>
        ///     Essentially resets this object for next use. Could dispose and make a new one
        ///     but where is the fun in that?
        /// </summary>
        public void VoidEvent()
        {
            if (_data.RSettings.ProgramSettings.Debug)
                _ = _log.LogMessage("Voiding test event", false, color: LOG_COLOR);

            IsValid = false;
            CanUseGallery = false;
            EventEditTime = null;
            StartDateTime = null;
            EndDateTime = null;
            Title = null;
            Creators.Clear();
            Creators.TrimExcess();
            GalleryImages.Clear();
            GalleryImages.TrimExcess();
            ImageGallery = null;
            WorkshopLink = null;
            IsCasual = true;
            Moderator = null;
            Description = null;
            ServerLocation = null;
            CompPassword = null;
            CompCasualServer = null;
        }

        public override string ToString()
        {
            return "eventValid: " + IsValid
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
                                  + "\ncreators: " + string.Join(", ", Creators)
                                  + "\ncompPassword: " + CompPassword;
        }
    }
}