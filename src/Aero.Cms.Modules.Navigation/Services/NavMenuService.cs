using Aero.Cms.Abstractions.Http.Clients;
using Aero.Cms.Abstractions.Events;
using Aero.Cms.Core.Entities;
using Aero.Cms.Modules.Navigation.Domain;
using Aero.Cms.Modules.Navigation.Events;
using System.Globalization;
using Wolverine;
using static Aero.Core.Railway.Prelude;

namespace Aero.Cms.Modules.Navigation.Services;

public sealed class NavMenuService(
    IDocumentSession session,
    ISiteContext siteContext,
    ILogger<NavMenuService> logger,
    IMessageBus? bus = null) : INavMenuService
{
    public async Task<Result<(IReadOnlyList<NavMenuDocument> Items, long TotalCount), AeroError>> ListAsync(
        int skip = 0,
        int take = 20,
        string? search = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var query = session.Query<NavMenuDocument>()
                .Where(x => x.SiteId == siteContext.SiteId && x.State != NavMenuLifecycleState.Archived);

            if (!string.IsNullOrWhiteSpace(search))
            {
                var s = search.Trim().ToLowerInvariant();
                query = query.Where(x => x.Name.ToLower().Contains(s) || x.Key.ToLower().Contains(s));
            }

            var stats = new global::Marten.Linq.QueryStatistics();
            var items = await ((global::Marten.Linq.IMartenQueryable<NavMenuDocument>)query)
                .OrderBy(x => x.Name)
                .Stats(out stats)
                .Skip(skip)
                .Take(take)
                .ToListAsync(token: cancellationToken);

            return Ok<(IReadOnlyList<NavMenuDocument> Items, long TotalCount), AeroError>((items, stats.TotalResults));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to list navigation menus for site {SiteId}", siteContext.SiteId);
            return Fail<(IReadOnlyList<NavMenuDocument> Items, long TotalCount), AeroError>(AeroError.DatabaseError(ex.Message));
        }
    }

    public async Task<Result<NavMenuDocument, AeroError>> GetAsync(long id, CancellationToken cancellationToken = default)
    {
        try
        {
            var menu = await session.LoadAsync<NavMenuDocument>(id, cancellationToken);
            if (menu is null || menu.SiteId != siteContext.SiteId)
            {
                return Fail<NavMenuDocument, AeroError>(AeroError.NotFoundError($"Navigation menu '{id}' not found or access denied."));
            }

            return Ok<NavMenuDocument, AeroError>(menu);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to load navigation menu {NavMenuId}", id);
            return Fail<NavMenuDocument, AeroError>(AeroError.DatabaseError(ex.Message));
        }
    }

    public async Task<Result<NavigationDetail, AeroError>> GetDetailAsync(long id, CancellationToken cancellationToken = default)
    {
        var menuResult = await GetAsync(id, cancellationToken);
        if (menuResult is Result<NavMenuDocument, AeroError>.Failure failure)
        {
            return Fail<NavigationDetail, AeroError>(failure.Error);
        }

        var menu = ((Result<NavMenuDocument, AeroError>.Ok)menuResult).Value;
        var snapshot = await LoadEditorSnapshotAsync(menu, cancellationToken);
        var version = await GetStreamVersionAsync(menu.Id, cancellationToken);
        return Ok<NavigationDetail, AeroError>(MapDetail(menu, snapshot, version));
    }

    public async Task<Result<IReadOnlyList<NavigationDetail>, AeroError>> ListCultureVariantsAsync(
        long id,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var menuResult = await GetAsync(id, cancellationToken);
            if (menuResult is Result<NavMenuDocument, AeroError>.Failure failure)
            {
                return Fail<IReadOnlyList<NavigationDetail>, AeroError>(failure.Error);
            }

            var menu = ((Result<NavMenuDocument, AeroError>.Ok)menuResult).Value;
            var TranslationGroupId = menu.TranslationGroupId ?? menu.Id;
            var variants = await session.Query<NavMenuDocument>()
                .Where(x => x.SiteId == menu.SiteId &&
                            x.TranslationGroupId == TranslationGroupId &&
                            x.State != NavMenuLifecycleState.Archived)
                .OrderBy(x => x.Culture)
                .ToListAsync(token: cancellationToken);

            var details = new List<NavigationDetail>(variants.Count);
            foreach (var variant in variants)
            {
                var snapshot = await LoadEditorSnapshotAsync(variant, cancellationToken);
                var version = await GetStreamVersionAsync(variant.Id, cancellationToken);
                details.Add(MapDetail(variant, snapshot, version));
            }

            return Ok<IReadOnlyList<NavigationDetail>, AeroError>(details);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to list navigation culture variants for {NavMenuId}", id);
            return Fail<IReadOnlyList<NavigationDetail>, AeroError>(AeroError.DatabaseError(ex.Message));
        }
    }

    public async Task<Result<long?, AeroError>> GetDefaultIdAsync(long siteId, CancellationToken cancellationToken = default)
    {
        try
        {
            var settings = await session.Query<SiteNavigationSettingsDocument>()
                .FirstOrDefaultAsync(x => x.SiteId == siteId, cancellationToken);

            return Ok<long?, AeroError>(settings?.DefaultNavMenuId);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return Ok<long?, AeroError>(null);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to load default navigation menu id for site {SiteId}", siteId);
            return Fail<long?, AeroError>(AeroError.DatabaseError(ex.Message));
        }
    }

    public async Task<Result<NavMenuSnapshot?, AeroError>> GetPublishedSnapshotAsync(
        long id,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var menu = await session.LoadAsync<NavMenuDocument>(id, cancellationToken);
            if (menu is null || menu.State == NavMenuLifecycleState.Archived || !menu.HasPublishedSnapshot)
            {
                return Ok<NavMenuSnapshot?, AeroError>(null);
            }

            var events = await session.Events.FetchStreamAsync(NavMenuStreams.Menu(id), token: cancellationToken);
            var published = events
                .OrderByDescending(x => x.Version)
                .Select(x => x.Data)
                .OfType<NavMenuPublished>()
                .FirstOrDefault();

            return Ok<NavMenuSnapshot?, AeroError>(published?.Snapshot);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return Ok<NavMenuSnapshot?, AeroError>(null);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to load published navigation snapshot {NavMenuId}", id);
            return Fail<NavMenuSnapshot?, AeroError>(AeroError.DatabaseError(ex.Message));
        }
    }

    public async Task<Result<NavMenuSnapshot?, AeroError>> ResolveSnapshotAsync(
        long siteId,
        long? pageOverrideId = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var navMenuId = pageOverrideId;
            if (navMenuId is null)
            {
                var defaultResult = await GetDefaultIdAsync(siteId, cancellationToken);
                if (defaultResult is Result<long?, AeroError>.Failure failure)
                {
                    return Fail<NavMenuSnapshot?, AeroError>(failure.Error);
                }

                navMenuId = ((Result<long?, AeroError>.Ok)defaultResult).Value;
            }

            if (navMenuId is null)
            {
                return Ok<NavMenuSnapshot?, AeroError>(null);
            }

            navMenuId = await ResolveCultureVariantIdAsync(siteId, navMenuId.Value, GetCurrentCulture(), cancellationToken);
            return await GetPublishedSnapshotAsync(navMenuId.Value, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return Ok<NavMenuSnapshot?, AeroError>(null);
        }
    }

    public async Task<Result<NavMenuDocument, AeroError>> CreateAsync(
        CreateNavigationRequest request,
        long? userId = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var siteId = siteContext.SiteId;
            if (siteId <= 0)
            {
                return Fail<NavMenuDocument, AeroError>(AeroError.InvalidRequestError("A current manager site is required."));
            }

            var culture = await GetSiteDefaultCultureAsync(cancellationToken);
            var key = NavMenuDocument.NormalizeKey(string.IsNullOrWhiteSpace(request.Name) ? "header" : request.Name);
            var duplicate = await session.Query<NavMenuDocument>()
                .AnyAsync(x => x.SiteId == siteId && x.Culture == culture && x.Key == key, cancellationToken);
            if (duplicate)
            {
                return Fail<NavMenuDocument, AeroError>(AeroError.ConflictError($"Navigation key '{key}' already exists for this site."));
            }

            var id = Snowflake.NewId();
            var now = DateTimeOffset.UtcNow;
            var snapshot = MapSnapshot(request.Items, request.SiteLogoUrl);
            snapshot.Validate();

            var created = new NavMenuCreated(siteId, request.Name, key, userId, now, Culture: culture, TranslationGroupId: id);
            var draftSaved = new NavMenuDraftSaved(siteId, request.Name, key, snapshot, userId, now, "Initial draft");

            session.Events.StartStream(NavMenuStreams.Menu(id), created, draftSaved);
            await session.SaveChangesAsync(cancellationToken);

            var menu = NavMenuDocument.Create(id, created);
            menu.Apply(draftSaved);
            return Ok<NavMenuDocument, AeroError>(menu);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to create navigation menu {Name}", request.Name);
            return Fail<NavMenuDocument, AeroError>(AeroError.DatabaseError(ex.Message));
        }
    }

    public async Task<Result<NavMenuDocument, AeroError>> SaveDraftAsync(
        long id,
        UpdateNavigationRequest request,
        long expectedVersion,
        long? userId = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var menuResult = await GetAsync(id, cancellationToken);
            if (menuResult is Result<NavMenuDocument, AeroError>.Failure failure)
            {
                return Fail<NavMenuDocument, AeroError>(failure.Error);
            }

            await EnsureExpectedVersionAsync(id, expectedVersion, cancellationToken);

            var menu = ((Result<NavMenuDocument, AeroError>.Ok)menuResult).Value;
            var snapshot = request.Rows.Count > 0
                ? MapSnapshot(request.Rows, request.SiteLogoUrl)
                : request.Components.Count > 0
                ? MapSnapshot(request.Components, request.SiteLogoUrl)
                : MapSnapshot(request.Items, request.SiteLogoUrl);
            snapshot.Validate();
            var now = DateTimeOffset.UtcNow;
            var draftSaved = new NavMenuDraftSaved(
                menu.SiteId,
                request.Name,
                menu.Key,
                snapshot,
                userId,
                now,
                null);

            await session.Events.AppendOptimistic(NavMenuStreams.Menu(id), cancellationToken, draftSaved);
            await session.SaveChangesAsync(cancellationToken);

            menu.Apply(draftSaved);
            return Ok<NavMenuDocument, AeroError>(menu);
        }
        catch (InvalidOperationException ex)
        {
            return Fail<NavMenuDocument, AeroError>(AeroError.ConflictError(ex.Message));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to save navigation draft {NavMenuId}", id);
            return Fail<NavMenuDocument, AeroError>(AeroError.DatabaseError(ex.Message));
        }
    }

    public async Task<Result<NavMenuDocument, AeroError>> PublishAsync(
        long id,
        long expectedVersion,
        long? userId = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var menuResult = await GetAsync(id, cancellationToken);
            if (menuResult is Result<NavMenuDocument, AeroError>.Failure failure)
            {
                return Fail<NavMenuDocument, AeroError>(failure.Error);
            }

            await EnsureExpectedVersionAsync(id, expectedVersion, cancellationToken);

            var menu = ((Result<NavMenuDocument, AeroError>.Ok)menuResult).Value;
            var draft = await LoadLatestDraftAsync(id, cancellationToken);
            if (draft is null)
            {
                return Fail<NavMenuDocument, AeroError>(AeroError.InvalidRequestError("Navigation menu has no draft to publish."));
            }

            draft.Snapshot.Validate();
            var now = DateTimeOffset.UtcNow;
            var published = new NavMenuPublished(menu.SiteId, draft.Snapshot, userId, now, draft.ChangeNote);

            await session.Events.AppendOptimistic(NavMenuStreams.Menu(id), cancellationToken, published);
            await session.SaveChangesAsync(cancellationToken);

            menu.Apply(published);
            await PublishNavigationChangedAsync(
                menu.Id,
                menu.SiteId,
                NavigationMenuChangeKind.Published,
                published.PublishedOn,
                cancellationToken);
            return Ok<NavMenuDocument, AeroError>(menu);
        }
        catch (InvalidOperationException ex)
        {
            return Fail<NavMenuDocument, AeroError>(AeroError.ConflictError(ex.Message));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to publish navigation menu {NavMenuId}", id);
            return Fail<NavMenuDocument, AeroError>(AeroError.DatabaseError(ex.Message));
        }
    }

    public async Task<Result<bool, AeroError>> SetDefaultAsync(
        long id,
        long? userId = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var menuResult = await GetAsync(id, cancellationToken);
            if (menuResult is Result<NavMenuDocument, AeroError>.Failure failure)
            {
                return Fail<bool, AeroError>(failure.Error);
            }

            var menu = ((Result<NavMenuDocument, AeroError>.Ok)menuResult).Value;
            if (!menu.HasPublishedSnapshot || menu.State == NavMenuLifecycleState.Archived)
            {
                return Fail<bool, AeroError>(AeroError.InvalidRequestError("Only published navigation menus can be set as default."));
            }

            var settings = await session.Query<SiteNavigationSettingsDocument>()
                .FirstOrDefaultAsync(x => x.SiteId == menu.SiteId, cancellationToken);
            var changed = new SiteDefaultNavMenuChanged(menu.SiteId, menu.Id, userId, DateTimeOffset.UtcNow);
            var streamKey = NavMenuStreams.SiteSettings(menu.SiteId);

            if (settings is null)
                session.Events.StartStream(streamKey, changed);
            else
                session.Events.Append(streamKey, changed);

            await session.SaveChangesAsync(cancellationToken);
            await PublishNavigationChangedAsync(
                menu.Id,
                menu.SiteId,
                NavigationMenuChangeKind.DefaultChanged,
                changed.ChangedOn,
                cancellationToken);
            return Ok<bool, AeroError>(true);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to set default navigation menu {NavMenuId}", id);
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
            var menuResult = await GetAsync(id, cancellationToken);
            if (menuResult is Result<NavMenuDocument, AeroError>.Failure failure)
            {
                return Fail<bool, AeroError>(failure.Error);
            }

            var menu = ((Result<NavMenuDocument, AeroError>.Ok)menuResult).Value;
            await EnsureExpectedVersionAsync(id, expectedVersion, cancellationToken);

            var archived = new NavMenuArchived(menu.SiteId, userId, DateTimeOffset.UtcNow);
            await session.Events.AppendOptimistic(
                NavMenuStreams.Menu(id),
                cancellationToken,
                archived);
            await session.SaveChangesAsync(cancellationToken);
            menu.Apply(archived);
            await PublishNavigationChangedAsync(
                menu.Id,
                menu.SiteId,
                NavigationMenuChangeKind.Archived,
                archived.ArchivedOn,
                cancellationToken);
            return Ok<bool, AeroError>(true);
        }
        catch (InvalidOperationException ex)
        {
            return Fail<bool, AeroError>(AeroError.ConflictError(ex.Message));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to archive navigation menu {NavMenuId}", id);
            return Fail<bool, AeroError>(AeroError.DatabaseError(ex.Message));
        }
    }

    private async Task<NavMenuSnapshot> LoadEditorSnapshotAsync(NavMenuDocument menu, CancellationToken cancellationToken)
    {
        var events = await session.Events.FetchStreamAsync(NavMenuStreams.Menu(menu.Id), token: cancellationToken);
        var published = events
            .OrderByDescending(x => x.Version)
            .Select(x => x.Data)
            .OfType<NavMenuPublished>()
            .FirstOrDefault();

        if (menu.State == NavMenuLifecycleState.Published && published is not null)
        {
            return published.Snapshot;
        }

        var draft = events
            .OrderByDescending(x => x.Version)
            .Select(x => x.Data)
            .OfType<NavMenuDraftSaved>()
            .FirstOrDefault();

        return draft?.Snapshot ?? published?.Snapshot ?? NavMenuSnapshot.Empty;
    }

    private async Task<NavMenuDraftSaved?> LoadLatestDraftAsync(long id, CancellationToken cancellationToken)
    {
        var events = await session.Events.FetchStreamAsync(NavMenuStreams.Menu(id), token: cancellationToken);
        var latestDraft = events
            .OrderByDescending(x => x.Version)
            .FirstOrDefault(x => x.Data is NavMenuDraftSaved);
        if (latestDraft is null)
        {
            return null;
        }

        var latestPublishedVersion = events
            .OrderByDescending(x => x.Version)
            .FirstOrDefault(x => x.Data is NavMenuPublished)
            ?.Version ?? 0;

        return latestPublishedVersion > latestDraft.Version
            ? null
            : (NavMenuDraftSaved)latestDraft.Data;
    }

    private async Task<long> GetStreamVersionAsync(long id, CancellationToken cancellationToken)
    {
        var state = await session.Events.FetchStreamStateAsync(NavMenuStreams.Menu(id), cancellationToken);
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
            throw new InvalidOperationException("Navigation menu was modified by another user.");
        }
    }

    public async Task<Result<NavMenuDocument, AeroError>> ForkToCultureAsync(
        long id,
        string targetCulture,
        long? userId = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var menuResult = await GetAsync(id, cancellationToken);
            if (menuResult is Result<NavMenuDocument, AeroError>.Failure failure)
            {
                return Fail<NavMenuDocument, AeroError>(failure.Error);
            }

            var source = ((Result<NavMenuDocument, AeroError>.Ok)menuResult).Value;
            var culture = NormalizeCulture(targetCulture);
            var TranslationGroupId = source.TranslationGroupId ?? source.Id;

            var duplicate = await session.Query<NavMenuDocument>()
                .AnyAsync(x =>
                    x.SiteId == source.SiteId &&
                    x.TranslationGroupId == TranslationGroupId &&
                    x.Culture == culture &&
                    x.State != NavMenuLifecycleState.Archived,
                    cancellationToken);
            if (duplicate)
            {
                return Fail<NavMenuDocument, AeroError>(AeroError.ConflictError($"A {culture} navigation translation already exists."));
            }

            var sourceSnapshot = await LoadEditorSnapshotAsync(source, cancellationToken);
            var targetId = Snowflake.NewId();
            var fork = NavMenuCultureForker.Fork(source, sourceSnapshot, targetId, culture, userId);

            session.Events.StartStream(NavMenuStreams.Menu(targetId), fork.Created, fork.DraftSaved);
            await session.SaveChangesAsync(cancellationToken);

            var menu = NavMenuDocument.Create(targetId, fork.Created);
            menu.Apply(fork.DraftSaved);
            return Ok<NavMenuDocument, AeroError>(menu);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to fork navigation menu {NavMenuId} to {Culture}", id, targetCulture);
            return Fail<NavMenuDocument, AeroError>(AeroError.DatabaseError(ex.Message));
        }
    }

    private async Task<long> ResolveCultureVariantIdAsync(
        long siteId,
        long defaultMenuId,
        string culture,
        CancellationToken cancellationToken)
    {
        var defaultMenu = await session.LoadAsync<NavMenuDocument>(defaultMenuId, cancellationToken);
        if (defaultMenu is null ||
            string.Equals(defaultMenu.Culture, culture, StringComparison.OrdinalIgnoreCase) ||
            defaultMenu.TranslationGroupId is null)
        {
            return defaultMenuId;
        }

        var cultureVariant = await session.Query<NavMenuDocument>()
            .Where(x => x.SiteId == siteId &&
                        x.TranslationGroupId == defaultMenu.TranslationGroupId &&
                        x.Culture == culture &&
                        x.State != NavMenuLifecycleState.Archived &&
                        x.HasPublishedSnapshot)
            .FirstOrDefaultAsync(cancellationToken);

        return cultureVariant?.Id ?? defaultMenuId;
    }

    private Task PublishNavigationChangedAsync(
        long navMenuId,
        long siteId,
        NavigationMenuChangeKind changeKind,
        DateTimeOffset changedOn,
        CancellationToken cancellationToken)
        => bus is null
            ? Task.CompletedTask
            : bus.PublishAsync(new NavigationMenuChangedEvent(navMenuId, siteId, changeKind, changedOn)).AsTask();

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

    private static NavMenuSnapshot MapSnapshot(IReadOnlyList<CreateNavigationItemRequest> items, string? siteLogoUrl)
        => new(
            NavMenuLayout.Default,
            NavMenuResponsiveSettings.Default,
            NavMenuStyleSettings.Default,
            items.OrderBy(x => x.Order)
                .Select(x => new NavLink
                {
                    Key = Snowflake.NewId().ToString(),
                    Label = x.Label,
                    Href = x.Url ?? string.Empty,
                    IsExternal = x.IsExternal,
                    Target = NormalizeTarget(x.Target, x.IsExternal),
                    OpenInNewTab = NormalizeTarget(x.Target, x.IsExternal) == "_blank",
                    PageId = x.PageId,
                    AltText = x.AltText,
                    Alignment = NavAlignment.Left
                })
                .Cast<INavMenuComponent>()
                .ToList(),
            siteLogoUrl);

    private static NavMenuSnapshot MapSnapshot(IReadOnlyList<UpdateNavigationItemRequest> items, string? siteLogoUrl)
        => new(
            NavMenuLayout.Default,
            NavMenuResponsiveSettings.Default,
            NavMenuStyleSettings.Default,
            items.OrderBy(x => x.Order)
                .Select(x => new NavLink
                {
                    Key = (x.Id == 0 ? Snowflake.NewId() : x.Id).ToString(),
                    Label = x.Label,
                    Href = x.Url ?? string.Empty,
                    IsExternal = x.IsExternal,
                    Target = NormalizeTarget(x.Target, x.IsExternal),
                    OpenInNewTab = NormalizeTarget(x.Target, x.IsExternal) == "_blank",
                    PageId = x.PageId,
                    AltText = x.AltText,
                    Alignment = NavAlignment.Left
                })
                .Cast<INavMenuComponent>()
                .ToList(),
            siteLogoUrl);

    private static NavMenuSnapshot MapSnapshot(IReadOnlyList<UpdateNavigationComponentRequest> components, string? siteLogoUrl)
        => new(
            NavMenuLayout.Default,
            NavMenuResponsiveSettings.Default,
            NavMenuStyleSettings.Default,
            components.OrderBy(x => x.Order)
                .Select(MapComponent)
                .ToList(),
            siteLogoUrl);

    private static NavMenuSnapshot MapSnapshot(IReadOnlyList<UpdateNavigationCanvasRowRequest> rows, string? siteLogoUrl)
    {
        var canvasRows = rows
            .OrderBy(x => x.Order)
            .Select(row => new NavCanvasRow
            {
                Key = (row.Id == 0 ? Snowflake.NewId() : row.Id).ToString(),
                Order = row.Order,
                Label = row.Label?.Trim(),
                DesktopDisplay = CleanDisplay(row.DesktopDisplay, "Flex"),
                TabletDisplay = CleanDisplay(row.TabletDisplay, "Flex"),
                MobileDisplay = CleanDisplay(row.MobileDisplay, "Stack"),
                Columns = row.Columns
                    .OrderBy(column => column.Order)
                    .Select(column => new NavCanvasColumn
                    {
                        Key = (column.Id == 0 ? Snowflake.NewId() : column.Id).ToString(),
                        Order = column.Order,
                        DesktopSpan = ClampSpan(column.DesktopSpan),
                        TabletSpan = ClampSpan(column.TabletSpan),
                        MobileSpan = ClampSpan(column.MobileSpan),
                        Blocks = column.Blocks
                            .OrderBy(block => block.Order)
                            .Select(block =>
                            {
                                var component = MapComponent(block);
                                return new NavCanvasBlock
                                {
                                    Key = component.Key,
                                    Order = block.Order,
                                    Component = component
                                };
                            })
                            .ToList()
                    })
                    .ToList()
            })
            .ToList();

        var snapshot = new NavMenuSnapshot
        {
            SiteLogoUrl = string.IsNullOrWhiteSpace(siteLogoUrl) ? null : siteLogoUrl.Trim(),
            Rows = canvasRows
        };

        return snapshot;
    }

    private static INavMenuComponent MapComponent(UpdateNavigationComponentRequest component)
    {
        var key = (component.Id == 0 ? Snowflake.NewId() : component.Id).ToString();
        var alignment = ParseAlignment(component.Alignment);
        var kind = component.Kind.Trim().ToLowerInvariant();

        return kind switch
        {
            "menu" => new NavMenu
            {
                Key = key,
                Alignment = alignment,
                Visibility = ParseVisibility(component.Visibility),
                Label = component.Label?.Trim() ?? "Menu",
                Children = component.Children
                    .OrderBy(x => x.Order)
                    .Select(MapComponent)
                    .ToList()
            },
            "html" or "richmenu" or "customhtml" => new NavHtml
            {
                Key = key,
                Alignment = alignment,
                Visibility = ParseVisibility(component.Visibility),
                Html = component.Html?.Trim() ?? string.Empty
            },
            "search" => new NavSearch
            {
                Key = key,
                Alignment = alignment,
                Visibility = ParseVisibility(component.Visibility),
                Placeholder = string.IsNullOrWhiteSpace(component.Placeholder) ? "Search..." : component.Placeholder.Trim(),
                SearchAction = string.IsNullOrWhiteSpace(component.SearchAction) ? "/search" : component.SearchAction.Trim(),
                ButtonLabel = string.IsNullOrWhiteSpace(component.ButtonLabel) ? "Search" : component.ButtonLabel.Trim()
            },
            "language" or "languageselect" => new NavLanguageSelect
            {
                Key = key,
                Alignment = alignment,
                Visibility = ParseVisibility(component.Visibility),
                Label = string.IsNullOrWhiteSpace(component.Label) ? "Language" : component.Label.Trim()
            },
            "login" or "register" or "authbutton" => new NavAuthButton
            {
                Key = key,
                Alignment = alignment,
                Visibility = ParseVisibility(component.Visibility),
                Label = component.Label?.Trim() ?? (kind == "register" ? "Register" : "Login"),
                Href = string.IsNullOrWhiteSpace(component.Url)
                    ? (kind == "register" ? "/register" : "/login")
                    : component.Url.Trim(),
                ButtonStyle = kind == "register" ? "Primary" : "Secondary"
            },
            _ => new NavLink
            {
                Key = key,
                Alignment = alignment,
                Visibility = ParseVisibility(component.Visibility),
                Label = component.Label?.Trim() ?? "Link",
                Href = component.Url?.Trim() ?? string.Empty,
                IsExternal = component.IsExternal,
                Target = NormalizeTarget(component.Target, component.IsExternal),
                OpenInNewTab = NormalizeTarget(component.Target, component.IsExternal) == "_blank",
                PageId = component.PageId,
                AltText = component.AltText?.Trim()
            }
        };
    }

    private static NavigationDetail MapDetail(NavMenuDocument menu, NavMenuSnapshot snapshot, long version)
        => new(
            menu.Id,
            menu.Name,
            menu.Key,
            snapshot.Components.OfType<NavLink>()
                .Select((x, index) => new NavigationItemDetail(
                    ParseKey(x.Key),
                    x.Label,
                    x.Href,
                    x.PageId,
                    index,
                    x.AltText,
                    x.IsExternal || IsHttpUrl(x.Href),
                    string.IsNullOrWhiteSpace(x.Target)
                        ? (x.OpenInNewTab ? "_blank" : "_self")
                        : x.Target))
                .ToList(),
            menu.CreatedOn.DateTime,
            (menu.ModifiedOn ?? menu.CreatedOn).DateTime,
            version,
            menu.State.ToString(),
            snapshot.SiteLogoUrl,
            menu.Culture,
            menu.TranslationGroupId,
            snapshot.Components
                .Select((x, index) => MapComponentDetail(x, index))
                .ToList(),
            snapshot.Rows
                .OrderBy(x => x.Order)
                .Select(MapRowDetail)
                .ToList());

    private static NavigationCanvasRowDetail MapRowDetail(NavCanvasRow row)
        => new(
            ParseKey(row.Key),
            row.Order,
            row.Label,
            row.DesktopDisplay,
            row.TabletDisplay,
            row.MobileDisplay,
            row.Columns.OrderBy(x => x.Order).Select(MapColumnDetail).ToList());

    private static NavigationCanvasColumnDetail MapColumnDetail(NavCanvasColumn column)
        => new(
            ParseKey(column.Key),
            column.Order,
            column.DesktopSpan,
            column.TabletSpan,
            column.MobileSpan,
            column.Blocks.OrderBy(x => x.Order).Select(x => MapComponentDetail(x.Component, x.Order)).ToList());

    private static NavigationComponentDetail MapComponentDetail(INavMenuComponent component, int order)
        => component switch
        {
            NavMenu menu => new NavigationComponentDetail(
                ParseKey(menu.Key),
                "menu",
                menu.Label,
                null,
                null,
                order,
                menu.Alignment.ToString(),
                Children: menu.Children.Select((x, index) => MapComponentDetail(x, index)).ToList(),
                Visibility: menu.Visibility.ToString()),
            NavHtml html => new NavigationComponentDetail(
                ParseKey(html.Key),
                "html",
                null,
                null,
                null,
                order,
                html.Alignment.ToString(),
                Html: html.Html,
                Visibility: html.Visibility.ToString()),
            NavSearch search => new NavigationComponentDetail(
                ParseKey(search.Key),
                "search",
                null,
                null,
                null,
                order,
                search.Alignment.ToString(),
                Placeholder: search.Placeholder,
                SearchAction: search.SearchAction,
                ButtonLabel: search.ButtonLabel,
                Visibility: search.Visibility.ToString()),
            NavLanguageSelect language => new NavigationComponentDetail(
                ParseKey(language.Key),
                "language",
                language.Label,
                null,
                null,
                order,
                language.Alignment.ToString(),
                Visibility: language.Visibility.ToString()),
            NavAuthButton authButton => new NavigationComponentDetail(
                ParseKey(authButton.Key),
                authButton.Label.Equals("Register", StringComparison.OrdinalIgnoreCase) ? "register" : "login",
                authButton.Label,
                authButton.Href,
                null,
                order,
                authButton.Alignment.ToString(),
                Visibility: authButton.Visibility.ToString()),
            NavLink link => new NavigationComponentDetail(
                ParseKey(link.Key),
                "link",
                link.Label,
                link.Href,
                link.PageId,
                order,
                link.Alignment.ToString(),
                link.AltText,
                link.IsExternal || IsHttpUrl(link.Href),
                string.IsNullOrWhiteSpace(link.Target)
                    ? (link.OpenInNewTab ? "_blank" : "_self")
                    : link.Target,
                Visibility: link.Visibility.ToString()),
            _ => new NavigationComponentDetail(0, "unknown", null, null, null, order)
        };

    private static NavAlignment ParseAlignment(string? alignment)
        => Enum.TryParse<NavAlignment>(alignment, true, out var parsed)
            ? parsed
            : NavAlignment.Left;

    private static NavAuthVisibility ParseVisibility(string? visibility)
        => Enum.TryParse<NavAuthVisibility>(visibility, true, out var parsed)
            ? parsed
            : NavAuthVisibility.Always;

    private static int ClampSpan(int span)
        => Math.Clamp(span, 1, 12);

    private static string CleanDisplay(string? value, string fallback)
        => string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();

    private static long ParseKey(string key)
        => long.TryParse(key, out var id) ? id : 0;

    private static string NormalizeTarget(string? target, bool isExternal)
        => target is "_self" or "_blank" or "_parent" or "_top"
            ? target
            : isExternal ? "_blank" : "_self";

    private static bool IsHttpUrl(string? url)
        => Uri.TryCreate(url, UriKind.Absolute, out var uri)
           && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);
}
