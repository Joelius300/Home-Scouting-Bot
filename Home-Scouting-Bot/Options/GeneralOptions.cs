namespace Home_Scouting_Bot.Options
{
    public class GeneralOptions
    {
        public string Token { get; set; } = string.Empty;
        public string Prefix { get; set; } = "!hs.";
        public string ErrorMessage { get; set; } = "Sorry {0}, something went wrong: {1}";
    }
}
