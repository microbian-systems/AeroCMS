using Aero.Cms.Core.Models;
using Microsoft.AspNetCore.Hosting;
using Serilog;

namespace Aero.Cms.Modules.Media;

/// <summary>
/// Defines an interface for IMediaService.
/// </summary>
public interface IMediaService
{
        /// <summary>
    /// GetAllAsync method.
    /// </summary>
Task<Result<IReadOnlyList<MediaAsset>, AeroError>> GetAllAsync(CancellationToken ct = default);
        /// <summary>
    /// GetByIdAsync method.
    /// </summary>
Task<Result<MediaAsset?, AeroError>> GetByIdAsync(long id, CancellationToken ct = default);
        /// <summary>
    /// GetByFolderAsync method.
    /// </summary>
Task<Result<IReadOnlyList<MediaAsset>, AeroError>> GetByFolderAsync(long? parentId, CancellationToken ct = default);
        /// <summary>
    /// GetByPathAsync method.
    /// </summary>
Task<Result<MediaAsset, AeroError>> GetByPathAsync(string url, CancellationToken ct = default);
        /// <summary>
    /// GetPagedAsync method.
    /// </summary>
Task<Result<(IReadOnlyList<MediaAsset> Items, long TotalCount), AeroError>> GetPagedAsync(
        long? parentId, int skip, int take, string? search = null, CancellationToken ct = default);
        /// <summary>
    /// CreateAsync method.
    /// </summary>
Task<Result<MediaAsset, AeroError>> CreateAsync(MediaAsset asset, CancellationToken ct = default);
        /// <summary>
    /// CreateFolderAsync method.
    /// </summary>
Task<Result<MediaAsset, AeroError>> CreateFolderAsync(string name, long? parentId = null, CancellationToken ct = default);
        /// <summary>
    /// UpdateAsync method.
    /// </summary>
Task<Result<MediaAsset, AeroError>> UpdateAsync(MediaAsset asset, CancellationToken ct = default);
        /// <summary>
    /// DeleteAsync method.
    /// </summary>
Task<Result<bool, AeroError>> DeleteAsync(long id, CancellationToken ct = default);
        /// <summary>
    /// SeedFromDirectoryAsync method.
    /// </summary>
Task<Result<int, AeroError>> SeedFromDirectoryAsync(string subfolder, CancellationToken ct = default);
}

/// <summary>
/// Represents a class for MediaService.
/// </summary>
public sealed class MediaService(IDocumentSession session, IWebHostEnvironment env) : IMediaService
{
        /// <summary>
    /// GetAllAsync method.
    /// </summary>
public async Task<Result<IReadOnlyList<MediaAsset>, AeroError>> GetAllAsync(CancellationToken ct = default)
    {
        try
        {
            var media = await session.Query<MediaAsset>()
                .OrderByDescending(x => x.IsFolder)
                .ThenByDescending(x => x.CreatedOn)
                .ToListAsync(ct);
            return Prelude.Ok<IReadOnlyList<MediaAsset>, AeroError>(media);
        }
        catch (Exception ex)
        {
            return Prelude.Fail<IReadOnlyList<MediaAsset>, AeroError>(AeroError.CreateError(ex.Message));
        }
    }

        /// <summary>
    /// GetByIdAsync method.
    /// </summary>
public async Task<Result<MediaAsset?, AeroError>> GetByIdAsync(long id, CancellationToken ct = default)
    {
        try
        {
            var media = await session.LoadAsync<MediaAsset>(id, ct);
            return media is null
                ? Prelude.Fail<MediaAsset?, AeroError>(AeroError.NotFoundError($"Media asset with ID {id} not found."))
                : Prelude.Ok<MediaAsset?, AeroError>(media);
        }
        catch (Exception ex)
        {
            return Prelude.Fail<MediaAsset?, AeroError>(AeroError.CreateError(ex.Message));
        }
    }

        /// <summary>
    /// GetByFolderAsync method.
    /// </summary>
public async Task<Result<IReadOnlyList<MediaAsset>, AeroError>> GetByFolderAsync(long? parentId, CancellationToken ct = default)
    {
        try
        {
            var query = session.Query<MediaAsset>();

            IReadOnlyList<MediaAsset> media;
            if (parentId.HasValue)
            {
                media = await query.Where(x => x.ParentId == parentId.Value)
                    .OrderByDescending(x => x.IsFolder)
                    .ThenByDescending(x => x.CreatedOn)
                    .ToListAsync(ct);
            }
            else
            {
                media = await query.Where(x => x.ParentId == null)
                    .OrderByDescending(x => x.IsFolder)
                    .ThenByDescending(x => x.CreatedOn)
                    .ToListAsync(ct);
            }

            return Prelude.Ok<IReadOnlyList<MediaAsset>, AeroError>(media);
        }
        catch (Exception ex)
        {
            return Prelude.Fail<IReadOnlyList<MediaAsset>, AeroError>(AeroError.CreateError(ex.Message));
        }
    }

