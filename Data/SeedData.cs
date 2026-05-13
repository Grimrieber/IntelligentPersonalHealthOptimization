using IntelligentPersonalHealthOptimization.Models;
using IntelligentPersonalHealthOptimization.Models.Enums;
using SQLite;

namespace IntelligentPersonalHealthOptimization.Data;

public static partial class SeedData
{
    public static async Task SeedExercisesAsync(SQLiteAsyncConnection connection)
    {
        var count = await connection.Table<Exercise>().CountAsync();
        if (count > 0)
            return;

        var exercises = GetExerciseLibrary();
        await connection.InsertAllAsync(exercises);
    }

    private static List<Exercise> GetExerciseLibrary()
    {
        return new List<Exercise>
        {
            // ===== CORRECTIVE EXERCISES =====
            new()
            {
                Name = "Foam Roll IT Band",
                Description = "Lie on your side with a foam roller under your outer thigh. Roll slowly from hip to just above the knee.",
                FormCues = "Keep core braced;Roll slowly for 30 seconds;Pause on tender spots;Avoid rolling over the knee joint",
                Category = ExerciseCategory.Corrective,
                PrimaryMuscle = MuscleGroup.Abductors,
                Equipment = "Foam Roller",
                DifficultyLevel = 1,
                CorrectsCompensations = "KneesValgus,HipDrop"
            },
            new()
            {
                Name = "Foam Roll Calves",
                Description = "Sit with a foam roller under your calves. Roll from ankle to just below the knee.",
                FormCues = "Cross one leg over the other for more pressure;Roll slowly;Rotate foot in and out to hit different areas",
                Category = ExerciseCategory.Corrective,
                PrimaryMuscle = MuscleGroup.Calves,
                Equipment = "Foam Roller",
                DifficultyLevel = 1,
                CorrectsCompensations = "FeetTurnOut,FeetFlatten,KneesDominant"
            },
            new()
            {
                Name = "Static Hip Flexor Stretch",
                Description = "Kneel on one knee in a lunge position. Push hips forward gently until you feel a stretch in the front of the rear hip.",
                FormCues = "Keep torso upright;Squeeze rear glute;Hold 30 seconds per side;Do not arch lower back",
                Category = ExerciseCategory.Corrective,
                PrimaryMuscle = MuscleGroup.HipFlexors,
                Equipment = "Bodyweight",
                DifficultyLevel = 1,
                CorrectsCompensations = "ExcessiveForwardLean,LowBackArches,AnklePronation"
            },
            new()
            {
                Name = "Glute Bridge",
                Description = "Lie on your back with knees bent and feet flat. Drive through heels to lift hips toward ceiling.",
                FormCues = "Squeeze glutes at the top;Keep core tight;Do not hyperextend lower back;Hold 2 seconds at top",
                Category = ExerciseCategory.Corrective,
                PrimaryMuscle = MuscleGroup.Glutes,
                Equipment = "Bodyweight",
                DifficultyLevel = 1,
                CorrectsCompensations = "KneesValgus,ExcessiveForwardLean,LowBackArches,HipDrop"
            },
            new()
            {
                Name = "Clamshell",
                Description = "Lie on your side with knees bent at 90 degrees. Keep feet together and lift the top knee.",
                FormCues = "Do not rotate pelvis;Keep feet together;Squeeze at the top;Control the movement down",
                Category = ExerciseCategory.Corrective,
                PrimaryMuscle = MuscleGroup.Glutes,
                SecondaryMuscles = "Abductors",
                Equipment = "Bodyweight",
                DifficultyLevel = 1,
                CorrectsCompensations = "KneesValgus,HipDrop,AnklePronation"
            },
            new()
            {
                Name = "Bird-Dog",
                Description = "On hands and knees, extend opposite arm and leg simultaneously while maintaining a stable core.",
                FormCues = "Keep back flat;Do not rotate hips;Extend fully;Hold 2 seconds at top",
                Category = ExerciseCategory.Corrective,
                PrimaryMuscle = MuscleGroup.Core,
                SecondaryMuscles = "LowerBack,Glutes",
                Equipment = "Bodyweight",
                DifficultyLevel = 1,
                CorrectsCompensations = "LowBackArches,LowBackRounds,AsymmetricShift"
            },
            new()
            {
                Name = "Dead Bug",
                Description = "Lie on back with arms extended toward ceiling and knees at 90 degrees. Lower opposite arm and leg while keeping lower back flat.",
                FormCues = "Press lower back into floor;Move slowly;Exhale as you extend;Return to start before switching sides",
                Category = ExerciseCategory.Corrective,
                PrimaryMuscle = MuscleGroup.Core,
                SecondaryMuscles = "HipFlexors",
                Equipment = "Bodyweight",
                DifficultyLevel = 1,
                CorrectsCompensations = "LowBackArches,LowBackRounds,ExcessiveForwardLean"
            },
            new()
            {
                Name = "Cat-Cow",
                Description = "On hands and knees, alternate between arching (cow) and rounding (cat) the spine.",
                FormCues = "Move through full range;Breathe in on cow, out on cat;Move slowly and controlled;Feel each vertebra move",
                Category = ExerciseCategory.Corrective,
                PrimaryMuscle = MuscleGroup.Core,
                SecondaryMuscles = "LowerBack",
                Equipment = "Bodyweight",
                DifficultyLevel = 1,
                CorrectsCompensations = "LowBackArches,LowBackRounds"
            },
            new()
            {
                Name = "Thoracic Extension on Foam Roller",
                Description = "Lie with foam roller across mid-back. Support head with hands and gently extend over the roller.",
                FormCues = "Keep hips on ground;Move roller to different segments;Extend on exhale;Do not force range of motion",
                Category = ExerciseCategory.Corrective,
                PrimaryMuscle = MuscleGroup.UpperBack,
                Equipment = "Foam Roller",
                DifficultyLevel = 1,
                CorrectsCompensations = "ArmsForward,ShoulderElevation,ExcessiveForwardLean"
            },
            new()
            {
                Name = "Wall Angel",
                Description = "Stand with back against wall. Slide arms up and down the wall in a snow angel pattern.",
                FormCues = "Keep lower back, head, and arms touching wall;Move slowly;Stop if pain occurs;Full range of motion",
                Category = ExerciseCategory.Corrective,
                PrimaryMuscle = MuscleGroup.UpperBack,
                SecondaryMuscles = "RotatorCuff,Shoulders",
                Equipment = "Wall",
                DifficultyLevel = 1,
                CorrectsCompensations = "ArmsForward,ArmsUneven,ShoulderElevation"
            },
            new()
            {
                Name = "Ankle Mobility Circles",
                Description = "Standing or seated, rotate ankle through full circles in both directions.",
                FormCues = "Make full circles;10 each direction;Keep rest of leg still;Do both ankles",
                Category = ExerciseCategory.Corrective,
                PrimaryMuscle = MuscleGroup.Calves,
                SecondaryMuscles = "TibialisAnterior",
                Equipment = "Bodyweight",
                DifficultyLevel = 1,
                CorrectsCompensations = "FeetTurnOut,FeetFlatten,AnklePronation"
            },
            new()
            {
                Name = "Side-Lying Leg Raise",
                Description = "Lie on your side with legs straight. Lift the top leg toward ceiling while keeping hips stacked.",
                FormCues = "Keep hips stacked;Lead with heel;Do not rotate;Control the descent",
                Category = ExerciseCategory.Corrective,
                PrimaryMuscle = MuscleGroup.Abductors,
                SecondaryMuscles = "Glutes",
                Equipment = "Bodyweight",
                DifficultyLevel = 1,
                CorrectsCompensations = "KneesValgus,HipDrop,TrunkLateralLean"
            },
            new()
            {
                Name = "Supine Hamstring Stretch",
                Description = "Lie on back. Lift one leg and gently pull toward chest keeping the knee straight.",
                FormCues = "Keep opposite leg flat;Hold 30 seconds;Do not bounce;Use a strap if needed",
                Category = ExerciseCategory.Corrective,
                PrimaryMuscle = MuscleGroup.Hamstrings,
                Equipment = "Bodyweight",
                DifficultyLevel = 1,
                CorrectsCompensations = "LowBackRounds,ExcessiveForwardLean"
            },
            new()
            {
                Name = "Piriformis Stretch",
                Description = "Lie on back, cross one ankle over opposite knee. Pull the uncrossed leg toward chest.",
                FormCues = "Keep head on floor;Hold 30 seconds;Breathe deeply;Do both sides",
                Category = ExerciseCategory.Corrective,
                PrimaryMuscle = MuscleGroup.Glutes,
                Equipment = "Bodyweight",
                DifficultyLevel = 1,
                CorrectsCompensations = "FeetTurnOut,AsymmetricShift"
            },
            new()
            {
                Name = "Child's Pose",
                Description = "Kneel and sit back on heels. Extend arms forward on floor and lower chest toward ground.",
                FormCues = "Breathe deeply into back;Hold 30+ seconds;Relax shoulders;Walk hands to each side for lateral stretch",
                Category = ExerciseCategory.Corrective,
                PrimaryMuscle = MuscleGroup.LowerBack,
                SecondaryMuscles = "Lats,Shoulders",
                Equipment = "Bodyweight",
                DifficultyLevel = 1,
                CorrectsCompensations = "LowBackArches,ShoulderElevation"
            },

            // ===== WARMUP / ACTIVATION EXERCISES =====
            new()
            {
                Name = "Glute Bridge March",
                Description = "Hold a glute bridge position and alternate lifting knees toward chest in a marching pattern.",
                FormCues = "Keep hips level;Do not let hips drop;Alternate smoothly;Squeeze glutes throughout",
                Category = ExerciseCategory.Activation,
                PrimaryMuscle = MuscleGroup.Glutes,
                SecondaryMuscles = "Core,HipFlexors",
                Equipment = "Bodyweight",
                DifficultyLevel = 2,
                CorrectsCompensations = "KneesValgus,HipDrop,ExcessiveForwardLean"
            },
            new()
            {
                Name = "Lateral Band Walk",
                Description = "Place a mini band around ankles or above knees. Side-step while maintaining tension.",
                FormCues = "Stay in quarter squat;Keep toes forward;Take small controlled steps;Do not let knees cave in",
                Category = ExerciseCategory.Activation,
                PrimaryMuscle = MuscleGroup.Glutes,
                SecondaryMuscles = "Abductors",
                Equipment = "Resistance Band",
                DifficultyLevel = 2,
                CorrectsCompensations = "KneesValgus,HipDrop,AnklePronation"
            },
            new()
            {
                Name = "Bodyweight Squat",
                Description = "Stand with feet shoulder-width apart. Squat down by pushing hips back and bending knees.",
                FormCues = "Keep chest up;Push knees out over toes;Go to comfortable depth;Stand up by driving through heels",
                Category = ExerciseCategory.Warmup,
                PrimaryMuscle = MuscleGroup.Quadriceps,
                SecondaryMuscles = "Glutes,Hamstrings",
                Equipment = "Bodyweight",
                DifficultyLevel = 1
            },
            new()
            {
                Name = "Inchworm",
                Description = "From standing, bend forward and walk hands out to plank position, then walk feet toward hands.",
                FormCues = "Keep legs as straight as possible;Engage core in plank;Walk hands out slowly;Full extension at top",
                Category = ExerciseCategory.Warmup,
                PrimaryMuscle = MuscleGroup.Core,
                SecondaryMuscles = "Hamstrings,Shoulders,Chest",
                Equipment = "Bodyweight",
                DifficultyLevel = 2
            },
            new()
            {
                Name = "Arm Circles",
                Description = "Extend arms to sides and make small circles, gradually increasing size.",
                FormCues = "Start with small circles;Gradually increase size;Do both directions;Keep core engaged",
                Category = ExerciseCategory.Warmup,
                PrimaryMuscle = MuscleGroup.Shoulders,
                SecondaryMuscles = "RotatorCuff",
                Equipment = "Bodyweight",
                DifficultyLevel = 1
            },
            new()
            {
                Name = "Leg Swings",
                Description = "Hold a wall for balance. Swing one leg forward and backward in a controlled pendulum motion.",
                FormCues = "Keep standing leg straight;Swing through full range;Control the motion;Do front-to-back and side-to-side",
                Category = ExerciseCategory.Warmup,
                PrimaryMuscle = MuscleGroup.HipFlexors,
                SecondaryMuscles = "Hamstrings,Glutes",
                Equipment = "Bodyweight",
                DifficultyLevel = 1
            },
            new()
            {
                Name = "Hip Circles",
                Description = "Stand on one leg and make large circles with the elevated knee.",
                FormCues = "Make full circles;Both directions;Maintain balance;Keep standing leg slightly bent",
                Category = ExerciseCategory.Warmup,
                PrimaryMuscle = MuscleGroup.HipFlexors,
                SecondaryMuscles = "Glutes,Core",
                Equipment = "Bodyweight",
                DifficultyLevel = 1
            },
            new()
            {
                Name = "High Knees",
                Description = "March or jog in place lifting knees to hip height with each step.",
                FormCues = "Drive knees up;Pump arms;Stay on balls of feet;Keep core engaged",
                Category = ExerciseCategory.Warmup,
                PrimaryMuscle = MuscleGroup.HipFlexors,
                SecondaryMuscles = "Quadriceps,Calves,Core",
                Equipment = "Bodyweight",
                DifficultyLevel = 2
            },

            // ===== MAIN EXERCISES =====
            new()
            {
                Name = "Goblet Squat",
                Description = "Hold a dumbbell or kettlebell at chest height. Squat down keeping the weight close to body.",
                FormCues = "Elbows inside knees at bottom;Keep chest tall;Push knees out;Drive through whole foot",
                Category = ExerciseCategory.Main,
                PrimaryMuscle = MuscleGroup.Quadriceps,
                SecondaryMuscles = "Glutes,Hamstrings,Core",
                Equipment = "Dumbbell",
                DifficultyLevel = 2
            },
            new()
            {
                Name = "Dumbbell Bench Press",
                Description = "Lie on a bench with a dumbbell in each hand. Press weights up over chest.",
                FormCues = "Shoulder blades pinched back;Lower with control;Press up and slightly in;Full range of motion",
                Category = ExerciseCategory.Main,
                PrimaryMuscle = MuscleGroup.Chest,
                SecondaryMuscles = "Shoulders,Triceps",
                Equipment = "Dumbbells, Bench",
                DifficultyLevel = 3
            },
            new()
            {
                Name = "Dumbbell Row",
                Description = "Hinge at hips with one hand on bench. Pull dumbbell toward hip with the other arm.",
                FormCues = "Keep back flat;Pull toward hip;Squeeze shoulder blade;Control the lowering",
                Category = ExerciseCategory.Main,
                PrimaryMuscle = MuscleGroup.UpperBack,
                SecondaryMuscles = "Lats,Biceps",
                Equipment = "Dumbbell, Bench",
                DifficultyLevel = 2
            },
            new()
            {
                Name = "Overhead Press",
                Description = "Stand with dumbbells at shoulder height. Press overhead until arms are fully extended.",
                FormCues = "Keep core tight;Do not arch back;Press straight up;Lower with control",
                Category = ExerciseCategory.Main,
                PrimaryMuscle = MuscleGroup.Shoulders,
                SecondaryMuscles = "Triceps,Core",
                Equipment = "Dumbbells",
                DifficultyLevel = 3
            },
            new()
            {
                Name = "Romanian Deadlift",
                Description = "Hold dumbbells at thighs. Hinge at hips pushing them back while lowering weights along legs.",
                FormCues = "Keep back flat;Slight knee bend;Feel hamstring stretch;Squeeze glutes to stand",
                Category = ExerciseCategory.Main,
                PrimaryMuscle = MuscleGroup.Hamstrings,
                SecondaryMuscles = "Glutes,LowerBack",
                Equipment = "Dumbbells",
                DifficultyLevel = 3
            },
            new()
            {
                Name = "Walking Lunges",
                Description = "Step forward into a lunge, push off front foot, and step the rear foot forward into the next lunge.",
                FormCues = "Keep torso upright;Front knee tracks over toes;Step far enough forward;Push through front heel",
                Category = ExerciseCategory.Main,
                PrimaryMuscle = MuscleGroup.Quadriceps,
                SecondaryMuscles = "Glutes,Hamstrings",
                Equipment = "Bodyweight",
                DifficultyLevel = 2
            },
            new()
            {
                Name = "Push-Ups",
                Description = "Start in plank position. Lower chest to ground and push back up.",
                FormCues = "Keep body in straight line;Elbows at 45 degrees;Full range of motion;Modify on knees if needed",
                Category = ExerciseCategory.Main,
                PrimaryMuscle = MuscleGroup.Chest,
                SecondaryMuscles = "Shoulders,Triceps,Core",
                Equipment = "Bodyweight",
                DifficultyLevel = 2
            },
            new()
            {
                Name = "Plank",
                Description = "Hold a push-up position on forearms. Keep body in a straight line from head to heels.",
                FormCues = "Do not let hips sag;Do not pike up;Breathe steadily;Engage core throughout",
                Category = ExerciseCategory.Main,
                PrimaryMuscle = MuscleGroup.Core,
                SecondaryMuscles = "Shoulders,Glutes",
                Equipment = "Bodyweight",
                DifficultyLevel = 1
            },
            new()
            {
                Name = "Pallof Press",
                Description = "Stand sideways to a cable or band anchor. Press the handle straight out from chest and hold.",
                FormCues = "Resist rotation;Keep arms at chest height;Squeeze core;Slow controlled presses",
                Category = ExerciseCategory.Main,
                PrimaryMuscle = MuscleGroup.Core,
                SecondaryMuscles = "Obliques",
                Equipment = "Resistance Band",
                DifficultyLevel = 2
            },
            new()
            {
                Name = "Band Lat Pulldown",
                Description = "Anchor a band overhead. Kneel or stand and pull the band down to chest level.",
                FormCues = "Squeeze shoulder blades together;Pull to upper chest;Control the return;Keep chest up",
                Category = ExerciseCategory.Main,
                PrimaryMuscle = MuscleGroup.Lats,
                SecondaryMuscles = "Biceps,UpperBack",
                Equipment = "Resistance Band",
                DifficultyLevel = 2
            },
            new()
            {
                Name = "Tricep Dip",
                Description = "Place hands on bench behind you with legs extended. Lower body by bending elbows, then press up.",
                FormCues = "Keep back close to bench;Elbows point back;Lower to 90 degrees;Press up fully",
                Category = ExerciseCategory.Main,
                PrimaryMuscle = MuscleGroup.Triceps,
                SecondaryMuscles = "Chest,Shoulders",
                Equipment = "Bench",
                DifficultyLevel = 2
            },
            new()
            {
                Name = "Bicep Curl",
                Description = "Stand holding dumbbells at sides. Curl weights up to shoulders by bending elbows.",
                FormCues = "Keep elbows at sides;Do not swing body;Full range of motion;Control the lowering",
                Category = ExerciseCategory.Main,
                PrimaryMuscle = MuscleGroup.Biceps,
                SecondaryMuscles = "Forearms",
                Equipment = "Dumbbells",
                DifficultyLevel = 1
            },
            new()
            {
                Name = "Lateral Raise",
                Description = "Stand with dumbbells at sides. Raise arms out to the sides until shoulder height.",
                FormCues = "Slight bend in elbows;Lead with pinkies;Stop at shoulder height;Control the descent",
                Category = ExerciseCategory.Main,
                PrimaryMuscle = MuscleGroup.Shoulders,
                SecondaryMuscles = "UpperBack",
                Equipment = "Dumbbells",
                DifficultyLevel = 2
            },
            new()
            {
                Name = "Standing Calf Raise",
                Description = "Stand on the edge of a step. Rise up on toes, then lower heels below the step.",
                FormCues = "Full range of motion;Pause at top;Control the lowering;Keep legs straight",
                Category = ExerciseCategory.Main,
                PrimaryMuscle = MuscleGroup.Calves,
                Equipment = "Step",
                DifficultyLevel = 1
            },
            new()
            {
                Name = "Step-Up",
                Description = "Step onto a bench or box with one foot, drive up to standing, then step down.",
                FormCues = "Drive through the heel;Full extension at top;Control the step down;Keep torso upright",
                Category = ExerciseCategory.Main,
                PrimaryMuscle = MuscleGroup.Quadriceps,
                SecondaryMuscles = "Glutes,Hamstrings",
                Equipment = "Bench",
                DifficultyLevel = 2
            },
            new()
            {
                Name = "Hip Thrust",
                Description = "Sit with upper back against bench, feet flat. Drive hips up squeezing glutes at top.",
                FormCues = "Chin tucked;Drive through heels;Full hip extension;Squeeze glutes 2 seconds at top",
                Category = ExerciseCategory.Main,
                PrimaryMuscle = MuscleGroup.Glutes,
                SecondaryMuscles = "Hamstrings,Core",
                Equipment = "Bench",
                DifficultyLevel = 3
            },
            new()
            {
                Name = "Band Face Pull",
                Description = "Anchor band at face height. Pull toward face with elbows high, squeezing rear delts.",
                FormCues = "Elbows stay high;Pull apart at the end;Squeeze shoulder blades;Control the return",
                Category = ExerciseCategory.Main,
                PrimaryMuscle = MuscleGroup.UpperBack,
                SecondaryMuscles = "RotatorCuff,Shoulders",
                Equipment = "Resistance Band",
                DifficultyLevel = 2
            },
            new()
            {
                Name = "Band Chop",
                Description = "Anchor band high. Pull diagonally across body from high to low in a chopping motion.",
                FormCues = "Rotate through core;Keep arms straight;Control the motion;Return slowly",
                Category = ExerciseCategory.Main,
                PrimaryMuscle = MuscleGroup.Obliques,
                SecondaryMuscles = "Core,Shoulders",
                Equipment = "Resistance Band",
                DifficultyLevel = 2
            },
            new()
            {
                Name = "Bodyweight Leg Curl",
                Description = "Lie face down. Curl heels toward glutes using hamstring contraction.",
                FormCues = "Keep hips down;Squeeze hamstrings;Controlled full range;Can use towel on smooth floor",
                Category = ExerciseCategory.Main,
                PrimaryMuscle = MuscleGroup.Hamstrings,
                SecondaryMuscles = "Calves",
                Equipment = "Bodyweight",
                DifficultyLevel = 1
            },
            new()
            {
                Name = "Bodyweight Leg Extension",
                Description = "Sit on edge of chair or bench. Extend one leg straight, squeezing the quad at top.",
                FormCues = "Full knee extension;Squeeze quad at top;Lower with control;Keep back straight",
                Category = ExerciseCategory.Main,
                PrimaryMuscle = MuscleGroup.Quadriceps,
                Equipment = "Bench",
                DifficultyLevel = 1
            },

            // ===== COOLDOWN / STRETCH EXERCISES =====
            new()
            {
                Name = "Standing Quad Stretch",
                Description = "Stand on one leg, pull the other heel toward glutes. Hold for 30 seconds.",
                FormCues = "Keep knees together;Stand tall;Use wall for balance;Do not arch back",
                Category = ExerciseCategory.Cooldown,
                PrimaryMuscle = MuscleGroup.Quadriceps,
                SecondaryMuscles = "HipFlexors",
                Equipment = "Bodyweight",
                DifficultyLevel = 1
            },
            new()
            {
                Name = "Standing Hamstring Stretch",
                Description = "Place heel on low surface. Lean forward from hips keeping back straight.",
                FormCues = "Keep back flat;Hinge at hips;Hold 30 seconds;Do not round back",
                Category = ExerciseCategory.Cooldown,
                PrimaryMuscle = MuscleGroup.Hamstrings,
                Equipment = "Bodyweight",
                DifficultyLevel = 1
            },
            new()
            {
                Name = "Chest Doorway Stretch",
                Description = "Place forearm on doorframe at 90 degrees. Step through doorway to stretch chest.",
                FormCues = "Elbow at shoulder height;Step forward slowly;Hold 30 seconds;Do both sides",
                Category = ExerciseCategory.Cooldown,
                PrimaryMuscle = MuscleGroup.Chest,
                SecondaryMuscles = "Shoulders",
                Equipment = "Doorway",
                DifficultyLevel = 1
            },
            new()
            {
                Name = "Lat Stretch",
                Description = "Grab a doorframe or pole overhead. Lean away to stretch the lat and side body.",
                FormCues = "Keep arm overhead;Lean away from arm;Hold 30 seconds;Breathe into the stretch",
                Category = ExerciseCategory.Cooldown,
                PrimaryMuscle = MuscleGroup.Lats,
                SecondaryMuscles = "Obliques",
                Equipment = "Doorway",
                DifficultyLevel = 1
            },
            new()
            {
                Name = "Figure-4 Stretch",
                Description = "Lie on back. Cross one ankle over opposite knee. Pull uncrossed leg toward chest.",
                FormCues = "Keep head on floor;Press crossed knee away;Hold 30 seconds;Breathe deeply",
                Category = ExerciseCategory.Cooldown,
                PrimaryMuscle = MuscleGroup.Glutes,
                SecondaryMuscles = "HipFlexors",
                Equipment = "Bodyweight",
                DifficultyLevel = 1
            },
            new()
            {
                Name = "Child's Pose Stretch",
                Description = "Kneel and sit back on heels. Extend arms forward and lower chest to ground.",
                FormCues = "Breathe into back;Hold 30+ seconds;Relax shoulders;Walk hands to each side for lateral stretch",
                Category = ExerciseCategory.Cooldown,
                PrimaryMuscle = MuscleGroup.LowerBack,
                SecondaryMuscles = "Lats,Shoulders",
                Equipment = "Bodyweight",
                DifficultyLevel = 1
            },
            new()
            {
                Name = "Scorpion Stretch",
                Description = "Lie face down with arms out. Lift one leg and rotate it across body toward opposite hand.",
                FormCues = "Keep shoulders on ground;Move slowly;Do not force range;Hold briefly each rep",
                Category = ExerciseCategory.Cooldown,
                PrimaryMuscle = MuscleGroup.HipFlexors,
                SecondaryMuscles = "Obliques,LowerBack",
                Equipment = "Bodyweight",
                DifficultyLevel = 2
            },
            new()
            {
                Name = "Standing Calf Stretch",
                Description = "Stand facing wall with one foot behind. Press rear heel into ground and lean forward.",
                FormCues = "Keep rear leg straight;Press heel down;Hold 30 seconds;Do both sides",
                Category = ExerciseCategory.Cooldown,
                PrimaryMuscle = MuscleGroup.Calves,
                SecondaryMuscles = "TibialisAnterior",
                Equipment = "Wall",
                DifficultyLevel = 1
            },

            // ===== ADDITIONAL CORRECTIVE EXERCISES (Posture-Targeted) =====
            new()
            {
                Name = "Chin Tuck",
                Description = "Sit or stand tall. Pull your chin straight back creating a double chin. Hold 5 seconds.",
                FormCues = "Keep eyes level;Pull chin back not down;Hold 5 seconds;Repeat 10 times",
                Category = ExerciseCategory.Corrective,
                PrimaryMuscle = MuscleGroup.Core,
                Equipment = "Bodyweight",
                DifficultyLevel = 1,
                CorrectsCompensations = "ShoulderElevation"
            },
            new()
            {
                Name = "Prone Y Raise",
                Description = "Lie face down with arms forming a Y shape overhead. Lift arms off ground squeezing shoulder blades.",
                FormCues = "Keep thumbs up;Squeeze shoulder blades;Lift 2-3 inches;Hold 2 seconds at top",
                Category = ExerciseCategory.Corrective,
                PrimaryMuscle = MuscleGroup.UpperBack,
                SecondaryMuscles = "RotatorCuff,Shoulders",
                Equipment = "Bodyweight",
                DifficultyLevel = 1,
                CorrectsCompensations = "ArmsForward,ShoulderElevation"
            },
            new()
            {
                Name = "Band Pull-Apart",
                Description = "Hold a resistance band at chest height with straight arms. Pull apart squeezing shoulder blades.",
                FormCues = "Keep arms straight;Squeeze at full extension;Control return;Keep shoulders down",
                Category = ExerciseCategory.Corrective,
                PrimaryMuscle = MuscleGroup.UpperBack,
                SecondaryMuscles = "RotatorCuff",
                Equipment = "Resistance Band",
                DifficultyLevel = 1,
                CorrectsCompensations = "ArmsForward,ShoulderElevation"
            },
            new()
            {
                Name = "Pec Ball Release",
                Description = "Place a tennis or lacrosse ball against chest muscle on a wall. Apply gentle pressure and move.",
                FormCues = "Focus on tight spots;Hold tender points 30 seconds;Keep pressure moderate;Breathe through it",
                Category = ExerciseCategory.Corrective,
                PrimaryMuscle = MuscleGroup.Chest,
                Equipment = "Bodyweight",
                DifficultyLevel = 1,
                CorrectsCompensations = "ArmsForward,ShoulderElevation"
            },

            // ===== ADDITIONAL MAIN EXERCISES (Equipment Variations) =====
            new()
            {
                Name = "Barbell Back Squat",
                Description = "Place barbell across upper back. Squat down by pushing hips back and bending knees.",
                FormCues = "Bar on upper traps;Chest up;Push knees out;Drive through heels to stand",
                Category = ExerciseCategory.Main,
                PrimaryMuscle = MuscleGroup.Quadriceps,
                SecondaryMuscles = "Glutes,Hamstrings,Core",
                Equipment = "Barbell",
                DifficultyLevel = 4
            },
            new()
            {
                Name = "Barbell Bench Press",
                Description = "Lie on bench with barbell. Lower to chest and press back up.",
                FormCues = "Shoulder blades retracted;Bar touches mid-chest;Press up and back;Keep feet flat",
                Category = ExerciseCategory.Main,
                PrimaryMuscle = MuscleGroup.Chest,
                SecondaryMuscles = "Shoulders,Triceps",
                Equipment = "Barbell,Bench",
                DifficultyLevel = 4
            },
            new()
            {
                Name = "Barbell Deadlift",
                Description = "Stand behind barbell. Hinge at hips and grip bar. Stand up by driving through legs.",
                FormCues = "Keep back flat;Bar stays close to body;Drive through heels;Lock out hips at top",
                Category = ExerciseCategory.Main,
                PrimaryMuscle = MuscleGroup.Hamstrings,
                SecondaryMuscles = "Glutes,LowerBack,Core",
                Equipment = "Barbell",
                DifficultyLevel = 5
            },
            new()
            {
                Name = "Barbell Overhead Press",
                Description = "Clean barbell to front rack. Press overhead until arms are locked out.",
                FormCues = "Keep core braced;Press straight up;Lock out overhead;Lower with control",
                Category = ExerciseCategory.Main,
                PrimaryMuscle = MuscleGroup.Shoulders,
                SecondaryMuscles = "Triceps,Core",
                Equipment = "Barbell",
                DifficultyLevel = 4
            },
            new()
            {
                Name = "Barbell Row",
                Description = "Hinge forward at hips holding barbell. Pull bar to lower chest.",
                FormCues = "Keep back flat;Pull to lower chest;Squeeze shoulder blades;Control lowering",
                Category = ExerciseCategory.Main,
                PrimaryMuscle = MuscleGroup.UpperBack,
                SecondaryMuscles = "Lats,Biceps",
                Equipment = "Barbell",
                DifficultyLevel = 4
            },
            new()
            {
                Name = "Kettlebell Swing",
                Description = "Hinge at hips with kettlebell between legs. Explosively drive hips forward to swing bell to chest height.",
                FormCues = "Hinge don't squat;Snap hips forward;Arms are loose;Control the backswing",
                Category = ExerciseCategory.Main,
                PrimaryMuscle = MuscleGroup.Glutes,
                SecondaryMuscles = "Hamstrings,Core,Shoulders",
                Equipment = "Kettlebell",
                DifficultyLevel = 3
            },
            new()
            {
                Name = "Kettlebell Goblet Squat",
                Description = "Hold kettlebell at chest by the horns. Squat down keeping elbows inside knees.",
                FormCues = "Elbows inside knees;Chest tall;Push knees out;Drive through whole foot",
                Category = ExerciseCategory.Main,
                PrimaryMuscle = MuscleGroup.Quadriceps,
                SecondaryMuscles = "Glutes,Core",
                Equipment = "Kettlebell",
                DifficultyLevel = 2
            },
            new()
            {
                Name = "Pull-Up",
                Description = "Hang from bar with overhand grip. Pull body up until chin clears bar.",
                FormCues = "Full dead hang start;Pull shoulder blades down first;Chin over bar;Control the descent",
                Category = ExerciseCategory.Main,
                PrimaryMuscle = MuscleGroup.Lats,
                SecondaryMuscles = "Biceps,UpperBack,Core",
                Equipment = "PullUpBar",
                DifficultyLevel = 4
            },
            new()
            {
                Name = "Chin-Up",
                Description = "Hang from bar with underhand grip. Pull body up until chin clears bar.",
                FormCues = "Supinated grip;Full range of motion;Control descent;Engage lats",
                Category = ExerciseCategory.Main,
                PrimaryMuscle = MuscleGroup.Biceps,
                SecondaryMuscles = "Lats,UpperBack",
                Equipment = "PullUpBar",
                DifficultyLevel = 4
            },
            new()
            {
                Name = "Cable Chest Fly",
                Description = "Stand between cable columns. With arms wide, bring handles together in front of chest.",
                FormCues = "Slight bend in elbows;Squeeze chest;Control the opening;Keep shoulders down",
                Category = ExerciseCategory.Main,
                PrimaryMuscle = MuscleGroup.Chest,
                SecondaryMuscles = "Shoulders",
                Equipment = "Cable",
                DifficultyLevel = 3
            },
            new()
            {
                Name = "Cable Row",
                Description = "Sit at cable row station. Pull handle to lower chest squeezing shoulder blades.",
                FormCues = "Keep torso upright;Pull to lower chest;Squeeze at peak;Slow return",
                Category = ExerciseCategory.Main,
                PrimaryMuscle = MuscleGroup.UpperBack,
                SecondaryMuscles = "Lats,Biceps",
                Equipment = "Cable",
                DifficultyLevel = 2
            },
            new()
            {
                Name = "Cable Tricep Pushdown",
                Description = "Stand at cable column with rope attachment. Push down extending elbows fully.",
                FormCues = "Elbows at sides;Full extension;Squeeze triceps;Control the return",
                Category = ExerciseCategory.Main,
                PrimaryMuscle = MuscleGroup.Triceps,
                SecondaryMuscles = "Forearms",
                Equipment = "Cable",
                DifficultyLevel = 2
            },
            new()
            {
                Name = "Cable Bicep Curl",
                Description = "Stand facing cable column with bar or rope. Curl up bending elbows.",
                FormCues = "Keep elbows at sides;Full range of motion;Squeeze at top;Control descent",
                Category = ExerciseCategory.Main,
                PrimaryMuscle = MuscleGroup.Biceps,
                SecondaryMuscles = "Forearms",
                Equipment = "Cable",
                DifficultyLevel = 2
            },
            new()
            {
                Name = "Leg Press",
                Description = "Sit in leg press machine. Push platform away by extending legs.",
                FormCues = "Feet shoulder width;Push through heels;Don't lock knees fully;Control the negative",
                Category = ExerciseCategory.Main,
                PrimaryMuscle = MuscleGroup.Quadriceps,
                SecondaryMuscles = "Glutes,Hamstrings",
                Equipment = "Machine",
                DifficultyLevel = 2
            },
            new()
            {
                Name = "Lat Pulldown",
                Description = "Sit at lat pulldown machine. Pull bar to upper chest with wide grip.",
                FormCues = "Lean back slightly;Pull to upper chest;Squeeze lats;Control the return",
                Category = ExerciseCategory.Main,
                PrimaryMuscle = MuscleGroup.Lats,
                SecondaryMuscles = "Biceps,UpperBack",
                Equipment = "Machine",
                DifficultyLevel = 2
            },
            new()
            {
                Name = "Machine Shoulder Press",
                Description = "Sit at shoulder press machine. Press handles overhead until arms extend.",
                FormCues = "Back flat against pad;Press straight up;Don't lock elbows;Control descent",
                Category = ExerciseCategory.Main,
                PrimaryMuscle = MuscleGroup.Shoulders,
                SecondaryMuscles = "Triceps",
                Equipment = "Machine",
                DifficultyLevel = 2
            },
            new()
            {
                Name = "Leg Curl Machine",
                Description = "Lie face down on leg curl machine. Curl pad toward glutes.",
                FormCues = "Keep hips down;Full range of motion;Squeeze hamstrings;Control lowering",
                Category = ExerciseCategory.Main,
                PrimaryMuscle = MuscleGroup.Hamstrings,
                SecondaryMuscles = "Calves",
                Equipment = "Machine",
                DifficultyLevel = 2
            },
            new()
            {
                Name = "Band Squat",
                Description = "Stand on resistance band with feet shoulder-width. Hold handles at shoulders and squat.",
                FormCues = "Keep chest up;Push knees out;Full depth;Stand up against band tension",
                Category = ExerciseCategory.Main,
                PrimaryMuscle = MuscleGroup.Quadriceps,
                SecondaryMuscles = "Glutes,Hamstrings",
                Equipment = "Resistance Band",
                DifficultyLevel = 2
            },
            new()
            {
                Name = "Band Chest Press",
                Description = "Anchor band behind you at chest height. Press handles forward extending arms.",
                FormCues = "Keep core engaged;Press straight out;Squeeze chest;Control return",
                Category = ExerciseCategory.Main,
                PrimaryMuscle = MuscleGroup.Chest,
                SecondaryMuscles = "Shoulders,Triceps",
                Equipment = "Resistance Band",
                DifficultyLevel = 2
            },
            new()
            {
                Name = "Band Row",
                Description = "Anchor band at chest height. Pull handles toward body squeezing shoulder blades.",
                FormCues = "Squeeze shoulder blades;Pull to lower chest;Stand tall;Control the release",
                Category = ExerciseCategory.Main,
                PrimaryMuscle = MuscleGroup.UpperBack,
                SecondaryMuscles = "Lats,Biceps",
                Equipment = "Resistance Band",
                DifficultyLevel = 1
            },
            new()
            {
                Name = "Band Shoulder Press",
                Description = "Stand on band with feet shoulder-width. Press handles overhead.",
                FormCues = "Keep core tight;Press straight up;Don't arch back;Control descent",
                Category = ExerciseCategory.Main,
                PrimaryMuscle = MuscleGroup.Shoulders,
                SecondaryMuscles = "Triceps,Core",
                Equipment = "Resistance Band",
                DifficultyLevel = 2
            },
            new()
            {
                Name = "Band Bicep Curl",
                Description = "Stand on band. Curl handles up to shoulders bending elbows.",
                FormCues = "Keep elbows at sides;Full range;Squeeze at top;Control lowering",
                Category = ExerciseCategory.Main,
                PrimaryMuscle = MuscleGroup.Biceps,
                SecondaryMuscles = "Forearms",
                Equipment = "Resistance Band",
                DifficultyLevel = 1
            },
            new()
            {
                Name = "Dumbbell Lunge",
                Description = "Hold dumbbells at sides. Step forward into a lunge, then push back to standing.",
                FormCues = "Keep torso upright;Front knee tracks over toes;Full range of motion;Drive through front heel",
                Category = ExerciseCategory.Main,
                PrimaryMuscle = MuscleGroup.Quadriceps,
                SecondaryMuscles = "Glutes,Hamstrings",
                Equipment = "Dumbbells",
                DifficultyLevel = 3
            },
            new()
            {
                Name = "Dumbbell Lateral Raise",
                Description = "Stand with dumbbells at sides. Raise arms out to the sides to shoulder height.",
                FormCues = "Slight elbow bend;Lead with pinkies;Stop at shoulder height;Control descent",
                Category = ExerciseCategory.Main,
                PrimaryMuscle = MuscleGroup.Shoulders,
                SecondaryMuscles = "UpperBack",
                Equipment = "Dumbbells",
                DifficultyLevel = 2
            },
            new()
            {
                Name = "Dumbbell Chest Fly",
                Description = "Lie on bench with dumbbells. Open arms wide then bring together over chest.",
                FormCues = "Slight elbow bend;Stretch at bottom;Squeeze at top;Control throughout",
                Category = ExerciseCategory.Main,
                PrimaryMuscle = MuscleGroup.Chest,
                SecondaryMuscles = "Shoulders",
                Equipment = "Dumbbells,Bench",
                DifficultyLevel = 3
            },
            new()
            {
                Name = "Stability Ball Crunch",
                Description = "Lie back on stability ball with feet flat. Curl upper body toward hips.",
                FormCues = "Support head lightly;Curl spine not hip flex;Squeeze abs at top;Control descent",
                Category = ExerciseCategory.Main,
                PrimaryMuscle = MuscleGroup.Core,
                SecondaryMuscles = "Obliques",
                Equipment = "StabilityBall",
                DifficultyLevel = 2
            },
            new()
            {
                Name = "Stability Ball Hamstring Curl",
                Description = "Lie on back with heels on stability ball. Lift hips and curl ball toward glutes.",
                FormCues = "Keep hips elevated;Curl ball in;Squeeze hamstrings;Extend slowly",
                Category = ExerciseCategory.Main,
                PrimaryMuscle = MuscleGroup.Hamstrings,
                SecondaryMuscles = "Glutes,Core",
                Equipment = "StabilityBall",
                DifficultyLevel = 3
            },
            new()
            {
                Name = "Mountain Climber",
                Description = "Start in plank position. Alternate driving knees toward chest rapidly.",
                FormCues = "Keep hips level;Drive knees fully;Stay on balls of feet;Keep core tight",
                Category = ExerciseCategory.Main,
                PrimaryMuscle = MuscleGroup.Core,
                SecondaryMuscles = "HipFlexors,Shoulders",
                Equipment = "Bodyweight",
                DifficultyLevel = 2
            },
            new()
            {
                Name = "Reverse Lunge",
                Description = "From standing, step backward into a lunge then drive forward to standing.",
                FormCues = "Step far enough back;Front knee over ankle;Drive through front heel;Stay upright",
                Category = ExerciseCategory.Main,
                PrimaryMuscle = MuscleGroup.Glutes,
                SecondaryMuscles = "Quadriceps,Hamstrings",
                Equipment = "Bodyweight",
                DifficultyLevel = 2
            },
            new()
            {
                Name = "Single-Leg Romanian Deadlift",
                Description = "Stand on one leg. Hinge forward at hips extending rear leg behind you.",
                FormCues = "Keep back flat;Hinge at hip;Reach toward ground;Squeeze glute to stand",
                Category = ExerciseCategory.Main,
                PrimaryMuscle = MuscleGroup.Hamstrings,
                SecondaryMuscles = "Glutes,Core",
                Equipment = "Bodyweight",
                DifficultyLevel = 3
            },
            new()
            {
                Name = "Diamond Push-Up",
                Description = "Push-up with hands close together forming a diamond shape under chest.",
                FormCues = "Hands under chest;Keep elbows close;Full range;Keep body straight",
                Category = ExerciseCategory.Main,
                PrimaryMuscle = MuscleGroup.Triceps,
                SecondaryMuscles = "Chest,Shoulders",
                Equipment = "Bodyweight",
                DifficultyLevel = 3
            },
            new()
            {
                Name = "Superman",
                Description = "Lie face down. Simultaneously lift arms and legs off ground, hold briefly.",
                FormCues = "Lift chest and thighs;Hold 2-3 seconds;Control descent;Keep neck neutral",
                Category = ExerciseCategory.Main,
                PrimaryMuscle = MuscleGroup.LowerBack,
                SecondaryMuscles = "Glutes,UpperBack",
                Equipment = "Bodyweight",
                DifficultyLevel = 1
            },
            new()
            {
                Name = "Side Plank",
                Description = "Support body on one forearm and side of foot. Hold body in straight line.",
                FormCues = "Hips stacked;Don't let hips sag;Breathe steadily;Hold each side equally",
                Category = ExerciseCategory.Main,
                PrimaryMuscle = MuscleGroup.Obliques,
                SecondaryMuscles = "Core,Shoulders",
                Equipment = "Bodyweight",
                DifficultyLevel = 2
            },

            // ===== ADDITIONAL COOLDOWN EXERCISES =====
            new()
            {
                Name = "Upper Trap Stretch",
                Description = "Gently pull head to one side with opposite hand, stretching the upper trapezius.",
                FormCues = "Keep opposite shoulder down;Gentle pressure;Hold 30 seconds;Both sides",
                Category = ExerciseCategory.Cooldown,
                PrimaryMuscle = MuscleGroup.UpperBack,
                Equipment = "Bodyweight",
                DifficultyLevel = 1
            },
            new()
            {
                Name = "Tricep Overhead Stretch",
                Description = "Reach one arm overhead and bend elbow. Use other hand to gently press elbow back.",
                FormCues = "Keep elbow pointed up;Gentle pressure;Hold 30 seconds;Both arms",
                Category = ExerciseCategory.Cooldown,
                PrimaryMuscle = MuscleGroup.Triceps,
                Equipment = "Bodyweight",
                DifficultyLevel = 1
            },
            new()
            {
                Name = "Hip Flexor Stretch",
                Description = "Kneel on one knee. Push hips forward while keeping torso upright.",
                FormCues = "Squeeze rear glute;Keep torso tall;Hold 30 seconds;Both sides",
                Category = ExerciseCategory.Cooldown,
                PrimaryMuscle = MuscleGroup.HipFlexors,
                Equipment = "Bodyweight",
                DifficultyLevel = 1
            },
            new()
            {
                Name = "Shoulder Cross-Body Stretch",
                Description = "Pull one arm across chest with opposite hand. Hold at a comfortable stretch.",
                FormCues = "Keep shoulder down;Gentle pressure;Hold 30 seconds;Both sides",
                Category = ExerciseCategory.Cooldown,
                PrimaryMuscle = MuscleGroup.Shoulders,
                SecondaryMuscles = "UpperBack",
                Equipment = "Bodyweight",
                DifficultyLevel = 1
            }
        };
    }
}
