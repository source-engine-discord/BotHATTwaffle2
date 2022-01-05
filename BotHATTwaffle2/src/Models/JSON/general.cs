namespace BotHATTwaffle2.Models.JSON
{
    public class General
    {
        public string WelcomeMessage { get; set; }
        public string GeneralChannel { get; set; }
        public string WelcomeChannel { get; set; }
        public string CSGOAnnouncementChannel { get; set; }
        public string TF2AnnouncementChannel { get; set; }
        public string CSGOTestingChannel { get; set; }
        public string TF2TestingChannel { get; set; }
        public string CompetitiveTestingChannel { get; set; }
        public string FallbackTestImageUrl { get; set; }
        public string CasualPassword { get; set; }
        public string[] CompPasswords { get; set; }
        public string CSGOCasualConfig { get; set; }
        public string CSGOCompConfig { get; set; }
        public string TF2Config { get; set; }
        public string PostgameConfig { get; set; }
        public string WebhookChannel { get; set; }
        public string AdminChannel { get; set; }
        public string VoidChannel { get; set; }
        public string BotChannel { get; set; }
        public ulong LevelTestingVoice { get; set; }
        public ulong AfkVoice { get; set; }
        public int FeedbackDuration { get; set; }
        public string AdminBotsChannel { get; set; }
        public string VerificationChannel { get; set; }
        public string VerificationRulesChannel { get; set; }
        public string CsgoPlaytestAdminChannel { get; set; }
    }
}