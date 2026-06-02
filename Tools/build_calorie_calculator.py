"""
Build NutritionDocs/CalorieCalculator.xlsx — a comprehensive calculator that
mirrors EVERY computation the app performs in the Nutrition Assessment:

  • BMR (Mifflin-St Jeor)
  • TDEE (BMR × activity multiplier)
  • Body Fat % (US Navy formula)
  • Daily Adjustment (goal default OR weight-based, ×timeline intensity,
    ×readiness dampener, clamped)
  • Target Calories (TDEE + adjustment, floored)
  • Macro split (protein / carb / fat) per goal AND per diet type
  • Macro grams (cal × ratio / kcal-per-gram)

All inputs are in IMPERIAL (lbs, inches, ft).
"""

from openpyxl import Workbook
from openpyxl.styles import Font, PatternFill, Alignment, Border, Side
from openpyxl.utils import get_column_letter
from openpyxl.worksheet.datavalidation import DataValidation
from pathlib import Path

OUT = Path(__file__).parent.parent / "NutritionDocs" / "CalorieCalculator_v3_WithProteinFloor.xlsx"

BOLD = Font(bold=True)
H1   = Font(bold=True, size=14, color="FFFFFF")
H2   = Font(bold=True, size=12)
NOTE = Font(italic=True, color="666666")
INPUT_FILL  = PatternFill("solid", fgColor="FFF3B0")
OUTPUT_FILL = PatternFill("solid", fgColor="DDF0DD")
HIGHLIGHT_FILL = PatternFill("solid", fgColor="FFE699")
HEADER_FILL = PatternFill("solid", fgColor="1F4E78")
SUB_FILL    = PatternFill("solid", fgColor="DCE6F1")
BORDER      = Border(*(Side(border_style="thin", color="B7B7B7"),)*4)


def cell(ws, addr, value, *, font=None, fill=None, fmt=None, align=None, border=True):
    c = ws[addr]
    c.value = value
    if font:   c.font = font
    if fill:   c.fill = fill
    if fmt:    c.number_format = fmt
    if align:  c.alignment = align
    if border: c.border = BORDER
    return c


def set_widths(ws, widths):
    for i, w in enumerate(widths, start=1):
        ws.column_dimensions[get_column_letter(i)].width = w


wb = Workbook()

# ==========================================================================
# SHEET 1: Inputs (Imperial)
# ==========================================================================
ws1 = wb.active
ws1.title = "Inputs"
set_widths(ws1, [32, 16, 16, 60])

cell(ws1, "A1", "NUTRITION CALCULATOR — INPUTS (IMPERIAL)",
     font=H1, fill=HEADER_FILL, align=Alignment(horizontal="left"), border=False)
ws1.merge_cells("A1:D1")
ws1.row_dimensions[1].height = 26

cell(ws1, "A2", "Edit yellow cells only. The Calculation sheet derives everything live.",
     font=NOTE, border=False)
ws1.merge_cells("A2:D2")

# ----- USER PROFILE -----
cell(ws1, "A4", "USER PROFILE", font=H2, fill=SUB_FILL)
cell(ws1, "B4", "Value", font=H2, fill=SUB_FILL)
cell(ws1, "C4", "Metric (auto)", font=H2, fill=SUB_FILL)
cell(ws1, "D4", "Notes", font=H2, fill=SUB_FILL)

profile = [
    ("Gender (M / F)",     "M",                 "",                                  "Mifflin-St Jeor: +5 male / -161 female"),
    ("Height (feet)",      5,                   "",                                  "App field: User.HeightCm"),
    ("Height (inches)",    10,                  '=(B6*12+B7)*2.54',                  "Computed cm = (ft×12 + in) × 2.54"),
    ("Weight (lb)",        210,                 '=B8/2.20462',                       "App field: User.WeightKg"),
    ("Age (years)",        40,                  "",                                  "Computed from User.DateOfBirth"),
    ("Activity Level",     "ModeratelyActive",  "",                                  "Sedentary / LightlyActive / ModeratelyActive / VeryActive / ExtremelyActive"),
]
row = 5
for label, val, metric, note in profile:
    cell(ws1, f"A{row}", label, font=BOLD)
    cell(ws1, f"B{row}", val, fill=INPUT_FILL, align=Alignment(horizontal="center"))
    if metric:
        cell(ws1, f"C{row}", metric, fill=OUTPUT_FILL, fmt="0.0", align=Alignment(horizontal="center"))
    else:
        cell(ws1, f"C{row}", "")
    cell(ws1, f"D{row}", note, font=NOTE)
    row += 1

# ----- BODY MEASUREMENTS (for US Navy body fat %) -----
row += 1
cell(ws1, f"A{row}", "BODY MEASUREMENTS (for body-fat %)", font=H2, fill=SUB_FILL)
cell(ws1, f"B{row}", "Value (in)", font=H2, fill=SUB_FILL)
cell(ws1, f"C{row}", "cm (auto)", font=H2, fill=SUB_FILL)
cell(ws1, f"D{row}", "Notes", font=H2, fill=SUB_FILL)
row += 1

