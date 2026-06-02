using IntelligentPersonalHealthOptimization.Models;
using IntelligentPersonalHealthOptimization.Models.Enums;
using SQLite;

namespace IntelligentPersonalHealthOptimization.Data;

public static partial class SeedData
{
    public static async Task SeedGymChainTemplatesAsync(SQLiteAsyncConnection connection)
    {
        var count = await connection.Table<GymChainEquipmentTemplate>().CountAsync();
        if (count > 0) return;

        await connection.InsertAllAsync(GetGymChainTemplates());
    }

    private static List<GymChainEquipmentTemplate> GetGymChainTemplates()
    {
        return
        [
            new()
            {
                ChainName = "Planet Fitness",
                TypicalEquipment = Join(
                    EquipmentItemType.SmithMachine,
                    EquipmentItemType.FixedDumbbells,
                    EquipmentItemType.AdjustableBench,
                    EquipmentItemType.FlatBench,
                    EquipmentItemType.CableColumn,
                    EquipmentItemType.LatPulldown,
                    EquipmentItemType.SeatedRowMachine,
                    EquipmentItemType.LegPress,
                    EquipmentItemType.LegCurl,
                    EquipmentItemType.LegExtension,
                    EquipmentItemType.ChestPressMachine,
                    EquipmentItemType.ShoulderPressMachine,
                    EquipmentItemType.PecDeck,
                    EquipmentItemType.CalfRaiseMachine,
                    EquipmentItemType.Treadmill,
                    EquipmentItemType.StationaryBike,
                    EquipmentItemType.Elliptical),
                Restrictions = "No deadlifts; no chalk; lunk alarm — avoid grunting / dropping weights",
                Notes = "Smith machine substitute for free squat/bench. No power racks or Olympic barbells.",
                SortOrder = 10
            },
            new()
            {
                ChainName = "LA Fitness",
                TypicalEquipment = Join(
                    EquipmentItemType.PowerRack,
                    EquipmentItemType.BarbellOlympic,
                    EquipmentItemType.SmithMachine,
                    EquipmentItemType.FixedDumbbells,
                    EquipmentItemType.AdjustableBench,
                    EquipmentItemType.FlatBench,
                    EquipmentItemType.CableColumn,
                    EquipmentItemType.CableCrossover,
                    EquipmentItemType.LatPulldown,
                    EquipmentItemType.SeatedRowMachine,
                    EquipmentItemType.LegPress,
                    EquipmentItemType.HackSquat,
                    EquipmentItemType.LegCurl,
                    EquipmentItemType.LegExtension,
                    EquipmentItemType.ChestPressMachine,
                    EquipmentItemType.ShoulderPressMachine,
                    EquipmentItemType.PecDeck,
                    EquipmentItemType.HipThrustMachine,
                    EquipmentItemType.CalfRaiseMachine,
                    EquipmentItemType.HyperextensionGhd,
                    EquipmentItemType.PullUpBarMounted,
                    EquipmentItemType.DipStation,
                    EquipmentItemType.Treadmill,
                    EquipmentItemType.StationaryBike,
                    EquipmentItemType.Elliptical,
                    EquipmentItemType.RowingMachine,
                    EquipmentItemType.StairClimber),
                Restrictions = "",
                Notes = "Full commercial gym. Racks often contested at peak hours.",
                SortOrder = 20
            },
            new()
            {
                ChainName = "Anytime Fitness",
                TypicalEquipment = Join(
                    EquipmentItemType.PowerRack,
                    EquipmentItemType.BarbellOlympic,
                    EquipmentItemType.SmithMachine,
                    EquipmentItemType.FixedDumbbells,
                    EquipmentItemType.AdjustableBench,
                    EquipmentItemType.FlatBench,
                    EquipmentItemType.CableColumn,
                    EquipmentItemType.LatPulldown,
                    EquipmentItemType.LegPress,
                    EquipmentItemType.LegCurl,
                    EquipmentItemType.LegExtension,
                    EquipmentItemType.ChestPressMachine,
                    EquipmentItemType.PullUpBarMounted,
                    EquipmentItemType.Treadmill,
                    EquipmentItemType.StationaryBike,
                    EquipmentItemType.Elliptical),
                Restrictions = "",
                Notes = "Smaller footprint than LA Fitness; usually 1-2 racks.",
                SortOrder = 30
            },
            new()
            {
                ChainName = "24 Hour Fitness",
                TypicalEquipment = Join(
                    EquipmentItemType.PowerRack,
                    EquipmentItemType.BarbellOlympic,
                    EquipmentItemType.SmithMachine,
                    EquipmentItemType.FixedDumbbells,
                    EquipmentItemType.AdjustableBench,
                    EquipmentItemType.FlatBench,
                    EquipmentItemType.CableColumn,
                    EquipmentItemType.CableCrossover,
                    EquipmentItemType.LatPulldown,
                    EquipmentItemType.SeatedRowMachine,
                    EquipmentItemType.LegPress,
                    EquipmentItemType.HackSquat,
                    EquipmentItemType.LegCurl,
                    EquipmentItemType.LegExtension,
                    EquipmentItemType.ChestPressMachine,
                    EquipmentItemType.ShoulderPressMachine,
                    EquipmentItemType.PecDeck,
                    EquipmentItemType.HipThrustMachine,
                    EquipmentItemType.PullUpBarMounted,
                    EquipmentItemType.Treadmill,
                    EquipmentItemType.StationaryBike,
                    EquipmentItemType.RowingMachine),
                Restrictions = "",
                Notes = "Comparable to LA Fitness inventory.",
                SortOrder = 40
            },
            new()
            {
                ChainName = "Equinox",
                TypicalEquipment = Join(
                    EquipmentItemType.PowerRack,
                    EquipmentItemType.BarbellOlympic,
                    EquipmentItemType.SmithMachine,
                    EquipmentItemType.FixedDumbbells,
                    EquipmentItemType.Kettlebells,
                    EquipmentItemType.AdjustableBench,
                    EquipmentItemType.FlatBench,
                    EquipmentItemType.CableColumn,
                    EquipmentItemType.CableCrossover,
                    EquipmentItemType.LatPulldown,
                    EquipmentItemType.SeatedRowMachine,
                    EquipmentItemType.LegPress,
                    EquipmentItemType.HackSquat,
                    EquipmentItemType.LegCurl,
                    EquipmentItemType.LegExtension,
                    EquipmentItemType.ChestPressMachine,
                    EquipmentItemType.ShoulderPressMachine,
                    EquipmentItemType.PecDeck,
                    EquipmentItemType.HipThrustMachine,
                    EquipmentItemType.HyperextensionGhd,
                    EquipmentItemType.PullUpBarMounted,
                    EquipmentItemType.DipStation,
                    EquipmentItemType.TrxSuspension,
                    EquipmentItemType.MedicineBall,
                    EquipmentItemType.BattleRopes,
                    EquipmentItemType.PlyoBox,
                    EquipmentItemType.Sled,
                    EquipmentItemType.Treadmill,
                    EquipmentItemType.StationaryBike,
                    EquipmentItemType.AirBike,
                    EquipmentItemType.RowingMachine,
                    EquipmentItemType.SkiErg,
                    EquipmentItemType.StairClimber),
                Restrictions = "",
                Notes = "Premium chain — usually has functional zone with sled, plyo, TRX.",
                SortOrder = 50
            },
            new()
            {
                ChainName = "Gold's Gym",
                TypicalEquipment = Join(
                    EquipmentItemType.PowerRack,
                    EquipmentItemType.BarbellOlympic,
                    EquipmentItemType.SmithMachine,
                    EquipmentItemType.FixedDumbbells,
                    EquipmentItemType.AdjustableBench,
                    EquipmentItemType.FlatBench,
                    EquipmentItemType.PreacherBench,
                    EquipmentItemType.CableColumn,
                    EquipmentItemType.CableCrossover,
                    EquipmentItemType.LatPulldown,
                    EquipmentItemType.SeatedRowMachine,
                    EquipmentItemType.LegPress,
                    EquipmentItemType.HackSquat,
                    EquipmentItemType.LegCurl,
                    EquipmentItemType.LegExtension,
                    EquipmentItemType.ChestPressMachine,
                    EquipmentItemType.ShoulderPressMachine,
                    EquipmentItemType.PecDeck,
                    EquipmentItemType.HipThrustMachine,
                    EquipmentItemType.CalfRaiseMachine,
                    EquipmentItemType.HyperextensionGhd,
                    EquipmentItemType.PullUpBarMounted,
                    EquipmentItemType.DipStation,
                    EquipmentItemType.Treadmill,
                    EquipmentItemType.StationaryBike,
                    EquipmentItemType.Elliptical,
                    EquipmentItemType.StairClimber),
                Restrictions = "",
                Notes = "Bodybuilder-friendly; heavy on isolation machines.",
                SortOrder = 60
            },
            new()
            {
                ChainName = "Lifetime Fitness",
                TypicalEquipment = Join(
                    EquipmentItemType.PowerRack,
                    EquipmentItemType.BarbellOlympic,
                    EquipmentItemType.SmithMachine,
                    EquipmentItemType.FixedDumbbells,
                    EquipmentItemType.Kettlebells,
                    EquipmentItemType.AdjustableBench,
                    EquipmentItemType.FlatBench,
                    EquipmentItemType.CableColumn,
                    EquipmentItemType.CableCrossover,
                    EquipmentItemType.LatPulldown,
                    EquipmentItemType.SeatedRowMachine,
                    EquipmentItemType.LegPress,
                    EquipmentItemType.HackSquat,
                    EquipmentItemType.LegCurl,
                    EquipmentItemType.LegExtension,
                    EquipmentItemType.ChestPressMachine,
                    EquipmentItemType.ShoulderPressMachine,
                    EquipmentItemType.PecDeck,
                    EquipmentItemType.HipThrustMachine,
                    EquipmentItemType.HyperextensionGhd,
                    EquipmentItemType.PullUpBarMounted,
                    EquipmentItemType.DipStation,
                    EquipmentItemType.TrxSuspension,
                    EquipmentItemType.MedicineBall,
                    EquipmentItemType.PlyoBox,
                    EquipmentItemType.Sled,
                    EquipmentItemType.Treadmill,
                    EquipmentItemType.StationaryBike,
                    EquipmentItemType.AirBike,
                    EquipmentItemType.RowingMachine,
                    EquipmentItemType.StairClimber),
                Restrictions = "",
                Notes = "Premium with functional training zone.",
                SortOrder = 70
            },
            new()
            {
                ChainName = "Crunch Fitness",
                TypicalEquipment = Join(
                    EquipmentItemType.PowerRack,
                    EquipmentItemType.BarbellOlympic,
                    EquipmentItemType.SmithMachine,
                    EquipmentItemType.FixedDumbbells,
                    EquipmentItemType.AdjustableBench,
                    EquipmentItemType.FlatBench,
                    EquipmentItemType.CableColumn,
                    EquipmentItemType.LatPulldown,
                    EquipmentItemType.LegPress,
                    EquipmentItemType.LegCurl,
                    EquipmentItemType.LegExtension,
                    EquipmentItemType.ChestPressMachine,
                    EquipmentItemType.PecDeck,
                    EquipmentItemType.PullUpBarMounted,
                    EquipmentItemType.Treadmill,
                    EquipmentItemType.StationaryBike,
                    EquipmentItemType.Elliptical),
                Restrictions = "",
                Notes = "Mid-tier commercial. Usually 1-2 racks.",
                SortOrder = 80
            },
            new()
            {
                ChainName = "CrossFit Affiliate",
                TypicalEquipment = Join(
                    EquipmentItemType.PowerRack,
                    EquipmentItemType.SquatStands,
                    EquipmentItemType.BarbellOlympic,
                    EquipmentItemType.Kettlebells,
                    EquipmentItemType.AdjustableDumbbells,
                    EquipmentItemType.FlatBench,
                    EquipmentItemType.PullUpBarMounted,
                    EquipmentItemType.GymnasticRings,
                    EquipmentItemType.DipStation,
                    EquipmentItemType.MedicineBall,
                    EquipmentItemType.SlamBall,
                    EquipmentItemType.PlyoBox,
                    EquipmentItemType.JumpRope,
                    EquipmentItemType.BattleRopes,
                    EquipmentItemType.Sled,
                    EquipmentItemType.AirBike,
                    EquipmentItemType.RowingMachine,
                    EquipmentItemType.SkiErg),
                Restrictions = "Class-based — may not always have free access to all stations",
                Notes = "Olympic lifting friendly; light on isolation machines.",
                SortOrder = 90
            },
            new()
            {
                ChainName = "YMCA",
                TypicalEquipment = Join(
                    EquipmentItemType.FixedDumbbells,
                    EquipmentItemType.AdjustableBench,
                    EquipmentItemType.FlatBench,
                    EquipmentItemType.CableColumn,
                    EquipmentItemType.LatPulldown,
                    EquipmentItemType.LegPress,
                    EquipmentItemType.LegCurl,
                    EquipmentItemType.LegExtension,
                    EquipmentItemType.ChestPressMachine,
                    EquipmentItemType.ShoulderPressMachine,
                    EquipmentItemType.Treadmill,
                    EquipmentItemType.StationaryBike,
                    EquipmentItemType.Elliptical),
                Restrictions = "Varies widely by branch",
                Notes = "Equipment availability ranges widely — some have racks/barbells, some don't.",
                SortOrder = 100
            }
        ];
    }

    private static string Join(params EquipmentItemType[] items) =>
        string.Join(",", items.Select(i => i.ToString()));
}
