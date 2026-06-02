"""
Line-for-line Python port of the app's actual calorie calculation code in
  Services/Implementation/NutritionService.cs
  (CalculateBMR, CalculateTDEE, CalculateAssessmentTargets)

If this Python output matches the Excel + Calculator.net for the same inputs,
the app code is correct. Any discrepancy in the running app must then be from
WRONG INPUTS stored in the DB (e.g., ActivityLevel set wrong in onboarding).

Run:  python Tools/verify_app_calc.py
"""

import math
from enum import Enum


# ===========================================================================
# Enums mirror Models/Enums/* exactly
# ===========================================================================
class Gender(Enum):
    Male = 0
    Female = 1


class ActivityLevel(Enum):
    Sedentary = 0
    LightlyActive = 1
    ModeratelyActive = 2
    VeryActive = 3
    ExtremelyActive = 4


class PrimaryNutritionGoal(Enum):
    LoseWeight = 0
    LoseBodyFat = 1
    BodyRecomposition = 2
    PrepareForCompetition = 3
    MaintainWeight = 4
    ImproveEndurance = 5
    IncreaseEnergy = 6
    ImproveAthletePerformance = 7
    BuildLeanMuscle = 8
    AggressiveMuscleGain = 9


class GoalTimeline(Enum):
    SixWeeks = 0
    EightWeeks = 1
    TwelveWeeks = 2
    SixMonths = 3


class DietType(Enum):
    Standard = 0
    Vegetarian = 1
    Vegan = 2
    Pescatarian = 3
    GlutenFree = 4
    DairyFree = 5
    Halal = 6
    Kosher = 7
    Keto = 8
    Paleo = 9
    Mediterranean = 10


# ===========================================================================
# Port of NutritionService.cs lines 21-275 — EXACT copy of the C# logic
# ===========================================================================

def calculate_bmr(weight_kg, height_cm, age, gender):
    """NutritionService.CalculateBMR — Mifflin-St Jeor."""
    bmr = 10 * weight_kg + 6.25 * height_cm - 5 * age
    return bmr + 5 if gender == Gender.Male else bmr - 161


def calculate_tdee(bmr, activity_level):
    """NutritionService.CalculateTDEE — Harris-Benedict activity multiplier."""
    factor = {
        ActivityLevel.Sedentary:        1.2,
        ActivityLevel.LightlyActive:    1.375,
        ActivityLevel.ModeratelyActive: 1.55,
        ActivityLevel.VeryActive:       1.725,
        ActivityLevel.ExtremelyActive:  1.9,
    }.get(activity_level, 1.55)
    return bmr * factor


def calculate_assessment_targets(
    tdee, goal, diet_type, gender,
    current_weight_kg=0, target_weight_kg=0,
    timeline=GoalTimeline.TwelveWeeks,
    workouts_per_week=0, avg_workout_minutes=0,
    focus_areas=None, confidence_level=5, readiness_score=5.0,
):
    """NutritionService.CalculateAssessmentTargets — the whole pipeline."""
    if focus_areas is None:
        focus_areas = []

    goal_default = {
        PrimaryNutritionGoal.LoseWeight:                 -500,
        PrimaryNutritionGoal.LoseBodyFat:                -500,
        PrimaryNutritionGoal.BodyRecomposition:          -250,
        PrimaryNutritionGoal.PrepareForCompetition:      -400,
        PrimaryNutritionGoal.BuildLeanMuscle:             250,
        PrimaryNutritionGoal.AggressiveMuscleGain:        500,
        PrimaryNutritionGoal.ImproveAthletePerformance:   200,
        PrimaryNutritionGoal.ImproveEndurance:            150,
        PrimaryNutritionGoal.IncreaseEnergy:              150,
        PrimaryNutritionGoal.MaintainWeight:                0,
    }.get(goal, 0)

    timeline_intensity = {
        GoalTimeline.SixWeeks:    1.3,
        GoalTimeline.EightWeeks:  1.2,
        GoalTimeline.TwelveWeeks: 1.0,
        GoalTimeline.SixMonths:   0.7,
    }.get(timeline, 1.0)

    if (target_weight_kg > 0
            and current_weight_kg > 0
            and abs(target_weight_kg - current_weight_kg) > 0.5):
        weight_diff_kg = target_weight_kg - current_weight_kg
        timeline_days = {
            GoalTimeline.SixWeeks:    42,
            GoalTimeline.EightWeeks:  56,
            GoalTimeline.TwelveWeeks: 84,
            GoalTimeline.SixMonths:   182,
        }.get(timeline, 84)
        weight_based_adjust = weight_diff_kg * 7700.0 / timeline_days
        daily_adjustment = max(-1000, min(750, weight_based_adjust * timeline_intensity))
    else:
        daily_adjustment = goal_default * timeline_intensity

    if readiness_score < 4.0:
        readiness_dampen = 0.5 + (readiness_score / 4.0) * 0.5
        daily_adjustment *= readiness_dampen

    calorie_floor = 1500 if gender == Gender.Male else 1200
    target_calories = max(tdee + daily_adjustment, calorie_floor)

    # Macro split — diet-type overrides
    if diet_type == DietType.Keto:
        protein_pct, carbs_pct, fat_pct = 0.25, 0.05, 0.70
    elif diet_type == DietType.Paleo:
        protein_pct, carbs_pct, fat_pct = 0.30, 0.25, 0.45
    elif diet_type == DietType.Mediterranean:
        protein_pct, carbs_pct, fat_pct = 0.20, 0.45, 0.35
    else:
        protein_pct, carbs_pct, fat_pct = {
            PrimaryNutritionGoal.LoseWeight:                (0.35, 0.35, 0.30),
            PrimaryNutritionGoal.LoseBodyFat:               (0.40, 0.30, 0.30),
            PrimaryNutritionGoal.BodyRecomposition:         (0.40, 0.35, 0.25),
            PrimaryNutritionGoal.PrepareForCompetition:     (0.45, 0.25, 0.30),
            PrimaryNutritionGoal.BuildLeanMuscle:           (0.30, 0.45, 0.25),
            PrimaryNutritionGoal.AggressiveMuscleGain:      (0.25, 0.50, 0.25),
            PrimaryNutritionGoal.ImproveAthletePerformance: (0.25, 0.50, 0.25),
            PrimaryNutritionGoal.ImproveEndurance:          (0.20, 0.55, 0.25),
            PrimaryNutritionGoal.IncreaseEnergy:            (0.25, 0.50, 0.25),
        }.get(goal, (0.30, 0.40, 0.30))

    # (focus_area adjustments omitted for brevity — no focus areas in user's case)

    protein_g = int(target_calories * protein_pct / 4)
    fat_g     = int(target_calories * fat_pct   / 9)

    # Body-weight-based protein floor for muscle-focused goals (g/kg)
    protein_floor_per_kg = {
        PrimaryNutritionGoal.PrepareForCompetition:    2.4,
        PrimaryNutritionGoal.LoseBodyFat:              2.0,
        PrimaryNutritionGoal.BodyRecomposition:        2.0,
        PrimaryNutritionGoal.AggressiveMuscleGain:     1.8,
        PrimaryNutritionGoal.BuildLeanMuscle:          1.8,
        PrimaryNutritionGoal.ImproveAthletePerformance: 1.6,
    }.get(goal, 0.0)

    if protein_floor_per_kg > 0 and current_weight_kg > 0:
        floor_g = int(round(protein_floor_per_kg * current_weight_kg))
        if floor_g > protein_g:
            protein_g = floor_g

    # Carbs = remaining calories after protein + fat
    remaining_kcal = target_calories - (protein_g * 4) - (fat_g * 9)
    carbs_g = max(0, int(remaining_kcal / 4))

    return int(target_calories), protein_g, carbs_g, fat_g, \
           goal_default, timeline_intensity, daily_adjustment


