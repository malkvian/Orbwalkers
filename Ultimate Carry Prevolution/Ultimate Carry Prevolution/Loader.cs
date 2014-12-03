using LeagueSharp;

namespace Ultimate_Carry_Prevolution
{
    internal class Loader
    {
        public const string VersionNumber = "1.6";
        public static bool IsBetaTester;

        public Loader()
        {
            IsBetaTester = true;
            Game.PrintChat("BetaTests enabled.");
            Chat.WellCome();
        }
    }
}