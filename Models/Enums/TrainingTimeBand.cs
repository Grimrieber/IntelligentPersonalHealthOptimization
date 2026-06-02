namespace IntelligentPersonalHealthOptimization.Models.Enums;

public enum TrainingTimeBand
{
    NotSpecified = 0,
    EarlyMorning,   // before 7am — typically off-peak
    MidMorning,     // 7-11am — off-peak
    Lunch,          // 11am-2pm — moderate
    Afternoon,      // 2-5pm — moderate
    EveningPeak,    // 5-8pm — peak / racks contested
    LateEvening     // 8pm+ — off-peak
}
