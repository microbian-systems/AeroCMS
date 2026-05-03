#!/usr/bin/env dotnet

#:package Serilog@4.3.1
#:package Serilog.Sinks.Console@6.1.1

// Disable AOT — we use reflection-based JSON serialization for Pexels API responses
#:property PublishAot=false

// ──────────────────────────────────────────────────────────
//  Pexels Asset Hydration Tool
//  Single-file .NET 10 app (no .csproj needed)
//
//  Usage:
//    dotnet tools/AssetHydration/asset-hydration.cs -- [output-dir] [count]
//
//  Args:
//    output-dir   Path to save media (default: ./src/Aero.Cms.Web/wwwroot/media)
//    count        Total assets to download (default: 200, 95% images / 5% videos)
//
//  Attribution:
//    Per Pexels license requirements, each downloaded file gets a sidecar
//    .attribution.json file with photographer/videographer credit info.
//    These are used by Aero CMS pages that display the media to show
//    proper attribution (e.g. "Photo by John Doe on Pexels").
// ──────────────────────────────────────────────────────────

using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Serilog;

// ─── Args ──────────────────────────────────────────────────

// First optional arg: output directory (default: ./src/Aero.Cms.Web/wwwroot/media)
// Second optional arg: total assets (default: 200)
string outputDir;
int totalAssets;

if (args.Length == 0)
{
    outputDir = "./src/Aero.Cms.Web/wwwroot/media";
    totalAssets = 200;
}
else if (args.Length == 1)
{
    // Single arg: if it's a number, it's the total; otherwise it's the output path
    if (int.TryParse(args[0], out totalAssets))
        outputDir = "./src/Aero.Cms.Web/wwwroot/media";
    else
    {
        outputDir = args[0];
        totalAssets = 200;
    }
}
else
{
    outputDir = args[0];
    totalAssets = int.TryParse(args[1], out var t) ? t : 200;
}

var imageCount = (int)(totalAssets * 0.95);
var videoCount = totalAssets - imageCount;

var pexelsApiKey = Environment.GetEnvironmentVariable("PEXELS_API_KEY")
    ?? throw new InvalidOperationException("PEXELS_API_KEY not set.");

// Resolve output directory relative to current working directory
var mediaDir = Path.GetFullPath(Path.Combine(Environment.CurrentDirectory, outputDir));
var imageDir = Path.Combine(mediaDir, "hydrated-images");
var videoDir = Path.Combine(mediaDir, "hydrated-videos");
Directory.CreateDirectory(imageDir);
Directory.CreateDirectory(videoDir);

// ─── Logging ───────────────────────────────────────────────
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}")
    .CreateLogger();

Log.Information("Asset Hydration Tool");
Log.Information("  Total:  {Total} ({ImageCount} images + {VideoCount} videos)", totalAssets, imageCount, videoCount);
Log.Information("  Output: {Dir}", mediaDir);
Log.Information("  API:    {Key}", pexelsApiKey[..8] + "...");

// ─── HTTP Clients ──────────────────────────────────────────
var pexelsHttp = new HttpClient
{
    BaseAddress = new Uri("https://api.pexels.com/v1/"),
    Timeout = TimeSpan.FromSeconds(15)
};
pexelsHttp.DefaultRequestHeaders.Add("Authorization", pexelsApiKey);

var downloadHttp = new HttpClient { Timeout = TimeSpan.FromMinutes(3) };

var jsonOpts = new JsonSerializerOptions { PropertyNameCaseInsensitive = true, PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower };

// ─── Stats ─────────────────────────────────────────────────
var sw = Stopwatch.StartNew();
var downloaded = 0;
var failed = 0;
var skipped = 0;

// ─── 1. Fetch Curated Photos ───────────────────────────────
Log.Information("Fetching curated photos...");
var photos = new List<PexelsPhoto>();
int page = 1;

while (photos.Count < imageCount)
{
    var perPage = Math.Min(80, imageCount - photos.Count);
    var result = await RetryAsync(() =>
        pexelsHttp.GetFromJsonAsync<CuratedResponse>($"curated?page={page}&per_page={perPage}", jsonOpts));

    if (result?.Photos is null || result.Photos.Count == 0) break;
    photos.AddRange(result.Photos);
    Log.Information("  Page {Page}: {Count} photos (total: {Total})", page, result.Photos.Count, photos.Count);
    page++;
}