measure_start_row = row
measurements = [
    ("Neck circumference (in)",   16.0,  "Required — US Navy formula"),
    ("Waist circumference (in)",  38.0,  "Measure at navel — required"),
    ("Hips circumference (in)",   40.0,  "Females only — for US Navy formula. Males can leave."),
    ("Chest (in, optional)",      42.0,  "Tracked only; not used in calc"),
    ("Bicep (in, optional)",      14.0,  "Tracked only; not used in calc"),
    ("Thigh (in, optional)",      22.0,  "Tracked only; not used in calc"),
    ("Calf (in, optional)",       15.0,  "Tracked only; not used in calc"),
]
for label, val, note in measurements:
    cell(ws1, f"A{row}", label, font=BOLD)
    cell(ws1, f"B{row}", val, fill=INPUT_FILL, fmt="0.0", align=Alignment(horizontal="center"))
    cell(ws1, f"C{row}", f'=B{row}*2.54', fill=OUTPUT_FILL, fmt="0.0", align=Alignment(horizontal="center"))
    cell(ws1, f"D{row}", note, font=NOTE)
    row += 1

# ----- ASSESSMENT GOALS -----
row += 1
cell(ws1, f"A{row}", "ASSESSMENT GOALS", font=H2, fill=SUB_FILL)
cell(ws1, f"B{row}", "Value", font=H2, fill=SUB_FILL)
cell(ws1, f"C{row}", "Metric (auto)", font=H2, fill=SUB_FILL)
cell(ws1, f"D{row}", "Notes", font=H2, fill=SUB_FILL)
row += 1

goal_start_row = row
goals = [
    ("Primary Goal",         "LoseWeight",
        "",
        "LoseWeight, LoseBodyFat, BodyRecomposition, PrepareForCompetition, "
        "BuildLeanMuscle, AggressiveMuscleGain, ImproveAthletePerformance, "
        "ImproveEndurance, IncreaseEnergy, MaintainWeight"),
    ("Target Weight (lb)",    185,
        f'=IF(B{goal_start_row+1}=0,0,B{goal_start_row+1}/2.20462)',
        "Leave 0 to skip weight-based math (use goal default only)"),
    ("Timeline",              "TwelveWeeks", "",  "SixWeeks, EightWeeks, TwelveWeeks, SixMonths"),
    ("Diet Type",             "Standard",    "",
        "Standard, Vegetarian, Vegan, Pescatarian, GlutenFree, DairyFree, Halal, Kosher, Keto, Paleo, Mediterranean"),
    ("Workouts per week",     4,             "",  "0-7 (info only — not added to TDEE)"),
    ("Avg workout (minutes)", 45,            "",  "0-120 (info only — not added to TDEE)"),
    ("Confidence Level",      7,             "",  "1-10 — feeds Readiness calc"),
    ("Readiness Score (0-10)", 5,             "",
        "Final score from Wellness Check-In; dampens if < 4"),
]
for label, val, metric, note in goals:
    cell(ws1, f"A{row}", label, font=BOLD)
    cell(ws1, f"B{row}", val, fill=INPUT_FILL, align=Alignment(horizontal="center"))
    if metric:
        cell(ws1, f"C{row}", metric, fill=OUTPUT_FILL, fmt="0.0", align=Alignment(horizontal="center"))
    else:
        cell(ws1, f"C{row}", "")
    cell(ws1, f"D{row}", note, font=NOTE)
    row += 1

# Data validation dropdowns
def add_dd(target_cell, values):
    dv = DataValidation(type="list", formula1='"' + ",".join(values) + '"', allow_blank=False)
    ws1.add_data_validation(dv)
    dv.add(target_cell)

add_dd("B5",  ["M", "F"])
add_dd("B10", ["Sedentary","LightlyActive","ModeratelyActive","VeryActive","ExtremelyActive"])
add_dd(f"B{goal_start_row}",   ["LoseWeight","LoseBodyFat","BodyRecomposition","PrepareForCompetition",
                                "BuildLeanMuscle","AggressiveMuscleGain","ImproveAthletePerformance",
                                "ImproveEndurance","IncreaseEnergy","MaintainWeight"])
add_dd(f"B{goal_start_row+2}", ["SixWeeks","EightWeeks","TwelveWeeks","SixMonths"])
add_dd(f"B{goal_start_row+3}", ["Standard","Vegetarian","Vegan","Pescatarian","GlutenFree",
                                "DairyFree","Halal","Kosher","Keto","Paleo","Mediterranean"])

