# Equipment Intel Specification

**Purpose:** Define what we need to know about a user's training environment so the prescription engine can generate a workout program that is actually executable in their setting. This document is the scope contract for the workout build-out — it does not modify code.

**Author / date:** Stephan, 2026-05-16
**Status:** Draft for review

---

## Table of Contents

1. [Why This Matters](#1-why-this-matters)
2. [Current State Audit](#2-current-state-audit)
3. [Equipment Taxonomy (Expanded)](#3-equipment-taxonomy-expanded)
4. [Branching Question Flow](#4-branching-question-flow)
5. [Data Model Proposal](#5-data-model-proposal)
6. [Seed-Data Normalization](#6-seed-data-normalization)
7. [Programmatic Impact](#7-programmatic-impact)
8. [Implementation Phases](#8-implementation-phases)
9. [Open Questions](#9-open-questions)

---

## 1. Why This Matters

The prescription engine already filters exercises by equipment availability — but the inputs are coarse and the outputs are silently degraded:

- A Planet Fitness member and a CrossFit gym member both check `Barbell` and `Machine`, get the same options, but one has no squat rack and the other has six.
- A user with adjustable dumbbells (5–50 lb pairs) and a user with one pair of 10s both check `Dumbbells`. The engine prescribes the same `DumbbellRow 3×10` to both.
- A home user with low ceilings gets prescribed overhead presses they can't perform.
- A user with downstairs neighbors gets prescribed jump squats.

The fix is **richer intel at intake** and **stricter matching in the engine**. This doc defines the intel.

---

## 2. Current State Audit

### What works today

| Component | File | What it does |
|---|---|---|
| `EquipmentType` enum | [Models/Enums/EquipmentType.cs](../Models/Enums/EquipmentType.cs) | 12 generic items: Bodyweight, Dumbbells, Barbell, Bands, Kettlebell, Cable, Machine, PullUpBar, Bench, FoamRoller, YogaMat, StabilityBall |
| `TrainingLocation` enum | [Models/Enums/TrainingLocation.cs](../Models/Enums/TrainingLocation.cs) | Gym / Home / Both |
| `TrainingProfile` | [Models/TrainingProfile.cs](../Models/TrainingProfile.cs) | Stores `AvailableEquipment` as CSV string + days, duration, freq |
| Onboarding step | [Views/Onboarding/TrainingBackgroundPage.xaml](../Views/Onboarding/TrainingBackgroundPage.xaml) | Multi-select equipment chips, suggests defaults per location |
| Engine equipment filter | [Rules/PrescriptionRules/MainProgramRules.cs:97-108](../Rules/PrescriptionRules/MainProgramRules.cs#L97-L108) | `HasAvailableEquipment()` — string compare against exercise's CSV `Equipment` field |
| Exercise library | [Data/SeedData.cs](../Data/SeedData.cs) | 92 exercises seeded (53 Main / 19 Corrective / 12 Cooldown / 6 Warmup / 2 Activation) |

### What's broken / thin

**Equipment string-tag inconsistency in seed data** — `MainProgramRules.HasAvailableEquipment` is case-insensitive but exact-match. Current tags in [Data/SeedData.cs](../Data/SeedData.cs):

| Tag in seed | Enum value | Will match? |
|---|---|---|
| `"Dumbbells"` (×6) | `Dumbbells` | yes |
| `"Dumbbell"` (×1) | — | **no** (singular) |
| `"Dumbbells, Bench"` (×1) | requires both | yes (multi-arg) |
| `"Dumbbells,Bench"` (×1) | requires both | yes |
| `"Dumbbell, Bench"` (×1) | — | **no** |
| `"Resistance Band"` (×11) | `Bands` | **no** (word mismatch) |
| `"PullUpBar"` (×2) | `PullUpBar` | yes |
| `"StabilityBall"` (×2) | `StabilityBall` | yes |
| `"Wall"` (×2) | — | yes (hardcoded allow in filter) |
| `"Doorway"` (×2) | — | yes (hardcoded allow) |
| `"Step"` (×1) | — | **no** (not in allowlist) |

**Net: ~14 of 92 exercises silently never match** even when the user has the equipment.

**Taxonomy too coarse for honest prescription:**
- `Machine` collapses Smith / leg-press / lat-pulldown / chest-press / hack-squat / leg-curl — radically different exercise pools.
- `Dumbbells` says nothing about weight range or pair count.
- `Barbell` doesn't reveal whether a rack exists, whether plates go above 135 lb, or whether it's a fixed barbell row.
- `Bench` doesn't distinguish flat-only vs adjustable (incline → no incline DB press).
- No cardio equipment captured.
- No suspension trainer, plyo box, med ball, jump rope, sled, battle ropes, ab wheel.
- No band tension level.

**Missing environmental context:**
- Ceiling height (overhead pressing, jumping)
- Floor type / noise tolerance (plyo at home)
- Available floor space (deadlift setup needs ~6×4 ft clear)
- For gym users: chain/brand, peak-hours preference (affects supersets and rack availability)

**Missing equipment-location pairing for "Both":**
- A user training partly at home and partly at the gym has different equipment per day. The engine treats the union, then potentially prescribes a cable row on a home day.

---

## 3. Equipment Taxonomy (Expanded)

Proposed full inventory, grouped by category. Each line: name → what it unlocks → typical follow-up question.

### 3.1 Free Weights

| Item | Unlocks | Follow-up |
|---|---|---|
| **Adjustable dumbbells** | Most main DB lifts across phases | Max weight per dumbbell? |
| **Fixed dumbbells** | DB lifts capped at heaviest pair | Lightest / heaviest pair? Increment? |
| **Barbell (Olympic, 45 lb)** | Squat, deadlift, bench, row, press | Plate total? Has clips? |
| **Barbell (Standard, 1" sleeve)** | Light barbell work only | Plate total? |
| **Fixed barbells (preloaded)** | Pre-set weight bar work | Range? |
| **Kettlebells** | Swings, goblets, TGUs, snatches | Sizes owned? |
| **Sandbag** | Carries, loaded squats, cleans | Adjustable? Weight? |
| **Weight vest** | Loaded carries, push-ups, pull-ups | Max load? |

### 3.2 Racks & Benches

| Item | Unlocks | Follow-up |
|---|---|---|
| **Power rack / cage** | Safe heavy squat, bench, rack pull, pin work | Has J-cups + safeties? Pull-up attached? |
| **Squat stands** | Squat, overhead press (no safeties) | Adjustable height? |
| **Smith machine** | Guided squat/press substitutions | Counterbalanced? |
| **Flat bench** | Bench press, DB rows, step-ups | — |
| **Adjustable bench** | Incline/decline press, incline row, seated work | Min/max angles? |
| **Preacher / specialty bench** | Isolation arm work | — |

### 3.3 Pulling / Suspended

| Item | Unlocks | Follow-up |
|---|---|---|
| **Pull-up bar (mounted)** | Pull-ups, chin-ups, hanging knee raise | Doorway / wall-mount / power-rack-attached? |
| **Doorway pull-up bar** | Pull-up variants up to user bodyweight + ~20 lb | — |
| **TRX / suspension trainer** | Row, push, single-leg squat, fallout | Anchor height? |
| **Dip station / parallettes** | Dips, L-sits, push-up variants | — |
| **Gymnastic rings** | Advanced pull/push, ring rows | — |

### 3.4 Cable & Machine

| Item | Unlocks | Follow-up |
|---|---|---|
| **Cable column (adjustable)** | Cable row, face pull, woodchop, tricep pushdown | Single or dual? Adjustable pulley height? |
| **Cable crossover** | Crossovers, flyes, full cable workouts | — |
| **Lat pulldown** | Pulldown variants when no pull-up bar | — |
| **Seated row machine** | Horizontal pull substitute | — |
| **Leg press** | Heavy quad work without spinal load | 45° / horizontal? |
| **Hack squat** | Quad-dominant squat substitute | — |
| **Leg curl (seated/lying)** | Direct hamstring work | — |
| **Leg extension** | Direct quad work | — |
| **Chest press machine** | Bench press substitute | — |
| **Shoulder press machine** | Overhead press substitute | — |
| **Pec deck / fly machine** | Direct chest isolation | — |
| **Glute kickback / hip thrust machine** | Direct glute work | — |
| **Calf raise machine** | Loaded calf work | — |
| **Hyperextension / GHD** | Lower back, hamstring, ab development | — |

### 3.5 Bands & Bodyweight Accessories

| Item | Unlocks | Follow-up |
|---|---|---|
| **Resistance bands (loop)** | Pull-aparts, banded squat/deadlift, mobility | Tension levels owned (light/medium/heavy/extra)? |
| **Resistance bands (tube w/ handles)** | Cable substitutes for home | Levels? |
| **Mini-bands** | Glute activation, lateral walks | — |
| **Foam roller** | Self-myofascial release | — |
| **Lacrosse / massage ball** | Trigger point work | — |
| **Yoga mat** | Floor work, stretching | — |
| **Stability ball** | Anti-extension, dynamic core | Size? |
| **BOSU** | Balance, single-leg work | — |
| **Ab wheel** | Anti-extension core | — |
| **Sliders / valsides** | Hamstring curls, mountain climbers | — |

### 3.6 Plyometric / Conditioning

| Item | Unlocks | Follow-up |
|---|---|---|
| **Plyo box** | Box jumps, step-ups, depth jumps | Heights available? |
| **Step / aerobic step** | Step-ups, light plyo | — |
| **Jump rope** | Conditioning, warm-up | — |
| **Medicine ball** | Slams, throws, rotational power | Weight(s)? Wall ball or dead ball? |
| **Slam ball** | Slams (non-bouncing) | Weight(s)? |
| **Battle ropes** | Conditioning, posterior shoulder | Length? Anchored? |
| **Sled** | Loaded carries, push/drag | Surface? |
| **Tire** | Flips, sledgehammer work | — |

### 3.7 Cardio

| Item | Unlocks | Follow-up |
|---|---|---|
| **Treadmill** | Running intervals, walk warm-up | Incline %? |
| **Stationary bike (upright/recumbent)** | Steady-state, intervals | — |
| **Air bike / Assault bike** | High-intensity intervals | — |
| **Rowing machine** | Full-body conditioning, warm-up | — |
| **Elliptical** | Low-impact steady-state | — |
| **Stair climber** | Posterior-chain endurance | — |
| **Ski erg** | Upper-body pull conditioning | — |

### 3.8 Environment / Constraints (not equipment but design-critical)

| Question | Affects |
|---|---|
| Ceiling height (low / standard / high) | Overhead press, jumps, swings |
| Floor type (concrete / wood / carpet / mat) | Plyometrics, dropping weights |
| Noise tolerance (apartment / house / shared) | Plyometrics, dropped barbells, grunting |
| Floor space (sq ft clear) | Deadlift setup, broad jumps, sled |
| Outdoor access (yard / park / trail) | Sprints, sled work, hill running |
| Mirror access | Form self-correction |
| Music / privacy | Adherence factor |

### 3.9 Gym-specific intel

| Question | Affects |
|---|---|
| Gym chain / type (Planet Fitness, LA Fitness, commercial, CrossFit, university, hotel, etc.) | Whether deadlifts/Olympic lifts are tolerated; rack count; specialty bars |
| Number of squat racks | Realistic to program at peak times? |
| Peak vs off-peak training | Supersets feasible? Cable station available? |
| Specialty bars (safety squat, trap, swiss) | Substitutes for shoulder/wrist limited users |
| Chalk allowed | Heavy pulling capacity |

---

## 4. Branching Question Flow

The screen branches off of `TrainingLocation` answered on `TrainingBackgroundPage`. Each branch should keep total taps ≤ ~12 to stay under a 2-minute completion budget.

### Branch A — Gym only

```
1. Which gym do you train at?
   [Chain dropdown: Planet Fitness, LA Fitness, Anytime, 24 Hour, Crunch,
    Lifetime, Gold's, Equinox, YMCA, CrossFit affiliate, Hotel/Apartment,
    University, Commercial (other), Garage gym (private)]

2. What free weights are available? (multi-select)
   [Olympic barbell + rack | Dumbbells (full set) | Kettlebells | Fixed barbells]

3. Which racks/benches? (multi-select)
   [Power rack | Squat stands | Smith machine | Flat bench | Adjustable bench]

4. Which machines do you have access to? (multi-select, "all of the above" option)
   [Cable column | Cable crossover | Lat pulldown | Leg press | Hack squat |
    Leg curl | Leg extension | Chest press | Shoulder press | Pec deck |
    Hip thrust machine | Hyperextension/GHD]

5. Cardio equipment? (multi-select, skip if not interested)
   [Treadmill | Bike | Air bike | Rower | Elliptical | Stair climber]

6. Conditioning/plyo? (multi-select)
   [Plyo box | Jump rope | Medicine ball | Battle ropes | Sled | TRX | Rings]

7. When do you usually train? (drives crowdedness assumption)
   [Early morning | Mid-morning | Lunch | Evening peak | Late evening]

8. Anything they DON'T allow? (optional free text)
   [e.g. deadlifts, chalk, dropping weights]
```

### Branch B — Home only

```
1. What weights do you own? (multi-select with sub-prompts)
   - Adjustable dumbbells → max weight per side?
   - Fixed dumbbells → list pairs owned (toggle row)
   - Barbell → Olympic or standard? Total plate weight?
   - Kettlebells → which sizes (multi-select 8/12/16/20/24/28/32 kg)?
   - Resistance bands → tension levels (light/medium/heavy/extra-heavy)?

2. Rack/bench setup? (multi-select)
   [Power rack | Squat stands | Adjustable bench | Flat bench | None]

3. Pull-up option?
   [Doorway bar | Wall-mounted | Power-rack-attached | None]

4. Accessories? (multi-select)
   [TRX/suspension | Rings | Plyo box | Med ball | Jump rope | Sliders |
    Ab wheel | Foam roller | Stability ball | Yoga mat]

5. Cardio gear? (multi-select, skip if none)
   [Treadmill | Bike | Rower | Elliptical]

6. Ceiling height?
   [Low (<8 ft) | Standard (8-9 ft) | High (>9 ft)]

7. Noise tolerance?
   [Can be loud | Moderate | Quiet — no jumping/dropping]

8. Floor space available?
   [Tight (<6×6 ft) | Moderate (6×6 to 10×10) | Open (>10×10)]

9. Outdoor access?
   [Yard | Driveway | Park nearby | None]
```

### Branch C — Both

```
- Run Branch A (gym) and Branch B (home).
- Add one question: "Which equipment lives where?" — quick chip-sort if any item
  was selected in BOTH branches' free-weight section.
- Each WorkoutDay later gets tagged with a Location so the engine knows
  which inventory to filter against on that day.
```

---

## 5. Data Model Proposal

The current CSV-string approach (`TrainingProfile.AvailableEquipment`) is the wrong shape for richer intel. Recommended:

### 5.1 Replace the enum-and-CSV with a normalized inventory

```csharp
// New: per-item inventory record
[Table("EquipmentInventory")]
public class EquipmentInventoryItem
{
    [PrimaryKey, AutoIncrement] public int Id { get; set; }
    [Indexed] public int UserId { get; set; }

    public EquipmentItemType ItemType { get; set; }   // expanded enum (~60 values)
    public TrainingLocation Location { get; set; }    // Gym, Home, Both

    public decimal? MaxLoadKg { get; set; }           // dumbbell/kettlebell max, plate total
    public decimal? MinLoadKg { get; set; }
    public decimal? IncrementKg { get; set; }         // for adjustable DBs
    [MaxLength(200)] public string Notes { get; set; } = string.Empty;
}

// New: environment constraints
[Table("TrainingEnvironment")]
public class TrainingEnvironment
{
    [PrimaryKey, AutoIncrement] public int Id { get; set; }
    [Indexed] public int UserId { get; set; }

    public CeilingHeight HomeCeiling { get; set; }
    public NoiseTolerance HomeNoise { get; set; }
    public FloorSpace HomeFloorSpace { get; set; }
    public bool HasOutdoorAccess { get; set; }

    [MaxLength(100)] public string GymChain { get; set; } = string.Empty;
    public TrainingTimeBand UsualGymTime { get; set; }
    [MaxLength(500)] public string GymRestrictions { get; set; } = string.Empty;
}
```

### 5.2 Expand `EquipmentItemType` enum

Replace the 12-item `EquipmentType` enum with a ~60-item taxonomy organized by section number in §3 above. Keep the old enum as `[Obsolete]` mapped to the new one for migration.

### 5.3 Migration path

1. New tables added in `DatabaseService.InitializeAsync()`.
2. On first launch with existing data, project the legacy CSV equipment list into `EquipmentInventoryItem` rows with `Location = TrainingProfile.TrainingLocation` and null loads.
3. Onboarding gains a new step (or post-onboarding banner) prompting completed users to fill the new equipment detail.

---

## 6. Seed-Data Normalization

Required before any new intel adds value (else even today's engine misfires). Fixes for [Data/SeedData.cs](../Data/SeedData.cs):

| Find | Replace |
|---|---|
| `Equipment = "Dumbbell"` | `Equipment = "Dumbbells"` |
| `Equipment = "Dumbbell, Bench"` | `Equipment = "Dumbbells,Bench"` |
| `Equipment = "Dumbbells, Bench"` | `Equipment = "Dumbbells,Bench"` |
| `Equipment = "Resistance Band"` | `Equipment = "Bands"` |
| `Equipment = "Step"` | Add `Step` to enum OR change to `Bodyweight,Step` and add Step to filter's hardcoded allowlist |

Also in [Rules/PrescriptionRules/MainProgramRules.cs:107](../Rules/PrescriptionRules/MainProgramRules.cs#L107) — the hardcoded `"wall" || "doorway"` allowlist should move to a proper `IsAlwaysAvailableEquipment()` helper and include `step` if we keep Step as a non-enum tag.

After normalization, all 92 seed exercises should be reachable when the user owns the matching equipment.

---

## 7. Programmatic Impact

How richer intel changes engine behavior:

| New input | Engine behavior change |
|---|---|
| Adjustable DB max weight | Reject DB lifts whose recommended weight (based on phase + benchmarks) exceeds available; substitute machine/cable variant if owned |
| Plate total | Cap barbell prescriptions; warn if Strength/Power phase prescription exceeds available load |
| No rack | Strip back squat / heavy bench from main lift pool; substitute goblet/front-foot-elevated/floor press |
| Smith only | Strip free-squat variants; route to Smith squat/RDL substitutes |
| Low ceiling | Strip overhead press, snatch, jump squat, swing |
| Quiet/apartment | Strip plyometrics, drop-style deadlifts, kettlebell snatches |
| Limited floor space | Strip broad jumps, walking lunges, sled, prowler |
| Day-location tagging (Both) | `WorkoutDay.Location` filter applied per day so home days don't prescribe gym-only exercises |
| Gym chain = Planet Fitness | Flag deadlifts/heavy grunting work for alternatives; favor machines |
| Peak-time gym | Reduce reliance on specific stations (cable, rack); prefer DB circuits |
| Outdoor access | Unlock sprints, hill repeats, sled drag |

A new rule set `EquipmentSubstitutionRules` slots between `MainProgramRules` and program creation, with a substitution map per movement pattern (squat / hinge / push / pull / carry / lunge / rotation). The seed library needs a `MovementPattern` tag added per exercise so substitution can fall back generically when no exact equipment-tagged exercise exists.

---

## 8. Implementation Phases

Sized so each phase is independently shippable.

### Phase 1 — Data hygiene (≈ 1 session)
- Fix seed-data equipment tag mismatches (§6)
- Move hardcoded allowlist to helper
- Add unit-style test: every seed exercise should be selectable under at least one plausible inventory

### Phase 2 — Enum + screen expansion (≈ 2-3 sessions)
- Expand `EquipmentItemType` enum to full taxonomy
- Build branched `EquipmentDetailPage` (Gym / Home / Both) per §4
- Migrate `TrainingProfile.AvailableEquipment` CSV to `EquipmentInventoryItem` rows on first load
- Wire new data into `PrescriptionEngine.GenerateProgramAsync`

### Phase 3 — Environment + substitution intel (≈ 2-3 sessions)
- Add `TrainingEnvironment` table + onboarding step
- Add `MovementPattern` tag to `Exercise`
- Implement `EquipmentSubstitutionRules` with fallback map
- Add load-cap checks against `MaxLoadKg`

### Phase 4 — Day-location pairing (≈ 1 session)
- Add `WorkoutDay.Location`
- For "Both" users, schedule days as Gym/Home and filter inventory per day at generation time

### Phase 5 — Gym-chain templates (optional, ≈ 1 session)
- Seed `GymChainEquipmentTemplate` (Planet Fitness, LA Fitness, etc.) with typical inventory presets
- Auto-populate inventory from chain selection; user confirms / edits

---

## 9. Open Questions

1. **Do we want gym-chain presets shipped (Phase 5)** or let users always hand-enter? Presets save time but go stale and vary by location.
2. **Adjustable DB increment granularity** — do we ask, or assume 5 lb increments?
3. **Body-side asymmetries** — single-arm dumbbell prescriptions need only one DB. Worth a separate flag, or assume "pair" by default?
4. **Cardio integration** — is cardio part of the workout program or a separate "Conditioning" module? Affects whether cardio equipment lives here or in a sibling spec.
5. **Re-prompt cadence** — equipment changes over time. Re-prompt on every Nth program regeneration, or only via Settings?
6. **Photo-based inventory** — far future, but worth noting: user photographs their gym/garage and a model identifies equipment. Out of scope here.

---

## Related Docs

- [CES Corrective Framework](../CES/CES_Corrective_Framework.md) — movement-screen → compensation → corrective mapping that drives the warm-up/corrective portion of every program
- [Nutrition System Breakdown](../NutritionDocs/NutritionSystemBreakdown.md) — parallel spec for the nutrition side

