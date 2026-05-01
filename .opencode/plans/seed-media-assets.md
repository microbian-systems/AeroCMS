# Seed Starter Media Assets into Database

**Goal**: Add a `SeedStarterMediaAsync()` method to `SeedDataService.cs` that reads files from `wwwroot/media/` and creates `MediaAsset` Marten records, so they appear in the media library after setup.

**Files to modify**: 1 (one)

---

## `src/Aero.Cms.Modules.Setup/SeedDataService.cs`

### 1. Add usings (after line 19)

```csharp
using Aero.Cms.Core.Models;
using Microsoft.AspNetCore.Hosting;
```

### 2. Inject `IWebHostEnvironment` into primary constructor (line 74)

Add `IWebHostEnvironment env` as the second parameter (after `IDocumentSession session`):

```csharp
public sealed class SeedDatabaseService(
    IDocumentSession session,
    IWebHostEnvironment env,
    ISetupIdentityBootstrapper identityBootstrapper,
    ...
```

### 3. Call `SeedStarterMediaAsync()` in `SeedStarterContentAsync()` (before line 268)

```csharp
// Seed starter media assets from wwwroot/media
await SeedStarterMediaAsync(cancellationToken);
```

### 4. Add `SeedStarterMediaAsync()` method (before `SaveModuleStateAsync` at line 283)

```csharp
private async Task SeedStarterMediaAsync(CancellationToken ct)
{
    var mediaDir = Path.Combine(env.WebRootPath, "media");
    if (!Directory.Exists(mediaDir))
    {
        log.Warning("Media directory not found at {Path}. Skipping media seed.", mediaDir);
        return;
    }

    var mimeMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        [".png"] = "image/png",
        [".jpg"] = "image/jpeg",
        [".jpeg"] = "image/jpeg",
        [".gif"] = "image/gif",
        [".webp"] = "image/webp",
        [".svg"] = "image/svg+xml",
        [".ico"] = "image/x-icon"
    };

    foreach (var filePath in Directory.EnumerateFiles(mediaDir))
    {
        var fileName = Path.GetFileName(filePath);
        var ext = Path.GetExtension(filePath);
        var mime = mimeMap.TryGetValue(ext, out var m) ? m : "application/octet-stream";

        var altText = Path.GetFileNameWithoutExtension(fileName)
            .Replace('-', ' ').Replace('_', ' ');

        var media = new MediaAsset
        {
            Id = Snowflake.NewId(),
            FileName = fileName,
            Url = $"/media/{fileName}",
            MimeType = mime,
            FileSize = new FileInfo(filePath).Length,
            AltText = altText,
            IsFolder = false
        };

        session.Store(media);
    }

    log.Information("Seeded {Count} media assets from {Path}",
        Directory.GetFiles(mediaDir).Length, mediaDir);
}
```

## Expected result

After setup, `MediaAsset` records exist in Marten for all 6 files in `wwwroot/media/`. The existing `MediaApi.GetAllMedia` (which queries `MediaAsset`) will return them, making the files visible in the media library.
