using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http.Resilience;
using Polly;
using Serilog;

// ─── Configuration ─────────────────────────────────────────
var totalAssets = args.Length > 0 && int.TryParse(args[0], out var t) ? t : 200;
var imageCount = (int)(totalAssets * 0.95); // 190 out of 200
var videoCount = totalAssets - imageCount;   // 10

var pexelsApiKey = Environment.GetEnvironmentVariable("PEXELS_API_KEY")
    ?? throw new InvalidOperationException("PEXELS_API_KEY environment variable is not set.");

// Output directory: tools/AssetHydration → ../../src/Aero.Cms.Web/wwwroot/media/
var mediaDir = Path.GetFullPath(
    Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "src", "Aero.Cms.Web", "wwwroot", "media"));
var imageDir = Path.Combine(mediaDir, "hydrated-images");
var videoDir = Path.Combine(mediaDir, "hydrated-videos");
Directory.CreateDirectory(imageDir);
Directory.CreateDirectory(videoDir);

// ─── Logging ───────────────────────────────────────────────
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .WriteTo.Console(
        outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}")
    .CreateLogger();

Log.Information("Asset Hydration Tool");
Log.Information("  Total assets: {Total}", totalAssets);
Log.Information("  Images:       {Count} → {Dir}", imageCount, imageDir);
Log.Information("  Videos:       {Count} → {Dir}", videoCount, videoDir);
Log.Information("  Pexels API:   {Key}", pexelsApiKey[..8] + "...");

// ─── HTTP Client with Resilience ───────────────────────────
var services = new ServiceCollection();

services.AddHttpClient("pexels", client =>
{
    client.BaseAddress = new Uri("https://api.pexels.com/v1/");
    client.DefaultRequestHeaders.Authorization =
        new AuthenticationHeaderValue(pexelsApiKey);
})
.AddStandardResilienceHandler(options =>
{
    // Retry with exponential backoff (max 3 retries)
    options.Retry.MaxRetryAttempts = 3;
    options.Retry.Delay = TimeSpan.FromSeconds(1);
    options.Retry.BackoffType = DelayBackoffType.Exponential;
    options.Retry.OnRetry = args =>
    {
        Log.Warning("HTTP request failed (attempt {Attempt}): {Outcome}",
            args.AttemptNumber, args.Outcome.Result?.StatusCode);
        return default;
    };

    // Circuit breaker — trip after 50% failures, minimum 5 samples
    options.CircuitBreaker.SamplingDuration = TimeSpan.FromSeconds(30);
    options.CircuitBreaker.MinimumThroughput = 5;
    options.CircuitBreaker.BreakDuration = TimeSpan.FromSeconds(15);

    // Total request timeout
    options.TotalRequestTimeout.Timeout = TimeSpan.FromSeconds(30);
});

var provider = services.BuildServiceProvider();
var httpFactory = provider.GetRequiredService<IHttpClientFactory>();
var pexelsHttp = httpFactory.CreateClient("pexels");

// Also create a simple HttpClient for file downloads (CDN downloads don't need the Pexels auth header)
var downloadHttp = new HttpClient { Timeout = TimeSpan.FromMinutes(2) };

var jsonOpts = new JsonSerializerOptions
{
    PropertyNameCaseInsensitive = true,
    PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
};

// ─── Stats ─────────────────────────────────────────────────
var sw = Stopwatch.StartNew();
var downloaded = 0;
var failed = 0;
var skipped = 0;

// ─── 1. Fetch Curated Photos ───────────────────────────────
Log.Information("Fetching curated photos...");
var photos = new List<PexelsPhoto>();
var page = 1;

while (photos.Count < imageCount)
{
    var perPage = Math.Min(80, imageCount - photos.Count);
    var curatedUrl = $"curated?page={page}&per_page={perPage}";

    try
    {
        var result = await pexelsHttp.GetFromJsonAsync<PexelsCuratedResponse>(curatedUrl, jsonOpts);
        if (result?.Photos is null || result.Photos.Count == 0) break;
        photos.AddRange(result.Photos);
        Log.Information("  Page {Page}: got {Count} photos (total: {Total})", page, result.Photos.Count, photos.Count);
        page++;
    }
    catch (Exception ex)
    {
        Log.Error(ex, "  Failed to fetch curated photos page {Page}", page);
        break;
    }
}

