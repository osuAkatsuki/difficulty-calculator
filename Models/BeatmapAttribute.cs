using Dapper.Contrib.Extensions;

namespace DifficultyCalculator.Models
{
    [Serializable]
    [Table("beatmap_attributes")]
    public class BeatmapAttribute
    {
        public ushort attribute_id { get; set; }
        public float attribute_value { get; set; }
    }
}