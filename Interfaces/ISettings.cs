namespace DifficultyCalculator.Interfaces
{
    public interface ISettings
    {
        string SQLHost { get; }
        string SQLUser { get; }
        string SQLPassword { get; }
        string SQLDatabase { get; }
        string BeatmapFolderPath { get; }
    }
}