# Save the cell addresses for use in Calculation sheet
ADDR = {
    "gender":   "Inputs!B5",
    "height_ft":"Inputs!B6",
    "height_in":"Inputs!B7",
    "height_cm":"Inputs!C7",
    "weight_lb":"Inputs!B8",
    "weight_kg":"Inputs!C8",
    "age":      "Inputs!B9",
    "activity": "Inputs!B10",
    "neck_in":  f"Inputs!B{measure_start_row}",
    "neck_cm":  f"Inputs!C{measure_start_row}",
    "waist_in": f"Inputs!B{measure_start_row+1}",
    "waist_cm": f"Inputs!C{measure_start_row+1}",
    "hips_in":  f"Inputs!B{measure_start_row+2}",
    "hips_cm":  f"Inputs!C{measure_start_row+2}",
    "goal":          f"Inputs!B{goal_start_row}",
    "target_lb":     f"Inputs!B{goal_start_row+1}",
    "target_kg":     f"Inputs!C{goal_start_row+1}",
    "timeline":      f"Inputs!B{goal_start_row+2}",
    "diet":          f"Inputs!B{goal_start_row+3}",
    "workouts_pw":   f"Inputs!B{goal_start_row+4}",
    "workout_mins":  f"Inputs!B{goal_start_row+5}",
    "confidence":    f"Inputs!B{goal_start_row+6}",
    "readiness":     f"Inputs!B{goal_start_row+7}",
}


# ==========================================================================
# SHEET 2: Calculation (live formulas)
# ==========================================================================
ws2 = wb.create_sheet("Calculation")
set_widths(ws2, [38, 18, 18, 58])

cell(ws2, "A1", "LIVE CALCULATION TRACE — matches NutritionService.cs",
     font=H1, fill=HEADER_FILL, align=Alignment(horizontal="left"), border=False)
ws2.merge_cells("A1:D1")
ws2.row_dimensions[1].height = 26

cell(ws2, "A2", "All formulas mirror the app exactly. Outputs of Step 4 are what the app shows on the Dashboard.",
     font=NOTE, border=False)
ws2.merge_cells("A2:D2")

row = 4

def section(label):
    global row
    cell(ws2, f"A{row}", label, font=H2, fill=SUB_FILL)
    cell(ws2, f"B{row}", "Value", font=H2, fill=SUB_FILL)
    cell(ws2, f"C{row}", "Unit", font=H2, fill=SUB_FILL)
    cell(ws2, f"D{row}", "Formula", font=H2, fill=SUB_FILL)
    row += 1


def calc_row(label, formula, unit, note, fmt="#,##0.00", highlight=False):
    global row
    cell(ws2, f"A{row}", label, font=BOLD)
    fill = HIGHLIGHT_FILL if highlight else OUTPUT_FILL
    f_font = Font(bold=True, size=12, color="1F4E78") if highlight else None
    cell(ws2, f"B{row}", formula, fill=fill, fmt=fmt,
         align=Alignment(horizontal="center"), font=f_font)
    cell(ws2, f"C{row}", unit, font=NOTE, align=Alignment(horizontal="center"))
    cell(ws2, f"D{row}", note, font=NOTE)
    addr = f"B{row}"
    row += 1
    return addr


section("STEP 1 — BMR (Mifflin-St Jeor)")
bmr = calc_row(
    "BMR",
    f'=10*{ADDR["weight_kg"]} + 6.25*{ADDR["height_cm"]} - 5*{ADDR["age"]} + IF({ADDR["gender"]}="M",5,-161)',
    "kcal/day",
    "10·W(kg) + 6.25·H(cm) − 5·Age + (5 M / −161 F)",
)

row += 1
section("STEP 2 — TDEE (BMR × Activity Multiplier)")
act_mult = calc_row(
    "Activity Multiplier",
    f'=VLOOKUP({ADDR["activity"]},Reference!$A$5:$B$9,2,FALSE)',
    "", "Sedentary 1.20 → ExtremelyActive 1.90", fmt="0.000",
)
tdee = calc_row(
    "TDEE",
    f'={bmr}*{act_mult}',
    "kcal/day", "Energy you burn at your activity level",
)

row += 1
section("STEP 3 — Body Composition (US Navy)")
bf = calc_row(
    "Body Fat %",
    f'=IF({ADDR["gender"]}="M", '
    f'IF({ADDR["waist_in"]}<={ADDR["neck_in"]}, 0, '
    f'MAX(0, MIN(60, 86.010*LOG10({ADDR["waist_cm"]}-{ADDR["neck_cm"]}) '
    f'- 70.041*LOG10({ADDR["height_cm"]}) + 36.76))), '
    f'IF(({ADDR["waist_cm"]}+{ADDR["hips_cm"]}-{ADDR["neck_cm"]})<=0, 0, '
    f'MAX(0, MIN(60, 163.205*LOG10({ADDR["waist_cm"]}+{ADDR["hips_cm"]}-{ADDR["neck_cm"]}) '
    f'- 97.684*LOG10({ADDR["height_cm"]}) - 78.387))))',
    "%", "Male: 86.010·log10(waist−neck) − 70.041·log10(height) + 36.76\n"
         "Female: 163.205·log10(waist+hips−neck) − 97.684·log10(height) − 78.387",
    fmt="0.0",
)

