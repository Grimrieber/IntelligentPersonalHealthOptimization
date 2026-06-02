namespace IntelligentPersonalHealthOptimization.Models.Enums;

public enum EquipmentItemType
{
    // ===== Free Weights =====
    AdjustableDumbbells = 100,
    FixedDumbbells = 101,
    BarbellOlympic = 110,
    BarbellStandard = 111,
    BarbellFixed = 112,
    Kettlebells = 120,
    Sandbag = 130,
    WeightVest = 140,

    // ===== Racks & Benches =====
    PowerRack = 200,
    SquatStands = 201,
    SmithMachine = 202,
    FlatBench = 210,
    AdjustableBench = 211,
    PreacherBench = 220,

    // ===== Pulling / Suspended =====
    PullUpBarMounted = 300,
    PullUpBarDoorway = 301,
    TrxSuspension = 310,
    DipStation = 320,
    GymnasticRings = 330,

    // ===== Cable / Machine =====
    CableColumn = 400,
    CableCrossover = 401,
    LatPulldown = 410,
    SeatedRowMachine = 411,
    LegPress = 420,
    HackSquat = 421,
    LegCurl = 422,
    LegExtension = 423,
    ChestPressMachine = 430,
    ShoulderPressMachine = 431,
    PecDeck = 432,
    HipThrustMachine = 440,
    CalfRaiseMachine = 450,
    HyperextensionGhd = 460,

    // ===== Bands & Bodyweight Accessories =====
    LoopBands = 500,
    TubeBandsHandles = 501,
    MiniBands = 502,
    FoamRoller = 510,
    MassageBall = 511,
    YogaMat = 520,
    StabilityBall = 521,
    Bosu = 522,
    AbWheel = 530,
    Sliders = 531,

    // ===== Plyometric / Conditioning =====
    PlyoBox = 600,
    AerobicStep = 601,
    JumpRope = 610,
    MedicineBall = 620,
    SlamBall = 621,
    BattleRopes = 630,
    Sled = 640,
    Tire = 641,

    // ===== Cardio =====
    Treadmill = 700,
    StationaryBike = 701,
    AirBike = 702,
    RowingMachine = 703,
    Elliptical = 704,
    StairClimber = 705,
    SkiErg = 706,

    // ===== Always-available (no equipment needed) =====
    Bodyweight = 900,
    Wall = 901,
    Doorway = 902,
    Step = 903
}
