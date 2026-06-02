# Equipment Intel — App Integration Spec

**Companion to:** [EquipmentIntelSpec.md](EquipmentIntelSpec.md)
**Purpose:** Defines *how* the Equipment Intel from the parent spec slots into the existing app — screen flow, file additions, data pulls from prior sections, navigation, and the Settings entry point. This doc does not redefine the questionnaire content (see parent §3–§4).

**Author / date:** Stephan, 2026-05-16
**Status:** Draft for review

---

## Table of Contents

1. [Where It Fits Today](#1-where-it-fits-today)
2. [Proposed Screen Flow](#2-proposed-screen-flow)
3. [Cross-Section Data Pulls](#3-cross-section-data-pulls)
4. [The "Promote What We Can Do" Layer](#4-the-promote-what-we-can-do-layer)
5. [File / Route Additions](#5-file--route-additions)
6. [Code Touch Points](#6-code-touch-points)
7. [Settings Re-Entry](#7-settings-re-entry)
8. [Build Order](#8-build-order)
9. [Open Decisions](#9-open-decisions)

---

## 1. Where It Fits Today

Current handoff from CES to program generation:

```
CES wizard ends → CesAssessmentCoordinator.CompleteAsync() →
    saves data → DisplayAlert → Shell.GoToAsync("//Workout")
    [user lands on Workout tab, taps "Generate"]
        → AssessmentResultViewModel.GenerateProgramAsync()
            → _prescriptionEngine.GenerateProgramAsync(userId, sessionId)
                → reads TrainingProfile.AvailableEquipment (CSV)
                → builds WorkoutProgram
```

The engine reads equipment from `TrainingProfile` set during onboarding's `TrainingBackgroundPage`. There is **no opportunity post-CES** for the user to refine equipment with full knowledge of their movement-quality results. That's the gap.

Reference: [CesAssessmentCoordinator.cs:101-110](../Services/Implementation/CesAssessmentCoordinator.cs#L101-L110), [AssessmentResultViewModel.cs:107-127](../ViewModels/AssessmentResultViewModel.cs#L107-L127), [PrescriptionEngine.cs:67-77](../Services/Implementation/PrescriptionEngine.cs#L67-L77).

---

## 2. Proposed Screen Flow

```
... CES wizard (Posture × 3 → Overhead Squat → Single-Leg → Push-Pull) ...
       ↓
CesResultPage  ← existing; movement score, compensations, syndromes
       ↓
       [tap "Build my program"]
       ↓
🆕 EquipmentIntroPage      ← 1-screen explainer: "We need 2 minutes of detail
                              about your equipment to design this for you."
       ↓
🆕 EquipmentDetailPage     ← branched by TrainingLocation (Gym / Home / Both)
                              implements parent spec §4
       ↓
🆕 EnvironmentDetailPage   ← ceiling / noise / space (Home or Both branches only)
       ↓
🆕 ProgramPreviewPage      ← unlock summary + "Generate my program" button
       ↓
       [tap Generate]
       ↓
PrescriptionEngine.GenerateProgramAsync()   ← unchanged signature, reads new inventory
       ↓
WorkoutProgramPage  ← existing
```

**Key design points:**
- `EquipmentIntroPage` is short — sets expectation, surfaces "we pulled some of this from your onboarding" so users know answers are pre-populated.
- Branching happens by reading `TrainingProfile.TrainingLocation` set at onboarding; user can override mid-flow.
- `EnvironmentDetailPage` is **skipped entirely** for `Gym`-only users (no ceiling/noise/space questions for a commercial gym).
- `ProgramPreviewPage` is the "promote" moment (§4 below).

---

## 3. Cross-Section Data Pulls

Every field the new screens read from elsewhere in the app, with the resulting behavior:

| Source screen | Source field | New screen | Use |
|---|---|---|---|
| `TrainingBackgroundPage` (onboarding) | `TrainingLocation` | `EquipmentDetailPage` | Selects branch (Gym / Home / Both). Skip-able to change. |
| `TrainingBackgroundPage` | `AvailableEquipment` (CSV) | `EquipmentDetailPage` | **Pre-checks** matching items in the new taxonomy. User refines weights/details. |
| `TrainingBackgroundPage` | `SessionDurationMinutes` | `ProgramPreviewPage` | "At 45 min × 3/wk, we can program ~6 main lifts/session" |
| `TrainingBackgroundPage` | `CurrentFrequency` | `EquipmentDetailPage`, `ProgramPreviewPage` | Drives day-split selection and preview |
| `GoalsPage` | `FitnessGoal` (Strength / Hyper / Endurance / Sport) | `EquipmentDetailPage` | **Re-orders question sections**: Strength surfaces rack/plate questions first; Endurance surfaces cardio first |
| `GoalsPage` | `FitnessGoal` | `ProgramPreviewPage` | "For your hypertrophy goal, your cable column unlocks 8 isolation variants" |
| `HealthScreeningPage` | Injuries / conditions | `EquipmentDetailPage` | **Flags substitution priority**: knee issue → highlight leg press / Smith questions; shoulder → highlight specialty-bar Qs |
| `HealthScreeningPage` | Conditions | `ProgramPreviewPage` | "Given your noted lower-back history, we'll route around heavy axial loading even if your rack supports it" |
| `FitnessBenchmarkPage` | Push-up / squat / cardio scores | `EquipmentDetailPage` | Suggests realistic max-load defaults for dumbbells (a beginner doesn't need to scroll to 100 lb) |
| `CesAssessment` (just completed) | `OverallMovementScore` | `ProgramPreviewPage` | "Your Stabilization-phase program will use lighter loads, so your 20 lb DBs are fine" |
| `CesAssessment` | Detected compensations | `EquipmentDetailPage` | Surfaces TRX / suspension / foam-roller questions because correctives need them |
| `PersonalInfoPage` | `WeightKg` | `EquipmentDetailPage` | Sanity-checks: 200 lb user + one 35 lb DB pair → flag mismatch |

**Implementation:** A new `IEquipmentIntelService` runs once on `EquipmentIntroPage` load, gathers all source fields, and exposes them as a `EquipmentIntelContext` object the downstream ViewModels consume. No raw DB calls scattered across screens.

---

## 4. The "Promote What We Can Do" Layer

The "intel-promoting" moment lives on **`ProgramPreviewPage`** and as a footer chip on each `EquipmentDetailPage` section.

### Footer chip (per section, e.g. after Free Weights section)

```
Your inventory now unlocks 31 exercises across this section.
[+ Add a TRX to unlock 6 single-leg pull variations]
[+ Add resistance bands (heavy) to unlock 4 accommodating-resistance variants]
```

Computed from `EquipmentSubstitutionRules` (parent spec §7) by simulating "add item X" and counting exercise-pool delta.

### ProgramPreviewPage

```
┌────────────────────────────────────────────┐
│  Your Program Preview                      │
├────────────────────────────────────────────┤
│  Phase:        Hypertrophy                 │
│  Days/week:    4                           │
│  Session:      45 min                      │
│  Exercises:    47 unlocked, ~24 will fit   │
│                                            │
│  Tailored from:                            │
│  ✓ Your movement score (62 — Hypertrophy)  │
│  ✓ Your hypertrophy goal                   │
│  ✓ Your gym inventory (12 items)           │
│  ✓ Your noted shoulder mobility (sub'd     │
│    landmine for overhead press)            │
│                                            │
│  Unlock more:                              │
│  + Adjustable bench → 9 incline pushes     │
│  + Med ball         → 4 rotational lifts   │
│                                            │
│       [   Generate my program   ]          │
└────────────────────────────────────────────┘
```

This page is the conversion point — users see *why* they're being asked these questions and what one more piece of gear would buy them. It's also the natural QA gate before a program is created.

---

## 5. File / Route Additions

| Path | Type | Purpose |
|---|---|---|
| `Models/EquipmentInventoryItem.cs` | New model | One row per owned item; per parent spec §5 |
| `Models/TrainingEnvironment.cs` | New model | Ceiling / noise / space / gym chain |
| `Models/Enums/EquipmentItemType.cs` | New enum | Full ~60-item taxonomy from parent §3 |
| `Models/Enums/CeilingHeight.cs`, `NoiseTolerance.cs`, `FloorSpace.cs`, `TrainingTimeBand.cs` | New enums | Environment dimensions |
| `Services/Interfaces/IEquipmentIntelService.cs` | New interface | Pulls cross-section data, computes unlock deltas |
| `Services/Implementation/EquipmentIntelService.cs` | New implementation | — |
| `Rules/PrescriptionRules/EquipmentSubstitutionRules.cs` | New rule set | Movement-pattern fallback map; used by engine AND unlock-delta calc |
| `ViewModels/Workout/EquipmentIntroViewModel.cs` | New VM | Loads `EquipmentIntelContext`, summarizes pulls |
| `ViewModels/Workout/EquipmentDetailViewModel.cs` | New VM | Owns inventory state, branching, footer chips |
| `ViewModels/Workout/EnvironmentDetailViewModel.cs` | New VM | Ceiling/noise/space |
| `ViewModels/Workout/ProgramPreviewViewModel.cs` | New VM | Unlock summary + generate trigger |
| `Views/Workout/EquipmentIntroPage.xaml(.cs)` | New page | — |
| `Views/Workout/EquipmentDetailPage.xaml(.cs)` | New page | — |
| `Views/Workout/EnvironmentDetailPage.xaml(.cs)` | New page | — |
| `Views/Workout/ProgramPreviewPage.xaml(.cs)` | New page | — |
| `AppShell.xaml.cs` | Edit | Register 4 new routes under `// Workout routes` block |
| `Constants/RouteConstants.cs` | Edit | Add `EquipmentIntro`, `EquipmentDetail`, `EnvironmentDetail`, `ProgramPreview` |
| `MauiProgram.cs` | Edit | DI registrations for new service + 4 new VMs |

Routes to add:

```csharp
// Equipment & Environment intel (post-CES, pre-program)
Routing.RegisterRoute("equipmentintro", typeof(EquipmentIntroPage));
Routing.RegisterRoute("equipmentdetail", typeof(EquipmentDetailPage));
Routing.RegisterRoute("environmentdetail", typeof(EnvironmentDetailPage));
Routing.RegisterRoute("programpreview", typeof(ProgramPreviewPage));
```

---

## 6. Code Touch Points

### 6.1 CesAssessmentCoordinator change

[Services/Implementation/CesAssessmentCoordinator.cs:101-110](../Services/Implementation/CesAssessmentCoordinator.cs#L101-L110) — replace the `Shell.GoToAsync("//Workout")` with the new flow:

```csharp
// before
await Shell.Current.DisplayAlert("Assessment Saved", $"...", "OK");
await Shell.Current.GoToAsync("//Workout");

// after
await Shell.Current.DisplayAlert("Assessment Saved", $"...", "OK");
await Shell.Current.GoToAsync(
    $"equipmentintro?sessionId={Data.Id}");
```

### 6.2 AssessmentResultViewModel change

[ViewModels/AssessmentResultViewModel.cs:107-127](../ViewModels/AssessmentResultViewModel.cs#L107-L127) — `GenerateProgramAsync` no longer fires `_prescriptionEngine.GenerateProgramAsync` directly. Instead it navigates to `equipmentintro`. The actual engine call moves to `ProgramPreviewViewModel.GenerateCommand`.

This means **the legacy `AssessmentResultPage` "Generate Program" button also routes through the new flow** — equipment intel becomes a single funnel for any program creation.

### 6.3 PrescriptionEngine input change

[Services/Implementation/PrescriptionEngine.cs:67-77](../Services/Implementation/PrescriptionEngine.cs#L67-L77) — replace:

```csharp
// before
availableEquipment = trainingProfile.AvailableEquipment.Split(',', ...)
```

with a query against `EquipmentInventoryItem` for `userId`, projecting to a richer `EquipmentInventory` object the rules can interrogate for `MaxLoadKg`, `Location`, etc. The legacy CSV becomes a fallback for users who haven't run the new flow yet (migration shim).

### 6.4 No change required to

- Existing `MainProgramRules` / `WarmupSelectionRules` / `CorrectiveExerciseRules` until Phase 3 (engine substitution work). The new screens *collect* richer intel before the engine catches up — engine still works off the projected `AvailableEquipment` list initially.

---

## 7. Settings Re-Entry

Equipment changes (apartment move, gym change, new gear). Add to `SettingsPage`:

```
─ Training ─────────────────────────
[ Update my equipment & environment ]   → equipmentintro?reentry=true
[ Re-take movement assessment       ]   (existing CES flow)
```

When entered via Settings, `EquipmentIntroPage` reads existing inventory + environment rows and pre-fills everything, so it's an edit flow not a fresh capture. On exit, it offers "Regenerate my program with these changes" (calls `PrescriptionEngine.GenerateProgramAsync` against the latest CES session).

---

## 8. Build Order

Built in 4 stages — each stage produces a working app that's strictly better than the prior.

### Stage A — Bare flow (no pre-fill, no unlock stats) [~2 sessions]
1. Add new models + enums + migrations
2. Register routes
3. Build the 4 new pages with the question content from parent §4
4. Coordinator change: CES result → new flow → engine call
5. Engine reads new inventory table (with CSV fallback)
6. **Outcome:** post-CES user gets a richer questionnaire; engine consumes it; same exercise pool, just better-tagged inputs

### Stage B — Cross-section pre-fill [~1 session]
1. Build `IEquipmentIntelService` + `EquipmentIntelContext`
2. EquipmentIntroPage shows "Pulled from your onboarding: gym, 8 items checked"
3. EquipmentDetailPage pre-checks matching items
4. EquipmentDetailPage re-orders sections based on `FitnessGoal`
5. **Outcome:** users see continuity from onboarding, faster completion

### Stage C — Unlock stats + preview page [~2 sessions]
1. Build `EquipmentSubstitutionRules` with movement-pattern fallback map
2. Add `MovementPattern` tag to `Exercise` (seed data update)
3. Footer chips on each section (live exercise count delta)
4. ProgramPreviewPage with full unlock summary
5. **Outcome:** intake becomes a value-promotion moment; users self-motivated to expand inventory

### Stage D — Engine substitution + day-location pairing [~2 sessions]
1. Engine uses `EquipmentSubstitutionRules` to swap when exact-match unavailable
2. `WorkoutDay.Location` tag for "Both" users; per-day inventory filter
3. Load-cap checks against `MaxLoadKg`
4. **Outcome:** programs become genuinely tailored, not just filtered

---

## 9. Decisions (Stage A)

Resolved 2026-05-16 with Stephan. Stage B+ decisions deferred to those stages.

| # | Decision | Resolution |
|---|---|---|
| 1 | Gating | **Skippable with warning banner.** If user bails, engine falls back to onboarding CSV; generated program shows banner "Based on minimal equipment intel — add detail for better picks." |
| 2 | Cardio scope | **Opt-in section.** `EquipmentDetailPage` shows a "Include cardio in your program?" toggle. If yes, surface cardio gear questions. Else skip. |
| 3 | Legacy `AssessmentResultPage` | **Delete after Stage A is proven stable.** Stage A ships with both pages routing through the new equipment flow. Removal lands in a follow-up. |
| 4 | Dumbbell intel depth | **Max weight + adjustable/fixed.** Assume pair ownership and 5 lb increments. Single-arm-only and custom increments deferred to Stage B if needed. |
| 5 | Gym-chain presets | **Ship 5-10 common-chain presets that auto-fill.** Picking a chain pre-checks typical inventory; user reviews/adjusts. Adds: seed `GymChainEquipmentTemplate` table + 5-10 chain rows. **Scope add over original spec.** |
| 6 | Settings re-entry | **Manual regen with prominent CTA.** On exit from Settings equipment flow, show "Your inventory changed. [Regenerate program]" button. No silent regeneration. |
| 7 | Settings edit grain | **Edit individual items.** List view in Settings; user can add/remove/edit one item at a time without re-running the full questionnaire. |

### Deferred to later stages

| # | Decision | When to revisit |
|---|---|---|
| D1 | Re-prompt cadence (every N program regens vs Settings-only) | Stage C, when unlock stats exist |
| D2 | Unlock-stats accuracy audit against small seed library | Stage C |
| D3 | Single-arm vs pair flag for dumbbells | Stage B, if real users need it |
| D4 | Adjustable-DB custom increments (other than 5 lb) | Stage B, if real users need it |
| D5 | Photo-based equipment inventory | Out of scope indefinitely |

---

## Related Docs

- [EquipmentIntelSpec.md](EquipmentIntelSpec.md) — the parent spec (taxonomy, questions, data model, phasing)
- [CES Corrective Framework](../CES/CES_Corrective_Framework.md) — the upstream flow this integrates with