photos = photos.Take(imageCount).ToList();
Log.Information("Fetched {Count} photos for download", photos.Count);

// ─── 2. Fetch Popular Videos ────────────────────────────────
Log.Information("Fetching popular videos...");
var videos = new List<PexelsVideo>();

try
{
    var videoPage = 1;
    while (videos.Count < videoCount)
    {
        var perPage = Math.Min(80, videoCount - videos.Count);
        var popularUrl = $"videos/popular?page={videoPage}&per_page={perPage}";
        var result = await pexelsHttp.GetFromJsonAsync<PexelsVideosResponse>(popularUrl, jsonOpts);
        if (result?.Videos is null || result.Videos.Count == 0) break;
        videos.AddRange(result.Videos);
        Log.Information("  Page {Page}: got {Count} videos (total: {Total})", videoPage, result.Videos.Count, videos.Count);
        videoPage++;
    }
}
catch (Exception ex)
{
    Log.Error(ex, "Failed to fetch popular videos");
}

videos = videos.Take(videoCount).ToList();
Log.Information("Fetched {Count} videos for download", videos.Count);

// ─── 3. Download Images ────────────────────────────────────
Log.Information("Downloading {Count} images...", photos.Count);
var imageSemaphore = new SemaphoreSlim(5); // 5 concurrent downloads

await Parallel.ForEachAsync(photos, async (photo, ct) =>
{
    await imageSemaphore.WaitAsync(ct);
    try
    {
        await DownloadImageAsync(photo, ct);
    }
    finally
    {
        imageSemaphore.Release();
    }
});

// ─── 4. Download Videos ────────────────────────────────────
Log.Information("Downloading {Count} videos...", videos.Count);
var videoSemaphore = new SemaphoreSlim(2); // 2 concurrent (videos are large)

await Parallel.ForEachAsync(videos, async (video, ct) =>
{
    await videoSemaphore.WaitAsync(ct);
    try
    {
        await DownloadVideoAsync(video, ct);
    }
    finally
    {
        videoSemaphore.Release();
    }
});

// ─── Report ─────────────────────────────────────────────────
sw.Stop();
Log.Information("═══════════════════════════════════════");
Log.Information("  Asset Hydration Complete");
Log.Information("  Duration:      {Elapsed:mm\\:ss}", sw.Elapsed);
Log.Information("  Downloaded:    {Count}", downloaded);
Log.Information("  Skipped:       {Count}", skipped);
Log.Information("  Failed:        {Count}", failed);
Log.Information("  Image Dir:     {Dir}", imageDir);
Log.Information("  Video Dir:     {Dir}", videoDir);
Log.Information("═══════════════════════════════════════");

await Log.CloseAndFlushAsync();

// ─── Helper Methods ─────────────────────────────────────────

async Task DownloadImageAsync(PexelsPhoto photo, CancellationToken ct)
{
    // Use medium size (~800-1000px) for blog-friendly images
    var imageUrl = photo.Src.Medium;
    if (string.IsNullOrEmpty(imageUrl))
        imageUrl = photo.Src.Large ?? photo.Src.Original;

    var filename = $"pexels-{photo.Id}.jpg";
    var filePath = Path.Combine(imageDir, filename);

    if (File.Exists(filePath))
    {
        Interlocked.Increment(ref skipped);
        return;
    }

    try
    {
        var response = await downloadHttp.GetAsync(imageUrl, HttpCompletionOption.ResponseHeadersRead, ct);
        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(ct);
        await using var fileStream = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None, 8192, true);
        await stream.CopyToAsync(fileStream, ct);

        var size = new FileInfo(filePath).Length;
        Interlocked.Increment(ref downloaded);

        Log.Information("  [IMG {Count}/{Total}] {Id}: {Name} ({Size} bytes)",
            downloaded, photos.Count, photo.Id, filename, size);
    }
    catch (Exception ex)
    {
        Interlocked.Increment(ref failed);
        Log.Error(ex, "  Failed to download image {Id}: {Url}", photo.Id, imageUrl);
        if (File.Exists(filePath)) File.Delete(filePath);
    }
}

