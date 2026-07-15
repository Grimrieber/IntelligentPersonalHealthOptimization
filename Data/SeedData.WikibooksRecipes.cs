using System.IO.Compression;
using System.Text.Json;
using IntelligentPersonalHealthOptimization.Models;
using SQLite;

namespace IntelligentPersonalHealthOptimization.Data;

public static partial class SeedData
{
    private const string WikibooksBundleAsset = "wikibooks_bundle.json.gz";
    private const string WikibooksSourceProvider = "Wikibooks";

    /// <summary>
    /// Seeds the SavedRecipe / SavedRecipeIngredient / SavedRecipeDirection
    /// tables from the bundled Wikibooks Cookbook gzip JSON the first time
    /// the app launches. Skips if any Wikibooks-sourced rows already exist.
    /// </summary>
    public static async Task SeedWikibooksRecipesAsync(SQLiteAsyncConnection connection)
    {
        var already = await connection.Table<SavedRecipe>()
            .Where(r => r.SourceProvider == WikibooksSourceProvider)
            .CountAsync();
        if (already > 0)
            return;

        WikibooksBundle? bundle;
        try
        {
            using var asset = await FileSystem.OpenAppPackageFileAsync(WikibooksBundleAsset);
            using var gz = new GZipStream(asset, CompressionMode.Decompress);
            bundle = await JsonSerializer.DeserializeAsync<WikibooksBundle>(
                gz,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        }
        catch (FileNotFoundException)
        {
            // Asset missing — silent skip rather than crashing the app.
            return;
        }

        if (bundle?.Recipes == null || bundle.Recipes.Count == 0)
            return;

        var recipes = new List<SavedRecipe>(bundle.Recipes.Count);
        var kept = new List<WikibooksRecipeDto>(bundle.Recipes.Count);
        foreach (var r in bundle.Recipes)
        {
            var nut = r.Nutrition;
            var health = RecipeHealth.Classify(
                r.Name, r.Category, nut?.CaloriesPerServing, nut?.SugarGrams,
                nut?.SatFatGrams, nut?.SodiumMg, nut?.FiberGrams, nut?.ProteinGrams);
            // Never seed the clearest junk (cake, candy, sugar-bomb desserts).
            if (health.HardRemove)
                continue;
            kept.Add(r);
            recipes.Add(new SavedRecipe
            {
                UserId = 0,                                  // shared catalog, not per-user
                SourceRecipeId = 0,                          // no upstream ID for Wikibooks
                RecipeName = Trunc(r.Name, 200) ?? string.Empty,
                CategoryName = Trunc(r.Category ?? "Uncategorized", 100) ?? "Uncategorized",
                PrepTime = Trunc(r.PrepTime, 50),
                CookTime = Trunc(r.CookTime, 50),
                RestTime = Trunc(r.RestTime, 50),
                Servings = Trunc(r.Servings, 50),
                Difficulty = Trunc(r.Difficulty, 20),
                Source = Trunc(r.Source, 200),
                Notes = r.Notes,
                ImageUrl = Trunc(r.ImageUrl, 500),
                Rating = null,
                IsFavorite = false,
                CaloriesPerServing = nut?.CaloriesPerServing,
                ProteinGrams = nut?.ProteinGrams,
                CarbsGrams = nut?.CarbsGrams,
                FatGrams = nut?.FatGrams,
                FiberGrams = nut?.FiberGrams,
                SugarGrams = nut?.SugarGrams,
                SodiumMg = nut?.SodiumMg,
                CholesterolMg = nut?.CholesterolMg,
                SatFatGrams = nut?.SatFatGrams,
                IngredientMatchRate = nut?.MatchRate,
                ServingSizeNote = Trunc(nut?.ServingSizeNote, 200),
                IsVegetarian = r.IsVegetarian,
                IsVegan = r.IsVegan,
                IsPescatarian = r.IsPescatarian,
                IsGlutenFree = r.IsGlutenFree,
                IsDairyFree = r.IsDairyFree,
                IsKeto = r.IsKeto,
                IsPaleo = r.IsPaleo,
                IsHalal = r.IsHalal,
                IsKosher = r.IsKosher,
                IsMediterranean = r.IsMediterranean,
                HealthScore = health.Score,
                HealthTier = health.Tier,
                IsHealthyTreat = health.IsTreat,
                SourceProvider = WikibooksSourceProvider,
                SavedAt = DateTime.UtcNow,
            });
        }

        // Insert recipes first so we have the auto-generated Ids.
        await connection.InsertAllAsync(recipes);

        // Now build ingredient + direction child rows keyed to the inserted Ids.
        // Iterate `kept` (post-junk-filter) so indices stay aligned with `recipes`.
        var ingredients = new List<SavedRecipeIngredient>(kept.Sum(r => r.Ingredients?.Count ?? 0));
        var directions  = new List<SavedRecipeDirection> (kept.Sum(r => r.Directions?.Count  ?? 0));

        for (int i = 0; i < kept.Count; i++)
        {
            var src = kept[i];
            var savedId = recipes[i].Id;

            if (src.Ingredients != null)
            {
                foreach (var ing in src.Ingredients)
                {
                    ingredients.Add(new SavedRecipeIngredient
                    {
                        SavedRecipeId = savedId,
                        SortOrder = ing.Order,
                        IngredientGroup = Trunc(ing.Group, 200),
                        Description = Trunc(ing.Description, 500) ?? string.Empty,
                    });
                }
            }

            if (src.Directions != null)
            {
                foreach (var dir in src.Directions)
                {
                    directions.Add(new SavedRecipeDirection
                    {
                        SavedRecipeId = savedId,
                        StepNumber = dir.Step,
                        DirectionGroup = Trunc(dir.Group, 200),
                        Instruction = dir.Instruction ?? string.Empty,
                    });
                }
            }
        }

        if (ingredients.Count > 0)
            await connection.InsertAllAsync(ingredients);
        if (directions.Count > 0)
            await connection.InsertAllAsync(directions);
    }

    // Bump suffix to re-run after a future bundle refresh.
    private const string ServingsBackfillMarker = "servings_backfill_2026_06_23_v1.done";

    /// <summary>
    /// One-shot: refresh Servings + per-serving nutrition on existing catalog
    /// rows from the (re-exported) bundle. The original bundle shipped ~2,600
    /// recipes without servings (nutrition stored as whole-recipe totals); the
    /// new bundle has estimated servings and divided per-serving values. Matches
    /// by RecipeName and only updates the nutrition/servings fields, so favorites
    /// (IsFavorite), ratings, and saved-at timestamps are preserved. New installs
    /// get the correct values straight from the seeder and skip this. Self-gated.
    /// </summary>
    // Bump suffix to re-run after a future bundle image refresh.
    // v2: every recipe now has an image (category food photos fill the ~80% with no page photo).
    // v3: tighter category queries — dessert pools stay desserts, less off-topic (no beef-on-brownie).
    // v4: per-recipe name matching — each recipe gets its OWN photo (category fallback only when no match).
    // v5: QUALITY RESET — auto-matched images produced garbage (canal on "Grand Union Bacon",
    //     slow cookers, humans). Keep ONLY verified Wikibooks page photos; everything else
    //     falls back to the emoji tile. This migration also CLEARS bad urls from prior versions.
    // v6: real Pexels dish photos (curated food library, human-filtered) fill the recipes
    //     without a page photo. ~91%+ now have a real photo; rest emoji until fetch completes.
    // v7: full Pexels coverage (99.96%, 2847/2848) + "descenery" re-pass that replaced
    //     place-named matches which returned scenery/animals (Buffalo Wings→a buffalo,
    //     Austrian Meatloaf→a mountain lake) with food-biased, caption-filtered dish photos.
    // v8: patched the last remaining imageless recipe (Rosto) → 100% coverage (2848/2848).
    // v9: de-duplicated same-name recipe groups (e.g. the 9 "Meatloaf" recipes each got a
    //     distinct photo from a deep 80-result pool) — Pexels had returned #1 for all of them.
    // v10: full cross-query dedup via Pixabay (100/min) — every recipe now has a UNIQUE photo
    //      (0 shared, was 65% shared). Mix: Pixabay fills dupes, Pexels/Wikimedia keep uniques.
    // v11: finished-dish audit — reject raw/prep/ingredient shots, prefer plated/cooked, and
    //      dedup by Pixabay IMAGE ID (webformatURL differs per-search for the same photo, so
    //      url-dedup let visual dupes through). Beef-stew family hand-picked via vision.
    // v12: SPECIFIC-name queries (was collapsing "Mexican Rice"→"rice" → sushi). Re-fetched all
    //      non-wiki by full dish name; fixes wrong-dish picks (Feijoada was a cake). Rice family
    //      hand-verified. Ongoing: category-by-category vision review tracked in used_ids ledger.
    // v13: category vision-review in progress — same-name families (meatloaf, meatballs, …)
    //      hand-picked from deep dish pools. Tracked in tools/used_ids.json ledger.
    // v24: full multi-pass re-audit to true 100%. Every one of 224 flagged recipes (9 HARD +
    //      215 MED "wrong specific dish") individually re-examined across 3 image APIs
    //      (Pixabay + Pexels + Wikimedia Commons). Obscure dishes recovered via native-name
    //      Commons search (dabo kolo, key wat, kesari, mopane worms, nsima, sauce feuilles…).
    //      Bundle schemaVersion 55. All 224 verified as accurate finished-dish photos.
    //      Then a "dig" pass on 10 borderline/best-available picks (blondies, toum, Louis
    //      dressing, tiramisu, stifado, creamed corn, charoset, lod-chong, okra stew, rice
    //      bake) replaced them with exact-dish photos → schemaVersion 56. Marker unchanged
    //      (v24 never deployed, so first install still re-applies the v56 images).
    // v25: durable-ledger re-audit (tools/recipe_image_ledger.json). EVERY recipe re-pooled
    //      across all 3 APIs and every candidate visually verified via labeled montage boards;
    //      only STABLE urls kept (upload.wikimedia.org + images.pexels.com — no expiring pixabay
    //      get-urls). Result: 2573 verified stable photos (1402 wikimedia + 1171 pexels), 275
    //      recipes with no accurate candidate nulled → clean emoji category fallback (better than
    //      shipping a wrong/human/scenery shot). schemaVersion 57. Zero expiring urls in bundle.
    // v26: emoji-fallback rescue. Re-hunted the 275 needs_fix recipes across ALL 3 APIs together
    //      (Pixabay's 100/min speed unblocked the Pexels-only bottleneck) with combined boards.
    //      Recovered ~150 (Fried Green Tomatoes, brownies, meatloaf, Duck a l'Orange, many ethnic
    //      dishes). Pexels/Commons picks stay stable hotlinks; Pixabay picks are DOWNLOADED and
    //      bundled locally under Resources/Raw/recipe_fix/ (imageUrl "asset:<id>.jpg") since
    //      Pixabay hotlinks expire — extracted to app storage + FileImageSource at migration.
    // v27: reliability fix. upload.wikimedia.org rate-limits (HTTP 429) BURSTS of thumbnail
    //      requests, so the browse list intermittently fell back to the emoji for the ~1400
    //      wikimedia-hosted recipes (half the catalog) even though the data was correct — a
    //      detail page (one request) always loaded fine. Fix: download+downscale all verified
    //      wikimedia thumbnails to Resources/Raw/recipe_fix/<id>.jpg and serve them as local
    //      FileImageSource ("asset:<id>.jpg"), same proven path as the Pixabay locals. Pexels
    //      (real CDN, no throttle) stays a stable hotlink. Now list thumbnails never throttle,
    //      never rot, work offline. schemaVersion 59.
    // v32: full offline. Localized the remaining ~1191 pexels-hotlinked photos to
    //      Resources/Raw/recipe_fix/<id>.jpg (asset:<id>.jpg), same path as the wikimedia/pixabay
    //      locals — so the WHOLE catalog's images work in airplane mode. Bundle now: 2630
    //      local-bundled + 111 emoji, ZERO hotlinks. schemaVersion 64. (tools/bundle_pexels_local.py)
    private const string RecipeImagesMarker = "recipe_images_2026_07_15_v32.done";
    private const string RecipeTimeCleanupMarker = "recipe_time_cleanup_2026_07_12_v2.done";

    /// <summary>
    /// One-shot: sync <see cref="SavedRecipe.ImageUrl"/> to the bundle EXACTLY — sets the
    /// verified page photo where the bundle has one, and CLEARS it (→ emoji fallback) where
    /// it doesn't. This removes the auto-matched garbage from earlier image versions.
    /// Matches by RecipeName, preserves favorites/ratings. Self-gated.
    /// </summary>
    public static async Task ApplyRecipeImagesAsync(SQLiteAsyncConnection connection)
    {
        var markerPath = Path.Combine(FileSystem.AppDataDirectory, RecipeImagesMarker);
        if (File.Exists(markerPath))
            return;

        try
        {
            WikibooksBundle? bundle;
            using (var asset = await FileSystem.OpenAppPackageFileAsync(WikibooksBundleAsset))
            using (var gz = new GZipStream(asset, CompressionMode.Decompress))
            {
                bundle = await JsonSerializer.DeserializeAsync<WikibooksBundle>(
                    gz, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            }
            if (bundle?.Recipes == null || bundle.Recipes.Count == 0)
                return;

            // The bundle now carries a real dish photo for ~100% of recipes; absent = no image.
            // A value of "asset:<file>" means the photo is bundled locally (Pixabay picks whose
            // remote urls expire): extract it from the app package to app storage and bind the
            // local file path so it never rots. Remote https urls (Pexels/Wikimedia) pass through.
            var imageByName = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
            foreach (var r in bundle.Recipes)
            {
                if (r.Name == null) continue;   // defensive: never key the map on null
                var url = r.ImageUrl;
                if (!string.IsNullOrEmpty(url) && url.StartsWith("asset:", StringComparison.Ordinal))
                    url = await ExtractLocalRecipeImageAsync(url.Substring("asset:".Length));
                imageByName[r.Name] = string.IsNullOrEmpty(url) ? null : url;
            }

            var rows = await connection.QueryAsync<SavedRecipe>(
                "SELECT * FROM SavedRecipe WHERE SourceProvider = ?", WikibooksSourceProvider);

            await connection.RunInTransactionAsync(conn =>
            {
                foreach (var row in rows)
                {
                    // Verified/curated photo if present, else NULL — clears prior garbage.
                    var target = imageByName.TryGetValue(row.RecipeName, out var url) ? Trunc(url, 500) : null;
                    if (row.ImageUrl != target)
                    {
                        row.ImageUrl = target;
                        conn.Update(row);
                    }
                }
            });

            // MAUI's Android image loader (Glide) disk-caches decoded bitmaps keyed by the source
            // file path. When a corrected photo reuses an existing local filename (e.g. a recipe
            // that was already "asset:<id>.jpg" gets a new image in a later marker), Glide keeps
            // serving the stale bitmap because the path/key is unchanged. Clear its disk cache once
            // per image-marker bump so the refreshed files re-decode. Glide rebuilds it on demand.
            ClearImageDiskCache();

            File.WriteAllText(markerPath, $"applied {DateTime.UtcNow:O}");
        }
        catch (Exception ex)
        {
            // Best-effort — don't crash app startup if it fails. Diagnostic breadcrumb
            // so a silent failure is recoverable (read via run-as files/…_error.txt).
            try
            {
                File.WriteAllText(
                    Path.Combine(FileSystem.AppDataDirectory, "recipe_images_error.txt"),
                    ex.ToString());
            }
            catch { }
        }
    }

    /// <summary>
    /// One-shot: re-sync PrepTime/CookTime/RestTime from the cleaned bundle. The Wikibooks
    /// scrape jammed the whole "Prep: X Cooking: Y Total: Z" block into a single cookTime
    /// string with no separators on ~151 recipes, which rendered as one unreadable line on
    /// the card. tools/clean_recipe_times.py split those back into the proper fields in the
    /// bundle; this mirrors them onto existing installs. Matches by RecipeName. Self-gated.
    /// </summary>
    public static async Task ApplyRecipeTimeCleanupAsync(SQLiteAsyncConnection connection)
    {
        var markerPath = Path.Combine(FileSystem.AppDataDirectory, RecipeTimeCleanupMarker);
        if (File.Exists(markerPath))
            return;

        try
        {
            WikibooksBundle? bundle;
            using (var asset = await FileSystem.OpenAppPackageFileAsync(WikibooksBundleAsset))
            using (var gz = new GZipStream(asset, CompressionMode.Decompress))
            {
                bundle = await JsonSerializer.DeserializeAsync<WikibooksBundle>(
                    gz, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            }
            if (bundle?.Recipes == null || bundle.Recipes.Count == 0)
                return;

            var timesByName = new Dictionary<string, (string?, string?, string?)>(StringComparer.OrdinalIgnoreCase);
            foreach (var r in bundle.Recipes)
            {
                if (r.Name == null) continue;
                timesByName[r.Name] = (Trunc(r.PrepTime, 50), Trunc(r.CookTime, 50), Trunc(r.RestTime, 50));
            }

            var rows = await connection.QueryAsync<SavedRecipe>(
                "SELECT * FROM SavedRecipe WHERE SourceProvider = ?", WikibooksSourceProvider);

            await connection.RunInTransactionAsync(conn =>
            {
                foreach (var row in rows)
                {
                    if (!timesByName.TryGetValue(row.RecipeName, out var t)) continue;
                    var (prep, cook, rest) = t;
                    if (row.PrepTime != prep || row.CookTime != cook || row.RestTime != rest)
                    {
                        row.PrepTime = prep;
                        row.CookTime = cook;
                        row.RestTime = rest;
                        conn.Update(row);
                    }
                }
            });

            File.WriteAllText(markerPath, $"applied {DateTime.UtcNow:O}");
        }
        catch (Exception ex)
        {
            try
            {
                File.WriteAllText(
                    Path.Combine(FileSystem.AppDataDirectory, "recipe_time_cleanup_error.txt"),
                    ex.ToString());
            }
            catch { }
        }
    }

    /// <summary>
    /// Copies a bundled recipe photo (MauiAsset under Resources/Raw/recipe_fix/) into app
    /// storage and returns its absolute file path for Image.Source binding. These are the
    /// Pixabay-sourced picks: bundling them locally sidesteps Pixabay's expiring hotlinks.
    /// Returns null if the packaged asset is missing (recipe falls back to the emoji tile).
    /// </summary>
    private static async Task<string?> ExtractLocalRecipeImageAsync(string fileName)
    {
        try
        {
            var destDir = Path.Combine(FileSystem.AppDataDirectory, "recipe_fix");
            Directory.CreateDirectory(destDir);
            var destPath = Path.Combine(destDir, fileName);
            // Always re-extract (overwrite) rather than skip-if-exists: this method only runs
            // inside the marker-gated ApplyRecipeImagesAsync, so it fires once per image-marker
            // version. Skipping existing files left stale photos in place when a recipe that was
            // already local-bundled got a corrected image in a later marker (e.g. the v28 fixes).
            using (var asset = await FileSystem.OpenAppPackageFileAsync($"recipe_fix/{fileName}"))
            using (var dest = File.Create(destPath))
            {
                await asset.CopyToAsync(dest);
            }
            return destPath;
        }
        catch
        {
            return null;   // packaged asset absent → caller stores null → emoji fallback
        }
    }

    /// <summary>
    /// Deletes the platform image loader's on-disk cache (Glide's "image_manager_disk_cache"
    /// under the app cache dir) so bitmaps whose backing file changed but kept the same path
    /// are re-decoded instead of served stale. Best-effort; Glide recreates it on demand.
    /// </summary>
    private static void ClearImageDiskCache()
    {
        try
        {
            var glideCache = Path.Combine(FileSystem.CacheDirectory, "image_manager_disk_cache");
            if (Directory.Exists(glideCache))
                Directory.Delete(glideCache, recursive: true);
        }
        catch { /* non-fatal: images just keep their current cache until next load */ }
    }

    public static async Task ApplyServingsBackfillAsync(SQLiteAsyncConnection connection)
    {
        var markerPath = Path.Combine(FileSystem.AppDataDirectory, ServingsBackfillMarker);
        if (File.Exists(markerPath))
            return;

        try
        {
            WikibooksBundle? bundle;
            using (var asset = await FileSystem.OpenAppPackageFileAsync(WikibooksBundleAsset))
            using (var gz = new GZipStream(asset, CompressionMode.Decompress))
            {
                bundle = await JsonSerializer.DeserializeAsync<WikibooksBundle>(
                    gz, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            }
            if (bundle?.Recipes == null || bundle.Recipes.Count == 0)
                return;

            var byName = new Dictionary<string, WikibooksRecipeDto>(StringComparer.OrdinalIgnoreCase);
            foreach (var r in bundle.Recipes)
                byName[r.Name] = r;   // dup names → same recipe, last wins is fine

            var rows = await connection.QueryAsync<SavedRecipe>(
                "SELECT * FROM SavedRecipe WHERE SourceProvider = ?", WikibooksSourceProvider);

            // Apply all row updates in a single transaction so ~3,900 writes
            // don't fsync individually and stall first launch.
            await connection.RunInTransactionAsync(conn =>
            {
                foreach (var row in rows)
                {
                    if (!byName.TryGetValue(row.RecipeName, out var src))
                        continue;

                    row.Servings = Trunc(src.Servings, 50);

                    var nut = src.Nutrition;
                    if (nut != null)
                    {
                        row.CaloriesPerServing = nut.CaloriesPerServing;
                        row.ProteinGrams = nut.ProteinGrams;
                        row.CarbsGrams = nut.CarbsGrams;
                        row.FatGrams = nut.FatGrams;
                        row.FiberGrams = nut.FiberGrams;
                        row.SugarGrams = nut.SugarGrams;
                        row.SodiumMg = nut.SodiumMg;
                        row.CholesterolMg = nut.CholesterolMg;
                        row.SatFatGrams = nut.SatFatGrams;
                        row.ServingSizeNote = Trunc(nut.ServingSizeNote, 200);
                    }

                    conn.Update(row);
                }
            });

            File.WriteAllText(markerPath, $"applied {DateTime.UtcNow:O}");
        }
        catch
        {
            // Best-effort — don't crash app startup if it fails.
        }
    }

    // Bump suffix to re-run after a future bundle refresh.
    // v2: second audit wave — drop 11 no-direction recipes + strip leaked wiki junk.
    // v3: refresh precomputed diet flags (IsVegan/IsVegetarian/...) from the bundle.
    // v4: refined classifier (drop "gravy" false-positive, honor vegan/vegetarian
    //     qualifiers + goat-cheese/duck-egg dairy remap).
    // v5: exhaustive audit — meat-fats (lard/suet/tallow) + fish condiments now block
    //     vegetarian/pescatarian correctly (closed 44 leaks). 0 leaks across all diets.
    // v6: perfection pass — added organ/cured meats (liver/guanciale), named cheeses
    //     (parmesan/feta/pecorino), crème, matzo/farina, composite cakes. Verified by
    //     Tools/wikibooks_diet_audit.py: 0 leaks + invariants hold on the shipped bundle.
    // v7: added 4 more diets — IsKeto/IsPaleo/IsHalal/IsKosher.
    // v8: added IsMediterranean (no red/processed meat).
    // v9: category-based classification + expanded fish/meat keywords — fixes on-device
    //     leaks (tilapia/filet-mignon/hot-dog flagged vegetarian). Audit gains a category oracle.
    // v10: ambiguous sausage/hot-dog family treated as pork-risk for halal/kosher unless
    //      qualified (beef/chicken/etc.) — "sage-flavored sausage" no longer halal.
    // v11: full-pool audit (every recipe, not a sample) — iguana now meat, chicken-wing dishes
    //      caught, "ale" is alcohol (halal), scaleless fish (eel) excluded from kosher.
    // v12: removed 387 non-meal recipes from the bundle (drinks, sauces, spice mixes, etc.)
    //      to save space — migration deletes them on-device. Smoothies/shakes kept.
    private const string RecipeAuditFixMarker = "recipe_audit_fix_2026_06_23_v12.done";

    /// <summary>
    /// One-shot audit cleanup: the re-exported bundle drops ~642 recipes whose
    /// nutrition couldn't be computed (unquantified ingredients) and re-estimates
    /// servings for implausible ones. This (a) removes on-device catalog rows no
    /// longer in the bundle and (b) refreshes servings/nutrition for the rest.
    /// New installs seed straight from the clean bundle and skip this. Self-gated.
    /// </summary>
    public static async Task ApplyRecipeAuditFixAsync(SQLiteAsyncConnection connection)
    {
        var markerPath = Path.Combine(FileSystem.AppDataDirectory, RecipeAuditFixMarker);
        if (File.Exists(markerPath))
            return;

        try
        {
            WikibooksBundle? bundle;
            using (var asset = await FileSystem.OpenAppPackageFileAsync(WikibooksBundleAsset))
            using (var gz = new GZipStream(asset, CompressionMode.Decompress))
            {
                bundle = await JsonSerializer.DeserializeAsync<WikibooksBundle>(
                    gz, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            }
            if (bundle?.Recipes == null || bundle.Recipes.Count == 0)
                return;

            var byName = new Dictionary<string, WikibooksRecipeDto>(StringComparer.OrdinalIgnoreCase);
            foreach (var r in bundle.Recipes)
                byName[r.Name] = r;

            var rows = await connection.QueryAsync<SavedRecipe>(
                "SELECT * FROM SavedRecipe WHERE SourceProvider = ?", WikibooksSourceProvider);

            await connection.RunInTransactionAsync(conn =>
            {
                foreach (var row in rows)
                {
                    if (!byName.TryGetValue(row.RecipeName, out var src))
                    {
                        // No longer in the clean bundle → audit-excluded. Remove it.
                        conn.Delete(row);
                        continue;
                    }

                    row.Servings = Trunc(src.Servings, 50);
                    row.IsVegetarian = src.IsVegetarian;
                    row.IsVegan = src.IsVegan;
                    row.IsPescatarian = src.IsPescatarian;
                    row.IsGlutenFree = src.IsGlutenFree;
                    row.IsDairyFree = src.IsDairyFree;
                    row.IsKeto = src.IsKeto;
                    row.IsPaleo = src.IsPaleo;
                    row.IsHalal = src.IsHalal;
                    row.IsKosher = src.IsKosher;
                    row.IsMediterranean = src.IsMediterranean;
                    var nut = src.Nutrition;
                    if (nut != null)
                    {
                        row.CaloriesPerServing = nut.CaloriesPerServing;
                        row.ProteinGrams = nut.ProteinGrams;
                        row.CarbsGrams = nut.CarbsGrams;
                        row.FatGrams = nut.FatGrams;
                        row.FiberGrams = nut.FiberGrams;
                        row.SugarGrams = nut.SugarGrams;
                        row.SodiumMg = nut.SodiumMg;
                        row.CholesterolMg = nut.CholesterolMg;
                        row.SatFatGrams = nut.SatFatGrams;
                        row.ServingSizeNote = Trunc(nut.ServingSizeNote, 200);
                    }
                    conn.Update(row);
                }
            });

            // Sweep child rows orphaned by the deletions above.
            await connection.ExecuteAsync(
                "DELETE FROM SavedRecipeIngredient WHERE SavedRecipeId NOT IN (SELECT Id FROM SavedRecipe)");
            await connection.ExecuteAsync(
                "DELETE FROM SavedRecipeDirection WHERE SavedRecipeId NOT IN (SELECT Id FROM SavedRecipe)");

            // Strip leaked wiki edit-link junk from category names
            // (e.g. 'Flatbread recipes&action=edit&redlink=1'). Done directly on
            // CategoryName rather than from the bundle so recategorized recipes
            // (Uncategorized → inferred) aren't reverted.
            await connection.ExecuteAsync(
                "UPDATE SavedRecipe SET CategoryName = TRIM(SUBSTR(CategoryName, 1, INSTR(CategoryName,'&')-1)) " +
                "WHERE SourceProvider = ? AND INSTR(CategoryName,'&') > 0", WikibooksSourceProvider);

            File.WriteAllText(markerPath, $"applied {DateTime.UtcNow:O}");
        }
        catch
        {
            // Best-effort — don't crash app startup if it fails.
        }
    }

    // Marker for the non-meal purge below.
    private const string NonMealPurgeMarker = "non_meal_purge_2026_06_23_v1.done";

    /// <summary>
    /// One-shot: delete catalog recipes that aren't meals — drinks (beverages,
    /// cocktails, juice, wine) and pure components (sauces, dressings, marinades,
    /// spice mixes, syrups, jams, stocks). Smoothies/shakes are kept. Purges by
    /// category via <see cref="RecipeCategoryGroups.IsMealPlanEligible"/>, so it is
    /// deterministic and works even if the bundled asset is stale. Frees DB space.
    /// Self-gated; runs once.
    /// </summary>
    public static async Task ApplyNonMealPurgeAsync(SQLiteAsyncConnection connection)
    {
        var markerPath = Path.Combine(FileSystem.AppDataDirectory, NonMealPurgeMarker);
        if (File.Exists(markerPath))
            return;

        try
        {
            var rows = await connection.QueryAsync<SavedRecipe>(
                "SELECT * FROM SavedRecipe WHERE SourceProvider = ?", WikibooksSourceProvider);
            var toDelete = rows
                .Where(r => !RecipeCategoryGroups.IsMealPlanEligible(r.CategoryName))
                .ToList();

            if (toDelete.Count > 0)
            {
                await connection.RunInTransactionAsync(conn =>
                {
                    foreach (var row in toDelete)
                        conn.Delete(row);
                });
                // Sweep child rows orphaned by the deletions.
                await connection.ExecuteAsync(
                    "DELETE FROM SavedRecipeIngredient WHERE SavedRecipeId NOT IN (SELECT Id FROM SavedRecipe)");
                await connection.ExecuteAsync(
                    "DELETE FROM SavedRecipeDirection WHERE SavedRecipeId NOT IN (SELECT Id FROM SavedRecipe)");
            }

            File.WriteAllText(markerPath, $"purged {toDelete.Count} non-meal recipes {DateTime.UtcNow:O}");
        }
        catch
        {
            // Best-effort — meal-plan filtering already excludes these at query time.
        }
    }

    // Marker for the duplicate-dish purge below. Bump the version suffix whenever
    // the bundle drops recipes and existing installs must re-sync.
    private const string DuplicateDishPurgeMarker = "duplicate_dish_purge_2026_07_12_v1.done";

    /// <summary>
    /// One-shot: re-sync the on-device catalog to the current bundle by deleting
    /// Wikibooks rows whose name is no longer present. Used to remove the 107
    /// same-dish duplicate recipes (Meatloaf II–V, Snickerdoodles II–IV, etc.)
    /// pruned from the bundle. New installs seed straight from the pruned bundle
    /// and skip this. Delete-only (nutrition refresh is handled by the audit-fix
    /// migration). Self-gated; runs once.
    /// </summary>
    public static async Task ApplyDuplicateDishPurgeAsync(SQLiteAsyncConnection connection)
    {
        var markerPath = Path.Combine(FileSystem.AppDataDirectory, DuplicateDishPurgeMarker);
        if (File.Exists(markerPath))
            return;

        try
        {
            WikibooksBundle? bundle;
            using (var asset = await FileSystem.OpenAppPackageFileAsync(WikibooksBundleAsset))
            using (var gz = new GZipStream(asset, CompressionMode.Decompress))
            {
                bundle = await JsonSerializer.DeserializeAsync<WikibooksBundle>(
                    gz, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            }
            if (bundle?.Recipes == null || bundle.Recipes.Count == 0)
                return;

            var bundleNames = new HashSet<string>(
                bundle.Recipes.Select(r => r.Name), StringComparer.OrdinalIgnoreCase);

            var rows = await connection.QueryAsync<SavedRecipe>(
                "SELECT * FROM SavedRecipe WHERE SourceProvider = ?", WikibooksSourceProvider);
            var toDelete = rows.Where(r => !bundleNames.Contains(r.RecipeName)).ToList();

            if (toDelete.Count > 0)
            {
                await connection.RunInTransactionAsync(conn =>
                {
                    foreach (var row in toDelete)
                        conn.Delete(row);
                });
                // Sweep child rows orphaned by the deletions.
                await connection.ExecuteAsync(
                    "DELETE FROM SavedRecipeIngredient WHERE SavedRecipeId NOT IN (SELECT Id FROM SavedRecipe)");
                await connection.ExecuteAsync(
                    "DELETE FROM SavedRecipeDirection WHERE SavedRecipeId NOT IN (SELECT Id FROM SavedRecipe)");
            }

            File.WriteAllText(markerPath, $"purged {toDelete.Count} duplicate recipes {DateTime.UtcNow:O}");
        }
        catch
        {
            // Best-effort — don't crash app startup if it fails.
        }
    }

    // Marker for the health-classification migration below. Bump the version suffix
    // if the RecipeHealth thresholds change and existing installs must re-classify.
    private const string HealthClassifyMarker = "health_classify_2026_07_14_v1.done";

    /// <summary>
    /// One-shot: classify every catalog recipe on a fitness health lens and (a) stamp
    /// HealthScore / HealthTier / IsHealthyTreat onto each row, and (b) physically
    /// delete the clearest junk (cake, candy, sugar-bomb desserts — see
    /// <see cref="RecipeHealth"/>). Computed entirely on-device from the nutrition
    /// already seeded on the phone — no bundle, no server. New installs classify at
    /// seed time and skip this. Self-gated; runs once.
    /// </summary>
    public static async Task ApplyHealthClassificationAsync(SQLiteAsyncConnection connection)
    {
        var markerPath = Path.Combine(FileSystem.AppDataDirectory, HealthClassifyMarker);
        if (File.Exists(markerPath))
            return;

        try
        {
            var rows = await connection.QueryAsync<SavedRecipe>(
                "SELECT * FROM SavedRecipe WHERE SourceProvider = ?", WikibooksSourceProvider);
            if (rows.Count == 0)
            {
                File.WriteAllText(markerPath, $"nothing to classify {DateTime.UtcNow:O}");
                return;
            }

            var toDelete = new List<SavedRecipe>();
            await connection.RunInTransactionAsync(conn =>
            {
                foreach (var row in rows)
                {
                    var h = RecipeHealth.Classify(row);
                    if (h.HardRemove)
                    {
                        conn.Delete(row);
                        toDelete.Add(row);
                        continue;
                    }
                    row.HealthScore = h.Score;
                    row.HealthTier = h.Tier;
                    row.IsHealthyTreat = h.IsTreat;
                    conn.Update(row);
                }
            });

            if (toDelete.Count > 0)
            {
                // Sweep child rows orphaned by the deletions.
                await connection.ExecuteAsync(
                    "DELETE FROM SavedRecipeIngredient WHERE SavedRecipeId NOT IN (SELECT Id FROM SavedRecipe)");
                await connection.ExecuteAsync(
                    "DELETE FROM SavedRecipeDirection WHERE SavedRecipeId NOT IN (SELECT Id FROM SavedRecipe)");
            }

            File.WriteAllText(markerPath,
                $"classified {rows.Count - toDelete.Count} recipes, removed {toDelete.Count} junk {DateTime.UtcNow:O}");
        }
        catch
        {
            // Best-effort — don't crash app startup if it fails.
        }
    }

    private static string? Trunc(string? s, int max)
        => s == null ? null : (s.Length <= max ? s : s.Substring(0, max));

    // ----- Bundle DTOs (matches Tools/wikibooks_export_bundle.py output) -----

    private sealed class WikibooksBundle
    {
        public int SchemaVersion { get; set; }
        public string? Source { get; set; }
        public string? License { get; set; }
        public string? SourceProvider { get; set; }
        public List<string>? Categories { get; set; }
        public List<WikibooksRecipeDto> Recipes { get; set; } = new();
    }

    private sealed class WikibooksRecipeDto
    {
        public string Name { get; set; } = string.Empty;
        public string? Category { get; set; }
        public string? PrepTime { get; set; }
        public string? CookTime { get; set; }
        public string? RestTime { get; set; }
        public string? Servings { get; set; }
        public string? Difficulty { get; set; }
        public string? Source { get; set; }
        public string? Notes { get; set; }
        public string? ImageUrl { get; set; }
        public bool IsVegetarian { get; set; }
        public bool IsVegan { get; set; }
        public bool IsPescatarian { get; set; }
        public bool IsGlutenFree { get; set; }
        public bool IsDairyFree { get; set; }
        public bool IsKeto { get; set; }
        public bool IsPaleo { get; set; }
        public bool IsHalal { get; set; }
        public bool IsKosher { get; set; }
        public bool IsMediterranean { get; set; }
        public List<WikibooksIngredientDto>? Ingredients { get; set; }
        public List<WikibooksDirectionDto>? Directions { get; set; }
        public WikibooksNutritionDto? Nutrition { get; set; }
    }

    private sealed class WikibooksIngredientDto
    {
        public int Order { get; set; }
        public string? Group { get; set; }
        public string? Description { get; set; }
    }

    private sealed class WikibooksDirectionDto
    {
        public int Step { get; set; }
        public string? Group { get; set; }
        public string? Instruction { get; set; }
    }

    private sealed class WikibooksNutritionDto
    {
        public int? CaloriesPerServing { get; set; }
        public double? ProteinGrams { get; set; }
        public double? CarbsGrams { get; set; }
        public double? FatGrams { get; set; }
        public double? FiberGrams { get; set; }
        public double? SodiumMg { get; set; }
        public double? CholesterolMg { get; set; }
        public double? SatFatGrams { get; set; }
        public double? SugarGrams { get; set; }
        public string? ServingSizeNote { get; set; }
        public double? MatchRate { get; set; }
    }
}