        /// <summary>
    /// GetByPathAsync method.
    /// </summary>
public async Task<Result<MediaAsset, AeroError>> GetByPathAsync(string url, CancellationToken ct = default)
    {
        try
        {
            var media = await session.Query<MediaAsset>()
                .FirstOrDefaultAsync(x => x.Url == url, ct);

            return media is null
                ? Prelude.Fail<MediaAsset, AeroError>(AeroError.NotFoundError($"Media asset with URL '{url}' not found."))
                : Prelude.Ok<MediaAsset, AeroError>(media);
        }
        catch (Exception ex)
        {
            return Prelude.Fail<MediaAsset, AeroError>(AeroError.CreateError(ex.Message));
        }
    }

        /// <summary>
    /// GetPagedAsync method.
    /// </summary>
public async Task<Result<(IReadOnlyList<MediaAsset> Items, long TotalCount), AeroError>> GetPagedAsync(
        long? parentId, int skip, int take, string? search = null, CancellationToken ct = default)
    {
        try
        {
            var query = session.Query<MediaAsset>();

            IQueryable<MediaAsset> filteredQuery = query;

            if (!string.IsNullOrWhiteSpace(search))
            {
                var s = search.ToLower();
                filteredQuery = filteredQuery.Where(x =>
                    x.FileName.ToLower().Contains(s) ||
                    (x.AltText != null && x.AltText.ToLower().Contains(s)));
            }
            else
            {
                filteredQuery = filteredQuery.Where(x => x.ParentId == parentId);
            }

            var stats = new global::AeroDB.Sable.QueryStatistics();
            var items = await ((global::AeroDB.Sable.ISurrealDbQueryable<MediaAsset>)filteredQuery)
                .OrderByDescending(x => x.IsFolder)
                .ThenByDescending(x => x.CreatedOn)
                .Stats(out stats)
                .Skip(skip)
                .Take(take)
                .ToListAsync(ct);

            return Prelude.Ok<(IReadOnlyList<MediaAsset> Items, long TotalCount), AeroError>((items, stats.TotalResults));
        }
        catch (Exception ex)
        {
            return Prelude.Fail<(IReadOnlyList<MediaAsset> Items, long TotalCount), AeroError>(AeroError.CreateError(ex.Message));
        }
    }

        /// <summary>
    /// CreateAsync method.
    /// </summary>
public async Task<Result<MediaAsset, AeroError>> CreateAsync(MediaAsset asset, CancellationToken ct = default)
    {
        try
        {
            asset.Id = Snowflake.NewId();
            session.Store(asset);
            await session.SaveChangesAsync(ct);
            return Prelude.Ok<MediaAsset, AeroError>(asset);
        }
        catch (Exception ex)
        {
            return Prelude.Fail<MediaAsset, AeroError>(AeroError.CreateError(ex.Message));
        }
    }

        /// <summary>
    /// CreateFolderAsync method.
    /// </summary>
public async Task<Result<MediaAsset, AeroError>> CreateFolderAsync(string name, long? parentId = null, CancellationToken ct = default)
    {
        try
        {
            var folder = new MediaAsset
            {
                Id = Snowflake.NewId(),
                FileName = name,
                IsFolder = true,
                ParentId = parentId,
                MimeType = "folder",
                Url = "#"
            };

            session.Store(folder);
            await session.SaveChangesAsync(ct);
            return Prelude.Ok<MediaAsset, AeroError>(folder);
        }
        catch (Exception ex)
        {
            return Prelude.Fail<MediaAsset, AeroError>(AeroError.CreateError(ex.Message));
        }
    }

        /// <summary>
    /// UpdateAsync method.
    /// </summary>
public async Task<Result<MediaAsset, AeroError>> UpdateAsync(MediaAsset asset, CancellationToken ct = default)
    {
        try
        {
            session.Store(asset);
            await session.SaveChangesAsync(ct);
            return Prelude.Ok<MediaAsset, AeroError>(asset);
        }
        catch (Exception ex)
        {
            return Prelude.Fail<MediaAsset, AeroError>(AeroError.CreateError(ex.Message));
        }
    }