async Task DownloadVideoAsync(PexelsVideo video, CancellationToken ct)
{
    // Pick the best SD quality file for blog compatibility
    var bestFile = video.VideoFiles
        .Where(f => f.Quality == "sd")
        .OrderByDescending(f => f.Width * f.Height)
        .FirstOrDefault()
        ?? video.VideoFiles
            .OrderByDescending(f => f.Width * f.Height)
            .FirstOrDefault();

    if (bestFile is null)
    {
        Log.Warning("  No video files for video {Id}", video.Id);
        Interlocked.Increment(ref failed);
        return;
    }

    var ext = Path.GetExtension(new Uri(bestFile.Link).AbsolutePath);
    if (string.IsNullOrEmpty(ext)) ext = ".mp4";

    var filename = $"pexels-{video.Id}{ext}";
    var filePath = Path.Combine(videoDir, filename);

    if (File.Exists(filePath))
    {
        Interlocked.Increment(ref skipped);
        return;
    }

    try
    {
        var response = await downloadHttp.GetAsync(bestFile.Link, HttpCompletionOption.ResponseHeadersRead, ct);
        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(ct);
        await using var fileStream = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None, 8192, true);
        await stream.CopyToAsync(fileStream, ct);

        var size = new FileInfo(filePath).Length;
        Interlocked.Increment(ref downloaded);

        Log.Information("  [VID {Count}/{Total}] {Id}: {Name} ({Size} bytes, {Quality}, {W}x{H})",
            downloaded, videos.Count, video.Id, filename, size, bestFile.Quality, bestFile.Width, bestFile.Height);
    }
    catch (Exception ex)
    {
        Interlocked.Increment(ref failed);
        Log.Error(ex, "  Failed to download video {Id}: {Link}", video.Id, bestFile.Link);
        if (File.Exists(filePath)) File.Delete(filePath);
    }
}

// ─── JSON Response Models ───────────────────────────────────
// (Mirror Aero.Services.Images models for self-containment)

public record PexelsCuratedResponse(
    [property: JsonPropertyName("photos")] IReadOnlyList<PexelsPhoto>? Photos,
    [property: JsonPropertyName("page")] int Page,
    [property: JsonPropertyName("per_page")] int PerPage,
    [property: JsonPropertyName("total_results")] int TotalResults
);

public record PexelsVideosResponse(
    [property: JsonPropertyName("videos")] IReadOnlyList<PexelsVideo>? Videos,
    [property: JsonPropertyName("page")] int Page,
    [property: JsonPropertyName("per_page")] int PerPage,
    [property: JsonPropertyName("total_results")] int TotalResults
);

public record PexelsPhoto(
    [property: JsonPropertyName("id")] int Id,
    [property: JsonPropertyName("url")] string Url,
    [property: JsonPropertyName("src")] PexelsSrc Src,
    [property: JsonPropertyName("alt")] string Alt,
    [property: JsonPropertyName("photographer")] string Photographer
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
    [property: JsonPropertyName("url")] string Url,
    [property: JsonPropertyName("image")] string Image,
    [property: JsonPropertyName("video_files")] IReadOnlyList<PexelsVideoFile>? VideoFiles,
    [property: JsonPropertyName("duration")] int Duration
);

public record PexelsVideoFile(
    [property: JsonPropertyName("id")] int Id,
    [property: JsonPropertyName("quality")] string Quality,
    [property: JsonPropertyName("file_type")] string FileType,
    [property: JsonPropertyName("link")] string Link,
    [property: JsonPropertyName("width")] int Width,
    [property: JsonPropertyName("height")] int Height
);