row += 1
section("STEP 4 — Daily Adjustment (deficit or surplus)")
goal_def = calc_row(
    "Goal Default Offset",
    f'=VLOOKUP({ADDR["goal"]},Reference!$A$14:$B$23,2,FALSE)',
    "kcal/day", "Used if no Target Weight set (or Target ≈ Current). LoseWeight = −500",
    fmt="+#,##0;-#,##0;0",
)
ti = calc_row(
    "Timeline Intensity",
    f'=VLOOKUP({ADDR["timeline"]},Reference!$A$28:$C$31,2,FALSE)',
    "", "6wk×1.30, 8wk×1.20, 12wk×1.00, 6mo×0.70", fmt="0.00",
)
td = calc_row(
    "Timeline Days",
    f'=VLOOKUP({ADDR["timeline"]},Reference!$A$28:$C$31,3,FALSE)',
    "days", "42, 56, 84, 182", fmt="0",
)
wd = calc_row(
    "Weight Diff (Target − Current)",
    f'=IF({ADDR["target_kg"]}=0,0,{ADDR["target_kg"]}-{ADDR["weight_kg"]})',
    "kg", "Negative = lose, Positive = gain", fmt="+0.0;-0.0;0",
)
wb_adj = calc_row(
    "Weight-Based Adjustment",
    f'=IF(AND({ADDR["target_kg"]}>0, ABS({wd})>0.5), '
    f'{wd}*7700/{td}*{ti}, {goal_def}*{ti})',
    "kcal/day",
    "If Target set & differs from Current by >0.5kg: weightDiff×7700/days×intensity\nElse: GoalDefault×intensity",
    fmt="+#,##0;-#,##0;0",
)
clamped = calc_row(
    "Clamped to [−1000, +750]",
    f'=MAX(-1000, MIN(750, {wb_adj}))',
    "kcal/day", "Safety: max 1000 kcal/day deficit, max 750 kcal/day surplus",
    fmt="+#,##0;-#,##0;0",
)
rd = calc_row(
    "Readiness Dampener",
    f'=IF({ADDR["readiness"]}<4, 0.5 + ({ADDR["readiness"]}/4)*0.5, 1.0)',
    "×", "If readiness < 4, dampen deficit/surplus toward maintenance",
    fmt="0.00",
)
final_adj = calc_row(
    "Final Daily Adjustment",
    f'={clamped}*{rd}',
    "kcal/day", "After readiness dampening", fmt="+#,##0;-#,##0;0",
)

row += 1
section("STEP 5 — Target Calories")
floor = calc_row(
    "Calorie Floor",
    f'=IF({ADDR["gender"]}="M", 1500, 1200)',
    "kcal/day", "Medical minimum (USDA / IoM)", fmt="#,##0",
)
target = calc_row(
    "TARGET CALORIES (kcal/day)",
    f'=MAX({tdee}+{final_adj}, {floor})',
    "kcal/day",
    "max(TDEE + Adjustment, Floor) — THIS is the number on the Dashboard",
    fmt="#,##0", highlight=True,
)

row += 1
section("STEP 6 — Macro Split & Grams")

# protein % depends on EITHER diet type (Keto/Paleo/Mediterranean override)
# OR goal-based default.
protein_pct = calc_row(
    "Protein %",
    f'=IF({ADDR["diet"]}="Keto",0.25,'
    f'IF({ADDR["diet"]}="Paleo",0.30,'
    f'IF({ADDR["diet"]}="Mediterranean",0.20,'
    f'VLOOKUP({ADDR["goal"]},Reference!$A$36:$D$45,2,FALSE))))',
    "%", "Keto/Paleo/Mediterranean override goal-based ratios. See Reference sheet.",
    fmt="0.0%",
)
carbs_pct = calc_row(
    "Carbs %",
    f'=IF({ADDR["diet"]}="Keto",0.05,'
    f'IF({ADDR["diet"]}="Paleo",0.25,'
    f'IF({ADDR["diet"]}="Mediterranean",0.45,'
    f'VLOOKUP({ADDR["goal"]},Reference!$A$36:$D$45,3,FALSE))))',
    "%", "", fmt="0.0%",
)
fat_pct = calc_row(
    "Fat %",
    f'=IF({ADDR["diet"]}="Keto",0.70,'
    f'IF({ADDR["diet"]}="Paleo",0.45,'
    f'IF({ADDR["diet"]}="Mediterranean",0.35,'
    f'VLOOKUP({ADDR["goal"]},Reference!$A$36:$D$45,4,FALSE))))',
    "%", "", fmt="0.0%",
)

