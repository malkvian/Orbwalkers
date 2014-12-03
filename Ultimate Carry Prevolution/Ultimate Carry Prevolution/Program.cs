using System;

namespace Ultimate_Carry_Prevolution
{
    internal class Program
    {
        private static void Main(string[] args)
        {
            Events.Game.OnGameStart += OnGameStart;
        }

        private static void OnGameStart(EventArgs args)
        {
            var l = new Loader();
        }
    }
}