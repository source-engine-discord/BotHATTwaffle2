using System;
using System.Collections.Generic;
using BotHATTwaffle2.Services;
using BotHATTwaffle2.src.Handlers;
using Discord.WebSocket;

namespace BotHATTwaffle2.src.Services.Calendar
{
    public class PlaytestEvent
    {
        private const ConsoleColor logColor = ConsoleColor.Magenta;

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

        public void SetGamemode(string input)
        {
            if (input.Contains("comp", StringComparison.OrdinalIgnoreCase))
                IsCasual = false;

            IsCasual = true;
        }


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

                    if (_data.RootSettings.program_settings.debug)
                        _ = _log.LogMessage("Can use image gallery for test event", false, color: logColor);
                }

                if (_data.RootSettings.program_settings.debug)
                    _ = _log.LogMessage($"Test event is valid!\n{ToString()}", false, color: logColor);

                return true;
            }

            if (_data.RootSettings.program_settings.debug)
                _ = _log.LogMessage($"Test even is not valid!\n{ToString()}", false, color: logColor);

            return false;
        }

        //Voids event
        public void VoidEvent()
        {
            if (_data.RootSettings.program_settings.debug)
                _ = _log.LogMessage("Voiding test event", false, color: logColor);

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
                                  + "\ncreators: " + string.Join(", ", Creators);
        }
    }
}