row += 1
# Body-weight-based protein floor (g/kg) — overrides %-split for muscle goals
protein_floor_per_kg = calc_row(
    "Protein Floor (g/kg) [muscle goals]",
    f'=IF({ADDR["goal"]}="PrepareForCompetition",2.4,'
    f'IF({ADDR["goal"]}="LoseBodyFat",2.0,'
    f'IF({ADDR["goal"]}="BodyRecomposition",2.0,'
    f'IF({ADDR["goal"]}="AggressiveMuscleGain",1.8,'
    f'IF({ADDR["goal"]}="BuildLeanMuscle",1.8,'
    f'IF({ADDR["goal"]}="ImproveAthletePerformance",1.6,0))))))',
    "g/kg", "ISSN floor for muscle-focused goals; 0 means use %-split only",
    fmt="0.0",
)
protein_floor_g = calc_row(
    "Protein Floor (g)",
    f'=ROUND({protein_floor_per_kg}*{ADDR["weight_kg"]},0)',
    "g/day", "= floor (g/kg) × Weight (kg)", fmt="#,##0",
)
protein_from_pct = calc_row(
    "Protein from %-split",
    f'=INT({target}*{protein_pct}/4)',
    "g/day", "target_cal × protein% ÷ 4", fmt="#,##0",
)
fat_g = calc_row(
    "Target Fat",
    f'=INT({target}*{fat_pct}/9)',
    "g/day", "target_cal × fat% ÷ 9 (fat % always honored)", fmt="#,##0", highlight=True,
)
protein_g = calc_row(
    "Target Protein (with floor)",
    f'=MAX({protein_from_pct},{protein_floor_g})',
    "g/day", "Greater of %-split protein or g/kg floor", fmt="#,##0", highlight=True,
)
carbs_g = calc_row(
    "Target Carbs (from remainder)",
    f'=MAX(0,INT(({target}-{protein_g}*4-{fat_g}*9)/4))',
    "g/day", "Remaining kcal after protein + fat, ÷ 4", fmt="#,##0", highlight=True,
)

row += 1
section("STEP 7 — Reverse-Verification")
verify = calc_row(
    "Macro Calorie Sum",
    f'={protein_g}*4 + {carbs_g}*4 + {fat_g}*9',
    "kcal/day",
    "Should match TARGET to within ±5 kcal (rounding). If off, ratios sum ≠ 1.0.",
    fmt="#,##0",
)

# Worked example callout
row += 2
cell(ws2, f"A{row}",
     "SANITY CHECK — Defaults (210 lb male, 5'10\", 40yo, ModeratelyActive, "
     "LoseWeight, 185 lb in 12 weeks, Standard diet):",
     font=BOLD)
ws2.merge_cells(f"A{row}:D{row}")
row += 1
expected = [
    ("Expected BMR",          "≈ 1,880 kcal"),
    ("Expected TDEE",         "≈ 2,914 kcal (1880 × 1.55)"),
    ("Expected Adjustment",   "≈ −946 kcal/day (−11.3 kg over 84 days × 1.0)"),
    ("Expected Target",       "≈ 1,968 kcal/day"),
    ("Expected Protein/Carbs/Fat", "≈ 172 / 172 / 66 g (35/35/30 split for LoseWeight)"),
]
for label, val in expected:
    cell(ws2, f"A{row}", label, font=NOTE)
    cell(ws2, f"B{row}", val, font=NOTE)
    row += 1


# ==========================================================================
# SHEET 3: Field Mapping
# ==========================================================================
ws3 = wb.create_sheet("Field Mapping")
set_widths(ws3, [28, 36, 28, 48])

cell(ws3, "A1", "FIELD MAPPING — App UI → Model Property → SQLite Column → Used By",
     font=H1, fill=HEADER_FILL, align=Alignment(horizontal="left"), border=False)
ws3.merge_cells("A1:D1")
ws3.row_dimensions[1].height = 26

for i, h in enumerate(["Field", "App UI Location", "Model Property (.cs)", "SQLite Column / Used By"], start=1):
    cell(ws3, f"{get_column_letter(i)}3", h, font=H2, fill=SUB_FILL)

