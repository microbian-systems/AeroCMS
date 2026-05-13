using Aero.Cms.Abstractions.Events;
using Aero.Cms.Abstractions.Http.Clients;
using Aero.Cms.Abstractions.Interfaces;
using Aero.Cms.Modules.Footer.Domain;
using Aero.Cms.Modules.Footer.Events;
using Wolverine;
using static Aero.Core.Railway.Prelude;

namespace Aero.Cms.Modules.Footer.Services;

public sealed class FooterService(
    IDocumentSession session,
    ISiteContext siteContext,
    ILogger<FooterService> logger,
    IMessageBus? bus = null) : IFooterService
{
    public async Task<Result<(IReadOnlyList<FooterDocument> Items, long TotalCount), AeroError>> ListAsync(
        int skip = 0,
        int take = 20,
        string? search = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var query = session.Query<FooterDocument>()
                .Where(x => x.SiteId == siteContext.SiteId && x.State != FooterLifecycleState.Archived);

            if (!string.IsNullOrWhiteSpace(search))
            {
                var s = search.Trim().ToLowerInvariant();
                query = query.Where(x => x.Name.ToLower().Contains(s) || x.Key.ToLower().Contains(s));
            }

            var stats = new global::Marten.Linq.QueryStatistics();
            var items = await ((global::Marten.Linq.IMartenQueryable<FooterDocument>)query)
                .OrderBy(x => x.Name)
                .Stats(out stats)
                .Skip(skip)
                .Take(take)
                .ToListAsync(token: cancellationToken);

            return Ok<(IReadOnlyList<FooterDocument> Items, long TotalCount), AeroError>((items, stats.TotalResults));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to list footers for site {SiteId}", siteContext.SiteId);
            return Fail<(IReadOnlyList<FooterDocument> Items, long TotalCount), AeroError>(AeroError.DatabaseError(ex.Message));
        }
    }

    public async Task<Result<FooterDocument, AeroError>> GetAsync(long id, CancellationToken cancellationToken = default)
    {
        try
        {
            var footer = await session.LoadAsync<FooterDocument>(id, cancellationToken);
            if (footer is null || footer.SiteId != siteContext.SiteId)
            {
                return Fail<FooterDocument, AeroError>(AeroError.NotFoundError($"Footer '{id}' not found or access denied."));
            }

            return Ok<FooterDocument, AeroError>(footer);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to load footer {FooterId}", id);
            return Fail<FooterDocument, AeroError>(AeroError.DatabaseError(ex.Message));
        }
    }

    public async Task<Result<FooterDetail, AeroError>> GetDetailAsync(long id, CancellationToken cancellationToken = default)
    {
        var footerResult = await GetAsync(id, cancellationToken);
        if (footerResult is Result<FooterDocument, AeroError>.Failure failure)
        {
            return Fail<FooterDetail, AeroError>(failure.Error);
        }

        var footer = ((Result<FooterDocument, AeroError>.Ok)footerResult).Value;
        var snapshot = await LoadEditorSnapshotAsync(footer, cancellationToken);
        var version = await GetStreamVersionAsync(footer.Id, cancellationToken);
        return Ok<FooterDetail, AeroError>(MapDetail(footer, snapshot, version));
    }

    public async Task<Result<long?, AeroError>> GetDefaultIdAsync(long siteId, CancellationToken cancellationToken = default)
    {
        try
        {
            var settings = await session.Query<SiteFooterSettingsDocument>()
                .FirstOrDefaultAsync(x => x.SiteId == siteId, cancellationToken);

            return Ok<long?, AeroError>(settings?.DefaultFooterId);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to load default footer id for site {SiteId}", siteId);
            return Fail<long?, AeroError>(AeroError.DatabaseError(ex.Message));
        }
    }

    public async Task<Result<FooterSnapshot?, AeroError>> GetPublishedSnapshotAsync(
        long id,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var footer = await session.LoadAsync<FooterDocument>(id, cancellationToken);
            if (footer is null || footer.State == FooterLifecycleState.Archived || !footer.HasPublishedSnapshot)
            {
                return Ok<FooterSnapshot?, AeroError>(null);
            }

            var events = await session.Events.FetchStreamAsync(FooterStreams.Footer(id), token: cancellationToken);
            var published = events
                .OrderByDescending(x => x.Version)
                .Select(x => x.Data)
                .OfType<FooterPublished>()
                .FirstOrDefault();

            return Ok<FooterSnapshot?, AeroError>(published?.Snapshot);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to load published footer snapshot {FooterId}", id);
            return Fail<FooterSnapshot?, AeroError>(AeroError.DatabaseError(ex.Message));
        }
    }

    public async Task<Result<FooterSnapshot?, AeroError>> ResolveSnapshotAsync(
        long siteId,
        CancellationToken cancellationToken = default)
    {
        var defaultResult = await GetDefaultIdAsync(siteId, cancellationToken);
        if (defaultResult is Result<long?, AeroError>.Failure failure)
        {
            return Fail<FooterSnapshot?, AeroError>(failure.Error);
        }

        var footerId = ((Result<long?, AeroError>.Ok)defaultResult).Value;
        if (footerId is not null)
        {
            return await GetPublishedSnapshotAsync(footerId.Value, cancellationToken);
        }

        try
        {
            var fallback = await session.Query<FooterDocument>()
                .Where(x => x.SiteId == siteId && x.State != FooterLifecycleState.Archived && x.HasPublishedSnapshot)
                .OrderBy(x => x.CreatedOn)
                .FirstOrDefaultAsync(cancellationToken);

            return fallback is null
                ? Ok<FooterSnapshot?, AeroError>(null)
                : await GetPublishedSnapshotAsync(fallback.Id, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to load fallback footer snapshot for site {SiteId}", siteId);
            return Fail<FooterSnapshot?, AeroError>(AeroError.DatabaseError(ex.Message));
        }
    }

    public async Task<Result<FooterDocument, AeroError>> CreateAsync(
        CreateFooterRequest request,
        long? userId = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var siteId = siteContext.SiteId;
            if (siteId <= 0)
            {
                return Fail<FooterDocument, AeroError>(AeroError.InvalidRequestError("A current manager site is required."));
            }

            var key = FooterDocument.NormalizeKey(string.IsNullOrWhiteSpace(request.Name) ? "footer" : request.Name);
            var duplicate = await session.Query<FooterDocument>()
                .AnyAsync(x => x.SiteId == siteId && x.Key == key, cancellationToken);
            if (duplicate)
            {
                return Fail<FooterDocument, AeroError>(AeroError.ConflictError($"Footer key '{key}' already exists for this site."));
            }

            var id = Snowflake.NewId();
            var now = DateTimeOffset.UtcNow;
            var snapshot = MapSnapshot(request);
            snapshot.Validate();

            var created = new FooterCreated(siteId, request.Name, key, request.Description, userId, now);
            var draftSaved = new FooterDraftSaved(siteId, request.Name, key, request.Description, snapshot, userId, now, "Initial draft");

            session.Events.StartStream(FooterStreams.Footer(id), created, draftSaved);
            await session.SaveChangesAsync(cancellationToken);

            var footer = FooterDocument.Create(id, created);
            footer.Apply(draftSaved);
            return Ok<FooterDocument, AeroError>(footer);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to create footer {Name}", request.Name);
            return Fail<FooterDocument, AeroError>(AeroError.DatabaseError(ex.Message));
        }
    }

    public async Task<Result<FooterDocument, AeroError>> SaveDraftAsync(
        long id,
        UpdateFooterRequest request,
        long expectedVersion,
        long? userId = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var footerResult = await GetAsync(id, cancellationToken);
            if (footerResult is Result<FooterDocument, AeroError>.Failure failure)
            {
                return Fail<FooterDocument, AeroError>(failure.Error);
            }

            await EnsureExpectedVersionAsync(id, expectedVersion, cancellationToken);

            var footer = ((Result<FooterDocument, AeroError>.Ok)footerResult).Value;
            var snapshot = MapSnapshot(request);
            snapshot.Validate();
            var draftSaved = new FooterDraftSaved(
                footer.SiteId,
                request.Name,
                footer.Key,
                request.Description,
                snapshot,
                userId,
                DateTimeOffset.UtcNow,
                null);

            await session.Events.AppendOptimistic(FooterStreams.Footer(id), cancellationToken, draftSaved);
            await session.SaveChangesAsync(cancellationToken);

            footer.Apply(draftSaved);
            return Ok<FooterDocument, AeroError>(footer);
        }
        catch (InvalidOperationException ex)
        {
            return Fail<FooterDocument, AeroError>(AeroError.ConflictError(ex.Message));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to save footer draft {FooterId}", id);
            return Fail<FooterDocument, AeroError>(AeroError.DatabaseError(ex.Message));
        }
    }

    public async Task<Result<FooterDocument, AeroError>> PublishAsync(
        long id,
        long expectedVersion,
        long? userId = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var footerResult = await GetAsync(id, cancellationToken);
            if (footerResult is Result<FooterDocument, AeroError>.Failure failure)
            {
                return Fail<FooterDocument, AeroError>(failure.Error);
            }

            await EnsureExpectedVersionAsync(id, expectedVersion, cancellationToken);

            var footer = ((Result<FooterDocument, AeroError>.Ok)footerResult).Value;
            var draft = await LoadLatestDraftAsync(id, cancellationToken);
            if (draft is null)
            {
                return Fail<FooterDocument, AeroError>(AeroError.InvalidRequestError("Footer has no draft to publish."));
            }

            draft.Snapshot.Validate();
            var published = new FooterPublished(footer.SiteId, draft.Snapshot, userId, DateTimeOffset.UtcNow, draft.ChangeNote);

            await session.Events.AppendOptimistic(FooterStreams.Footer(id), cancellationToken, published);
            await session.SaveChangesAsync(cancellationToken);

            footer.Apply(published);
            await PublishFooterChangedAsync(footer.Id, footer.SiteId, FooterChangeKind.Published, published.PublishedOn, cancellationToken);
            return Ok<FooterDocument, AeroError>(footer);
        }
        catch (InvalidOperationException ex)
        {
            return Fail<FooterDocument, AeroError>(AeroError.ConflictError(ex.Message));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to publish footer {FooterId}", id);
            return Fail<FooterDocument, AeroError>(AeroError.DatabaseError(ex.Message));
        }
    }

    public async Task<Result<bool, AeroError>> SetDefaultAsync(
        long id,
        long? userId = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var footerResult = await GetAsync(id, cancellationToken);
            if (footerResult is Result<FooterDocument, AeroError>.Failure failure)
            {
                return Fail<bool, AeroError>(failure.Error);
            }

            var footer = ((Result<FooterDocument, AeroError>.Ok)footerResult).Value;
            if (!footer.HasPublishedSnapshot || footer.State == FooterLifecycleState.Archived)
            {
                return Fail<bool, AeroError>(AeroError.InvalidRequestError("Only published footers can be set as default."));
            }

            var settings = await session.Query<SiteFooterSettingsDocument>()
                .FirstOrDefaultAsync(x => x.SiteId == footer.SiteId, cancellationToken);
            var changed = new SiteDefaultFooterChanged(footer.SiteId, footer.Id, userId, DateTimeOffset.UtcNow);
            var streamKey = FooterStreams.SiteSettings(footer.SiteId);

            if (settings is null)
                session.Events.StartStream(streamKey, changed);
            else
                session.Events.Append(streamKey, changed);

            await session.SaveChangesAsync(cancellationToken);
            await PublishFooterChangedAsync(footer.Id, footer.SiteId, FooterChangeKind.DefaultChanged, changed.ChangedOn, cancellationToken);
            return Ok<bool, AeroError>(true);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to set default footer {FooterId}", id);
            return Fail<bool, AeroError>(AeroError.DatabaseError(ex.Message));
        }
    }

    public async Task<Result<bool, AeroError>> ArchiveAsync(
        long id,
        long expectedVersion,
        long? userId = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var footerResult = await GetAsync(id, cancellationToken);
            if (footerResult is Result<FooterDocument, AeroError>.Failure failure)
            {
                return Fail<bool, AeroError>(failure.Error);
            }

            var footer = ((Result<FooterDocument, AeroError>.Ok)footerResult).Value;
            await EnsureExpectedVersionAsync(id, expectedVersion, cancellationToken);

            var archived = new FooterArchived(footer.SiteId, userId, DateTimeOffset.UtcNow);
            await session.Events.AppendOptimistic(FooterStreams.Footer(id), cancellationToken, archived);
            await session.SaveChangesAsync(cancellationToken);

            footer.Apply(archived);
            await PublishFooterChangedAsync(footer.Id, footer.SiteId, FooterChangeKind.Archived, archived.ArchivedOn, cancellationToken);
            return Ok<bool, AeroError>(true);
        }
        catch (InvalidOperationException ex)
        {
            return Fail<bool, AeroError>(AeroError.ConflictError(ex.Message));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to archive footer {FooterId}", id);
            return Fail<bool, AeroError>(AeroError.DatabaseError(ex.Message));
        }
    }

    private async Task<FooterSnapshot> LoadEditorSnapshotAsync(FooterDocument footer, CancellationToken cancellationToken)
    {
        var events = await session.Events.FetchStreamAsync(FooterStreams.Footer(footer.Id), token: cancellationToken);
        var published = events
            .OrderByDescending(x => x.Version)
            .Select(x => x.Data)
            .OfType<FooterPublished>()
            .FirstOrDefault();

        if (footer.State == FooterLifecycleState.Published && published is not null)
        {
            return published.Snapshot;
        }

        var draft = events
            .OrderByDescending(x => x.Version)
            .Select(x => x.Data)
            .OfType<FooterDraftSaved>()
            .FirstOrDefault();

        return draft?.Snapshot ?? published?.Snapshot ?? FooterSnapshot.Empty;
    }

    private async Task<FooterDraftSaved?> LoadLatestDraftAsync(long id, CancellationToken cancellationToken)
    {
        var events = await session.Events.FetchStreamAsync(FooterStreams.Footer(id), token: cancellationToken);
        var latestDraft = events.OrderByDescending(x => x.Version).FirstOrDefault(x => x.Data is FooterDraftSaved);
        if (latestDraft is null)
        {
            return null;
        }

        var latestPublishedVersion = events.OrderByDescending(x => x.Version).FirstOrDefault(x => x.Data is FooterPublished)?.Version ?? 0;
        return latestPublishedVersion > latestDraft.Version
            ? null
            : (FooterDraftSaved)latestDraft.Data;
    }

    private async Task<long> GetStreamVersionAsync(long id, CancellationToken cancellationToken)
    {
        var state = await session.Events.FetchStreamStateAsync(FooterStreams.Footer(id), cancellationToken);
        return state?.Version ?? 0;
    }

    private async Task EnsureExpectedVersionAsync(long id, long expectedVersion, CancellationToken cancellationToken)
    {
        if (expectedVersion <= 0)
        {
            return;
        }

        var currentVersion = await GetStreamVersionAsync(id, cancellationToken);
        if (currentVersion != expectedVersion)
        {
            throw new InvalidOperationException("Footer was modified by another user.");
        }
    }

    private Task PublishFooterChangedAsync(
        long footerId,
        long siteId,
        FooterChangeKind changeKind,
        DateTimeOffset changedOn,
        CancellationToken cancellationToken)
        => bus is null
            ? Task.CompletedTask
            : bus.PublishAsync(new FooterChangedEvent(footerId, siteId, changeKind, changedOn)).AsTask();

    private static FooterSnapshot MapSnapshot(CreateFooterRequest request)
        => new()
        {
            Brand = new FooterBrandSettings
            {
                CompanyName = string.IsNullOrWhiteSpace(request.CompanyName) ? "Aero CMS" : request.CompanyName.Trim(),
                Tagline = Clean(request.Tagline),
                LogoUrl = Clean(request.LogoUrl),
                LogoAltText = string.IsNullOrWhiteSpace(request.CompanyName) ? "Aero CMS logo" : $"{request.CompanyName.Trim()} logo"
            },
            Style = FooterStyleSettings.Default with
            {
                BackgroundImageUrl = Clean(request.BackgroundImageUrl),
                OverlayOpacity = request.OverlayOpacity
            },
            Legal = FooterLegalSettings.Default with { CopyrightText = Clean(request.CopyrightText) },
            Sections = (request.LinkGroups ?? [])
                .OrderBy(x => x.Order)
                .Select(MapLinkGroup)
                .Cast<IFooterComponent>()
                .ToList()
        };

    private static FooterSnapshot MapSnapshot(UpdateFooterRequest request)
        => new()
        {
            Brand = new FooterBrandSettings
            {
                CompanyName = string.IsNullOrWhiteSpace(request.CompanyName) ? "Aero CMS" : request.CompanyName.Trim(),
                Tagline = Clean(request.Tagline),
                LogoUrl = Clean(request.LogoUrl),
                LogoAltText = string.IsNullOrWhiteSpace(request.CompanyName) ? "Aero CMS logo" : $"{request.CompanyName.Trim()} logo"
            },
            Style = FooterStyleSettings.Default with
            {
                BackgroundImageUrl = Clean(request.BackgroundImageUrl),
                OverlayOpacity = request.OverlayOpacity
            },
            Legal = FooterLegalSettings.Default with { CopyrightText = Clean(request.CopyrightText) },
            Sections = request.LinkGroups
                .OrderBy(x => x.Order)
                .Select(MapLinkGroup)
                .Cast<IFooterComponent>()
                .ToList()
        };

    private static FooterLinkGroup MapLinkGroup(CreateFooterLinkGroupRequest group)
        => new()
        {
            Key = Snowflake.NewId().ToString(),
            Order = group.Order,
            Title = group.Title.Trim(),
            Links = group.Links.OrderBy(x => x.Order).Select(x => new FooterLink(x.Label.Trim(), x.Href.Trim(), x.OpenInNewTab, Snowflake.NewId())).ToList()
        };

    private static FooterLinkGroup MapLinkGroup(UpdateFooterLinkGroupRequest group)
        => new()
        {
            Key = (group.Id == 0 ? Snowflake.NewId() : group.Id).ToString(),
            Order = group.Order,
            Title = group.Title.Trim(),
            Links = group.Links.OrderBy(x => x.Order).Select(x => new FooterLink(x.Label.Trim(), x.Href.Trim(), x.OpenInNewTab, x.Id == 0 ? Snowflake.NewId() : x.Id)).ToList()
        };

    private static FooterDetail MapDetail(FooterDocument footer, FooterSnapshot snapshot, long version)
        => new(
            footer.Id,
            footer.Name,
            footer.Description,
            snapshot.Sections.OfType<FooterLinkGroup>()
                .Select((x, index) => new FooterLinkGroupDetail(
                    ParseKey(x.Key),
                    x.Title,
                    x.Links.Select((link, linkIndex) => new FooterLinkDetail(link.Id, link.Label, link.Href, linkIndex, link.OpenInNewTab)).ToList(),
                    index))
                .ToList(),
            footer.CreatedOn.DateTime,
            (footer.ModifiedOn ?? footer.CreatedOn).DateTime,
            version,
            footer.State.ToString(),
            snapshot.Brand.CompanyName,
            snapshot.Brand.Tagline,
            snapshot.Brand.LogoUrl,
            snapshot.Style.BackgroundImageUrl,
            snapshot.Style.OverlayOpacity,
            snapshot.Legal.CopyrightText);

    private static long ParseKey(string key)
        => long.TryParse(key, out var id) ? id : 0;

    private static string? Clean(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
