using IntelligentPersonalHealthOptimization.Models;
using IntelligentPersonalHealthOptimization.Models.Enums;
using SQLite;

namespace IntelligentPersonalHealthOptimization.Data;

public static partial class SeedData
{
    public static async Task SeedCesExercisesAsync(SQLiteAsyncConnection connection)
    {
        // Check if CES exercises already exist (check for a known CES exercise)
        var existing = await connection.Table<Exercise>()
            .Where(e => e.Name == "Pec SMR")
            .CountAsync();
        if (existing > 0) return;

        var exercises = GetCesExerciseLibrary();
        await connection.InsertAllAsync(exercises);
    }

    private static List<Exercise> GetCesExerciseLibrary()
    {
        return new List<Exercise>
        {
            // =================================================================
            // UPPER CROSSED SYNDROME — INHIBIT (SMR)
            // =================================================================
            new()
            {
                Name = "Pec SMR",
                Description = "Place lacrosse ball on chest near armpit against wall or doorframe. Lean into wall and hold on tender spots for 30-90 seconds per side.",
                FormCues = "Slow deep breaths;Hold tender spots 30-90 sec;Don't roll quickly;1-2 spots per side",
                Category = ExerciseCategory.Inhibit,
                PrimaryMuscle = MuscleGroup.Chest,
                Equipment = "Lacrosse Ball",
                DifficultyLevel = 1,
                CorrectsCompensations = "ArmsForward,ShoulderElevation,RoundedShoulders,Kyphosis"
            },
            new()
            {
                Name = "Upper Trap SMR",
                Description = "Stand with lacrosse ball between upper trapezius and wall. Apply pressure by leaning in. Hold on tender areas for 30-90 seconds per side.",
                FormCues = "Deep breaths throughout;Hold pressure on knots;Don't roll on neck bones;Moderate pressure",
                Category = ExerciseCategory.Inhibit,
                PrimaryMuscle = MuscleGroup.UpperTrapezius,
                Equipment = "Lacrosse Ball",
                DifficultyLevel = 1,
                CorrectsCompensations = "ShoulderElevation,ForwardHead,RoundedShoulders"
            },
            new()
            {
                Name = "Suboccipital Release",
                Description = "Lie face up, place double lacrosse ball (or two balls taped together) at base of skull. Allow head weight to provide pressure. Gently nod yes and no.",
                FormCues = "Hold 60-120 seconds;Let gravity do the work;Gentle nods only;Deep slow breaths",
                Category = ExerciseCategory.Inhibit,
                PrimaryMuscle = MuscleGroup.Suboccipitals,
                Equipment = "Lacrosse Ball",
                DifficultyLevel = 1,
                CorrectsCompensations = "ForwardHead"
            },
            new()
            {
                Name = "Lat SMR",
                Description = "Lie on side with foam roller under armpit/lat region. Slowly roll from armpit to mid-rib area. Hold tender spots.",
                FormCues = "Slow rolls 30-60 sec per side;Thumb up position;Hold tender spots 30 sec;Deep breaths",
                Category = ExerciseCategory.Inhibit,
                PrimaryMuscle = MuscleGroup.Lats,
                Equipment = "Foam Roller",
                DifficultyLevel = 1,
                CorrectsCompensations = "ArmsForward,RoundedShoulders"
            },
            new()
            {
                Name = "Levator Scapulae SMR",
                Description = "Place lacrosse ball between upper neck and top of shoulder blade against wall. Lean in and hold on tender spots.",
                FormCues = "Hold 30-90 sec per side;Moderate pressure;Deep breaths;Avoid spine directly",
                Category = ExerciseCategory.Inhibit,
                PrimaryMuscle = MuscleGroup.LevatorScapulae,
                Equipment = "Lacrosse Ball",
                DifficultyLevel = 1,
                CorrectsCompensations = "ShoulderElevation,ForwardHead"
            },

            // =================================================================
            // UPPER CROSSED SYNDROME — LENGTHEN (Stretching)
            // =================================================================
            new()
            {
                Name = "Doorway Chest Stretch",
                Description = "Stand in doorway with forearm on frame at 90 degrees. Step through until stretch is felt across chest. For pec minor, raise arm to 120 degrees.",
                FormCues = "Hold 30 sec, 2-3 sets per side;Pull shoulder blade down;Don't shrug;Breathe into stretch",
                Category = ExerciseCategory.Lengthen,
                PrimaryMuscle = MuscleGroup.Chest,
                SecondaryMuscles = "PectoralisMinor",
                Equipment = "Bodyweight",
                DifficultyLevel = 1,
                CorrectsCompensations = "ArmsForward,RoundedShoulders,Kyphosis"
            },
            new()
            {
                Name = "Upper Trap Stretch",
                Description = "Sit tall. Gently pull head laterally (ear toward shoulder) with hand. Opposite hand holds chair or reaches toward floor.",
                FormCues = "Hold 30 sec, 2-3 sets per side;Gentle pull only;Keep shoulders down;Breathe deeply",
                Category = ExerciseCategory.Lengthen,
                PrimaryMuscle = MuscleGroup.UpperTrapezius,
                Equipment = "Bodyweight",
                DifficultyLevel = 1,
                CorrectsCompensations = "ShoulderElevation,ForwardHead"
            },
            new()
            {
                Name = "Levator Scapulae Stretch",
                Description = "Sit tall. Rotate head 45 degrees to one side, then look down (chin toward armpit). Gently pull with hand on back of head.",
                FormCues = "Hold 30 sec, 2-3 sets per side;Rotate first, then look down;Gentle pressure;Don't force",
                Category = ExerciseCategory.Lengthen,
                PrimaryMuscle = MuscleGroup.LevatorScapulae,
                Equipment = "Bodyweight",
                DifficultyLevel = 1,
                CorrectsCompensations = "ShoulderElevation,ForwardHead"
            },
            new()
            {
                Name = "SCM Stretch",
                Description = "Sit tall. Extend neck (look up slightly), rotate head away from target side, and laterally flex away. Gentle stretch — never force.",
                FormCues = "Hold 30 sec, 2-3 sets per side;Very gentle;Stop if dizzy;Never force the neck",
                Category = ExerciseCategory.Lengthen,
                PrimaryMuscle = MuscleGroup.SCM,
                Equipment = "Bodyweight",
                DifficultyLevel = 1,
                CorrectsCompensations = "ForwardHead"
            },
            new()
            {
                Name = "Lat Stretch Kneeling",
                Description = "Kneel next to a bench or doorframe. Extend arm overhead onto surface. Sit hips back and allow torso to drop, feeling stretch along the side.",
                FormCues = "Hold 30 sec, 2-3 sets per side;Breathe into the stretch;Sit back to deepen",
                Category = ExerciseCategory.Lengthen,
                PrimaryMuscle = MuscleGroup.Lats,
                Equipment = "Bodyweight",
                DifficultyLevel = 1,
                CorrectsCompensations = "ArmsForward,RoundedShoulders"
            },

            // =================================================================
            // UPPER CROSSED SYNDROME — ACTIVATE
            // =================================================================
            new()
            {
                Name = "Chin Tuck",
                Description = "Sit or stand tall. Draw chin straight back (make a double chin) without tilting head. Hold 2 seconds. Focus on deep front-of-neck muscles.",
                FormCues = "10-15 reps, 2-3 sets;2 sec hold each;Slide chin back, don't tilt;Feel deep neck muscles engage",
                Category = ExerciseCategory.Activate,
                PrimaryMuscle = MuscleGroup.DeepCervicalFlexors,
                Equipment = "Bodyweight",
                DifficultyLevel = 1,
                CorrectsCompensations = "ForwardHead"
            },
            new()
            {
                Name = "Prone Y Raise",
                Description = "Lie face down. Arms extended overhead in Y position, thumbs up. Lift arms 3-6 inches. Squeeze shoulder blades down and together. Hold 2 seconds.",
                FormCues = "10-12 reps, 2-3 sets;Thumbs up;Squeeze shoulder blades DOWN;Small controlled lift",
                Category = ExerciseCategory.Activate,
                PrimaryMuscle = MuscleGroup.LowerTrapezius,
                Equipment = "Bodyweight",
                DifficultyLevel = 1,
                CorrectsCompensations = "ArmsForward,RoundedShoulders,Kyphosis,ShoulderElevation"
            },
            new()
            {
                Name = "Prone T Raise",
                Description = "Lie face down. Arms extended to sides in T position, thumbs up. Lift arms 3-6 inches, squeezing shoulder blades together. Hold 2 seconds.",
                FormCues = "10-12 reps, 2-3 sets;Thumbs up;Squeeze blades together;Don't shrug",
                Category = ExerciseCategory.Activate,
                PrimaryMuscle = MuscleGroup.MiddleTrapezius,
                SecondaryMuscles = "Rhomboids",
                Equipment = "Bodyweight",
                DifficultyLevel = 1,
                CorrectsCompensations = "ArmsForward,RoundedShoulders"
            },
            new()
            {
                Name = "Prone W Raise",
                Description = "Lie face down. Arms in W position (elbows bent 90 degrees). Lift arms while externally rotating and squeezing shoulder blades. Hold 2 seconds.",
                FormCues = "10-12 reps, 2-3 sets;External rotate at top;Squeeze blades;Controlled tempo",
                Category = ExerciseCategory.Activate,
                PrimaryMuscle = MuscleGroup.LowerTrapezius,
                SecondaryMuscles = "RotatorCuff",
                Equipment = "Bodyweight",
                DifficultyLevel = 1,
                CorrectsCompensations = "ArmsForward,RoundedShoulders,ShoulderElevation"
            },
            new()
            {
                Name = "Band Pull-Apart",
                Description = "Stand with arms extended forward holding resistance band. Pull band apart by squeezing shoulder blades together. Controlled return.",
                FormCues = "15-20 reps, 2-3 sets;Keep shoulders down;Squeeze blades at end;Slow return",
                Category = ExerciseCategory.Activate,
                PrimaryMuscle = MuscleGroup.MiddleTrapezius,
                SecondaryMuscles = "Rhomboids,RotatorCuff",
                Equipment = "Resistance Band",
                DifficultyLevel = 1,
                CorrectsCompensations = "ArmsForward,RoundedShoulders,ShoulderElevation"
            },
            new()
            {
                Name = "Wall Slide",
                Description = "Stand with back flat against wall. Arms in goalpost position, elbows and wrists touching wall. Slide arms up overhead maintaining wall contact.",
                FormCues = "10-12 reps, 2-3 sets;3 sec up, 3 sec down;Keep all contact points;Reduce range if contact lost",
                Category = ExerciseCategory.Activate,
                PrimaryMuscle = MuscleGroup.LowerTrapezius,
                SecondaryMuscles = "SerratusAnterior",
                Equipment = "Bodyweight",
                DifficultyLevel = 1,
                CorrectsCompensations = "ArmsForward,RoundedShoulders,Kyphosis,ShoulderElevation"
            },
            new()
            {
                Name = "Scapular Push-Up Plus",
                Description = "In push-up position, arms straight. Without bending elbows, push shoulder blades apart (protract) then let them come together.",
                FormCues = "12-15 reps, 2-3 sets;Arms stay straight;Focus on blade movement only;Round upper back at top",
                Category = ExerciseCategory.Activate,
                PrimaryMuscle = MuscleGroup.SerratusAnterior,
                Equipment = "Bodyweight",
                DifficultyLevel = 2,
                CorrectsCompensations = "ScapularWinging,RoundedShoulders"
            },

            // =================================================================
            // UPPER CROSSED — INTEGRATE
            // =================================================================
            new()
            {
                Name = "Cable Row with Chin Tuck",
                Description = "Perform standing row while maintaining chin tuck. Focus on scapular retraction without shoulder elevation.",
                FormCues = "12-15 reps, 2-3 sets;Chin stays tucked;Don't shrug;Light to moderate resistance",
                Category = ExerciseCategory.Integrate,
                PrimaryMuscle = MuscleGroup.UpperBack,
                SecondaryMuscles = "DeepCervicalFlexors,MiddleTrapezius",
                Equipment = "Resistance Band",
                DifficultyLevel = 2,
                CorrectsCompensations = "ForwardHead,RoundedShoulders,ShoulderElevation"
            },
            new()
            {
                Name = "Farmer's Carry",
                Description = "Carry moderate dumbbells at sides. Walk with tall posture — ears over shoulders, shoulders down and back, core braced.",
                FormCues = "30-40 sec walks, 3 sets;Ears over shoulders;Shoulders down and back;Core braced throughout",
                Category = ExerciseCategory.Integrate,
                PrimaryMuscle = MuscleGroup.Core,
                SecondaryMuscles = "UpperBack,Shoulders",
                Equipment = "Dumbbell",
                DifficultyLevel = 2,
                CorrectsCompensations = "ForwardHead,RoundedShoulders,Kyphosis"
            },

            // =================================================================
            // LOWER CROSSED SYNDROME — INHIBIT
            // =================================================================
            new()
            {
                Name = "Hip Flexor TFL SMR",
                Description = "Lie face down with foam roller on front of hip, just below and outside the hip bone. Roll slowly from hip to mid-thigh. Hold on tender areas.",
                FormCues = "Hold tender spots 30-90 sec per side;Slow rolling;Deep breaths;Avoid hip bone directly",
                Category = ExerciseCategory.Inhibit,
                PrimaryMuscle = MuscleGroup.TFL,
                SecondaryMuscles = "HipFlexors",
                Equipment = "Foam Roller",
                DifficultyLevel = 1,
                CorrectsCompensations = "ExcessiveForwardLean,LowBackArches,KneesValgus,HipDrop,AnteriorPelvicTilt"
            },
            new()
            {
                Name = "Quad SMR",
                Description = "Lie face down with foam roller under thighs. Roll from hip to just above knee. Pause on tender spots.",
                FormCues = "30-60 sec per side;Hold tender spots 30 sec;Avoid kneecap;Can cross legs for pressure",
                Category = ExerciseCategory.Inhibit,
                PrimaryMuscle = MuscleGroup.Quadriceps,
                Equipment = "Foam Roller",
                DifficultyLevel = 1,
                CorrectsCompensations = "LowBackArches,AnteriorPelvicTilt,KneeHyperextension"
            },
            new()
            {
                Name = "Lumbar Erector SMR",
                Description = "Place double lacrosse ball on each side of spine in lumbar region. Lie on it and gently shift weight. Stay on muscle, never directly on spine.",
                FormCues = "Hold 30-60 sec per spot;Never on spine bones;Gentle pressure;Stay in muscle tissue",
                Category = ExerciseCategory.Inhibit,
                PrimaryMuscle = MuscleGroup.ErectorSpinae,
                Equipment = "Lacrosse Ball",
                DifficultyLevel = 1,
                CorrectsCompensations = "LowBackArches,AnteriorPelvicTilt,Lordosis"
            },
            new()
            {
                Name = "Adductor SMR",
                Description = "Lie face down with inner thigh on foam roller. Roll from groin toward knee. Hold on tender spots.",
                FormCues = "Hold tender spots 30-90 sec per side;Gentle pressure near groin;Deep breaths;Slow rolls",
                Category = ExerciseCategory.Inhibit,
                PrimaryMuscle = MuscleGroup.Adductors,
                Equipment = "Foam Roller",
                DifficultyLevel = 1,
                CorrectsCompensations = "KneesValgus,HipDrop"
            },

            // =================================================================
            // LOWER CROSSED — LENGTHEN
            // =================================================================
            new()
            {
                Name = "Kneeling Hip Flexor Stretch",
                Description = "Half-kneeling position. Squeeze glute on kneeling side and shift hips forward. Keep torso upright.",
                FormCues = "Hold 30 sec, 2-3 sets per side;Squeeze back glute first;Stay upright;Feel stretch deep in front of hip",
                Category = ExerciseCategory.Lengthen,
                PrimaryMuscle = MuscleGroup.HipFlexors,
                Equipment = "Bodyweight",
                DifficultyLevel = 1,
                CorrectsCompensations = "ExcessiveForwardLean,LowBackArches,AnteriorPelvicTilt"
            },
            new()
            {
                Name = "Rectus Femoris Stretch",
                Description = "Half-kneeling position. Grab rear foot and pull heel toward glute for added quad stretch. Use wall for balance.",
                FormCues = "Hold 30 sec, 2-3 sets per side;Keep torso upright;Wall for balance if needed",
                Category = ExerciseCategory.Lengthen,
                PrimaryMuscle = MuscleGroup.Quadriceps,
                Equipment = "Bodyweight",
                DifficultyLevel = 1,
                CorrectsCompensations = "LowBackArches,AnteriorPelvicTilt"
            },
            new()
            {
                Name = "Standing TFL Stretch",
                Description = "Stand with one leg crossed behind the other. Push hip laterally. Reach arm overhead toward the opposite side.",
                FormCues = "Hold 30 sec, 2-3 sets per side;Push hip away from rear leg;Reach overhead for deeper stretch",
                Category = ExerciseCategory.Lengthen,
                PrimaryMuscle = MuscleGroup.TFL,
                SecondaryMuscles = "ITBand",
                Equipment = "Bodyweight",
                DifficultyLevel = 1,
                CorrectsCompensations = "KneesValgus,HipDrop"
            },

            // =================================================================
            // LOWER CROSSED — ACTIVATE
            // =================================================================
            new()
            {
                Name = "CES Glute Bridge",
                Description = "Lie on back, knees bent, feet flat. Push through heels, squeeze glutes, lift hips. Hold 5 seconds at top. Stop when hips align with knees and shoulders.",
                FormCues = "15 reps, 2-3 sets, 5 sec hold;Push through heels;Squeeze glutes HARD;Don't hyperextend back",
                Category = ExerciseCategory.Activate,
                PrimaryMuscle = MuscleGroup.Glutes,
                Equipment = "Bodyweight",
                DifficultyLevel = 1,
                CorrectsCompensations = "ExcessiveForwardLean,LowBackArches,AnteriorPelvicTilt,HipDrop,KneesValgus"
            },
            new()
            {
                Name = "Dead Bug",
                Description = "Lie on back, arms toward ceiling, knees at 90 degrees. Press low back flat. Slowly extend opposite arm and leg while maintaining flat back.",
                FormCues = "8-10 reps per side, 2-3 sets;Low back stays FLAT;Exhale during extension;Slow controlled tempo",
                Category = ExerciseCategory.Activate,
                PrimaryMuscle = MuscleGroup.TransverseAbdominis,
                SecondaryMuscles = "Core,Multifidus",
                Equipment = "Bodyweight",
                DifficultyLevel = 1,
                CorrectsCompensations = "LowBackArches,AnteriorPelvicTilt,LowBackSag"
            },
            new()
            {
                Name = "CES Bird Dog",
                Description = "On hands and knees. Extend opposite arm and leg simultaneously. Keep hips level and spine neutral. Hold 5 seconds.",
                FormCues = "10 reps per side, 2-3 sets, 5 sec hold;No rotation;Hips level;Place water bottle on back to check",
                Category = ExerciseCategory.Activate,
                PrimaryMuscle = MuscleGroup.Multifidus,
                SecondaryMuscles = "Glutes,Core",
                Equipment = "Bodyweight",
                DifficultyLevel = 1,
                CorrectsCompensations = "LowBackArches,AnteriorPelvicTilt,AsymmetricShift"
            },
            new()
            {
                Name = "Pallof Press",
                Description = "Stand perpendicular to cable/band anchor at chest height. Hold handle at chest, press straight out. Resist rotation. Hold 2 seconds.",
                FormCues = "10-12 reps per side, 2-3 sets;Resist rotation;Press straight out;Core braced throughout",
                Category = ExerciseCategory.Activate,
                PrimaryMuscle = MuscleGroup.Obliques,
                SecondaryMuscles = "TransverseAbdominis,Core",
                Equipment = "Resistance Band",
                DifficultyLevel = 2,
                CorrectsCompensations = "LowBackArches,AsymmetricShift,SpinalDeviation"
            },
            new()
            {
                Name = "Prone Hip Extension",
                Description = "Lie face down. Squeeze one glute and lift leg 6 inches off floor without rotating pelvis or arching low back. Hold 2 seconds.",
                FormCues = "12-15 reps per side, 2-3 sets;Squeeze glute first;Don't arch back;Small controlled lift",
                Category = ExerciseCategory.Activate,
                PrimaryMuscle = MuscleGroup.Glutes,
                Equipment = "Bodyweight",
                DifficultyLevel = 1,
                CorrectsCompensations = "ExcessiveForwardLean,LowBackArches,AnteriorPelvicTilt"
            },

            // =================================================================
            // LOWER CROSSED — INTEGRATE
            // =================================================================
            new()
            {
                Name = "Goblet Squat with Pause",
                Description = "Hold kettlebell/dumbbell at chest. Squat to parallel, pause 3 seconds. Chest up, core braced, knees over toes.",
                FormCues = "10-12 reps, 2-3 sets, 3 sec pause;Drive through heels;Squeeze glutes at top;Chest stays up",
                Category = ExerciseCategory.Integrate,
                PrimaryMuscle = MuscleGroup.Glutes,
                SecondaryMuscles = "Quadriceps,Core",
                Equipment = "Dumbbell",
                DifficultyLevel = 2,
                CorrectsCompensations = "ExcessiveForwardLean,LowBackArches,KneesValgus,AnteriorPelvicTilt"
            },
            new()
            {
                Name = "Step-Up to Balance",
                Description = "Step onto 8-12 inch box. Drive through heel, stand tall at top, balance 2 seconds on stance leg. Step down with control.",
                FormCues = "10 reps per side, 2-3 sets;Drive through heel;No push-off from trail leg;Balance at top",
                Category = ExerciseCategory.Integrate,
                PrimaryMuscle = MuscleGroup.Glutes,
                SecondaryMuscles = "Quadriceps,Core",
                Equipment = "Bodyweight",
                DifficultyLevel = 2,
                CorrectsCompensations = "HipDrop,KneesValgus,ExcessiveForwardLean"
            },
            new()
            {
                Name = "Hip Hinge RDL",
                Description = "Hold light dumbbells. Hinge at hips pushing them backward, slight knee bend, flat back. Squeeze glutes to stand.",
                FormCues = "10-12 reps, 2-3 sets;Push hips BACK;Flat back throughout;Feel hamstring stretch at bottom",
                Category = ExerciseCategory.Integrate,
                PrimaryMuscle = MuscleGroup.Glutes,
                SecondaryMuscles = "Hamstrings,ErectorSpinae",
                Equipment = "Dumbbell",
                DifficultyLevel = 2,
                CorrectsCompensations = "ExcessiveForwardLean,LowBackArches,AnteriorPelvicTilt"
            },

            // =================================================================
            // PRONATION DISTORTION — INHIBIT
            // =================================================================
            new()
            {
                Name = "Peroneal SMR",
                Description = "Sit with outer lower leg on foam roller. Roll slowly from below the knee to above the ankle. Hold on tender spots.",
                FormCues = "Hold tender spots 30-90 sec per side;Outer shin area;Moderate pressure;Slow rolling",
                Category = ExerciseCategory.Inhibit,
                PrimaryMuscle = MuscleGroup.Peroneals,
                Equipment = "Foam Roller",
                DifficultyLevel = 1,
                CorrectsCompensations = "FeetFlatten,AnklePronation"
            },
            new()
            {
                Name = "Lateral Calf SMR",
                Description = "Sit with calves on foam roller. Cross one leg for pressure. Roll from behind knee to above Achilles. Focus on outer calf.",
                FormCues = "Hold tender spots 30-90 sec per side;Cross legs for pressure;Focus outer calf;Deep breaths",
                Category = ExerciseCategory.Inhibit,
                PrimaryMuscle = MuscleGroup.Calves,
                Equipment = "Foam Roller",
                DifficultyLevel = 1,
                CorrectsCompensations = "FeetFlatten,FeetTurnOut,KneesValgus,AnklePronation"
            },
            new()
            {
                Name = "TFL IT Band SMR",
                Description = "Lie on side with foam roller under outer thigh. Roll from hip to just above knee. Focus on TFL (just below front hip bone).",
                FormCues = "Slow rolls 30-60 sec per side;Hold tender spots;Focus on TFL at hip;Avoid rolling on knee",
                Category = ExerciseCategory.Inhibit,
                PrimaryMuscle = MuscleGroup.TFL,
                SecondaryMuscles = "ITBand",
                Equipment = "Foam Roller",
                DifficultyLevel = 1,
                CorrectsCompensations = "KneesValgus,HipDrop"
            },
            new()
            {
                Name = "Biceps Femoris SMR",
                Description = "Sit on foam roller with weight shifted to outer hamstring. Roll from sit bone to above knee.",
                FormCues = "Hold tender spots 30-90 sec per side;Outer hamstring focus;Moderate pressure",
                Category = ExerciseCategory.Inhibit,
                PrimaryMuscle = MuscleGroup.Hamstrings,
                Equipment = "Foam Roller",
                DifficultyLevel = 1,
                CorrectsCompensations = "FeetTurnOut,KneesVarus"
            },

            // =================================================================
            // PRONATION DISTORTION — LENGTHEN
            // =================================================================
            new()
            {
                Name = "Standing Calf Stretch Gastrocnemius",
                Description = "Stand facing wall, one foot back with heel down and leg straight. Lean into wall until calf stretch is felt. Keep heel flat.",
                FormCues = "Hold 30 sec, 2-3 sets per side;Back leg straight;Heel stays flat;Lean gently into wall",
                Category = ExerciseCategory.Lengthen,
                PrimaryMuscle = MuscleGroup.Calves,
                Equipment = "Bodyweight",
                DifficultyLevel = 1,
                CorrectsCompensations = "FeetTurnOut,FeetFlatten,AnklePronation"
            },
            new()
            {
                Name = "Soleus Stretch",
                Description = "Same wall position as calf stretch but bend the rear knee slightly while keeping heel down. Stretch shifts to deeper soleus.",
                FormCues = "Hold 30 sec, 2-3 sets per side;Bend rear knee;Heel stays DOWN;Feel deeper calf stretch",
                Category = ExerciseCategory.Lengthen,
                PrimaryMuscle = MuscleGroup.Calves,
                Equipment = "Bodyweight",
                DifficultyLevel = 1,
                CorrectsCompensations = "FeetTurnOut,FeetFlatten,KneeHyperextension"
            },
            new()
            {
                Name = "Standing Adductor Stretch",
                Description = "Wide stance. Shift weight to one side, bending that knee, keeping the other leg straight. Feel stretch along inner thigh.",
                FormCues = "Hold 30 sec, 2-3 sets per side;Keep straight leg straight;Don't lean forward;Breathe into stretch",
                Category = ExerciseCategory.Lengthen,
                PrimaryMuscle = MuscleGroup.Adductors,
                Equipment = "Bodyweight",
                DifficultyLevel = 1,
                CorrectsCompensations = "KneesValgus"
            },

            // =================================================================
            // PRONATION DISTORTION — ACTIVATE
            // =================================================================
            new()
            {
                Name = "Short Foot Exercise",
                Description = "Stand barefoot. Without curling toes, shorten foot by pulling arch up (ball of foot toward heel). Hold 5 seconds.",
                FormCues = "10-15 reps per foot, 2-3 sets, 5 sec hold;Don't curl toes;Arch should visibly lift;Subtle movement",
                Category = ExerciseCategory.Activate,
                PrimaryMuscle = MuscleGroup.IntrinsicFoot,
                Equipment = "Bodyweight",
                DifficultyLevel = 1,
                CorrectsCompensations = "FeetFlatten,AnklePronation"
            },
            new()
            {
                Name = "Tibialis Posterior Activation",
                Description = "Sit with resistance band looped around forefoot. Plantarflex and invert foot (point toes down and inward) against resistance.",
                FormCues = "15-20 reps per side, 2-3 sets;Point and invert;2 sec hold at end;Slow controlled reps",
                Category = ExerciseCategory.Activate,
                PrimaryMuscle = MuscleGroup.TibialPosterior,
                Equipment = "Resistance Band",
                DifficultyLevel = 1,
                CorrectsCompensations = "FeetFlatten,AnklePronation"
            },
            new()
            {
                Name = "Side-Lying Clamshell",
                Description = "Lie on side, knees bent 90 degrees, feet together. Open top knee like a clamshell without rolling hips back. Hold 2 seconds at top.",
                FormCues = "15-20 reps per side, 2-3 sets;Keep feet touching;Don't roll hips back;Feel outer hip burn",
                Category = ExerciseCategory.Activate,
                PrimaryMuscle = MuscleGroup.Abductors,
                Equipment = "Bodyweight",
                DifficultyLevel = 1,
                CorrectsCompensations = "KneesValgus,HipDrop,AnklePronation"
            },
            new()
            {
                Name = "Lateral Band Walk",
                Description = "Place mini-band above knees or around ankles. Slight squat position. Step sideways maintaining tension.",
                FormCues = "15 steps each direction, 2-3 sets;Stay in squat;Don't let knees cave;Toes forward",
                Category = ExerciseCategory.Activate,
                PrimaryMuscle = MuscleGroup.Abductors,
                SecondaryMuscles = "Glutes",
                Equipment = "Resistance Band",
                DifficultyLevel = 1,
                CorrectsCompensations = "KneesValgus,HipDrop,AnklePronation"
            },
            new()
            {
                Name = "Terminal Knee Extension",
                Description = "Loop band behind knee, anchor in front. Start with slight knee bend. Extend knee fully against resistance, squeezing VMO.",
                FormCues = "15-20 reps per side, 2-3 sets;Squeeze inner quad;Hold 2 sec at full extension;Slow and controlled",
                Category = ExerciseCategory.Activate,
                PrimaryMuscle = MuscleGroup.Quadriceps,
                Equipment = "Resistance Band",
                DifficultyLevel = 1,
                CorrectsCompensations = "KneesValgus"
            },

            // =================================================================
            // PRONATION DISTORTION — INTEGRATE
            // =================================================================
            new()
            {
                Name = "Single-Leg Balance",
                Description = "Stand on one leg on foam pad or pillow. Maintain arch position. Keep knee aligned over 2nd-3rd toe.",
                FormCues = "30 sec per leg, 3 sets;Don't let foot flatten;Knee over toes;Progress to eyes closed",
                Category = ExerciseCategory.Integrate,
                PrimaryMuscle = MuscleGroup.Glutes,
                SecondaryMuscles = "IntrinsicFoot,TibialPosterior,Core",
                Equipment = "Bodyweight",
                DifficultyLevel = 1,
                CorrectsCompensations = "FeetFlatten,AnklePronation,KneesValgus,HipDrop"
            },
            new()
            {
                Name = "Squat with Mini-Band",
                Description = "Place mini-band above knees. Squat to comfortable depth while actively pressing knees outward against band.",
                FormCues = "12-15 reps, 2-3 sets;Push knees OUT against band;Maintain foot arch;Focus on glutes",
                Category = ExerciseCategory.Integrate,
                PrimaryMuscle = MuscleGroup.Glutes,
                SecondaryMuscles = "Quadriceps,Core,Abductors",
                Equipment = "Resistance Band",
                DifficultyLevel = 2,
                CorrectsCompensations = "KneesValgus,FeetFlatten,AnklePronation"
            },
            new()
            {
                Name = "Step-Down with Alignment",
                Description = "Stand on 6-8 inch box. Slowly lower opposite foot toward floor. Keep stance knee tracking over 2nd-3rd toe.",
                FormCues = "8-10 reps per side, 2-3 sets;Slow controlled descent;Knee tracks over toes;Don't let knee cave",
                Category = ExerciseCategory.Integrate,
                PrimaryMuscle = MuscleGroup.Glutes,
                SecondaryMuscles = "Quadriceps,Core",
                Equipment = "Bodyweight",
                DifficultyLevel = 2,
                CorrectsCompensations = "KneesValgus,HipDrop"
            },

            // =================================================================
            // THORACIC MOBILITY — INHIBIT + LENGTHEN
            // =================================================================
            new()
            {
                Name = "Thoracic Spine SMR",
                Description = "Lie on foam roller placed horizontally across mid-back. Support head with hands. Roll slowly from mid-back to upper back.",
                FormCues = "Slow rolls 60 sec;Hold tender spots 30 sec;Never roll into neck;Support head throughout",
                Category = ExerciseCategory.Inhibit,
                PrimaryMuscle = MuscleGroup.UpperBack,
                Equipment = "Foam Roller",
                DifficultyLevel = 1,
                CorrectsCompensations = "Kyphosis,ArmsForward,RoundedShoulders"
            },
            new()
            {
                Name = "Thoracic Extension over Roller",
                Description = "Lie on foam roller horizontal across mid-back. Support head, let upper back extend over roller. Move roller to different segments.",
                FormCues = "10-15 reps, 2-3 sets;5 reps per segment;Breathe and relax;Upper, mid, lower thoracic",
                Category = ExerciseCategory.Lengthen,
                PrimaryMuscle = MuscleGroup.UpperBack,
                Equipment = "Foam Roller",
                DifficultyLevel = 1,
                CorrectsCompensations = "Kyphosis,ArmsForward,RoundedShoulders"
            },
            new()
            {
                Name = "Open Book Rotation",
                Description = "Side-lying, knees stacked and bent 90 degrees. Top arm reaches forward then rotates open toward ceiling. Follow hand with eyes.",
                FormCues = "8-10 reps per side, 2-3 sets;Keep knees stacked;Rotation from thoracic, not lumbar;Hold open 2-3 sec",
                Category = ExerciseCategory.Lengthen,
                PrimaryMuscle = MuscleGroup.UpperBack,
                Equipment = "Bodyweight",
                DifficultyLevel = 1,
                CorrectsCompensations = "Kyphosis,RoundedShoulders"
            },

            // =================================================================
            // ANKLE DORSIFLEXION — LENGTHEN
            // =================================================================
            new()
            {
                Name = "Wall Ankle Mobilization",
                Description = "Face wall, foot 4-6 inches away. Drive knee forward over toes toward wall, keeping heel down. Knee should touch wall.",
                FormCues = "15-20 reps per side, 2-3 sets;Heel stays flat;Drive knee forward;Move foot back to progress",
                Category = ExerciseCategory.Lengthen,
                PrimaryMuscle = MuscleGroup.Calves,
                Equipment = "Bodyweight",
                DifficultyLevel = 1,
                CorrectsCompensations = "FeetTurnOut,FeetFlatten,ExcessiveForwardLean"
            },

            // =================================================================
            // ADDITIONAL ACTIVATE EXERCISES
            // =================================================================
            new()
            {
                Name = "Single-Leg Glute Bridge",
                Description = "Same as glute bridge but one leg extended. Keep hips level — do not let non-working side drop.",
                FormCues = "10-12 reps per side, 2-3 sets, 3 sec hold;Hips stay level;Push through heel;Squeeze glute hard",
                Category = ExerciseCategory.Activate,
                PrimaryMuscle = MuscleGroup.Glutes,
                Equipment = "Bodyweight",
                DifficultyLevel = 2,
                CorrectsCompensations = "HipDrop,KneesValgus,ExcessiveForwardLean"
            },
            new()
            {
                Name = "Tibialis Anterior Raise",
                Description = "Stand with back against wall. Lift toes and forefeet off ground (dorsiflexion). Hold 2 seconds. Slow lower.",
                FormCues = "15-20 reps, 2-3 sets;Lean back into wall;Lift toes as high as possible;Slow controlled movement",
                Category = ExerciseCategory.Activate,
                PrimaryMuscle = MuscleGroup.TibialisAnterior,
                Equipment = "Bodyweight",
                DifficultyLevel = 1,
                CorrectsCompensations = "FeetTurnOut,ExcessiveForwardLean"
            },
            new()
            {
                Name = "Prone Superman",
                Description = "Lie face down, arms extended overhead. Simultaneously lift arms, chest, and legs 2-3 inches off floor. Hold 3 seconds.",
                FormCues = "10-12 reps, 2-3 sets, 3 sec hold;Small controlled lifts;Don't hyperextend;Focus on erectors",
                Category = ExerciseCategory.Activate,
                PrimaryMuscle = MuscleGroup.ErectorSpinae,
                SecondaryMuscles = "Glutes,LowerBack",
                Equipment = "Bodyweight",
                DifficultyLevel = 1,
                CorrectsCompensations = "FlatBack,PosteriorPelvicTilt"
            },
            new()
            {
                Name = "Standing Hip Flexor March",
                Description = "Stand tall. Drive one knee up toward chest against resistance. Controlled lower. Focus on deep hip flexor engagement.",
                FormCues = "12-15 reps per side, 2-3 sets;Stand tall;Controlled tempo;Feel deep front-of-hip muscles",
                Category = ExerciseCategory.Activate,
                PrimaryMuscle = MuscleGroup.HipFlexors,
                Equipment = "Bodyweight",
                DifficultyLevel = 1,
                CorrectsCompensations = "FlatBack,PosteriorPelvicTilt"
            },
            new()
            {
                Name = "Prone Hamstring Curl",
                Description = "Lie face down with band around ankles. Curl heels toward glutes. Hold 2 seconds at full bend.",
                FormCues = "15-20 reps, 2-3 sets, 2 sec hold;Slow controlled curls;Squeeze hamstrings at top;Don't lift hips",
                Category = ExerciseCategory.Activate,
                PrimaryMuscle = MuscleGroup.Hamstrings,
                Equipment = "Resistance Band",
                DifficultyLevel = 1,
                CorrectsCompensations = "KneeHyperextension"
            },
            new()
            {
                Name = "Adductor Squeeze",
                Description = "Lie on back, knees bent. Place pillow or ball between knees. Squeeze firmly. Hold 5 seconds.",
                FormCues = "15-20 reps, 2-3 sets, 5 sec hold;Squeeze firmly;Keep breathing;Feel inner thighs engage",
                Category = ExerciseCategory.Activate,
                PrimaryMuscle = MuscleGroup.Adductors,
                Equipment = "Bodyweight",
                DifficultyLevel = 1,
                CorrectsCompensations = "KneesVarus"
            },

            // =================================================================
            // ADDITIONAL INTEGRATE EXERCISES
            // =================================================================
            new()
            {
                Name = "Single-Leg Deadlift",
                Description = "Stand on one leg. Hinge at hip, reaching opposite leg behind. Keep hips square. Squeeze glute to return.",
                FormCues = "8-10 reps per side, 2-3 sets;Hips stay square;Flat back;Bodyweight or light dumbbell",
                Category = ExerciseCategory.Integrate,
                PrimaryMuscle = MuscleGroup.Glutes,
                SecondaryMuscles = "Hamstrings,Core",
                Equipment = "Bodyweight",
                DifficultyLevel = 2,
                CorrectsCompensations = "HipDrop,ExcessiveForwardLean,AnteriorPelvicTilt"
            },
            new()
            {
                Name = "Walking Lunge with Alignment",
                Description = "Perform walking lunges with controlled tempo. At each step: maintain foot arch, keep front knee over 2nd-3rd toe.",
                FormCues = "10 reps per leg, 2-3 sets;3 sec descent;Knee over toes;Don't let knee cave",
                Category = ExerciseCategory.Integrate,
                PrimaryMuscle = MuscleGroup.Glutes,
                SecondaryMuscles = "Quadriceps,Core",
                Equipment = "Bodyweight",
                DifficultyLevel = 2,
                CorrectsCompensations = "KneesValgus,FeetFlatten,HipDrop"
            },
            new()
            {
                Name = "Lateral Lunge",
                Description = "Step wide to one side, bend that knee, keep opposite leg straight. Push back to start.",
                FormCues = "10 reps per side, 2-3 sets;Keep straight leg straight;Knee tracks over toes;Push through heel to return",
                Category = ExerciseCategory.Integrate,
                PrimaryMuscle = MuscleGroup.Adductors,
                SecondaryMuscles = "Glutes,Quadriceps",
                Equipment = "Bodyweight",
                DifficultyLevel = 2,
                CorrectsCompensations = "KneesVarus"
            },

            // =================================================================
            // PIRIFORMIS — INHIBIT + LENGTHEN
            // =================================================================
            new()
            {
                Name = "Piriformis SMR",
                Description = "Sit on lacrosse ball with one glute. Cross ankle over opposite knee. Roll to find tender spots and hold.",
                FormCues = "Hold 30-90 sec per side;Cross leg to access piriformis;Moderate pressure;Deep breaths",
                Category = ExerciseCategory.Inhibit,
                PrimaryMuscle = MuscleGroup.Piriformis,
                Equipment = "Lacrosse Ball",
                DifficultyLevel = 1,
                CorrectsCompensations = "FeetTurnOut,KneesVarus"
            },
            new()
            {
                Name = "Supine Piriformis Stretch",
                Description = "Lie on back. Cross one ankle over opposite knee. Pull uncrossed leg toward chest until stretch is felt in the crossed leg's glute.",
                FormCues = "Hold 30 sec, 2-3 sets per side;Pull gently;Feel stretch in deep glute;Keep head on floor",
                Category = ExerciseCategory.Lengthen,
                PrimaryMuscle = MuscleGroup.Piriformis,
                Equipment = "Bodyweight",
                DifficultyLevel = 1,
                CorrectsCompensations = "FeetTurnOut,KneesVarus"
            }
        };
    }
}
