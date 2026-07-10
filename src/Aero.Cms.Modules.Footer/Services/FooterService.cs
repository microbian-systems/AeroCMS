using Aero.Cms.Abstractions.Events;
using Aero.Cms.Abstractions.Http.Clients;
using Aero.Cms.Core.Entities;
using Aero.Cms.Modules.Footer.Domain;
using Aero.Cms.Modules.Footer.Events;
using System.Globalization;
using Wolverine;
using static Aero.Core.Railway.Prelude;

namespace Aero.Cms.Modules.Footer.Services;

/// <summary>
/// Represents a class for FooterService.
/// </summary>
public sealed class FooterService(
    IDocumentSession session,
    ISiteContext siteContext,
    ILogger<FooterService> logger,
    IMessageBus? bus = null) : IFooterService
{
        /// <summary>
    /// ListAsync method.
    /// </summary>
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

            var stats = new global::AeroDB.Sable.QueryStatistics();
            var items = await ((global::AeroDB.Sable.ISurrealDbQueryable<FooterDocument>)query)
                .OrderBy(x => x.Name)
                .Stats(out stats)
                .Skip(skip)
                .Take(take)
                .ToListAsync(cancellationToken);

            return Ok<(IReadOnlyList<FooterDocument> Items, long TotalCount), AeroError>((items, stats.TotalResults));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to list footers for site {SiteId}", siteContext.SiteId);
            return Fail<(IReadOnlyList<FooterDocument> Items, long TotalCount), AeroError>(AeroError.DatabaseError(ex.Message));
        }
    }

        /// <summary>
    /// GetAsync method.
    /// </summary>
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

        /// <summary>
    /// GetDetailAsync method.
    /// </summary>
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

        /// <summary>
    /// ListCultureVariantsAsync method.
    /// </summary>
public async Task<Result<IReadOnlyList<FooterDetail>, AeroError>> ListCultureVariantsAsync(
        long id,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var footerResult = await GetAsync(id, cancellationToken);
            if (footerResult is Result<FooterDocument, AeroError>.Failure failure)
            {
                return Fail<IReadOnlyList<FooterDetail>, AeroError>(failure.Error);
            }

            var footer = ((Result<FooterDocument, AeroError>.Ok)footerResult).Value;
            var TranslationGroupId = footer.TranslationGroupId ?? footer.Id;
            var variants = await session.Query<FooterDocument>()
                .Where(x => x.SiteId == footer.SiteId &&
                            x.TranslationGroupId == TranslationGroupId &&
                            x.State != FooterLifecycleState.Archived)
                .OrderBy(x => x.Culture)
                .ToListAsync(cancellationToken);

            var details = new List<FooterDetail>(variants.Count);
            foreach (var variant in variants)
            {
                var snapshot = await LoadEditorSnapshotAsync(variant, cancellationToken);
                var version = await GetStreamVersionAsync(variant.Id, cancellationToken);
                details.Add(MapDetail(variant, snapshot, version));
            }

            return Ok<IReadOnlyList<FooterDetail>, AeroError>(details);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to list footer culture variants for {FooterId}", id);
            return Fail<IReadOnlyList<FooterDetail>, AeroError>(AeroError.DatabaseError(ex.Message));
        }
    }

        /// <summary>
    /// GetDefaultIdAsync method.
    /// </summary>
public async Task<Result<long?, AeroError>> GetDefaultIdAsync(long siteId, CancellationToken cancellationToken = default)
    {
        try
        {
            var settings = await session.Query<SiteFooterSettingsDocument>()
                .FirstOrDefaultAsync(x => x.SiteId == siteId, cancellationToken);

            return Ok<long?, AeroError>(settings?.DefaultFooterId);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return Ok<long?, AeroError>(null);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to load default footer id for site {SiteId}", siteId);
            return Fail<long?, AeroError>(AeroError.DatabaseError(ex.Message));
        }
    }

        /// <summary>
    /// GetPublishedSnapshotAsync method.
    /// </summary>
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

            var events = await session.Events.FetchStreamAsync(FooterStreams.Footer(id), ct: cancellationToken);
            var published = events
                .OrderByDescending(x => x.Version)
                .Select(x => x.Data)
                .OfType<FooterPublished>()
                .FirstOrDefault();

            return Ok<FooterSnapshot?, AeroError>(published?.Snapshot);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return Ok<FooterSnapshot?, AeroError>(null);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to load published footer snapshot {FooterId}", id);
            return Fail<FooterSnapshot?, AeroError>(AeroError.DatabaseError(ex.Message));
        }
    }

        /// <summary>
    /// ResolveSnapshotAsync method.
    /// </summary>
