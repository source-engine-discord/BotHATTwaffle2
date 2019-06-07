using System;
using System.Collections.Generic;
using System.Text;
using Discord;

namespace BotHATTwaffle2.src.Services.Calendar
{
    public class PlaytestEvent
    {
        public bool IsValid { get; private set; }
        public DateTime? EventEditTime { get; set; } //What is the last time the event was edited?
        public DateTime? StartDateTime { get; set; }
        public string Title { get; set; }
        public List<IGuildUser> Creators;
        public Uri ImageGallery;
        public Uri WorkshopLink;
        public bool IsCasual;
        public IGuildUser Moderator;
        public string Description;
        public string ServerLocation { get; set; }

        public PlaytestEvent()
        {
            Creators = new List<IGuildUser>();
            VoidEvent();
        }

        //Voids event
        public void VoidEvent()
        {
            IsValid = false;
            EventEditTime = null;
            StartDateTime = null;
            Title = null;
            Creators.Clear();
            Creators.TrimExcess();
            ImageGallery = null;
            WorkshopLink = null;
            IsCasual = true;
            Moderator = null;
            Description = null;
            ServerLocation = null;
        }

        public string ToString()
        {
            return "\neventValid: " + IsValid
                                   + "\neventEditTime: " + EventEditTime
                                   + "\ndateTime: " + StartDateTime
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
