using System;
using BotHATTwaffle2.Models.LiteDB;
using Google.Apis.Calendar.v3.Data;

namespace BotHATTwaffle2.src.Services.Calendar
{
    public class Playtest
    {
        public enum TypeOfTest
        {
            Scheduled,
            Requested
        }

        public Playtest(int testType, string testName, DateTime startTime)
        {
            TestType = testType;
            TestName = testName;
            StartTime = startTime;
        }

        public Playtest(Event googleCalendarEvent)
        {
            TestType = (int) TypeOfTest.Scheduled;
            TestName = googleCalendarEvent.Summary;
            StartTime = googleCalendarEvent.Start.DateTime.Value;
        }

        public Playtest(PlaytestRequest playtestRequest)
        {
            TestType = (int) TypeOfTest.Requested;
            TestName = playtestRequest.MapName;
            StartTime = playtestRequest.TestDate;
        }

        public int TestType { get; }
        public string TestName { get; }
        public DateTime StartTime { get; }

        public override string ToString()
        {
            return $"{nameof(TestType)}: {TestType}\n{nameof(TestName)}: {TestName}\n{nameof(StartTime)}: {StartTime}";
        }
    }
}