public async Task<Result<FooterSnapshot?, AeroError>> ResolveSnapshotAsync(
        long siteId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var defaultResult = await GetDefaultIdAsync(siteId, cancellationToken);
            if (defaultResult is Result<long?, AeroError>.Failure failure)
            {
                return Fail<FooterSnapshot?, AeroError>(failure.Error);
            }

            var footerId = ((Result<long?, AeroError>.Ok)defaultResult).Value;
            if (footerId is not null)
            {
                footerId = await ResolveCultureVariantIdAsync(siteId, footerId.Value, GetCurrentCulture(), cancellationToken);
                return await GetPublishedSnapshotAsync(footerId.Value, cancellationToken);
            }

            var fallback = await session.Query<FooterDocument>()
                .Where(x => x.SiteId == siteId && x.State != FooterLifecycleState.Archived && x.HasPublishedSnapshot)
                .OrderBy(x => x.CreatedOn)
                .FirstOrDefaultAsync(cancellationToken);

            return fallback is null
                ? Ok<FooterSnapshot?, AeroError>(null)
                : await GetPublishedSnapshotAsync(fallback.Id, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return Ok<FooterSnapshot?, AeroError>(null);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to load fallback footer snapshot for site {SiteId}", siteId);
            return Fail<FooterSnapshot?, AeroError>(AeroError.DatabaseError(ex.Message));
        }
    }

        /// <summary>
    /// CreateAsync method.
    /// </summary>
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

            var culture = await GetSiteDefaultCultureAsync(cancellationToken);
            var key = FooterDocument.NormalizeKey(string.IsNullOrWhiteSpace(request.Name) ? "footer" : request.Name);
            var duplicate = await session.Query<FooterDocument>()
                .Where(x => x.SiteId == siteId && x.Culture == culture && x.Key == key).AnyAsync(cancellationToken);
            if (duplicate)
            {
                return Fail<FooterDocument, AeroError>(AeroError.ConflictError($"Footer key '{key}' already exists for this site."));
            }

            var id = Snowflake.NewId();
            var now = DateTimeOffset.UtcNow;
            var snapshot = MapSnapshot(request);
            snapshot.Validate();

            var created = new FooterCreated(siteId, request.Name, key, request.Description, userId, now, Culture: culture, TranslationGroupId: id);
            var draftSaved = new FooterDraftSaved(siteId, request.Name, key, request.Description, snapshot, userId, now, "Initial draft");

            session.Events.StartStream(FooterStreams.Footer(id), new object[] { created, draftSaved });
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

        /// <summary>
    /// SaveDraftAsync method.
    /// </summary>
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

            await session.Events.AppendOptimistic(FooterStreams.Footer(id), expectedVersion, [draftSaved], cancellationToken);
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

        /// <summary>
    /// PublishAsync method.
    /// </summary>
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

            await session.Events.AppendOptimistic(FooterStreams.Footer(id), expectedVersion, [published], cancellationToken);
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

        /// <summary>
    /// SetDefaultAsync method.
    /// </summary>
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
                session.Events.StartStream(streamKey, new object[] { changed });
            else
                session.Events.Append(streamKey, new object[] { changed });

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

        /// <summary>
    /// ArchiveAsync method.
    /// </summary>
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
            await session.Events.AppendOptimistic(FooterStreams.Footer(id), expectedVersion, [archived], cancellationToken);
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
        var events = await session.Events.FetchStreamAsync(FooterStreams.Footer(footer.Id), ct: cancellationToken);
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
        var events = await session.Events.FetchStreamAsync(FooterStreams.Footer(id), ct: cancellationToken);
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

    private async Task<string> GetSiteDefaultCultureAsync(CancellationToken cancellationToken)
    {
        var site = await session.LoadAsync<SitesModel>(siteContext.SiteId, cancellationToken);
        return NormalizeCulture(site?.DefaultCulture);
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

        /// <summary>
    /// ForkToCultureAsync method.
    /// </summary>
public async Task<Result<FooterDocument, AeroError>> ForkToCultureAsync(
        long id,
        string targetCulture,
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

            var source = ((Result<FooterDocument, AeroError>.Ok)footerResult).Value;
            var culture = NormalizeCulture(targetCulture);
            var TranslationGroupId = source.TranslationGroupId ?? source.Id;

            var duplicate = await session.Query<FooterDocument>()
                .Where(x =>
                    x.SiteId == source.SiteId &&
                    x.TranslationGroupId == TranslationGroupId &&
                    x.Culture == culture &&
                    x.State != FooterLifecycleState.Archived)
                .AnyAsync(cancellationToken);
            if (duplicate)
            {
                return Fail<FooterDocument, AeroError>(AeroError.ConflictError($"A {culture} footer translation already exists."));
            }

            var sourceSnapshot = await LoadEditorSnapshotAsync(source, cancellationToken);
            var targetId = Snowflake.NewId();
            var fork = FooterCultureForker.Fork(source, sourceSnapshot, targetId, culture, userId);

            session.Events.StartStream(FooterStreams.Footer(targetId), new object[] { fork.Created, fork.DraftSaved });
            await session.SaveChangesAsync(cancellationToken);

            var footer = FooterDocument.Create(targetId, fork.Created);
            footer.Apply(fork.DraftSaved);
            return Ok<FooterDocument, AeroError>(footer);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to fork footer {FooterId} to {Culture}", id, targetCulture);
            return Fail<FooterDocument, AeroError>(AeroError.DatabaseError(ex.Message));
        }
    }

    private async Task<long> ResolveCultureVariantIdAsync(
        long siteId,
        long defaultFooterId,
        string culture,
        CancellationToken cancellationToken)
    {
        var defaultFooter = await session.LoadAsync<FooterDocument>(defaultFooterId, cancellationToken);
        if (defaultFooter is null ||
            string.Equals(defaultFooter.Culture, culture, StringComparison.OrdinalIgnoreCase) ||
            defaultFooter.TranslationGroupId is null)
        {
            return defaultFooterId;
        }

        var cultureVariant = await session.Query<FooterDocument>()
            .Where(x => x.SiteId == siteId &&
                        x.TranslationGroupId == defaultFooter.TranslationGroupId &&
                        x.Culture == culture &&
                        x.State != FooterLifecycleState.Archived &&
                        x.HasPublishedSnapshot)
            .FirstOrDefaultAsync(cancellationToken);

        return cultureVariant?.Id ?? defaultFooterId;
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

    private static string GetCurrentCulture()
        => NormalizeCulture(CultureInfo.CurrentUICulture.Name);

    private static string NormalizeCulture(string? culture)
    {
        if (string.IsNullOrWhiteSpace(culture))
            return SitesModel.DefaultCultureName;

        try
        {
            return CultureInfo.GetCultureInfo(culture.Trim()).Name;
        }
        catch (CultureNotFoundException)
        {
            return SitesModel.DefaultCultureName;
        }
    }

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
            Legal = FooterLegalSettings.Default with
            {
                CopyrightText = Clean(request.CopyrightText),
                LegalLinks = MapLegalLinksCreate(request.LegalLinks ?? [])
            },
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
            Legal = FooterLegalSettings.Default with
            {
                CopyrightText = Clean(request.CopyrightText),
                LegalLinks = MapLegalLinksUpdate(request.LegalLinks ?? [])
            },
            Rows = request.Rows.Count > 0
                ? request.Rows
                    .OrderBy(x => x.Order)
                    .Select(MapRow)
                    .ToList()
                : [],
            Sections = request.Rows.Count > 0
                ? []
                : request.Components.Count > 0
                ? request.Components
                    .OrderBy(x => x.Order)
                    .Select(MapComponent)
                    .ToList()
                : request.LinkGroups
                    .OrderBy(x => x.Order)
                    .Select(MapLinkGroup)
                    .Cast<IFooterComponent>()
                    .ToList()
        };

    private static FooterCanvasRow MapRow(UpdateFooterCanvasRowRequest row)
        => new()
        {
            Key = (row.Id == 0 ? Snowflake.NewId() : row.Id).ToString(),
            Order = row.Order,
            Label = Clean(row.Label),
            DesktopDisplay = Clean(row.DesktopDisplay) ?? "Grid",
            TabletDisplay = Clean(row.TabletDisplay) ?? "Grid",
            MobileDisplay = Clean(row.MobileDisplay) ?? "Stack",
            Columns = row.Columns
                .OrderBy(x => x.Order)
                .Select(column => new FooterCanvasColumn
                {
                    Key = (column.Id == 0 ? Snowflake.NewId() : column.Id).ToString(),
                    Order = column.Order,
                    DesktopSpan = Math.Clamp(column.DesktopSpan, 1, 12),
                    TabletSpan = Math.Clamp(column.TabletSpan, 1, 12),
                    MobileSpan = Math.Clamp(column.MobileSpan, 1, 12),
                    Blocks = column.Blocks
                        .OrderBy(block => block.Order)
                        .Select(block =>
                        {
                            var component = MapComponent(block);
                            return new FooterCanvasBlock
                            {
                                Key = component.Key,
                                Order = block.Order,
                                Component = component
                            };
                        })
                        .ToList()
                })
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

    private static IFooterComponent MapComponent(UpdateFooterComponentRequest component)
    {
        var key = (component.Id == 0 ? Snowflake.NewId() : component.Id).ToString();
        var placement = ParsePlacement(component.Placement);
        var kind = component.Kind.Trim().ToLowerInvariant();

        return kind switch
        {
            "text" => new FooterTextBlock
            {
                Key = key,
                Order = component.Order,
                Placement = placement,
                Text = component.Text?.Trim() ?? string.Empty
            },
            "social" or "sociallinks" => new FooterSocialLinks
            {
                Key = key,
                Order = component.Order,
                Placement = placement,
                Links = component.SocialLinks
                    .Select(x => new FooterSocialLink(x.Platform.Trim(), x.Href.Trim()))
                    .ToList()
            },
            "newsletter" => new FooterNewsletterSignup
            {
                Key = key,
                Order = component.Order,
                Placement = placement,
                EndpointKey = component.EndpointKey?.Trim() ?? string.Empty,
                Placeholder = string.IsNullOrWhiteSpace(component.Placeholder) ? "Email address" : component.Placeholder.Trim(),
                ButtonLabel = string.IsNullOrWhiteSpace(component.ButtonLabel) ? "Subscribe" : component.ButtonLabel.Trim()
            },
            "search" => new FooterSearch
            {
                Key = key,
                Order = component.Order,
                Placement = placement,
                Placeholder = string.IsNullOrWhiteSpace(component.Placeholder) ? "Search..." : component.Placeholder.Trim(),
                SearchAction = string.IsNullOrWhiteSpace(component.SearchAction) ? "/search" : component.SearchAction.Trim()
            },
            "spacer" => new FooterSpacer
            {
                Key = key,
                Order = component.Order,
                Placement = placement,
                SizeToken = string.IsNullOrWhiteSpace(component.SizeToken) ? "md" : component.SizeToken.Trim()
            },
            _ => new FooterLinkGroup
            {
                Key = key,
                Order = component.Order,
                Placement = placement,
                Title = component.Title?.Trim() ?? "Links",
                Links = component.Links
                    .OrderBy(x => x.Order)
                    .Select(x => new FooterLink(x.Label.Trim(), x.Href.Trim(), x.OpenInNewTab, x.Id == 0 ? Snowflake.NewId() : x.Id))
                    .ToList()
            }
        };
    }

    private static FooterDetail MapDetail(FooterDocument footer, FooterSnapshot snapshot, long version)
        => new(
            footer.Id,
            footer.Name,
            footer.Description,
            snapshot.Components.OfType<FooterLinkGroup>()
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
            snapshot.Legal.CopyrightText,
            footer.Culture,
            footer.TranslationGroupId,
            snapshot.Legal.LegalLinks.Select(x => new FooterLinkDetail(x.Id, x.Label, x.Href, 0, x.OpenInNewTab)).ToList(),
            snapshot.Components
                .OrderBy(x => x.Order)
                .Select(MapComponentDetail)
                .ToList(),
            snapshot.Rows
                .OrderBy(x => x.Order)
                .Select(MapRowDetail)
                .ToList());

    private static FooterCanvasRowDetail MapRowDetail(FooterCanvasRow row)
        => new(
            ParseKey(row.Key),
            row.Order,
            row.Label,
            row.DesktopDisplay,
            row.TabletDisplay,
            row.MobileDisplay,
            row.Columns.OrderBy(x => x.Order).Select(MapColumnDetail).ToList());

    private static FooterCanvasColumnDetail MapColumnDetail(FooterCanvasColumn column)
        => new(
            ParseKey(column.Key),
            column.Order,
            column.DesktopSpan,
            column.TabletSpan,
            column.MobileSpan,
            column.Blocks.OrderBy(x => x.Order).Select(x => MapComponentDetail(x.Component)).ToList());

    private static FooterComponentDetail MapComponentDetail(IFooterComponent component)
        => component switch
        {
            FooterLinkGroup group => new FooterComponentDetail(
                ParseKey(group.Key),
                "linkGroup",
                group.Order,
                group.Placement.ToString(),
                group.Title,
                Links: group.Links.Select((link, index) => new FooterLinkDetail(link.Id, link.Label, link.Href, index, link.OpenInNewTab)).ToList()),
            FooterTextBlock text => new FooterComponentDetail(
                ParseKey(text.Key),
                "text",
                text.Order,
                text.Placement.ToString(),
                Text: text.Text),
            FooterSocialLinks social => new FooterComponentDetail(
                ParseKey(social.Key),
                "social",
                social.Order,
                social.Placement.ToString(),
                SocialLinks: social.Links.Select(x => new FooterSocialLinkDetail(x.Platform, x.Href)).ToList()),
            FooterNewsletterSignup newsletter => new FooterComponentDetail(
                ParseKey(newsletter.Key),
                "newsletter",
                newsletter.Order,
                newsletter.Placement.ToString(),
                EndpointKey: newsletter.EndpointKey,
                Placeholder: newsletter.Placeholder,
                ButtonLabel: newsletter.ButtonLabel),
            FooterSearch search => new FooterComponentDetail(
                ParseKey(search.Key),
                "search",
                search.Order,
                search.Placement.ToString(),
                Placeholder: search.Placeholder,
                SearchAction: search.SearchAction),
            FooterSpacer spacer => new FooterComponentDetail(
                ParseKey(spacer.Key),
                "spacer",
                spacer.Order,
                spacer.Placement.ToString(),
                SizeToken: spacer.SizeToken),
            _ => new FooterComponentDetail(0, "unknown", component.Order, component.Placement.ToString())
        };

    private static FooterSectionPlacement ParsePlacement(string? placement)
        => Enum.TryParse<FooterSectionPlacement>(placement, true, out var parsed)
            ? parsed
            : FooterSectionPlacement.Main;

    private static long ParseKey(string key)
        => long.TryParse(key, out var id) ? id : 0;

    private static List<FooterLink> MapLegalLinksCreate(IReadOnlyList<CreateFooterLinkRequest> links)
        => links.OrderBy(x => x.Order).Select(x => new FooterLink(x.Label.Trim(), x.Href.Trim(), x.OpenInNewTab, Snowflake.NewId())).ToList();

    private static List<FooterLink> MapLegalLinksUpdate(IReadOnlyList<UpdateFooterLinkRequest> links)
        => links.OrderBy(x => x.Order).Select(x => new FooterLink(x.Label.Trim(), x.Href.Trim(), x.OpenInNewTab, x.Id == 0 ? Snowflake.NewId() : x.Id)).ToList();

    private static string? Clean(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