photos = photos.Take(imageCount).ToList();
Log.Information("Fetched {Count} photos", photos.Count);

// ─── 2. Fetch Popular Videos ───────────────────────────────
Log.Information("Fetching popular videos...");
var videos = new List<PexelsVideo>();
page = 1;

while (videos.Count < videoCount)
{
    var perPage = Math.Min(80, videoCount - videos.Count);
    var result = await RetryAsync(() =>
        pexelsHttp.GetFromJsonAsync<VideosResponse>($"videos/popular?page={page}&per_page={perPage}", jsonOpts));

    if (result?.Videos is null || result.Videos.Count == 0) break;
    videos.AddRange(result.Videos);
    Log.Information("  Page {Page}: {Count} videos (total: {Total})", page, result.Videos.Count, videos.Count);
    page++;
}

videos = videos.Take(videoCount).ToList();
Log.Information("Fetched {Count} videos", videos.Count);

// ─── 3. Download Images ────────────────────────────────────
Log.Information("Downloading {Count} images...", photos.Count);

await Parallel.ForEachAsync(photos, new ParallelOptions { MaxDegreeOfParallelism = 5 }, async (photo, ct) =>
{
    var url = photo.Src.Medium ?? photo.Src.Large ?? photo.Src.Original;
    if (string.IsNullOrEmpty(url)) { Interlocked.Increment(ref failed); return; }

    var filename = $"pexels-{photo.Id}.jpg";
    var filePath = Path.Combine(imageDir, filename);

    if (File.Exists(filePath)) { Interlocked.Increment(ref skipped); return; }

    try
    {
        await DownloadFileAsync(url, filePath, ct);
        var size = new FileInfo(filePath).Length;
        Interlocked.Increment(ref downloaded);

        // Save Pexels attribution sidecar for content creator credit
        await SaveAttributionAsync(imageDir, filename, new
        {
            photo.Id,
            photo.Photographer,
            photo.PhotographerUrl,
            photo.Url,
            photo.Alt,
            File = filename,
            Type = "image",
            DownloadedAt = DateTimeOffset.UtcNow
        });

        Log.Information("  [IMG] {Name} ({Size} bytes, © {Photographer})", filename, size, photo.Photographer);
    }
    catch (Exception ex)
    {
        Interlocked.Increment(ref failed);
        Log.Error(ex, "  Failed image {Id}", photo.Id);
        if (File.Exists(filePath)) File.Delete(filePath);
    }
});

// ─── 4. Download Videos ────────────────────────────────────
Log.Information("Downloading {Count} videos...", videos.Count);

await Parallel.ForEachAsync(videos, new ParallelOptions { MaxDegreeOfParallelism = 2 }, async (video, ct) =>
{
    var best = video.VideoFiles?.Where(f => f.Quality == "sd").OrderByDescending(f => f.Width * f.Height).FirstOrDefault()
        ?? video.VideoFiles?.OrderByDescending(f => f.Width * f.Height).FirstOrDefault();

    if (best is null) { Interlocked.Increment(ref failed); return; }

    var ext = Path.GetExtension(new Uri(best.Link).AbsolutePath);
    if (string.IsNullOrEmpty(ext)) ext = ".mp4";

    var filename = $"pexels-{video.Id}{ext}";
    var filePath = Path.Combine(videoDir, filename);

    if (File.Exists(filePath)) { Interlocked.Increment(ref skipped); return; }

    try
    {
        await DownloadFileAsync(best.Link, filePath, ct);
        var size = new FileInfo(filePath).Length;
        Interlocked.Increment(ref downloaded);

        // Save Pexels attribution sidecar for content creator credit
        await SaveAttributionAsync(videoDir, filename, new
        {
            video.Id,
            Photographer = video.User?.Name,
            PhotographerUrl = video.User?.Url,
            Url = $"https://www.pexels.com/video/{video.Id}/",
            Alt = (string?)null,
            File = filename,
            Type = "video",
            Quality = best.Quality,
            DownloadedAt = DateTimeOffset.UtcNow
        });

        Log.Information("  [VID] {Name} ({Size} bytes, {Q}, © {Photographer})", filename, size, best.Quality, video.User?.Name ?? "unknown");
    }
    catch (Exception ex)
    {
        Interlocked.Increment(ref failed);
        Log.Error(ex, "  Failed video {Id}", video.Id);
        if (File.Exists(filePath)) File.Delete(filePath);
    }
});

