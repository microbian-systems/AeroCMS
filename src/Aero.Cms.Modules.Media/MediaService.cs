using Aero.Cms.Core.Models;
using Microsoft.AspNetCore.Hosting;
using Serilog;

namespace Aero.Cms.Modules.Media;

/// <summary>
/// Provides result-based media queries, persistence, deletion, and development seeding.
/// </summary>
/// <remarks>
/// This contract does not accept or derive a tenant/site boundary. Callers must authorize
/// operations and constrain use to assets owned by the intended site.
/// </remarks>
public interface IMediaService
{
    /// <summary>
    /// Loads all media assets, with folders first and newer assets before older assets.
    /// </summary>
    /// <param name="ct">Cancels the query.</param>
    /// <returns>A success containing all assets, or a failure created from the caught exception message.</returns>
Task<Result<IReadOnlyList<MediaAsset>, AeroError>> GetAllAsync(CancellationToken ct = default);
    /// <summary>
    /// Loads a media asset by document identifier.
    /// </summary>
    /// <param name="id">The asset identifier.</param>
    /// <param name="ct">Cancels the lookup.</param>
    /// <returns>A success containing the asset, or a not-found/database failure.</returns>
Task<Result<MediaAsset?, AeroError>> GetByIdAsync(long id, CancellationToken ct = default);
    /// <summary>
    /// Loads the direct children of a folder.
    /// </summary>
    /// <param name="parentId">The parent identifier, or <see langword="null"/> for root assets.</param>
    /// <param name="ct">Cancels the query.</param>
    /// <returns>A success containing folders first and newer children first, or a database failure.</returns>
Task<Result<IReadOnlyList<MediaAsset>, AeroError>> GetByFolderAsync(long? parentId, CancellationToken ct = default);
    /// <summary>
    /// Loads the first asset whose stored URL exactly matches the supplied path.
    /// </summary>
    /// <param name="url">The stored URL to match.</param>
    /// <param name="ct">Cancels the query.</param>
    /// <returns>A success containing the asset, or a not-found/database failure.</returns>
Task<Result<MediaAsset, AeroError>> GetByPathAsync(string url, CancellationToken ct = default);
    /// <summary>
    /// Loads a page of media assets and the provider-reported total result count.
    /// </summary>
    /// <param name="parentId">
    /// The folder to filter when <paramref name="search"/> is empty; ignored for non-empty searches.
    /// </param>
    /// <param name="skip">The number of matching rows to skip.</param>
    /// <param name="take">The maximum number of rows to return.</param>
    /// <param name="search">An optional case-normalized substring search over file name and alternate text.</param>
    /// <param name="ct">Cancels the query.</param>
    /// <returns>A success containing the page and total count, or a query failure.</returns>
Task<Result<(IReadOnlyList<MediaAsset> Items, long TotalCount), AeroError>> GetPagedAsync(
        long? parentId, int skip, int take, string? search = null, CancellationToken ct = default);
    /// <summary>
    /// Assigns a new Snowflake identifier to an asset and commits it.
    /// </summary>
    /// <param name="asset">The asset whose identifier will be overwritten before persistence.</param>
    /// <param name="ct">Cancels the commit.</param>
    /// <returns>A success containing the persisted asset, or a persistence failure.</returns>
Task<Result<MediaAsset, AeroError>> CreateAsync(MediaAsset asset, CancellationToken ct = default);
    /// <summary>
    /// Creates and commits a folder-shaped media asset.
    /// </summary>
    /// <param name="name">The folder file name; this service performs no validation or uniqueness check.</param>
    /// <param name="parentId">The optional parent folder identifier.</param>
    /// <param name="ct">Cancels the commit.</param>
    /// <returns>A success containing the persisted folder, or a persistence failure.</returns>
Task<Result<MediaAsset, AeroError>> CreateFolderAsync(string name, long? parentId = null, CancellationToken ct = default);
    /// <summary>
    /// Stores the caller-supplied complete asset state and commits it.
    /// </summary>
    /// <param name="asset">The asset state to store without an existence or ownership check.</param>
    /// <param name="ct">Cancels the commit.</param>
    /// <returns>A success containing the supplied asset, or a persistence failure.</returns>
Task<Result<MediaAsset, AeroError>> UpdateAsync(MediaAsset asset, CancellationToken ct = default);
    /// <summary>
    /// Deletes an asset and, for non-folders, its corresponding web-root media file when present.
    /// </summary>
    /// <param name="id">The asset identifier.</param>
    /// <param name="ct">Cancels database work; file deletion is synchronous and cannot be rolled back.</param>
    /// <returns>A successful <see langword="true"/> value, or a not-found/database/file-system failure.</returns>
Task<Result<bool, AeroError>> DeleteAsync(long id, CancellationToken ct = default);
    /// <summary>
    /// Imports supported media files from a web-root media subdirectory.
    /// </summary>
    /// <param name="subfolder">The subpath combined beneath the web-root <c>media</c> directory.</param>
    /// <param name="ct">Cancels file reads, duplicate queries, or the final commit.</param>
    /// <returns>The number of newly staged asset records, zero for a missing directory, or a failure.</returns>
    /// <remarks>
    /// The caller must supply a trusted, contained subpath; this contract does not validate traversal.
    /// Existing URLs are skipped. Malformed attribution sidecars are logged and ignored, and all new
    /// assets are committed together after enumeration.
    /// </remarks>
Task<Result<int, AeroError>> SeedFromDirectoryAsync(string subfolder, CancellationToken ct = default);
}

/// <summary>
/// Implements media operations over one scoped document session and the host web root.
/// </summary>
/// <param name="session">The session used for all queries, staging, and commits.</param>
/// <param name="env">Provides the web-root path for file deletion and seeding.</param>
/// <remarks>
/// Public methods catch all exceptions, including cancellation, and convert them to
/// <see cref="AeroError"/> failures rather than propagating them.
/// </remarks>
public sealed class MediaService(IDocumentSession session, IWebHostEnvironment env) : IMediaService
{
    /// <inheritdoc />
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

    /// <inheritdoc />
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

    /// <inheritdoc />
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

    /// <inheritdoc />
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

    /// <inheritdoc />
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

    /// <inheritdoc />
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

    /// <inheritdoc />
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

    /// <inheritdoc />
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

    /// <inheritdoc />
    /// <remarks>
    /// A matching physical file is removed before the document-session commit. A later commit
    /// failure therefore leaves the file deleted while the database record can remain.
    /// The stored <c>FileName</c> is used directly when constructing the deletion path; callers
    /// must ensure it is a trusted leaf filename because this method does not validate that the
    /// resolved path remains beneath the media directory.
    /// </remarks>
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

    /// <inheritdoc />
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
    /// <summary>
    /// Represents the Pexels attribution metadata accepted from a sidecar JSON file.
    /// </summary>
    /// <param name="Id">The source media identifier.</param>
    /// <param name="Photographer">The creator display name.</param>
    /// <param name="PhotographerUrl">The creator profile URL.</param>
    /// <param name="Url">The source media URL.</param>
    /// <param name="Alt">Optional source alternate text; currently not copied to the asset.</param>
    /// <param name="File">The source file name; currently not copied to the asset.</param>
    /// <param name="Type">The source media type; currently not copied to the asset.</param>
    /// <param name="DownloadedAt">The source download timestamp; currently not copied to the asset.</param>
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