        /// <summary>
    /// DeleteAsync method.
    /// </summary>
public async Task<Result<bool, AeroError>> DeleteAsync(long id, CancellationToken ct = default)
    {
        try
        {
            var media = await session.LoadAsync<MediaAsset>(id, ct);
            if (media is null)
            {
                return Prelude.Fail<bool, AeroError>(AeroError.NotFoundError($"Media asset with ID {id} not found."));
            }

            if (!media.IsFolder)
            {
                var mediaDir = Path.Combine(env.WebRootPath, "media");
                var filePath = Path.Combine(mediaDir, media.FileName);
                if (File.Exists(filePath))
                {
                    File.Delete(filePath);
                }
            }

            session.Delete<MediaAsset>(id);
            await session.SaveChangesAsync(ct);
            return Prelude.Ok<bool, AeroError>(true);
        }
        catch (Exception ex)
        {
            return Prelude.Fail<bool, AeroError>(AeroError.CreateError(ex.Message));
        }
    }

        /// <summary>
    /// SeedFromDirectoryAsync method.
    /// </summary>
public async Task<Result<int, AeroError>> SeedFromDirectoryAsync(string subfolder, CancellationToken ct = default)
    {
        try
        {
            var mediaDir = Path.Combine(env.WebRootPath, "media", subfolder);
            if (!Directory.Exists(mediaDir))
            {
                Log.Warning("Media subfolder not found at {Path}. Skipping seed.", mediaDir);
                return Prelude.Ok<int, AeroError>(0);
            }

            var mimeMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                [".png"] = "image/png",
                [".jpg"] = "image/jpeg",
                [".jpeg"] = "image/jpeg",
                [".gif"] = "image/gif",
                [".webp"] = "image/webp",
                [".svg"] = "image/svg+xml",
                [".ico"] = "image/x-icon",
                [".mp4"] = "video/mp4",
                [".webm"] = "video/webm"
            };

            var count = 0;
            var imageFiles = Directory.EnumerateFiles(mediaDir, "*.*")
                .Where(f => mimeMap.ContainsKey(Path.GetExtension(f)))
                .ToList();

            foreach (var filePath in imageFiles)
            {
                var fileName = Path.GetFileName(filePath);
                var ext = Path.GetExtension(filePath);
                var mime = mimeMap.GetValueOrDefault(ext, "application/octet-stream");
                var altText = Path.GetFileNameWithoutExtension(fileName)
                    .Replace('-', ' ').Replace('_', ' ');

                // Check for existing record by URL to avoid duplicates
                var url = $"/media/{subfolder}/{fileName}";
                var exists = await session.Query<MediaAsset>()
                    .Where(x => x.Url == url).AnyAsync(ct);
                if (exists) continue;

                // Read attribution sidecar if present
                MediaAttribution? attribution = null;
                var attrPath = Path.Combine(mediaDir, $"{fileName}.attribution.json");
                if (File.Exists(attrPath))
                {
                    try
                    {
                        var attrJson = await File.ReadAllTextAsync(attrPath, ct);
                        var attr = System.Text.Json.JsonSerializer.Deserialize<AttributionFile>(attrJson);
                        if (attr is not null)
                        {
                            attribution = new MediaAttribution
                            {
                                CreatorName = attr.Photographer,
                                CreatorUrl = attr.PhotographerUrl,
                                SourceUrl = attr.Url,
                                Platform = "Pexels",
                                MediaType = MediaType.Image
                            };
                        }
                    }
                    catch (Exception ex)
                    {
                        Log.Warning("Failed to read attribution for {File}: {Msg}", fileName, ex.Message);
                    }
                }

                // Try to get width/height — we don't decode the image, leave as 0
                var media = new MediaAsset
                {
                    Id = Aero.Core.Snowflake.NewId(),
                    FileName = fileName,
                    Url = url,
                    MimeType = mime,
                    FileSize = new FileInfo(filePath).Length,
                    AltText = attribution?.CreatorName is not null
                        ? $"{altText} — Photo by {attribution.CreatorName}"
                        : altText,
                    IsFolder = false,
                    Attribution = attribution
                };

                session.Store(media);
                count++;
            }

            await session.SaveChangesAsync(ct);
            Log.Information("Seeded {Count} media assets from {Path}", count, mediaDir);
            return Prelude.Ok<int, AeroError>(count);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to seed media from directory {Subfolder}", subfolder);
            return Prelude.Fail<int, AeroError>(AeroError.CreateError(ex.Message));
        }
    }

    // ─── Internal model for deserializing attribution sidecar JSON ───
    private sealed record AttributionFile(
        int Id,
        string Photographer,
        string PhotographerUrl,
        string Url,
        string? Alt,
        string File,
        string Type,
        DateTimeOffset DownloadedAt);
}

