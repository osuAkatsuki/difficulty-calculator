using DifficultyCalculator.Interfaces;
using Config.Net;

namespace DifficultyCalculator
{
    public static class Globals
    {
        public static ISettings Settings = new ConfigurationBuilder<ISettings>().UseJsonFile("./config.json").Build();
    }
}