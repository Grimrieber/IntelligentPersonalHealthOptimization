using SQLite;

namespace IntelligentPersonalHealthOptimization.Models;

[Table("BodyMeasurements")]
public class BodyMeasurement
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    [Indexed]
    public int ProgressEntryId { get; set; }

    [MaxLength(50)]
    public string MeasurementName { get; set; } = string.Empty;

    public double ValueCm { get; set; }
}