mappings = [
    ("Gender", "Onboarding → Body Metrics", "User.Gender (enum)", "Users.Gender → BMR sign offset"),
    ("Height", "Onboarding → Body Metrics → Height slider", "User.HeightCm (double)", "Users.HeightCm → BMR"),
    ("Weight", "Onboarding → Body Metrics → Weight slider\nNutrition Assessment → Goals → Current Weight slider (new)", "User.WeightKg (double)", "Users.WeightKg → BMR, Body Fat %"),
    ("Date of Birth", "Onboarding → Body Metrics → DOB picker", "User.DateOfBirth", "Users.DateOfBirth → Age in BMR"),
    ("Activity Level", "Onboarding → Activity Level page", "User.ActivityLevel (enum)", "Users.ActivityLevel → TDEE multiplier (1.2–1.9). No post-onboarding UI to change yet."),
    ("Neck Circumference", "Nutrition Assessment → Body Measurements", "NutritionAssessment.NeckCm", "NutritionAssessments.NeckCm → US Navy BF%"),
    ("Waist Circumference", "Nutrition Assessment → Body Measurements", "NutritionAssessment.WaistCm", "NutritionAssessments.WaistCm → US Navy BF%"),
    ("Hips Circumference", "Nutrition Assessment → Body Measurements (women)", "NutritionAssessment.HipsCm", "NutritionAssessments.HipsCm → US Navy BF% (female only)"),
    ("Chest/Bicep/Thigh/Calf", "Nutrition Assessment → Body Measurements (optional)", "NutritionAssessment.ChestCm/etc.", "NutritionAssessments.{Chest,Bicep,Thigh,Calf}Cm → tracked only, not in calc"),
    ("Primary Goal", "Nutrition Assessment → Goals → Primary Goal dropdown", "NutritionAssessment.PrimaryGoal (enum)", "NutritionAssessments.PrimaryGoal → Goal Default offset + Macro split"),
    ("Target Weight", "Nutrition Assessment → Goals → Target Weight slider", "NutritionAssessment.TargetWeightKg", "NutritionAssessments.TargetWeightKg → Weight-based adjustment"),
    ("Timeline", "Nutrition Assessment → Goals → Goal Timeline", "NutritionAssessment.SelectedTimeline (enum)", "NutritionAssessments.SelectedTimeline → Timeline Intensity + Days"),
    ("Diet Type", "Nutrition Assessment → Diet Plan", "NutritionAssessment.SelectedDietType (enum)", "NutritionAssessments.SelectedDietType → Macro override (Keto/Paleo/Mediterranean)"),
    ("Workouts per Week", "Nutrition Assessment → Goals → Planned Exercise +/−", "NutritionAssessment.WorkoutsPerWeek", "NutritionAssessments.WorkoutsPerWeek → display only (not added to TDEE)"),
    ("Avg Workout Minutes", "Nutrition Assessment → Goals → Planned Exercise +/−", "NutritionAssessment.AvgWorkoutMinutes", "NutritionAssessments.AvgWorkoutMinutes → display only"),
    ("Confidence Level", "Nutrition Assessment → Motivation/Barriers", "NutritionAssessment.ConfidenceLevel", "NutritionAssessments.ConfidenceLevel → feeds Readiness"),
    ("Readiness Score", "Computed: confidence (50%) + motivation/challenges ratio (50%)", "NutritionAssessment.ReadinessScore", "NutritionAssessments.ReadinessScore → Dampener when < 4"),
    ("Daily Water Glasses", "Nutrition Assessment → Diet Plan", "NutritionAssessment.DailyWaterGlasses", "NutritionAssessments.DailyWaterGlasses → Dashboard hydration ring"),
    ("Calorie Target (dashboard)", "Nutrition Coach → top calorie ring", "NutritionDashboardViewModel.CaloriesTarget", "RECOMPUTED live each load from inputs above (was previously profile snapshot)"),
]

for i, (field, ui, model, sql) in enumerate(mappings, start=4):
    cell(ws3, f"A{i}", field, font=BOLD)
    cell(ws3, f"B{i}", ui, align=Alignment(wrap_text=True, vertical="top"))
    cell(ws3, f"C{i}", model, align=Alignment(wrap_text=True, vertical="top"))
    cell(ws3, f"D{i}", sql, align=Alignment(wrap_text=True, vertical="top"))
    ws3.row_dimensions[i].height = 36


# ==========================================================================
# SHEET 4: Reference Tables
# ==========================================================================
ws4 = wb.create_sheet("Reference")
set_widths(ws4, [32, 14, 14, 14, 50])

cell(ws4, "A1", "REFERENCE TABLES (consumed by Calculation sheet's VLOOKUPs)",
     font=H1, fill=HEADER_FILL, align=Alignment(horizontal="left"), border=False)
ws4.merge_cells("A1:E1")
ws4.row_dimensions[1].height = 26

# A5:B9 — Activity Multipliers
cell(ws4, "A4", "ACTIVITY LEVEL", font=H2, fill=SUB_FILL)
cell(ws4, "B4", "Multiplier", font=H2, fill=SUB_FILL)
cell(ws4, "E4", "Description", font=H2, fill=SUB_FILL)
activity = [
    ("Sedentary",         1.200, "Little/no exercise, desk job"),
    ("LightlyActive",     1.375, "1-3 light workouts/wk"),
    ("ModeratelyActive",  1.550, "3-5 workouts/wk"),
    ("VeryActive",        1.725, "6-7 hard workouts/wk"),
    ("ExtremelyActive",   1.900, "Physical job + daily intense training"),
]
for i, (name, mult, note) in enumerate(activity, start=5):
    cell(ws4, f"A{i}", name)
    cell(ws4, f"B{i}", mult, fmt="0.000", align=Alignment(horizontal="center"))
    cell(ws4, f"E{i}", note, font=NOTE)