// ─── Report ─────────────────────────────────────────────────
sw.Stop();
Log.Information("═══════════════════════════════════════");
Log.Information("  Complete in {Elapsed:mm\\:ss}", sw.Elapsed);
Log.Information("  Downloaded: {Down}  Skipped: {Skip}  Failed: {Fail}", downloaded, skipped, failed);
Log.Information("  Images: {Dir}", imageDir);
Log.Information("  Videos: {Dir}", videoDir);
Log.Information("═══════════════════════════════════════");

await Log.CloseAndFlushAsync();

// ─── Helpers ───────────────────────────────────────────────

static async Task<T?> RetryAsync<T>(Func<Task<T?>> operation, int maxRetries = 3)
{
    for (int attempt = 1; attempt <= maxRetries; attempt++)
    {
        try { return await operation(); }
        catch (Exception ex) when (attempt < maxRetries)
        {
            Log.Warning("  Retry {Attempt}/{Max} after error: {Msg}", attempt, maxRetries, ex.Message);
            await Task.Delay(TimeSpan.FromSeconds(Math.Pow(2, attempt)));
        }
    }
    return await operation(); // last attempt — let it throw
}

static async Task DownloadFileAsync(string url, string path, CancellationToken ct)
{
    using var http = new HttpClient { Timeout = TimeSpan.FromMinutes(3) };
    var response = await http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct);
    response.EnsureSuccessStatusCode();
    await using var stream = await response.Content.ReadAsStreamAsync(ct);
    await using var fs = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None, 8192, true);
    await stream.CopyToAsync(fs, ct);
}

static async Task SaveAttributionAsync<T>(string dir, string filename, T data)
{
    var attrPath = Path.Combine(dir, $"{filename}.attribution.json");
    var json = JsonSerializer.Serialize(data, new JsonSerializerOptions
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
    });
    await File.WriteAllTextAsync(attrPath, json);
}

// ─── JSON Models ────────────────────────────────────────────

public record CuratedResponse(
    [property: JsonPropertyName("photos")] IReadOnlyList<PexelsPhoto>? Photos,
    [property: JsonPropertyName("total_results")] int TotalResults
);

public record VideosResponse(
    [property: JsonPropertyName("videos")] IReadOnlyList<PexelsVideo>? Videos,
    [property: JsonPropertyName("total_results")] int TotalResults
);

public record PexelsPhoto(
    [property: JsonPropertyName("id")] int Id,
    [property: JsonPropertyName("url")] string Url,
    [property: JsonPropertyName("src")] PexelsSrc Src,
    [property: JsonPropertyName("alt")] string Alt,
    [property: JsonPropertyName("photographer")] string Photographer,
    [property: JsonPropertyName("photographer_url")] string PhotographerUrl
);

public record PexelsSrc(
    [property: JsonPropertyName("original")] string? Original,
    [property: JsonPropertyName("large2x")] string? Large2x,
    [property: JsonPropertyName("large")] string? Large,
    [property: JsonPropertyName("medium")] string? Medium,
    [property: JsonPropertyName("small")] string? Small,
    [property: JsonPropertyName("portrait")] string? Portrait,
    [property: JsonPropertyName("landscape")] string? Landscape,
    [property: JsonPropertyName("tiny")] string? Tiny
);

public record PexelsVideo(
    [property: JsonPropertyName("id")] int Id,
    [property: JsonPropertyName("video_files")] IReadOnlyList<PexelsVideoFile>? VideoFiles,
    [property: JsonPropertyName("user")] PexelsUser? User
);

public record PexelsUser(
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("url")] string Url
);

public record PexelsVideoFile(
    [property: JsonPropertyName("id")] int Id,
    [property: JsonPropertyName("quality")] string Quality,
    [property: JsonPropertyName("link")] string Link,
    [property: JsonPropertyName("width")] int Width,
    [property: JsonPropertyName("height")] int Height
);
