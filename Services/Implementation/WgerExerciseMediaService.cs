using System.Collections.Concurrent;
using System.Text.Json;
using IntelligentPersonalHealthOptimization.Services.Interfaces;

namespace IntelligentPersonalHealthOptimization.Services.Implementation;

/// <summary>
/// Resolves exercise demonstration images via wger.de's public API.
/// Content is CC-BY-SA 4.0 — every rendered image must show attribution.
/// API docs: https://wger.de/api/v2/
///
/// Lookup flow:
///   1) exercise-translation/?name=...     try exact name match
///   2) exercise-translation/?search=...   fall back to relevance search
///   3) exerciseimage/?exercise=...        fetch first image for that exercise id
///   4) video/?exercise=...                fetch first video for that exercise id (if any)
/// </summary>
public class WgerExerciseMediaService : IExerciseMediaService
{
    private const string ApiBase = "https://wger.de/api/v2";
    private const int EnglishLanguageId = 2;
    private const string Attribution = "via wger.de (CC BY-SA 4.0)";

    private readonly HttpClient _http;

    // Session-lifetime cache. Both hits and misses are cached to avoid repeat round-trips.
    private readonly ConcurrentDictionary<string, ExerciseMedia?> _cache = new(StringComparer.OrdinalIgnoreCase);

    public WgerExerciseMediaService()
    {
        _http = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(8)
        };
        _http.DefaultRequestHeaders.Add("User-Agent", "HealthOptimizer/1.0");
        _http.DefaultRequestHeaders.Add("Accept", "application/json");
    }

    public async Task<ExerciseMedia?> FindByNameAsync(string exerciseName, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(exerciseName)) return null;

        var key = exerciseName.Trim();
        if (_cache.TryGetValue(key, out var cached))
            return cached;

        try
        {
            var exerciseId = await FindExerciseIdAsync(key, ct);
            if (exerciseId == null)
            {
                _cache[key] = null;
                return null;
            }

            var imageUrl = await FindFirstImageUrlAsync(exerciseId.Value, ct);
            var videoUrl = await FindFirstVideoUrlAsync(exerciseId.Value, ct);

            // If neither image nor video, treat as no result
            if (string.IsNullOrWhiteSpace(imageUrl) && string.IsNullOrWhiteSpace(videoUrl))
            {
                _cache[key] = null;
                return null;
            }

            var media = new ExerciseMedia
            {
                ImageUrl = imageUrl ?? string.Empty,
                VideoUrl = videoUrl ?? string.Empty,
                Attribution = Attribution,
                SourceUrl = $"https://wger.de/en/exercise/{exerciseId}/view"
            };
            _cache[key] = media;
            return media;
        }
        catch (Exception ex)
        {
            Services.CrashLogger.Log("WgerExerciseMediaService.FindByName", ex);
            _cache[key] = null;
            return null;
        }
    }

    private async Task<int?> FindExerciseIdAsync(string name, CancellationToken ct)
    {
        // 1) Exact-name match
        var exactId = await QueryExerciseIdAsync($"name={Uri.EscapeDataString(name)}", ct);
        if (exactId != null) return exactId;

        // 2) Relevance search — wger ranks by best match
        var searchId = await QueryExerciseIdAsync($"search={Uri.EscapeDataString(name)}", ct);
        if (searchId != null) return searchId;

        // 3) Try stripping common equipment prefixes that may not match wger's naming
        foreach (var prefix in new[] { "Dumbbell ", "Barbell ", "Cable ", "Machine ", "Resistance Band " })
        {
            if (name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                var trimmed = name[prefix.Length..].Trim();
                var altId = await QueryExerciseIdAsync($"search={Uri.EscapeDataString(trimmed)}", ct);
                if (altId != null) return altId;
            }
        }

        return null;
    }

    private async Task<int?> QueryExerciseIdAsync(string queryString, CancellationToken ct)
    {
        var url = $"{ApiBase}/exercise-translation/?language={EnglishLanguageId}&limit=1&{queryString}";
        using var resp = await _http.GetAsync(url, ct);
        if (!resp.IsSuccessStatusCode) return null;

        await using var stream = await resp.Content.ReadAsStreamAsync(ct);
        using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct);

        if (!doc.RootElement.TryGetProperty("results", out var results) || results.GetArrayLength() == 0)
            return null;

        if (results[0].TryGetProperty("exercise", out var exId) && exId.ValueKind == JsonValueKind.Number)
            return exId.GetInt32();

        return null;
    }

    private async Task<string?> FindFirstImageUrlAsync(int exerciseId, CancellationToken ct)
    {
        var url = $"{ApiBase}/exerciseimage/?exercise={exerciseId}&limit=1";
        using var resp = await _http.GetAsync(url, ct);
        if (!resp.IsSuccessStatusCode) return null;

        await using var stream = await resp.Content.ReadAsStreamAsync(ct);
        using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct);

        if (!doc.RootElement.TryGetProperty("results", out var results) || results.GetArrayLength() == 0)
            return null;

        if (results[0].TryGetProperty("image", out var imgEl) && imgEl.ValueKind == JsonValueKind.String)
            return imgEl.GetString();

        return null;
    }

    private async Task<string?> FindFirstVideoUrlAsync(int exerciseId, CancellationToken ct)
    {
        var url = $"{ApiBase}/video/?exercise={exerciseId}&limit=1";
        using var resp = await _http.GetAsync(url, ct);
        if (!resp.IsSuccessStatusCode) return null;

        await using var stream = await resp.Content.ReadAsStreamAsync(ct);
        using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct);

        if (!doc.RootElement.TryGetProperty("results", out var results) || results.GetArrayLength() == 0)
            return null;

        if (results[0].TryGetProperty("video", out var vEl) && vEl.ValueKind == JsonValueKind.String)
            return vEl.GetString();

        return null;
    }
}