# A14:B23 — Goal Defaults
cell(ws4, "A13", "PRIMARY GOAL", font=H2, fill=SUB_FILL)
cell(ws4, "B13", "Default Offset", font=H2, fill=SUB_FILL)
cell(ws4, "E13", "Used when Target Weight unset or ≈ Current", font=H2, fill=SUB_FILL)
goals_offsets = [
    ("LoseWeight",                  -500),
    ("LoseBodyFat",                 -500),
    ("PrepareForCompetition",       -400),
    ("BodyRecomposition",           -250),
    ("MaintainWeight",                 0),
    ("ImproveEndurance",            +150),
    ("IncreaseEnergy",              +150),
    ("ImproveAthletePerformance",   +200),
    ("BuildLeanMuscle",             +250),
    ("AggressiveMuscleGain",        +500),
]
for i, (name, off) in enumerate(goals_offsets, start=14):
    cell(ws4, f"A{i}", name)
    cell(ws4, f"B{i}", off, fmt="+#,##0;-#,##0;0", align=Alignment(horizontal="center"))

# A28:C31 — Timeline
cell(ws4, "A27", "TIMELINE", font=H2, fill=SUB_FILL)
cell(ws4, "B27", "Intensity", font=H2, fill=SUB_FILL)
cell(ws4, "C27", "Days", font=H2, fill=SUB_FILL)
timeline = [
    ("SixWeeks",     1.30, 42),
    ("EightWeeks",   1.20, 56),
    ("TwelveWeeks",  1.00, 84),
    ("SixMonths",    0.70, 182),
]
for i, (name, intensity, days) in enumerate(timeline, start=28):
    cell(ws4, f"A{i}", name)
    cell(ws4, f"B{i}", intensity, fmt="0.00", align=Alignment(horizontal="center"))
    cell(ws4, f"C{i}", days, fmt="0", align=Alignment(horizontal="center"))

# A36:D45 — Goal-based macro split (Protein, Carbs, Fat)
cell(ws4, "A35", "PRIMARY GOAL", font=H2, fill=SUB_FILL)
cell(ws4, "B35", "Protein %", font=H2, fill=SUB_FILL)
cell(ws4, "C35", "Carbs %", font=H2, fill=SUB_FILL)
cell(ws4, "D35", "Fat %", font=H2, fill=SUB_FILL)
cell(ws4, "E35", "Used when Diet Type = Standard / Veg / Pesc / etc. "
                  "(Keto/Paleo/Mediterranean override)", font=H2, fill=SUB_FILL)
macros = [
    ("LoseWeight",                  0.35, 0.35, 0.30),
    ("LoseBodyFat",                 0.40, 0.30, 0.30),
    ("BodyRecomposition",           0.40, 0.35, 0.25),
    ("PrepareForCompetition",       0.45, 0.25, 0.30),
    ("BuildLeanMuscle",             0.30, 0.45, 0.25),
    ("AggressiveMuscleGain",        0.25, 0.50, 0.25),
    ("ImproveAthletePerformance",   0.25, 0.50, 0.25),
    ("ImproveEndurance",            0.20, 0.55, 0.25),
    ("IncreaseEnergy",              0.25, 0.50, 0.25),
    ("MaintainWeight",              0.30, 0.40, 0.30),
]
for i, (name, p, c, f) in enumerate(macros, start=36):
    cell(ws4, f"A{i}", name)
    cell(ws4, f"B{i}", p, fmt="0.0%", align=Alignment(horizontal="center"))
    cell(ws4, f"C{i}", c, fmt="0.0%", align=Alignment(horizontal="center"))
    cell(ws4, f"D{i}", f, fmt="0.0%", align=Alignment(horizontal="center"))

cell(ws4, "A47", "DIET TYPE OVERRIDES", font=H2, fill=SUB_FILL)
cell(ws4, "B47", "Protein %", font=H2, fill=SUB_FILL)
cell(ws4, "C47", "Carbs %", font=H2, fill=SUB_FILL)
cell(ws4, "D47", "Fat %", font=H2, fill=SUB_FILL)
cell(ws4, "E47", "Diet-specific ratios that override goal-based macros", font=H2, fill=SUB_FILL)
diets = [
    ("Keto",            0.25, 0.05, 0.70),
    ("Paleo",           0.30, 0.25, 0.45),
    ("Mediterranean",   0.20, 0.45, 0.35),
]
for i, (name, p, c, f) in enumerate(diets, start=48):
    cell(ws4, f"A{i}", name)
    cell(ws4, f"B{i}", p, fmt="0.0%", align=Alignment(horizontal="center"))
    cell(ws4, f"C{i}", c, fmt="0.0%", align=Alignment(horizontal="center"))
    cell(ws4, f"D{i}", f, fmt="0.0%", align=Alignment(horizontal="center"))