def run_scenario(label, *, weight_lb, height_in, age, gender, activity_level,
                 goal, target_lb, timeline, diet_type=DietType.Standard,
                 readiness_score=5.0, confidence_level=5):
    weight_kg = weight_lb / 2.20462
    height_cm = height_in * 2.54
    target_kg = target_lb / 2.20462

    bmr = calculate_bmr(weight_kg, height_cm, age, gender)
    tdee = calculate_tdee(bmr, activity_level)
    target_cal, p, c, f, goal_def, t_int, daily_adj = calculate_assessment_targets(
        tdee, goal, diet_type, gender,
        current_weight_kg=weight_kg,
        target_weight_kg=target_kg,
        timeline=timeline,
        readiness_score=readiness_score,
        confidence_level=confidence_level,
    )

    print(f"\n========= {label} =========")
    print(f"  Inputs:  Weight={weight_lb}lb ({weight_kg:.2f}kg)  Height={height_in}in ({height_cm:.2f}cm)")
    print(f"           Age={age}  Gender={gender.name}  Activity={activity_level.name}")
    print(f"           Goal={goal.name}  Target={target_lb}lb ({target_kg:.2f}kg)  Timeline={timeline.name}")
    print(f"           Diet={diet_type.name}  Readiness={readiness_score}  Confidence={confidence_level}")
    print(f"  BMR      = {bmr:.2f} kcal/day")
    print(f"  TDEE     = {tdee:.2f} kcal/day")
    print(f"  GoalDefault   = {goal_def:+d} kcal/day")
    print(f"  TimelineIntensity = {t_int}")
    print(f"  DailyAdjustment   = {daily_adj:+.2f} kcal/day  (clamped to [-1000, +750])")
    print(f"  TARGET CALORIES   = {target_cal} kcal/day")
    print(f"  Macros:  Protein={p}g  Carbs={c}g  Fat={f}g")
    print(f"           Sum check: {p*4 + c*4 + f*9} kcal (should ≈ TARGET)")


if __name__ == "__main__":
    print("=" * 70)
    print("VERIFICATION: Python port of NutritionService.cs run against the")
    print("same inputs the Excel + Calculator.net used.")
    print("=" * 70)

    # SCENARIO 1: What the user EXPECTED (matches Excel)
    run_scenario(
        "SCENARIO 1 — Excel/intended inputs (ModeratelyActive + 185lb target)",
        weight_lb=210, height_in=70, age=40, gender=Gender.Male,
        activity_level=ActivityLevel.ModeratelyActive,
        goal=PrimaryNutritionGoal.LoseWeight, target_lb=185,
        timeline=GoalTimeline.TwelveWeeks,
        diet_type=DietType.Standard, readiness_score=5.0, confidence_level=7,
    )

    # SCENARIO 2: What the app ACTUALLY has stored (matches the 2647 dashboard)
    run_scenario(
        "SCENARIO 2 — Actual stored inputs (VeryActive + 195.8lb target)",
        weight_lb=210, height_in=70, age=40, gender=Gender.Male,
        activity_level=ActivityLevel.VeryActive,
        goal=PrimaryNutritionGoal.LoseWeight, target_lb=195.8,
        timeline=GoalTimeline.TwelveWeeks,
        diet_type=DietType.Standard, readiness_score=7.5, confidence_level=5,
    )

    print("\n" + "=" * 70)
    print("EXPECTED RESULTS:")
    print("  Scenario 1 should match Excel (1896-1897) and Calculator.net (1897)")
    print("  Scenario 2 should match the app's current dashboard (2647)")
    print("=" * 70)
