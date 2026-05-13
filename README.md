# Intelligent Personal Health Optimization

A .NET MAUI mobile app for personal health, fitness, and nutrition optimization. Targets Android (Pixel 7 / API 35) primarily.

## Features

- **CES Corrective Exercise Assessment** — 8-step wizard (Posture, Overhead Squat, Single-Leg Squat, Push/Pull) that detects distortion syndromes and generates corrective exercise programs.
- **Nutrition Coach** — calorie/macro calculations, recipe browsing, food entry logging.
- **Wellness Check-In** — behavioral and eating-disorder screening.
- **Security** — encrypted SQLite via SQLCipher, PIN/biometric lock, security question recovery.
- **Recipe Database integration** — pulls from a local SQL Server `RecipeDB` (Windows-only dev) or via API at `localhost:5199`.

## Project Layout

| Path | Purpose |
| --- | --- |
| `CES/` | Corrective Exercise framework documents (excluded from repo) |
| `Constants/` | App-wide constants and config keys |
| `Data/` | EF Core / SQLite data layer |
| `Models/` | Domain models |
| `NutritionCoachFiles/`, `NutritionDocs/` | Reference content (excluded from repo) |
| `Services/` | Business logic and platform implementations |
| `Tools/` | Python utilities for recipe imports (Spoonacular) |
| `ViewModels/`, `Views/` | MVVM UI layer |

## Setup

### Prerequisites
- .NET 8 SDK with MAUI workload
- Visual Studio 2022 (Windows) or Rider
- Android emulator (Pixel 7, API 35) for testing
- SQL Server LocalDB or full instance for `RecipeDB` (optional, Windows only)

### Environment variables
The app and tooling read secrets from environment variables. Set these on your dev machine before running:

```powershell
setx RECIPEDB_CONNECTION_STRING "Data Source=localhost;Initial Catalog=RecipeDB;User ID=sa;Password=<your-password>;TrustServerCertificate=True;Connect Timeout=10;"
setx SPOONACULAR_API_KEY "<your-spoonacular-key>"
setx RECIPEDB_SQL_CONNECTION "DRIVER={ODBC Driver 17 for SQL Server};SERVER=localhost;DATABASE=RecipeDB;UID=sa;PWD=<your-password>;TrustServerCertificate=yes;"
```

Reopen your shell after `setx` for the variables to take effect.

### Build and run

```powershell
dotnet build
dotnet build -t:Run -f net8.0-android
```

## Tools

`Tools/spoonacular_import.py` and `Tools/spoonacular_daily.py` pull recipes from the Spoonacular API into staging tables in `RecipeDB`. Requires `SPOONACULAR_API_KEY` and `RECIPEDB_SQL_CONNECTION` env vars and Python with `requests` and `pyodbc`.