cell(ws4, "A52", "All other diet types (Standard, Vegetarian, Vegan, Pescatarian, "
                  "GlutenFree, DairyFree, Halal, Kosher) use the goal-based ratios above.",
     font=NOTE, border=False)
ws4.merge_cells("A52:E52")


# ==========================================================================
# SHEET 5: Sources
# ==========================================================================
ws5 = wb.create_sheet("Sources")
set_widths(ws5, [32, 80])

cell(ws5, "A1", "FORMULA SOURCES & CITATIONS",
     font=H1, fill=HEADER_FILL, align=Alignment(horizontal="left"), border=False)
ws5.merge_cells("A1:B1")
ws5.row_dimensions[1].height = 26

src = [
    ("BMR (Mifflin-St Jeor)",
     "Mifflin, M.D., et al. (1990). 'A new predictive equation for resting energy expenditure in healthy individuals.' American Journal of Clinical Nutrition, 51(2), 241-247. Standard equation used by USDA, NIH, and the Academy of Nutrition and Dietetics. More accurate than the older Harris-Benedict for modern populations."),
    ("Activity Multipliers",
     "Standard Harris-Benedict activity factors (Roza & Shizgal 1984 revision). 1.2 sedentary → 1.9 extremely active. Identical to MyFitnessPal, Cronometer, Lose It!, Fitbit. Any sports-nutrition textbook (e.g. McArdle, Exercise Physiology)."),
    ("US Navy Body Fat Formula",
     "Hodgdon, J.A. & Beckett, M.B. (1984). 'Prediction of percent body fat for U.S. Navy men/women from body circumferences and height.' Naval Health Research Center Reports 84-11 and 84-29. Used by the US military for fitness assessment. Accuracy ±3% vs DEXA."),
    ("Caloric Deficit (500 kcal/day)",
     "Standard 1-lb/week loss rule. 3,500 kcal ≈ 1 lb of mixed body tissue; 7,700 kcal ≈ 1 kg. Recommended by the CDC, NIH, and Academy of Nutrition and Dietetics for sustainable weight loss."),
    ("Calorie Floor (1500/1200)",
     "Institute of Medicine recommended minimum daily intake without medical supervision. Adopted by USDA Dietary Guidelines. App enforces as hard floor."),
    ("Deficit clamp (−1000) / Surplus clamp (+750)",
     "Maximum safe rates from AND Position Statement on Weight Loss (max ~1 kg/wk loss, ~0.5 kg/wk lean gain). −1000 kcal/day ≈ 1 kg/wk; +750 ≈ 0.5 kg/wk."),
    ("Macro Splits — Goal-based",
     "Custom curve drawing from: ISSN Position Stand on Protein and Exercise (Jäger et al 2017), ACSM Joint Position Stand on Nutrition and Athletic Performance (2016). Higher protein for fat loss (preserve LBM), higher carbs for endurance/muscle building."),
    ("Macro Splits — Keto / Paleo / Mediterranean",
     "Diet-specific community standards: Keto 70%F/25%P/5%C from Phinney & Volek 'Art and Science of Low Carbohydrate Living'. Paleo 45%F/30%P/25%C standard reading of 'The Paleo Diet' (Cordain). Mediterranean 35%F/45%C/20%P from PREDIMED trial nutrient analysis."),
    ("7,700 kcal per kg constant",
     "Standard energy density of mixed-tissue body weight change. Comes from Wishnofsky 1958 + later validation (Hall 2008 'What is the required energy deficit per unit weight loss?'). 3,500 kcal/lb × 2.2 lb/kg ≈ 7,700."),
    ("Timeline Intensity (custom)",
     "OPINIONATED design choice: shorter timelines amplify the deficit/surplus to deliver visible results, longer timelines moderate it. NOT from any published formula. Tune as desired."),
    ("Readiness Dampener (custom)",
     "OPINIONATED design choice: low Wellness Check-In readiness (<4/10) auto-softens aggressive deficit/surplus. Reflects 'meet users where they are' coaching principle. Not from a single source."),
]
for i, (k, v) in enumerate(src, start=3):
    cell(ws5, f"A{i}", k, font=BOLD, align=Alignment(vertical="top"))
    cell(ws5, f"B{i}", v, align=Alignment(wrap_text=True, vertical="top"))
    ws5.row_dimensions[i].height = 64


OUT.parent.mkdir(parents=True, exist_ok=True)
wb.save(OUT)
print(f"Wrote {OUT}")
print(f"  Size: {OUT.stat().st_size / 1024:.1f} KB")
