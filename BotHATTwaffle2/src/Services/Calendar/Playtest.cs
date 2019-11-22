using System;

using BotHATTwaffle2.Models.LiteDB;

using Google.Apis.Calendar.v3.Data;

namespace BotHATTwaffle2.Services.Calendar
{
    public class Playtest
    {
        public enum TypeOfTest
        {
            Scheduled,
            Requested
        }

        public Playtest(int testType, string testName, DateTime startTime, string game)
        {
            TestType = testType;
            TestName = testName;
            StartTime = startTime;
            Game = game;
        }

        public Playtest(Event googleCalendarEvent)
        {
            TestType = (int) TypeOfTest.Scheduled;
            TestName = googleCalendarEvent.Summary;
            StartTime = googleCalendarEvent.Start.DateTime.Value;
            Game = TestName.Substring(0, TestName.IndexOf(' '));
        }

        public Playtest(PlaytestRequest playtestRequest)
        {
            TestType = (int) TypeOfTest.Requested;
            TestName = playtestRequest.MapName;
            StartTime = playtestRequest.TestDate;
            Game = playtestRequest.Game;
        }

        public int TestType { get; }
        public string TestName { get; }
        public DateTime StartTime { get; }
        public string Game { get; }

        public override string ToString()
        {
            return $"{nameof(TestType)}: {TestType}\n{nameof(TestName)}: {TestName}\n{nameof(StartTime)}: {StartTime}";
        }
    }
}