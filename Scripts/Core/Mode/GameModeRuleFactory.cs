namespace PixelChroma
{
    public static class GameModeRuleFactory
    {
        private static readonly GameModeRule Basic = new BasicGameModeRule();
        private static readonly GameModeRule Infection = new InfectionGameModeRule();

        public static GameModeRule Create(GameMode mode)
        {
            return mode switch
            {
                GameMode.Infection => Infection,
                GameMode.Basic => Basic,
                _ => Basic,
            };
        }
    }
}