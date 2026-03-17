# ASP.NET Core Block-Based CMS — Full Architecture Specification

> **Purpose:** Complete design specification for an agent swarm implementation. Covers all subsystems, data models, interfaces, code structure, flow diagrams, and implementation notes.

---

## Table of Contents

1. [System Overview](#1-system-overview)
2. [Technology Stack](#2-technology-stack)
3. [Block System](#3-block-system)
4. [Page & Layout System](#4-page--layout-system)
5. [Rendering Pipeline](#5-rendering-pipeline)
6. [Dynamic Blocks & ViewComponents](#6-dynamic-blocks--viewcomponents)
7. [Runtime Razor Compilation from Database](#7-runtime-razor-compilation-from-database)
8. [Output Caching & Cache Busting](#8-output-caching--cache-busting)
9. [Localization & Culture Routing](#9-localization--culture-routing)
10. [Module System](#10-module-system)
11. [Admin UI Shell](#11-admin-ui-shell)
12. [Content Lifecycle & Publishing Workflow](#12-content-lifecycle--publishing-workflow)
13. [Media Library](#13-media-library)
14. [Navigation & Menus](#14-navigation--menus)
15. [Users, Roles & Permissions](#15-users-roles--permissions)
16. [Multi-Tenancy](#16-multi-tenancy)
17. [Audit Log](#17-audit-log)
18. [Taxonomy](#18-taxonomy)
19. [Content Relationships](#19-content-relationships)
20. [Recycle Bin](#20-recycle-bin)
21. [Full-Text Search](#21-full-text-search)
22. [Headless API](#22-headless-api)
23. [Webhooks](#23-webhooks)
24. [Forms Builder](#24-forms-builder)
25. [Import / Export](#25-import--export)
26. [Redirects Manager](#26-redirects-manager)
27. [SEO Module](#27-seo-module)
28. [A/B Testing](#28-ab-testing)
29. [Personalisation](#29-personalisation)
30. [Analytics Hooks](#30-analytics-hooks)
31. [Notifications](#31-notifications)
32. [Marten Storage Conventions](#32-marten-storage-conventions)
33. [Project Structure](#33-project-structure)
34. [Agent Swarm Implementation Notes](#34-agent-swarm-implementation-notes)

---

## 1. System Overview

A block-based CMS built on ASP.NET Core where:

- **Pages** are composed of ordered **LayoutRegions**, each with 1–3 **Columns**, each containing ordered **Blocks**
- **Blocks** are polymorphic content units (hero, rich text, card grid, dynamic ViewComponent, etc.)
- **Modules** are independently deployable assemblies that contribute block types, admin UI, pipeline hooks, and event handlers
- **The rendering pipeline** is a Chain of Responsibility pattern — hooks from any module can intercept reads, saves, and block renders
- **Storage** is PostgreSQL via Marten (document model) — no EF Core for CMS documents; EF Core may be used for identity/auth only
- For Login - use ASP.NET Core Identity (EF Core, separate DB context)
    - but abstract the auth and have it pluggable using interface IAeroSecurity so we can later integrate keycloak or ms entra (for now only focus on aspnetcore identity)
    - Rather than use [Authenticate("role")] attributes on controllers, use authorization filters in code this will enable us to use decorators on auth and use more compledx logic, etc.
- **Admin UI** is server-rendered Razor with HTMX for interactivity, each module contributing its own views via Razor Class Libraries (RCL)

### High-Level Flow

```
HTTP Request /en/about
      │
      ├── CultureRedirectMiddleware    (ensure culture prefix)
      ├── RequestLocalizationMiddleware (set CultureInfo)
      ├── ETagMiddleware               (conditional GET handling)
      ├── OutputCacheMiddleware        (serve from cache if warm)
      │
      └── PageController.Index(culture, slug)
                │
                └── PageService.GetPageAsync()
                          │
                          └── PageReadPipeline (Chain of Responsibility)
                                    ├── [Order -10] AuthorizationHook
                                    ├── [Order -5]  CacheReadHook
                                    ├── [Order  0]  CorePageReadHook  ← Marten fetch
                                    ├── [Order  5]  SeoEnrichmentHook
                                    └── [Order  10] AnalyticsHook
                                          │
                                          └── PageLayoutRenderer
                                                    └── RegionRenderer
                                                              └── ColumnRenderer
                                                                        └── BlockRendererRegistry
                                                                                  └── IBlockRenderer per type
```

---

## 2. Technology Stack

| Concern | Technology |
|---|---|
| Framework | ASP.NET Core 8+ |
| Server rendering | Razor / cshtml + ViewComponents |
| Interactivity | HTMX + Alpine.js |
| Styling | Tailwind CSS (+ tailwindcss-rtl for RTL support) |
| Database | PostgreSQL via Marten (document store) |
| Identity/Auth | ASP.NET Core Identity (EF Core, separate DB context) |
| Caching | ASP.NET Core Output Cache + IMemoryCache |
| Runtime compilation | Microsoft.AspNetCore.Mvc.Razor.RuntimeCompilation |
| Roslyn | Microsoft.CodeAnalysis.CSharp (module/component upload) |
| Image processing | SixLabors.ImageSharp |
| Search | Meilisearch (via Meilisearch.NET client) |
| Background jobs | Hangfire (scheduling, webhooks, notifications) |
| Markdown | Markdig |

---

## 3. Block System

### 3.1 Core Abstractions

```csharp
public abstract record BlockBase
{
    public long Id { get; init; } = Snowflake.NewId();
    public int Order { get; init; }
    public abstract string BlockType { get; }
}
```

### 3.2 Built-In Block Types

```csharp
public record HeroBlock : BlockBase
{
    public override string BlockType => "hero";
    public LocalizedString Heading { get; init; } = new(new() { ["en"] = "" });
    public LocalizedString Subheading { get; init; } = new(new() { ["en"] = "" });
    public string? ImageUrl { get; init; }
    public string? CtaLabel { get; init; }
    public string? CtaHref { get; init; }
}

public record RichTextBlock : BlockBase
{
    public override string BlockType => "rich-text";
    public LocalizedString Html { get; init; } = new(new() { ["en"] = "" });
}

public record CardGridBlock : BlockBase
{
    public override string BlockType => "card-grid";
    public List<CardItem> Cards { get; init; } = [];
}

public record DynamicBlock : BlockBase
{
    public override string BlockType => "dynamic";
    public string ComponentName { get; init; } = "";
    public Dictionary<string, object?> Parameters { get; init; } = [];
}

public record RazorTemplateBlock : BlockBase
{
    public override string BlockType => "razor-template";
    public string RazorContent { get; init; } = "";
    public Dictionary<string, object?> ViewData { get; init; } = [];
}

public record SeoMetaBlock : BlockBase
{
    public override string BlockType => "seo-meta";
    public LocalizedString MetaTitle { get; init; } = new(new() { ["en"] = "" });
    public LocalizedString MetaDescription { get; init; } = new(new() { ["en"] = "" });
    public string? OgImageUrl { get; init; }
}

public record CodeSnippetBlock : BlockBase
{
    public override string BlockType => "code-snippet";
    public string Language { get; init; } = "";
    public string Code { get; init; } = "";
}
```

### 3.3 Renderer Interfaces

```csharp
public interface IBlockRenderer
{
    string BlockType { get; }
    Task<IHtmlContent> RenderAsync(BlockBase block, ViewContext viewContext);
}

public abstract class BlockRenderer<TBlock> : IBlockRenderer
    where TBlock : BlockBase
{
    public abstract string BlockType { get; }

    public async Task<IHtmlContent> RenderAsync(BlockBase block, ViewContext viewContext)
    {
        if (block is not TBlock typed)
            throw new InvalidOperationException($"Expected {typeof(TBlock).Name}");
        return await RenderAsync(typed, viewContext);
    }

    protected abstract Task<IHtmlContent> RenderAsync(TBlock block, ViewContext viewContext);
}
```

### 3.4 Block Renderer Registry

```csharp
public interface IBlockRendererRegistry
{
    IBlockRenderer Resolve(string blockType);
    void Register(IBlockRenderer renderer);
}

public class BlockRendererRegistry : IBlockRendererRegistry
{
    private readonly ConcurrentDictionary<string, IBlockRenderer> _renderers = new(StringComparer.OrdinalIgnoreCase);

    public BlockRendererRegistry(IEnumerable<IBlockRenderer> renderers)
    {
        foreach (var r in renderers)
            _renderers[r.BlockType] = r;
    }

    public IBlockRenderer Resolve(string blockType)
    {
        if (_renderers.TryGetValue(blockType, out var renderer)) return renderer;
        throw new InvalidOperationException($"No renderer registered for block type '{blockType}'");
    }

    public void Register(IBlockRenderer renderer)
        => _renderers[renderer.BlockType] = renderer;
}
```

### 3.5 Polymorphic JSON Serialization

```csharp
public class BlockJsonConverter : JsonConverter<BlockBase>
{
    public override BlockBase? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        using var doc = JsonDocument.ParseValue(ref reader);
        var blockType = doc.RootElement.GetProperty("blockType").GetString();

        return blockType switch
        {
            "hero"          => doc.RootElement.Deserialize<HeroBlock>(options),
            "rich-text"     => doc.RootElement.Deserialize<RichTextBlock>(options),
            "card-grid"     => doc.RootElement.Deserialize<CardGridBlock>(options),
            "dynamic"       => doc.RootElement.Deserialize<DynamicBlock>(options),
            "razor-template"=> doc.RootElement.Deserialize<RazorTemplateBlock>(options),
            "seo-meta"      => doc.RootElement.Deserialize<SeoMetaBlock>(options),
            "code-snippet"  => doc.RootElement.Deserialize<CodeSnippetBlock>(options),
            _               => throw new JsonException($"Unknown block type: {blockType}")
        };
    }

    public override void Write(Utf8JsonWriter writer, BlockBase value, JsonSerializerOptions options)
        => JsonSerializer.Serialize(writer, (object)value, options);
}
```

### 3.6 Partial View Renderer (Shared Utility)

```csharp
public class PartialViewRenderer
{
    private readonly ICompositeViewEngine _viewEngine;
    private readonly ITempDataDictionaryFactory _tempDataFactory;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public async Task<string> RenderAsync(string viewPath, object model)
    {
        var httpContext = _httpContextAccessor.HttpContext!;
        var actionContext = new ActionContext(httpContext, httpContext.GetRouteData(), new ActionDescriptor());
        var viewResult = _viewEngine.GetView(null, viewPath, false);
        var view = viewResult.View ?? throw new InvalidOperationException($"View not found: {viewPath}");

        var viewData = new ViewDataDictionary(new EmptyModelMetadataProvider(), new ModelStateDictionary())
        { Model = model };

        var tempData = _tempDataFactory.GetTempData(httpContext);
        using var sw = new StringWriter();
        var viewContext = new ViewContext(actionContext, view, viewData, tempData, sw, new HtmlHelperOptions());
        await view.RenderAsync(viewContext);
        return sw.ToString();
    }
}
```

---

## 4. Page & Layout System

### 4.1 Domain Model

```csharp
public enum LayoutType
{
    SingleColumn,
    TwoColumn,      // 70/30
    TwoColumnEqual, // 50/50
    ThreeColumn     // 33/33/33
}

public enum PageType { Standard, Blog, Landing }

public enum PageStatus { Draft, PendingReview, Approved, Published, Scheduled, Unpublished, Archived }

public enum SaveOperation { Create, Update, Publish, Unpublish, Schedule, Archive }

public record ColumnDefinition
{
    public long Id { get; init; } = Snowflake.NewId();
    public int Index { get; init; }
    public string CssClass { get; init; } = "";
    public List<BlockBase> Blocks { get; init; } = [];
}

public record LayoutRegion
{
    public long Id { get; init; } = Snowflake.NewId();
    public string RegionKey { get; init; } = "";
    public LayoutType Layout { get; init; }
    public int Order { get; init; }
    public List<ColumnDefinition> Columns { get; init; } = [];
}

public record PageMeta
{
    public string? MetaTitle { get; init; }
    public string? MetaDescription { get; init; }
    public string? OgImageUrl { get; init; }
    public string? CanonicalUrl { get; init; }
}

public record Page
{
    public long Id { get; init; } = Snowflake.NewId();
    public long PageGroupId { get; init; }     // groups all culture variants
    public string Culture { get; init; } = "en";
    public string Slug { get; init; } = "";
    public string Title { get; init; } = "";
    public PageType PageType { get; init; }
    public PageStatus Status { get; init; } = PageStatus.Draft;
    public List<LayoutRegion> Regions { get; init; } = [];
    public PageMeta Meta { get; init; } = new();
    public long? TenantId { get; init; }
    public DateTimeOffset? PublishAt { get; init; }
    public DateTimeOffset? UnpublishAt { get; init; }
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; init; } = DateTimeOffset.UtcNow;
    public string? CreatedBy { get; init; }
    public string? UpdatedBy { get; init; }
    public int Version { get; init; } = 1;
}
```

### 4.2 Layout Factory

```csharp
public interface ILayoutFactory
{
    LayoutRegion Create(LayoutType layout, string regionKey, int order);
}

public class LayoutFactory : ILayoutFactory
{
    private static readonly Dictionary<LayoutType, string[]> _columnClasses = new()
    {
        [LayoutType.SingleColumn]   = ["col-span-12"],
        [LayoutType.TwoColumn]      = ["col-span-8", "col-span-4"],
        [LayoutType.TwoColumnEqual] = ["col-span-6", "col-span-6"],
        [LayoutType.ThreeColumn]    = ["col-span-4", "col-span-4", "col-span-4"],
    };

    public LayoutRegion Create(LayoutType layout, string regionKey, int order)
    {
        var classes = _columnClasses[layout];
        var columns = classes.Select((css, i) => new ColumnDefinition
        {
            Index = i, CssClass = css, Blocks = []
        }).ToList();

        return new LayoutRegion
        {
            RegionKey = regionKey,
            Layout    = layout,
            Order     = order,
            Columns   = columns
        };
    }
}
```

### 4.3 Page Type Factories

```csharp
public class BlogPageFactory
{
    private readonly ILayoutFactory _layoutFactory;

    public Page CreateBlogPost(string slug, string title, string culture = "en")
    {
        var groupId = Snowflake.NewId();
        return new Page
        {
            Slug        = slug,
            Title       = title,
            Culture     = culture,
            PageGroupId = groupId,
            PageType    = PageType.Blog,
            Regions     =
            [
                _layoutFactory.Create(LayoutType.SingleColumn, "hero", 0),
                _layoutFactory.Create(LayoutType.TwoColumn,    "body", 1),
                _layoutFactory.Create(LayoutType.SingleColumn, "footer-cta", 2),
            ]
        };
    }
}

public class LandingPageFactory
{
    private readonly ILayoutFactory _layoutFactory;

    public Page Create(string slug, string title, string culture = "en")
    {
        return new Page
        {
            Slug        = slug,
            Title       = title,
            Culture     = culture,
            PageGroupId = Snowflake.NewId(),
            PageType    = PageType.Landing,
            Regions     =
            [
                _layoutFactory.Create(LayoutType.SingleColumn,   "hero",     0),
                _layoutFactory.Create(LayoutType.ThreeColumn,    "features", 1),
                _layoutFactory.Create(LayoutType.TwoColumnEqual, "social",   2),
                _layoutFactory.Create(LayoutType.SingleColumn,   "cta",      3),
            ]
        };
    }
}
```

### 4.4 Page Versioning

Every save creates an immutable `PageRevision`. The `Page` document holds only the current state.

```csharp
public record PageRevision
{
    public long Id { get; init; } = Snowflake.NewId();
    public long PageId { get; init; }
    public int Version { get; init; }
    public Page Snapshot { get; init; } = default!;
    public string? ChangeSummary { get; init; }
    public string? CreatedBy { get; init; }
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
}
```

---

## 5. Rendering Pipeline

### 5.1 Pipeline Context Base

```csharp
public abstract class PipelineContext
{
    public bool IsShortCircuited { get; private set; }
    public string? ShortCircuitReason { get; private set; }
    public void ShortCircuit(string reason) { IsShortCircuited = true; ShortCircuitReason = reason; }
}

public class PageReadContext : PipelineContext
{
    public required string Slug { get; init; }
    public required string Culture { get; init; }
    public long? TenantId { get; init; }
    public bool IncludeDraft { get; init; }       // for preview mode
    public Page? Page { get; set; }
    public Dictionary<string, object> Metadata { get; } = new();
}

public class PageSaveContext : PipelineContext
{
    public required Page Page { get; set; }
    public required SaveOperation Operation { get; init; }
    public List<string> ValidationErrors { get; } = [];
    public bool HasValidationErrors => ValidationErrors.Count > 0;
}

public class BlockRenderContext : PipelineContext
{
    public required BlockBase Block { get; init; }
    public required ViewContext ViewContext { get; init; }
    public IHtmlContent? Output { get; set; }
    public Dictionary<string, object> RenderData { get; } = new();
}
```

### 5.2 Hook Interfaces

```csharp
public interface IPageReadHook
{
    int Order { get; }
    Task ExecuteAsync(PageReadContext ctx, CancellationToken ct);
}

public interface IPageSaveHook
{
    int Order { get; }
    Task ExecuteAsync(PageSaveContext ctx, CancellationToken ct);
}

public interface IBlockRenderHook
{
    int Order { get; }
    Task ExecuteAsync(BlockRenderContext ctx, CancellationToken ct);
}
```

### 5.3 Pipeline Executor

```csharp
public class CmsPipeline<TContext> where TContext : PipelineContext
{
    private readonly IReadOnlyList<Func<TContext, CancellationToken, Task>> _stages;

    public CmsPipeline(IEnumerable<Func<TContext, CancellationToken, Task>> stages)
        => _stages = stages.ToList();

    public async Task ExecuteAsync(TContext ctx, CancellationToken ct = default)
    {
        foreach (var stage in _stages)
        {
            if (ctx.IsShortCircuited) break;
            await stage(ctx, ct);
        }
    }
}
```

### 5.4 Core Hooks (Order = 0)

```csharp
public class CorePageReadHook : IPageReadHook
{
    private readonly IPageRepository _repo;
    public int Order => 0;

    public async Task ExecuteAsync(PageReadContext ctx, CancellationToken ct)
    {
        ctx.Page = ctx.IncludeDraft
            ? await _repo.GetDraftBySlugAsync(ctx.Slug, ctx.Culture, ctx.TenantId)
            : await _repo.GetPublishedBySlugAsync(ctx.Slug, ctx.Culture, ctx.TenantId);

        if (ctx.Page is null)
            ctx.ShortCircuit("Page not found");
    }
}

public class CorePageSaveHook : IPageSaveHook
{
    private readonly IPageRepository _repo;
    private readonly ICmsEventBus _eventBus;
    public int Order => 0;

    public async Task ExecuteAsync(PageSaveContext ctx, CancellationToken ct)
    {
        if (ctx.HasValidationErrors) { ctx.ShortCircuit("Validation failed"); return; }
        await _repo.SaveAsync(ctx.Page, ct);
        await _eventBus.PublishAsync(new PageSavedEvent { Page = ctx.Page, Operation = ctx.Operation }, ct);
    }
}
```

### 5.5 Renderers

```csharp
public class PageLayoutRenderer
{
    private readonly IRegionRenderer _regionRenderer;

    public async Task<IHtmlContent> RenderAsync(Page page, ViewContext ctx)
    {
        var builder = new HtmlContentBuilder();
        foreach (var region in page.Regions.OrderBy(r => r.Order))
            builder.AppendHtml(await _regionRenderer.RenderAsync(region, ctx));
        return builder;
    }
}

public class RegionRenderer : IRegionRenderer
{
    private readonly IColumnRenderer _columnRenderer;

    public async Task<IHtmlContent> RenderAsync(LayoutRegion region, ViewContext ctx)
    {
        var builder = new HtmlContentBuilder();
        builder.AppendHtml($"<div class=\"grid grid-cols-12 gap-6\" data-region=\"{region.RegionKey}\">");
        foreach (var col in region.Columns.OrderBy(c => c.Index))
            builder.AppendHtml(await _columnRenderer.RenderAsync(col, ctx));
        builder.AppendHtml("</div>");
        return builder;
    }
}

public class ColumnRenderer : IColumnRenderer
{
    private readonly IBlockRendererRegistry _registry;
    private readonly IEnumerable<IBlockRenderHook> _hooks;

    public async Task<IHtmlContent> RenderAsync(ColumnDefinition column, ViewContext ctx)
    {
        var builder = new HtmlContentBuilder();
        builder.AppendHtml($"<div class=\"{column.CssClass}\">");
        foreach (var block in column.Blocks.OrderBy(b => b.Order))
        {
            var renderCtx = new BlockRenderContext { Block = block, ViewContext = ctx };
            var pipeline  = new CmsPipeline<BlockRenderContext>(
                _hooks.OrderBy(h => h.Order)
                      .Select<IBlockRenderHook, Func<BlockRenderContext, CancellationToken, Task>>(
                          h => (c, t) => h.ExecuteAsync(c, t)));

            // Core render
            var renderer = _registry.Resolve(block.BlockType);
            renderCtx.Output = await renderer.RenderAsync(block, ctx);
            await pipeline.ExecuteAsync(renderCtx);
            builder.AppendHtml(renderCtx.Output ?? HtmlString.Empty);
        }
        builder.AppendHtml("</div>");
        return builder;
    }
}
```

### 5.6 PageService (Entry Point)

```csharp
public class PageService
{
    private readonly PageReadPipelineFactory  _readFactory;
    private readonly PageSavePipelineFactory  _saveFactory;
    private readonly ICmsEventBus             _eventBus;
    private readonly IPageRepository          _repo;

    public async Task<Page?> GetPageAsync(string slug, string culture,
        long? tenantId = null, bool includeDraft = false, CancellationToken ct = default)
    {
        var ctx = new PageReadContext { Slug = slug, Culture = culture, TenantId = tenantId, IncludeDraft = includeDraft };
        await _readFactory.Build().ExecuteAsync(ctx, ct);
        return ctx.Page;
    }

    public async Task<SaveResult> SavePageAsync(Page page, SaveOperation operation, CancellationToken ct = default)
    {
        var ctx = new PageSaveContext { Page = page, Operation = operation };
        await _saveFactory.Build().ExecuteAsync(ctx, ct);

        if (ctx.HasValidationErrors) return SaveResult.Invalid(ctx.ValidationErrors);
        if (ctx.IsShortCircuited)    return SaveResult.Failed(ctx.ShortCircuitReason!);

        if (operation == SaveOperation.Publish)
            await _eventBus.PublishAsync(new PagePublishedEvent { Page = page }, ct);

        return SaveResult.Ok();
    }
}
```

---

## 6. Dynamic Blocks & ViewComponents

### 6.1 DynamicBlockRenderer

```csharp
public class DynamicBlockRenderer : BlockRenderer<DynamicBlock>
{
    private readonly IViewComponentHelper _viewComponentHelper;
    public override string BlockType => "dynamic";

    protected override async Task<IHtmlContent> RenderAsync(DynamicBlock block, ViewContext ctx)
    {
        (_viewComponentHelper as IViewContextAware)?.Contextualize(ctx);
        return await _viewComponentHelper.InvokeAsync(block.ComponentName, block.Parameters);
    }
}
```

### 6.2 Token Interpolation (for RichText)

```csharp
public interface ITemplateTokenResolver
{
    Task<Dictionary<string, string>> ResolveAsync(HttpContext ctx);
}

public class TokenInterpolator
{
    private static readonly Regex _tokenRegex = new(@"\{\{([\w.]+)\}\}", RegexOptions.Compiled);

    public async Task<string> InterpolateAsync(string content, HttpContext ctx, ITemplateTokenResolver resolver)
    {
        var tokens = await resolver.ResolveAsync(ctx);
        return _tokenRegex.Replace(content, m =>
        {
            var key = m.Groups[1].Value;
            return tokens.TryGetValue(key, out var v) ? v : m.Value;
        });
    }
}
```

---

## 7. Runtime Razor Compilation from Database

Enables `.cshtml` templates stored in the database to be rendered with full Razor support (`@Model`, `@inject`, `@foreach`, tag helpers).

### 7.1 Change Token Registry

```csharp
public class TemplateChangeToken : IChangeToken
{
    private CancellationTokenSource _cts = new();
    public bool HasChanged => _cts.Token.IsCancellationRequested;
    public bool ActiveChangeCallbacks => true;
    public IDisposable RegisterChangeCallback(Action<object?> callback, object? state)
        => _cts.Token.Register(callback, state);
    public void SignalChange()
    {
        var old = Interlocked.Exchange(ref _cts, new CancellationTokenSource());
        old.Cancel(); old.Dispose();
    }
}

public class TemplateChangeTokenRegistry
{
    private readonly ConcurrentDictionary<string, TemplateChangeToken> _tokens = new();
    public IChangeToken GetOrCreate(string path) => _tokens.GetOrAdd(path, _ => new TemplateChangeToken());
    public void SignalChange(string path) { if (_tokens.TryGetValue(path, out var t)) t.SignalChange(); }
}
```

### 7.2 Database File Provider

```csharp
public class DatabaseTemplateFileProvider : IFileProvider
{
    public const string VirtualRoot = "/DbTemplates/";
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly TemplateChangeTokenRegistry _changeTokens;

    public IFileInfo GetFileInfo(string subpath)
    {
        if (!subpath.StartsWith(VirtualRoot, StringComparison.OrdinalIgnoreCase))
            return new NotFoundFileInfo(subpath);

        using var scope = _scopeFactory.CreateScope();
        var repo    = scope.ServiceProvider.GetRequiredService<ITemplateRepository>();
        var content = repo.GetRazorContent(subpath);

        return content is not null
            ? new InMemoryFileInfo(Path.GetFileName(subpath), content)
            : new NotFoundFileInfo(subpath);
    }

    public IDirectoryContents GetDirectoryContents(string subpath)
        => NotFoundDirectoryContents.Singleton;

    public IChangeToken Watch(string filter) => _changeTokens.GetOrCreate(filter);
}

// InMemoryFileInfo: wraps a string as a MemoryStream for Razor to read during compilation.
// The string originates from PostgreSQL (via Marten). "In memory" refers only to the
// transient Stream — not the storage layer.
public class InMemoryFileInfo : IFileInfo
{
    private readonly byte[] _bytes;
    public InMemoryFileInfo(string name, string content)
    {
        Name  = name;
        _bytes = Encoding.UTF8.GetBytes(content);
        LastModified = DateTimeOffset.UtcNow;
    }
    public bool Exists => true;
    public bool IsDirectory => false;
    public long Length => _bytes.Length;
    public string? PhysicalPath => null;
    public string Name { get; }
    public DateTimeOffset LastModified { get; }
    public Stream CreateReadStream() => new MemoryStream(_bytes);
}
```

### 7.3 Template Repository (Marten)

```csharp
public class RazorTemplateDocument
{
    public long Id { get; set; }
    public string VirtualPath { get; set; } = "";
    public string RazorContent { get; set; } = "";
    public DateTimeOffset UpdatedAt { get; set; }
}

public class TemplateRepository : ITemplateRepository
{
    private readonly IDocumentStore _store;
    private readonly TemplateChangeTokenRegistry _changeTokens;

    // Called synchronously by IFileProvider (Razor's constraint)
    public string? GetRazorContent(string virtualPath)
    {
        using var session = _store.QuerySession();
        return session.Query<RazorTemplateDocument>()
            .Where(t => t.VirtualPath == virtualPath)
            .Select(t => t.RazorContent)
            .FirstOrDefault();
    }

    public async Task SaveTemplateAsync(string virtualPath, string razorContent, CancellationToken ct = default)
    {
        await using var session = _store.LightweightSession();
        var doc = await session.Query<RazorTemplateDocument>()
            .FirstOrDefaultAsync(t => t.VirtualPath == virtualPath, ct)
            ?? new RazorTemplateDocument { Id = Snowflake.NewId() };

        doc.VirtualPath  = virtualPath;
        doc.RazorContent = razorContent;
        doc.UpdatedAt    = DateTimeOffset.UtcNow;
        session.Store(doc);
        await session.SaveChangesAsync(ct);
        _changeTokens.SignalChange(virtualPath);
    }
}
```

### 7.4 Compilation Lifecycle

```
FIRST REQUEST
  Razor asks DatabaseTemplateFileProvider.GetFileInfo("/DbTemplates/abc.cshtml")
  → Marten QuerySession fetches RazorContent from PostgreSQL
  → Returns InMemoryFileInfo (MemoryStream wrapping the string)
  → Razor reads stream, compiles to assembly, caches compiled assembly
  → Stream is GC'd (was transient bridge only)

SUBSEQUENT REQUESTS (unchanged content)
  → Razor serves from compiled assembly cache
  → GetFileInfo() is never called again — zero DB hits

CONTENT UPDATED
  → TemplateRepository.SaveTemplateAsync() writes to Marten
  → TemplateChangeTokenRegistry.SignalChange() fires
  → Razor sees IChangeToken.HasChanged = true
  → Razor evicts compiled assembly
  → Next request triggers fresh DB fetch + recompile
```

---

## 8. Output Caching & Cache Busting

### 8.1 Content Hash / ETag Service

```csharp
public class PageCacheKeyService : IPageCacheKeyService
{
    private readonly JsonSerializerOptions _jsonOptions;

    public string ComputeETag(IEnumerable<BlockBase> blocks)
    {
        var json  = JsonSerializer.Serialize(blocks.OrderBy(b => b.Order), _jsonOptions);
        var hash  = SHA256.HashData(Encoding.UTF8.GetBytes(json));
        return $"\"{Convert.ToHexString(hash)[..16]}\"";
    }

    public string ComputeCacheTag(long pageId) => $"page-{pageId}";
}
```

### 8.2 Output Cache Policy (ASP.NET Core 7+)

```csharp
builder.Services.AddOutputCache(options =>
{
    options.AddPolicy("BlockPage", policy => policy
        .Expire(TimeSpan.FromHours(24))
        .SetVaryByQuery("*")
        .Tag("cms-pages")
        .AddPolicy<BlockPageCachePolicy>());
});
```

### 8.3 ETag Middleware

```csharp
public class ETagMiddleware : IMiddleware
{
    public async Task InvokeAsync(HttpContext context, RequestDelegate next)
    {
        var originalBody = context.Response.Body;
        using var buffer = new MemoryStream();
        context.Response.Body = buffer;
        await next(context);
        buffer.Seek(0, SeekOrigin.Begin);
        var body  = await new StreamReader(buffer).ReadToEndAsync();
        var hash  = SHA256.HashData(Encoding.UTF8.GetBytes(body));
        var etag  = $"\"{Convert.ToHexString(hash)[..16]}\"";

        context.Response.Headers.ETag         = etag;
        context.Response.Headers.CacheControl = "public, max-age=0, must-revalidate";

        if (context.Request.Headers.IfNoneMatch.ToString() == etag)
        {
            context.Response.StatusCode = StatusCodes.Status304NotModified;
            context.Response.Body       = originalBody;
            return;
        }
        buffer.Seek(0, SeekOrigin.Begin);
        context.Response.Body = originalBody;
        await buffer.CopyToAsync(originalBody);
    }
}
```

### 8.4 Cache Invalidation on Save

```csharp
public class CacheInvalidationHook : IPageSaveHook
{
    private readonly IOutputCacheStore _cacheStore;
    public int Order => 10;

    public async Task ExecuteAsync(PageSaveContext ctx, CancellationToken ct)
    {
        await _cacheStore.EvictByTagAsync($"page-slug-{ctx.Page.Slug}", ct);
        await _cacheStore.EvictByTagAsync($"page-{ctx.Page.Id}", ct);
    }
}
```

### 8.5 Static Asset Versioning

```html
<link rel="stylesheet" asp-href-include="~/css/blocks/*.css" asp-append-version="true" />
```

---

## 9. Localization & Culture Routing

### 9.1 Setup

```csharp
builder.Services
    .AddLocalization(o => o.ResourcesPath = "Resources")
    .AddControllersWithViews()
    .AddViewLocalization()
    .AddDataAnnotationsLocalization();

builder.Services.Configure<RequestLocalizationOptions>(options =>
{
    var supported = new[] { new CultureInfo("en"), new CultureInfo("fr"), new CultureInfo("es"), new CultureInfo("ar") };
    options.DefaultRequestCulture  = new RequestCulture("en");
    options.SupportedCultures      = supported;
    options.SupportedUICultures    = supported;
    options.RequestCultureProviders =
    [
        new RouteDataRequestCultureProvider(),
        new QueryStringRequestCultureProvider(),
        new CookieRequestCultureProvider(),
        new AcceptLanguageHeaderRequestCultureProvider(),
    ];
});
```

### 9.2 Culture Route Constraint

```csharp
public class CultureCodeRouteConstraint : IRouteConstraint
{
    private static readonly HashSet<string> _supported = ["en", "fr", "es", "ar"];
    public bool Match(HttpContext? ctx, IRouter? route, string routeKey,
        RouteValueDictionary values, RouteDirection direction)
    {
        if (!values.TryGetValue(routeKey, out var value)) return false;
        return _supported.Contains(value?.ToString() ?? "", StringComparer.OrdinalIgnoreCase);
    }
}

// Route: {culture:culturecode}/{**slug}
```

### 9.3 LocalizedString Value Type

```csharp
public record LocalizedString
{
    private readonly Dictionary<string, string> _values;
    public LocalizedString(Dictionary<string, string> values) => _values = values;

    public string Get(string culture)
        => _values.TryGetValue(culture, out var v) ? v
         : _values.TryGetValue("en", out var fallback) ? fallback
         : "";

    public static implicit operator string(LocalizedString ls)
        => ls.Get(CultureInfo.CurrentUICulture.TwoLetterISOLanguageName);
}
```

### 9.4 Page Variants (One Document Per Culture)

```csharp
// Pages sharing a PageGroupId are culture variants of the same page
// Queried by slug + culture for page delivery
// Queried by PageGroupId for hreflang generation + language switcher
```

### 9.5 hreflang + Language Switcher

```html
@foreach (var variant in Model.Variants)
{
    <link rel="alternate" hreflang="@variant.Culture" href="/@variant.Culture/@variant.Slug" />
}
<link rel="alternate" hreflang="x-default" href="/en/@Model.Page.Slug" />
```

### 9.6 RTL Support

```html
@{ var isRtl = new[] { "ar", "he", "fa" }.Contains(Model.Culture, StringComparer.OrdinalIgnoreCase); }
<html lang="@Model.Culture" dir="@(isRtl ? "rtl" : "ltr")">
```

---

## 10. Module System

### 10.1 IModule Contract

```csharp
public interface IModule
{
    string Name { get; }
    string Version { get; }
    string Author { get; }
    IReadOnlyList<string> Dependencies { get; }
    void ConfigureServices(IServiceCollection services);
    void Configure(IModuleBuilder builder);
}

public interface IModuleBuilder
{
    IModuleBuilder AddBlock<TBlock, TRenderer>()
        where TBlock : BlockBase
        where TRenderer : class, IBlockRenderer;
    IModuleBuilder AddPageReadHook<THook>() where THook : class, IPageReadHook;
    IModuleBuilder AddPageSaveHook<THook>() where THook : class, IPageSaveHook;
    IModuleBuilder AddBlockRenderHook<THook>() where THook : class, IBlockRenderHook;
    IModuleBuilder AddEventHandler<TEvent, THandler>()
        where TEvent : class, ICmsEvent
        where THandler : class, ICmsEventHandler<TEvent>;
    IModuleBuilder AddAdminSection(AdminSectionDescriptor section);
}
```

### 10.2 AssemblyLoadContext (Isolated, Unloadable)

```csharp
public class CmsAssemblyLoadContext : AssemblyLoadContext
{
    public CmsAssemblyLoadContext(string name) : base(name, isCollectible: true) { }

    protected override Assembly? Load(AssemblyName name)
    {
        var existing = Default.Assemblies.FirstOrDefault(a => a.GetName().Name == name.Name);
        return existing;
    }
}
```

### 10.3 Module Registry

```csharp
public class ModuleRegistry
{
    private readonly ConcurrentDictionary<string, CmsModuleDescriptor> _modules = new();
    public IReadOnlyCollection<CmsModuleDescriptor> All => _modules.Values.ToList();
    public bool TryGet(string name, out CmsModuleDescriptor? descriptor) => _modules.TryGetValue(name, out descriptor);
    public void Register(CmsModuleDescriptor descriptor)
    {
        if (_modules.TryRemove(descriptor.Module.Name, out var prev)) prev.LoadContext?.Unload();
        _modules[descriptor.Module.Name] = descriptor;
    }
    public bool Unload(string name)
    {
        if (!_modules.TryRemove(name, out var d)) return false;
        d.LoadContext?.Unload();
        return true;
    }
}
```

### 10.4 Module Loader

```csharp
public class ModuleLoader
{
    public LoadResult Load(byte[] assemblyBytes)
    {
        var ctx      = new CmsAssemblyLoadContext($"module-{Snowflake.NewId():N}");
        using var ms = new MemoryStream(assemblyBytes);
        var assembly = ctx.LoadFromStream(ms);

        var moduleType = assembly.GetTypes()
            .FirstOrDefault(t => typeof(IModule).IsAssignableFrom(t) && !t.IsAbstract)
            ?? throw new InvalidOperationException("No IModule found.");

        var module = (IModule)Activator.CreateInstance(moduleType)!;

        foreach (var dep in module.Dependencies)
            if (!_registry.TryGet(dep, out _))
                return LoadResult.Failure($"Missing dependency: {dep}");

        module.ConfigureServices(_services);
        var builder = _builderFactory.Create();
        module.Configure(builder);
        builder.Apply();

        _fileProviderRegistry.Register(assembly); // EmbeddedFileProvider for views

        _registry.Register(new CmsModuleDescriptor
        {
            Module = module, Assembly = assembly, LoadContext = ctx,
            Status = ModuleStatus.Active, LoadedAt = DateTimeOffset.UtcNow
        });

        return LoadResult.Ok(module.Name);
    }
}
```

### 10.5 Module Persistence (Marten)

```csharp
public class InstalledModuleDocument
{
    public string Id { get; set; } = "";         // = module Name
    public string Version { get; set; } = "";
    public string Author { get; set; } = "";
    public byte[] AssemblyBytes { get; set; } = [];
    public bool IsActive { get; set; }
    public DateTimeOffset InstalledAt { get; set; }
    public DateTimeOffset? DisabledAt { get; set; }
}
```

### 10.6 Bootstrap Service

```csharp
public class ModuleBootstrapService : IHostedService
{
    public async Task StartAsync(CancellationToken ct)
    {
        await using var session = _store.QuerySession();
        var modules = await session.Query<InstalledModuleDocument>()
            .Where(m => m.IsActive).ToListAsync(ct);

        foreach (var doc in modules)
            _loader.Load(doc.AssemblyBytes);
    }
    public Task StopAsync(CancellationToken ct) => Task.CompletedTask;
}
```

### 10.7 Event Bus

```csharp
public interface ICmsEvent { DateTimeOffset OccurredAt => DateTimeOffset.UtcNow; }
public interface ICmsEventHandler<TEvent> where TEvent : ICmsEvent
{
    Task HandleAsync(TEvent evt, CancellationToken ct);
}

public class CmsEventBus : ICmsEventBus
{
    private readonly IServiceProvider _sp;
    public async Task PublishAsync<TEvent>(TEvent evt, CancellationToken ct) where TEvent : class, ICmsEvent
    {
        var handlers = _sp.GetServices<ICmsEventHandler<TEvent>>();
        await Task.WhenAll(handlers.Select(async h =>
        {
            try { await h.HandleAsync(evt, ct); }
            catch (Exception ex) { /* log */ }
        }));
    }
}
```

### 10.8 Core Events

```csharp
public record PageSavedEvent     : ICmsEvent { public required Page Page { get; init; } public required SaveOperation Operation { get; init; } }
public record PagePublishedEvent : ICmsEvent { public required Page Page { get; init; } }
public record PageUnpublishedEvent : ICmsEvent { public required Page Page { get; init; } }
public record BlockRenderedEvent : ICmsEvent { public required string BlockType { get; init; } public required TimeSpan Duration { get; init; } }
public record MediaUploadedEvent : ICmsEvent { public required MediaItem Media { get; init; } }
public record FormSubmittedEvent : ICmsEvent { public required FormSubmission Submission { get; init; } }
```

---

## 11. Admin UI Shell

### 11.1 AdminSectionDescriptor

```csharp
public record AdminSectionDescriptor
{
    public required string ModuleName { get; init; }
    public required string MenuLabel { get; init; }
    public required string Icon { get; init; }
    public int MenuOrder { get; init; } = 100;
    public string? MenuGroup { get; init; }
    public required string AreaName { get; init; }
    public required string ControllerName { get; init; }
    public string ActionName { get; init; } = "Index";
    public string RequiredPermission { get; init; } = "Admin";
    public List<AdminMenuItemDescriptor> Children { get; init; } = [];
}
```

### 11.2 Admin Menu Registry

```csharp
public class AdminMenuRegistry
{
    private readonly ConcurrentDictionary<string, AdminSectionDescriptor> _sections = new();
    public IReadOnlyList<AdminSectionDescriptor> GetAll() => _sections.Values.OrderBy(s => s.MenuOrder).ToList();
    public IReadOnlyList<AdminSectionDescriptor> GetForUser(ClaimsPrincipal user)
        => GetAll().Where(s => user.HasClaim("permission", s.RequiredPermission) || user.IsInRole("SuperAdmin")).ToList();
    public void Register(AdminSectionDescriptor section) => _sections[section.ModuleName] = section;
    public void Unregister(string name) => _sections.TryRemove(name, out _);
}
```

### 11.3 Embedded Module Views (RCL Pattern)

Each module embeds its admin views and serves them via `EmbeddedFileProvider` registered into Razor's `CompositeFileProvider`. Module controllers use `[Area("ModuleName")]` attribute. No admin shell view changes are needed to add a new module's UI.

### 11.4 Module Settings in Marten

Each module defines its own POCO settings document with a stable `Id`. Marten stores it as `jsonb` automatically — no migrations, no shared schema.

```csharp
// Pattern every module follows:
public class MyModuleSettings
{
    public static readonly string DocumentId = "my-module-settings";
    public string Id { get; set; } = DocumentId;
    // ... module-specific settings ...
}
```

### 11.5 Built-In Admin Sections

| Section | Group | Module |
|---|---|---|
| Dashboard | — | Core |
| Pages | Content | Core |
| Media Library | Content | Core |
| Navigation | Content | Core |
| Forms | Content | FormsModule |
| Redirects | Content | Core |
| Taxonomy | Content | Core |
| Modules | System | Core |
| Users & Roles | System | Core |
| Tenants | System | Core |
| Audit Log | System | Core |
| SEO | Settings | SeoModule |
| Search | Settings | SearchModule |
| Webhooks | Settings | WebhookModule |

---

## 12. Content Lifecycle & Publishing Workflow

### 12.1 Status State Machine

```
Draft ──► PendingReview ──► Approved ──► Published
  ▲              │               │           │
  │         (rejected)      (scheduled)  (unpublish)
  └──────────────────────────────────────► Unpublished
                                                │
                                            (archive)
                                           Archived
```

### 12.2 WorkflowService

```csharp
public class ContentWorkflowService
{
    public async Task<WorkflowResult> SubmitForReviewAsync(long pageId, string submittedBy, CancellationToken ct)
    {
        var page = await _repo.GetByIdAsync(pageId, ct);
        if (page is null) return WorkflowResult.NotFound();
        if (page.Status != PageStatus.Draft) return WorkflowResult.InvalidTransition();

        var updated = page with { Status = PageStatus.PendingReview };
        await _pageService.SavePageAsync(updated, SaveOperation.Update, ct);
        await _eventBus.PublishAsync(new PageSubmittedForReviewEvent { Page = updated, SubmittedBy = submittedBy }, ct);
        await _notificationService.NotifyReviewersAsync(updated, ct);
        return WorkflowResult.Ok();
    }

    public async Task<WorkflowResult> ApproveAsync(long pageId, string approvedBy, CancellationToken ct) { ... }
    public async Task<WorkflowResult> RejectAsync(long pageId, string rejectedBy, string reason, CancellationToken ct) { ... }
    public async Task<WorkflowResult> PublishAsync(long pageId, string publishedBy, CancellationToken ct) { ... }
    public async Task<WorkflowResult> ScheduleAsync(long pageId, DateTimeOffset publishAt, CancellationToken ct) { ... }
    public async Task<WorkflowResult> UnpublishAsync(long pageId, string unpublishedBy, CancellationToken ct) { ... }
}
```

### 12.3 Scheduled Publishing

```csharp
// Hangfire recurring job — runs every minute
public class ScheduledPublishJob
{
    private readonly IPageRepository _repo;
    private readonly ContentWorkflowService _workflow;

    [AutomaticRetry(Attempts = 3)]
    public async Task ExecuteAsync()
    {
        var due = await _repo.GetScheduledDueAsync(DateTimeOffset.UtcNow);
        foreach (var page in due)
            await _workflow.PublishAsync(page.Id, "scheduler", CancellationToken.None);
    }
}
```

### 12.4 Preview Mode

```csharp
// Generates a signed, time-limited preview token
public class PreviewTokenService
{
    private readonly IDataProtector _protector;

    public string GenerateToken(long pageId, string culture, TimeSpan validity)
    {
        var payload = JsonSerializer.Serialize(new { pageId, culture, expires = DateTimeOffset.UtcNow.Add(validity) });
        return _protector.Protect(payload);
    }

    public PreviewClaim? ValidateToken(string token)
    {
        try
        {
            var json    = _protector.Unprotect(token);
            var payload = JsonSerializer.Deserialize<dynamic>(json)!;
            // validate expiry, return claim
            return new PreviewClaim(payload.pageId, payload.culture);
        }
        catch { return null; }
    }
}

// Middleware checks for ?preview=<token> and sets IncludeDraft = true on PageReadContext
```

### 12.5 Content Locking

```csharp
public class ContentLockDocument
{
    public long Id { get; set; }      // = PageId
    public string LockedBy { get; set; } = "";
    public string LockedByName { get; set; } = "";
    public DateTimeOffset LockedAt { get; set; }
    public DateTimeOffset ExpiresAt { get; set; }
}

// Lock acquired on page open in editor, released on save/close, auto-expires after 30 min
// Hangfire job cleans stale locks
```

### 12.6 Content Versioning

```csharp
// On every save, a PageRevision is stored in Marten
// PageRevision.Snapshot = deep copy of Page at that moment
// Admin UI: revision list, side-by-side diff, rollback button
// Rollback: load snapshot, set Version++, save as new revision
```

---

## 13. Media Library

### 13.1 Domain Model

```csharp
public class MediaItem
{
    public long Id { get; set; } = Snowflake.NewId();
    public string FileName { get; set; } = "";
    public string MimeType { get; set; } = "";
    public long FileSizeBytes { get; set; }
    public string StoragePath { get; set; } = "";   // relative to storage root
    public string PublicUrl { get; set; } = "";
    public string? CdnUrl { get; set; }
    public string? AltText { get; set; }
    public string? Title { get; set; }
    public FocalPoint? FocalPoint { get; set; }
    public MediaFolder? Folder { get; set; }
    public List<string> Tags { get; set; } = [];
    public Dictionary<string, string> Metadata { get; set; } = [];
    public long? TenantId { get; set; }
    public DateTimeOffset UploadedAt { get; set; } = DateTimeOffset.UtcNow;
    public string UploadedBy { get; set; } = "";
}

public record FocalPoint(double X, double Y); // 0.0–1.0 relative coordinates

public class MediaFolder
{
    public long Id { get; set; }
    public string Name { get; set; } = "";
    public long? ParentId { get; set; }
}
```

### 13.2 Image Processing Pipeline

```csharp
public class ImageProcessingService
{
    private readonly IImageStorage _storage;

    public async Task<MediaItem> ProcessAndStoreAsync(
        Stream sourceStream, string fileName, string mimeType, CancellationToken ct)
    {
        using var image = await Image.LoadAsync(sourceStream, ct);

        // Store original
        var originalPath = await _storage.SaveAsync(sourceStream, fileName, ct);

        // Generate responsive variants
        var variants = new[] { 400, 800, 1200, 1920 };
        foreach (var width in variants.Where(w => w < image.Width))
        {
            var resized = image.Clone(ctx => ctx.Resize(new ResizeOptions
            {
                Width = width,
                Mode  = ResizeMode.Max
            }));
            using var ms = new MemoryStream();
            await resized.SaveAsWebpAsync(ms, ct);
            ms.Seek(0, SeekOrigin.Begin);
            await _storage.SaveAsync(ms, $"{Path.GetFileNameWithoutExtension(fileName)}-{width}w.webp", ct);
        }

        return new MediaItem
        {
            FileName      = fileName,
            MimeType      = mimeType,
            FileSizeBytes = sourceStream.Length,
            StoragePath   = originalPath,
            PublicUrl     = _storage.GetPublicUrl(originalPath),
        };
    }
}
```

### 13.3 Storage Abstraction

```csharp
public interface IMediaStorage
{
    Task<string> SaveAsync(Stream stream, string fileName, CancellationToken ct);
    Task DeleteAsync(string path, CancellationToken ct);
    string GetPublicUrl(string path);
}

// Implementations: LocalDiskStorage, AzureBlobStorage, S3Storage
```

---

## 14. Navigation & Menus

### 14.1 Domain Model

```csharp
public class NavigationMenu
{
    public long Id { get; set; }
    public string Handle { get; set; } = "";     // "main-nav", "footer", "sidebar"
    public string Label { get; set; } = "";
    public List<NavigationItem> Items { get; set; } = [];
    public long? TenantId { get; set; }
}

public class NavigationItem
{
    public long Id { get; set; } = Snowflake.NewId();
    public LocalizedString Label { get; set; } = new(new() { ["en"] = "" });
    public NavigationTarget Target { get; set; } = default!;
    public int Order { get; set; }
    public string? CssClass { get; set; }
    public bool OpenInNewTab { get; set; }
    public List<NavigationItem> Children { get; set; } = [];
}

// Target is polymorphic: PageTarget, ExternalUrlTarget, AnchorTarget
public abstract record NavigationTarget;
public record PageTarget(long PageGroupId, string Culture) : NavigationTarget;
public record ExternalUrlTarget(string Url) : NavigationTarget;
public record AnchorTarget(string Anchor) : NavigationTarget;
```

### 14.2 Redirects

```csharp
public class RedirectRule
{
    public long Id { get; set; }
    public string FromPath { get; set; } = "";    // supports wildcards
    public string ToPath { get; set; } = "";
    public int StatusCode { get; set; } = 301;
    public bool IsRegex { get; set; }
    public bool IsActive { get; set; } = true;
    public long? TenantId { get; set; }
}

// RedirectMiddleware: runs after routing, checks slug-change misses against redirect rules
public class RedirectMiddleware : IMiddleware
{
    public async Task InvokeAsync(HttpContext context, RequestDelegate next)
    {
        await next(context);
        if (context.Response.StatusCode == 404)
        {
            var redirect = await _repo.FindMatchAsync(context.Request.Path);
            if (redirect is not null)
            {
                context.Response.StatusCode = redirect.StatusCode;
                context.Response.Headers.Location = redirect.ToPath;
            }
        }
    }
}
```

---

## 15. Users, Roles & Permissions

### 15.1 Permission Model

```csharp
// Permissions are strings: "pages.read", "pages.publish", "media.upload", "modules.install"
// Roles aggregate permissions
// Users have roles + optional direct permission grants/denials

public class CmsRole
{
    public long Id { get; set; }
    public string Name { get; set; } = "";
    public List<string> Permissions { get; set; } = [];
    public long? TenantId { get; set; }
}

public class CmsUser
{
    public long Id { get; set; }
    public string Email { get; set; } = "";
    public List<long> RoleIds { get; set; } = [];
    public List<string> DirectGrants { get; set; } = [];
    public List<string> DirectDenials { get; set; } = [];
    public long? TenantId { get; set; }
}
```

### 15.2 Permission Evaluation

```csharp
public class PermissionService
{
    public bool HasPermission(CmsUser user, string permission)
    {
        if (user.DirectDenials.Contains(permission)) return false;
        if (user.DirectGrants.Contains(permission)) return true;
        var roles = _roleRepo.GetByIds(user.RoleIds);
        return roles.Any(r => r.Permissions.Contains(permission) || r.Permissions.Contains("*"));
    }
}
```

### 15.3 Built-In Permissions

```
pages.read          pages.create        pages.update
pages.publish       pages.delete        pages.approve
media.read          media.upload        media.delete
modules.view        modules.install     modules.uninstall
users.read          users.create        users.manage
settings.read       settings.update
audit.read
```

---

## 16. Multi-Tenancy

### 16.1 Tenant Model

```csharp
public class Tenant
{
    public long Id { get; set; }
    public string Name { get; set; } = "";
    public string Host { get; set; } = "";           // e.g. "client-a.myplatform.com"
    public string? CustomDomain { get; set; }        // e.g. "www.client-a.com"
    public string DefaultCulture { get; set; } = "en";
    public List<string> SupportedCultures { get; set; } = ["en"];
    public TenantSettings Settings { get; set; } = new();
    public bool IsActive { get; set; } = true;
}
```

### 16.2 Tenant Resolution Middleware

```csharp
public class TenantMiddleware : IMiddleware
{
    public async Task InvokeAsync(HttpContext context, RequestDelegate next)
    {
        var host   = context.Request.Host.Host;
        var tenant = await _tenantRepo.GetByHostAsync(host)
                  ?? await _tenantRepo.GetByCustomDomainAsync(host);

        if (tenant is null || !tenant.IsActive)
        {
            context.Response.StatusCode = 404; return;
        }

        context.Items["TenantId"] = tenant.Id;
        context.Items["Tenant"]   = tenant;
        await next(context);
    }
}
```

All Marten queries include a `TenantId` filter when multi-tenancy is enabled. Marten itself supports row-level tenancy via `IDocumentSession.ForTenant()`.

---

## 17. Audit Log

```csharp
public class AuditEntry
{
    public long Id { get; set; } = Snowflake.NewId();
    public string Action { get; set; } = "";          // "page.published", "module.installed"
    public string EntityType { get; set; } = "";
    public string EntityId { get; set; } = "";
    public string PerformedBy { get; set; } = "";
    public string? PerformedByName { get; set; }
    public Dictionary<string, object?> Before { get; set; } = [];
    public Dictionary<string, object?> After { get; set; } = [];
    public string? IpAddress { get; set; }
    public string? UserAgent { get; set; }
    public long? TenantId { get; set; }
    public DateTimeOffset OccurredAt { get; set; } = DateTimeOffset.UtcNow;
}

// AuditLogHook : IPageSaveHook { Order = 99 }
// Writes immutable AuditEntry to Marten after every pipeline completion
// Never modified after write — append-only
```

---

## 18. Taxonomy

```csharp
public class TaxonomyVocabulary
{
    public long Id { get; set; }
    public string Handle { get; set; } = "";       // "category", "tag", "topic"
    public string Label { get; set; } = "";
    public bool AllowHierarchy { get; set; }
    public bool AllowMultiple { get; set; } = true;
    public long? TenantId { get; set; }
}

public class TaxonomyTerm
{
    public long Id { get; set; }
    public long VocabularyId { get; set; }
    public LocalizedString Name { get; set; } = new(new() { ["en"] = "" });
    public string Slug { get; set; } = "";
    public long? ParentId { get; set; }
    public int Order { get; set; }
}

// Pages reference terms: Page.TermIds = List<long>
// Term archives are standard Pages with BlogListBlock filtered by TermId
```

---

## 19. Content Relationships

```csharp
public class ContentRelationship
{
    public long Id { get; set; }
    public long SourcePageId { get; set; }
    public long TargetPageGroupId { get; set; }
    public string RelationshipType { get; set; } = ""; // "related", "linked-product", "author"
    public int Order { get; set; }
}

// Queried as: "give me all pages related to page X of type 'related'"
// Rendered via RelatedContentBlock which uses a ViewComponent to fetch and display
```

---

## 20. Recycle Bin

```csharp
// Soft-delete pattern — Page.Status = Archived + DeletedAt timestamp
// Purge job (Hangfire) permanently removes after configurable retention period (default 30 days)
// Restore: set Status back to Draft, clear DeletedAt

public class RecycleBinService
{
    public async Task SoftDeleteAsync(long pageId, string deletedBy, CancellationToken ct) { ... }
    public async Task RestoreAsync(long pageId, CancellationToken ct) { ... }
    public async Task PurgeAsync(long pageId, CancellationToken ct) { ... }  // permanent
}
```

---

## 21. Full-Text Search

### 21.1 Search Index Document

```csharp
public class PageSearchDocument
{
    public string Id { get; set; } = "";          // = pageId:culture
    public string PageId { get; set; } = "";
    public string Culture { get; set; } = "";
    public string Title { get; set; } = "";
    public string Slug { get; set; } = "";
    public string FullText { get; set; } = "";    // all block text content extracted
    public string PageType { get; set; } = "";
    public List<string> Tags { get; set; } = [];
    public DateTimeOffset PublishedAt { get; set; }
    public long? TenantId { get; set; }
}
```

### 21.2 Indexing Event Handler

```csharp
public class SearchIndexHandler : ICmsEventHandler<PagePublishedEvent>
{
    private readonly IMeilisearchClient _search;
    private readonly IBlockTextExtractor _extractor;

    public async Task HandleAsync(PagePublishedEvent evt, CancellationToken ct)
    {
        var text = _extractor.ExtractAllText(evt.Page);  // walks all blocks, extracts plain text
        var doc  = new PageSearchDocument
        {
            Id        = $"{evt.Page.Id}:{evt.Page.Culture}",
            PageId    = evt.Page.Id.ToString(),
            Culture   = evt.Page.Culture,
            Title     = evt.Page.Title,
            Slug      = evt.Page.Slug,
            FullText  = text,
            PageType  = evt.Page.PageType.ToString(),
        };
        await _search.Index("pages").AddDocumentsAsync(new[] { doc }, ct: ct);
    }
}
```

---

## 22. Headless API

All page/block content is accessible via a versioned JSON API for MAUI, SPA, or third-party consumers.

```csharp
[ApiController]
[Route("api/v1")]
public class PagesApiController : ControllerBase
{
    [HttpGet("pages/{culture}/{slug}")]
    public async Task<IActionResult> GetPage(string culture, string slug)
    {
        var page = await _pageService.GetPageAsync(slug, culture);
        if (page is null) return NotFound();
        return Ok(new PageApiResponse(page));
    }

    [HttpGet("pages")]
    public async Task<IActionResult> ListPages(
        [FromQuery] string culture = "en",
        [FromQuery] string? type = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        var result = await _repo.ListPublishedAsync(culture, type, page, pageSize);
        return Ok(result);
    }
}
```

API responses serialize blocks polymorphically using `BlockJsonConverter`. The same domain model serves both server-rendered and headless consumers.

---

## 23. Webhooks

```csharp
public class WebhookSubscription
{
    public long Id { get; set; }
    public string TargetUrl { get; set; } = "";
    public List<string> Events { get; set; } = [];   // "page.published", "form.submitted"
    public string? Secret { get; set; }              // HMAC-SHA256 signing key
    public bool IsActive { get; set; } = true;
    public long? TenantId { get; set; }
}

// WebhookDispatcher : ICmsEventHandler<ICmsEvent>
// Dispatches via Hangfire background job (with retry) to all matching subscriptions
// Signs payload with HMAC-SHA256 using subscription secret
// Stores delivery log: WebhookDeliveryLog { SubscriptionId, Event, StatusCode, ResponseBody, AttemptedAt }
```

---

## 24. Forms Builder

### 24.1 Form Domain Model

```csharp
public class CmsForm
{
    public long Id { get; set; }
    public string Handle { get; set; } = "";
    public LocalizedString Title { get; set; } = new(new() { ["en"] = "" });
    public List<FormField> Fields { get; set; } = [];
    public FormSubmissionSettings Submission { get; set; } = new();
    public long? TenantId { get; set; }
}

public abstract record FormField
{
    public string Key { get; init; } = "";
    public LocalizedString Label { get; init; } = new(new() { ["en"] = "" });
    public bool IsRequired { get; init; }
    public abstract string FieldType { get; }
}

public record TextFormField : FormField { public override string FieldType => "text"; public int? MaxLength { get; init; } }
public record EmailFormField : FormField { public override string FieldType => "email"; }
public record SelectFormField : FormField { public override string FieldType => "select"; public List<LocalizedString> Options { get; init; } = []; }
public record CheckboxFormField : FormField { public override string FieldType => "checkbox"; }
public record FileFormField : FormField { public override string FieldType => "file"; public List<string> AllowedMimeTypes { get; init; } = []; }

public class FormSubmissionSettings
{
    public string? RedirectUrl { get; set; }
    public LocalizedString? SuccessMessage { get; set; }
    public string? NotificationEmail { get; set; }
    public bool StoreSubmissions { get; set; } = true;
}

public class FormSubmission
{
    public long Id { get; set; } = Snowflake.NewId();
    public long FormId { get; set; }
    public Dictionary<string, object?> Data { get; set; } = [];
    public string? SubmittedByIp { get; set; }
    public DateTimeOffset SubmittedAt { get; set; } = DateTimeOffset.UtcNow;
    public long? TenantId { get; set; }
}
```

### 24.2 Form Block

```csharp
public record FormBlock : BlockBase
{
    public override string BlockType => "form";
    public long FormId { get; init; }
}

// FormBlockRenderer fetches the CmsForm and renders fields via Views/Blocks/Form.cshtml
// Form submission POSTs to /cms/forms/{formId}/submit
// Raises FormSubmittedEvent after storing submission
```

---

## 25. Import / Export

```csharp
public class SiteExportService
{
    // Exports entire site (or single tenant) as a JSON archive
    public async Task<Stream> ExportAsync(long? tenantId, CancellationToken ct)
    {
        var export = new SiteExport
        {
            ExportedAt  = DateTimeOffset.UtcNow,
            Version     = "1.0",
            Pages       = await _repo.GetAllAsync(tenantId, ct),
            Media       = await _mediaRepo.GetAllMetaAsync(tenantId, ct),
            Menus       = await _menuRepo.GetAllAsync(tenantId, ct),
            Taxonomy    = await _taxonomyRepo.GetAllAsync(tenantId, ct),
            Redirects   = await _redirectRepo.GetAllAsync(tenantId, ct),
            Forms       = await _formRepo.GetAllAsync(tenantId, ct),
        };
        var json   = JsonSerializer.Serialize(export, _jsonOptions);
        var stream = new MemoryStream(Encoding.UTF8.GetBytes(json));
        return stream;
    }
}

public class SiteImportService
{
    // Imports with conflict strategy: Skip | Overwrite | CreateNew
    public async Task ImportAsync(Stream jsonStream, ImportOptions options, CancellationToken ct) { ... }
}
```

---

## 26. Redirects Manager

Covered in Section 14.2. Admin UI provides:
- Create/edit/delete individual redirect rules
- Bulk CSV import (FromPath, ToPath, StatusCode columns)
- Test tool to preview which rule matches a given path
- Hit count tracking per rule

---

## 27. SEO Module

Shipped as `CmsSeo` module assembly.

**Contributes:**
- `SeoMetaBlock` + renderer
- `SeoValidationHook` (IPageSaveHook, Order = -10): validates meta description length, requires meta block on publish
- `SeoEnrichmentHook` (IPageReadHook, Order = 5): injects structured data, resolves canonical URL
- `SitemapRebuildHandler` (ICmsEventHandler<PagePublishedEvent>): regenerates `/sitemap.xml`
- Admin section: Sitemap viewer, Redirect manager, Settings
- `SchemaOrgBlock`: renders JSON-LD structured data
- Auto-generates `robots.txt` from settings

---

## 28. A/B Testing

```csharp
public record AbTestVariant
{
    public long Id { get; init; } = Snowflake.NewId();
    public string Label { get; init; } = "";          // "Control", "Variant A"
    public int Weight { get; init; } = 50;            // percentage traffic split
    public List<BlockBase> Blocks { get; init; } = [];
}

public record AbTestBlock : BlockBase
{
    public override string BlockType => "ab-test";
    public List<AbTestVariant> Variants { get; init; } = [];
    public string GoalEvent { get; init; } = "";      // analytics event that counts as conversion
    public DateTimeOffset? EndsAt { get; init; }
}

// AbTestBlockRenderer:
//   1. Checks cookie for existing assignment
//   2. If none: assigns variant by weight, sets cookie
//   3. Renders assigned variant's blocks
//   4. Emits impression event
```

---

## 29. Personalisation

```csharp
public record PersonalisedBlock : BlockBase
{
    public override string BlockType => "personalised";
    public List<PersonalisationVariant> Variants { get; init; } = [];
    public BlockBase FallbackBlock { get; init; } = default!;
}

public record PersonalisationVariant
{
    public PersonalisationRule Rule { get; init; } = default!;
    public BlockBase Block { get; init; } = default!;
}

// Rules: GeoRule, CookieRule, QueryParamRule, UserSegmentRule, DeviceTypeRule
public abstract record PersonalisationRule
{
    public abstract bool Evaluate(HttpContext context);
}

public record GeoRule(string CountryCode) : PersonalisationRule
{
    public override bool Evaluate(HttpContext context)
        => context.Connection.RemoteIpAddress?.MapToIPv4().ToString().StartsWith("") ?? false;
        // In practice: use MaxMind or ip-api.com lookup
}
```

---

## 30. Analytics Hooks

The event bus provides natural analytics integration points. No analytics code lives in core — modules attach handlers:

```csharp
// AnalyticsModule registers:
public class PageViewHandler : ICmsEventHandler<PageReadCompletedEvent>
{
    public async Task HandleAsync(PageReadCompletedEvent evt, CancellationToken ct)
    {
        // Fire to Plausible, GA4, or internal analytics store
        await _analyticsClient.TrackPageViewAsync(new PageView
        {
            Url       = evt.Url,
            Referrer  = evt.Referrer,
            Culture   = evt.Culture,
            TenantId  = evt.TenantId,
        });
    }
}

// BlockRenderedEvent carries render duration for performance monitoring
// FormSubmittedEvent feeds conversion tracking
```

---

## 31. Notifications

```csharp
public interface INotificationChannel
{
    string ChannelType { get; }  // "email", "in-app", "slack"
    Task SendAsync(NotificationMessage message, CancellationToken ct);
}

public class NotificationMessage
{
    public string RecipientId { get; set; } = "";
    public string Subject { get; set; } = "";
    public string Body { get; set; } = "";
    public string? ActionUrl { get; set; }
    public string NotificationType { get; set; } = ""; // "review-requested", "page-published"
}

// InAppNotification stored as Marten document, fetched by admin nav bell icon
public class InAppNotification
{
    public long Id { get; set; } = Snowflake.NewId();
    public string UserId { get; set; } = "";
    public string Message { get; set; } = "";
    public string? ActionUrl { get; set; }
    public bool IsRead { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public long? TenantId { get; set; }
}
```

---

## 32. Marten Storage Conventions

| Document Type | Id Type | Notes |
|---|---|---|
| `Page` | `long` | Indexed: Slug, Culture, PageGroupId, TenantId, Status |
| `PageRevision` | `long` | Indexed: PageId, Version |
| `InstalledModuleDocument` | `string` (Name) | One per module |
| `RazorTemplateDocument` | `long` | Indexed: VirtualPath |
| `MediaItem` | `long` | Indexed: TenantId, Folder |
| `NavigationMenu` | `long` | Indexed: Handle, TenantId |
| `RedirectRule` | `long` | Indexed: FromPath, TenantId |
| `CmsForm` | `long` | Indexed: Handle, TenantId |
| `FormSubmission` | `long` | Indexed: FormId, TenantId |
| `TaxonomyVocabulary` | `long` | Indexed: Handle, TenantId |
| `TaxonomyTerm` | `long` | Indexed: VocabularyId, Slug |
| `ContentLockDocument` | `long` (PageId) | TTL via Hangfire cleanup |
| `AuditEntry` | `long` | Indexed: EntityId, PerformedBy, TenantId — append only |
| `InAppNotification` | `long` | Indexed: UserId, IsRead |
| `WebhookSubscription` | `long` | Indexed: TenantId |
| `Tenant` | `long` | Indexed: Host, CustomDomain |
| `CmsUser` | `long` | Indexed: Email, TenantId |
| `CmsRole` | `long` | Indexed: TenantId |
| `*ModuleSettings` | `string` (stable const) | One per module, loaded by module name |

### Marten Configuration

```csharp
builder.Services.AddMarten(options =>
{
    options.Connection(connectionString);
    options.UseSystemTextJsonForSerialization(configure: o =>
    {
        o.Converters.Add(new BlockJsonConverter());
    });

    // Indexes
    options.Schema.For<Page>()
        .Index(p => p.Slug)
        .Index(p => p.Culture)
        .Index(p => p.PageGroupId)
        .Index(p => p.TenantId)
        .Index(p => p.Status);

    options.Schema.For<AuditEntry>().Index(a => a.EntityId);
    options.Schema.For<MediaItem>().Index(m => m.TenantId);
    // ... etc
})
.UseLightweightSessions()
.ApplyAllDatabaseChangesOnStartup();
```

---

## 33. Project Structure

```
/src
  /Cms.Core
    /Blocks
      BlockBase.cs
      BlockJsonConverter.cs
      IBlockRenderer.cs
      BlockRendererRegistry.cs
      BuiltIn/
        HeroBlock.cs + HeroBlockRenderer.cs
        RichTextBlock.cs + RichTextBlockRenderer.cs
        CardGridBlock.cs + CardGridBlockRenderer.cs
        DynamicBlock.cs + DynamicBlockRenderer.cs
        RazorTemplateBlock.cs + RazorTemplateBlockRenderer.cs
        FormBlock.cs + FormBlockRenderer.cs
        AbTestBlock.cs + AbTestBlockRenderer.cs
        PersonalisedBlock.cs + PersonalisedBlockRenderer.cs
    /Pages
      Page.cs
      PageRevision.cs
      LayoutRegion.cs
      ColumnDefinition.cs
      LayoutFactory.cs
      BlogPageFactory.cs
      LandingPageFactory.cs
      PageService.cs
      IPageRepository.cs
    /Pipeline
      PipelineContext.cs (PageReadContext, PageSaveContext, BlockRenderContext)
      IPageReadHook.cs
      IPageSaveHook.cs
      IBlockRenderHook.cs
      CmsPipeline.cs
      CorePageReadHook.cs
      CorePageSaveHook.cs
    /Events
      ICmsEvent.cs
      ICmsEventHandler.cs
      ICmsEventBus.cs
      CmsEventBus.cs
      Events/ (all event records)
    /Modules
      IModule.cs
      IModuleBuilder.cs
      ModuleRegistry.cs
      ModuleLoader.cs
      CmsAssemblyLoadContext.cs
      InstalledModuleDocument.cs
    /Localization
      LocalizedString.cs
      CultureRedirectMiddleware.cs
      CultureCodeRouteConstraint.cs
    /Caching
      PageCacheKeyService.cs
      ETagMiddleware.cs
      CacheInvalidationHook.cs
      TemplateChangeTokenRegistry.cs
    /Rendering
      PageLayoutRenderer.cs
      RegionRenderer.cs
      ColumnRenderer.cs
      PartialViewRenderer.cs
      DatabaseTemplateFileProvider.cs
      InMemoryFileInfo.cs
    /Media
      MediaItem.cs
      MediaFolder.cs
      IMediaStorage.cs
      ImageProcessingService.cs
    /Search
      PageSearchDocument.cs
      SearchIndexHandler.cs
    /Workflow
      ContentWorkflowService.cs
      PreviewTokenService.cs
      ContentLockDocument.cs
      ScheduledPublishJob.cs
    /Forms
      CmsForm.cs
      FormSubmission.cs
      FormSubmissionService.cs
    /Navigation
      NavigationMenu.cs
      NavigationItem.cs
      RedirectRule.cs
      RedirectMiddleware.cs
    /Taxonomy
      TaxonomyVocabulary.cs
      TaxonomyTerm.cs
    /Users
      CmsUser.cs
      CmsRole.cs
      PermissionService.cs
    /Tenancy
      Tenant.cs
      TenantMiddleware.cs
    /Audit
      AuditEntry.cs
      AuditLogHook.cs
    /Notifications
      INotificationChannel.cs
      NotificationMessage.cs
      InAppNotification.cs
    /Webhooks
      WebhookSubscription.cs
      WebhookDispatcher.cs
    /ImportExport
      SiteExportService.cs
      SiteImportService.cs

  /Cms.Infrastructure
    /Marten
      MartenPageRepository.cs
      MartenMediaRepository.cs
      MartenTemplateRepository.cs
      MartenConfiguration.cs
    /Storage
      LocalDiskStorage.cs
      AzureBlobStorage.cs
    /Search
      MeilisearchClient.cs
    /Jobs
      HangfireConfiguration.cs
      ScheduledPublishJob.cs
      RecycleBinPurgeJob.cs
      ContentLockCleanupJob.cs

  /Cms.Web
    /Controllers
      PageController.cs
      MediaController.cs
      FormsController.cs
      PreviewController.cs
    /Areas
      /Admin
        /Controllers
          DashboardController.cs
          PagesAdminController.cs
          MediaAdminController.cs
          NavigationAdminController.cs
          ModulesAdminController.cs
          UsersAdminController.cs
          TenantsAdminController.cs
          AuditAdminController.cs
        /Views/...
    /Views
      /Blocks
        /Custom/        ← RazorTemplateBlock cshtml files
        Hero.cshtml
        RichText.cshtml
        CardGrid.cshtml
        Form.cshtml
      /Shared
        /Components
          /AdminNav/Default.cshtml
          /BlockZone/Default.cshtml
    /TagHelpers
      BlockZoneTagHelper.cs
      BlockAssetTagHelper.cs
    /ViewComponents
      AdminNavViewComponent.cs
      LatestPostsViewComponent.cs
      BlogListViewComponent.cs
    Program.cs

  /Cms.Modules.Seo        ← Example module as separate project/assembly
    SeoModule.cs
    /Blocks/...
    /Hooks/...
    /Controllers/...
    /Views/...

  /Cms.Modules.Search
  /Cms.Modules.Analytics
  /Cms.Modules.Webhooks
```

---

## 34. Agent Swarm Implementation Notes

### Suggested Agent Breakdown

| Agent | Responsibility |
|---|---|
| **CoreDomainAgent** | Block abstractions, Page model, LayoutFactory, LocalizedString, all domain records |
| **PipelineAgent** | PipelineContext hierarchy, all hook interfaces, CmsPipeline executor, PageService |
| **RenderingAgent** | PageLayoutRenderer, RegionRenderer, ColumnRenderer, PartialViewRenderer, all built-in block renderers |
| **StorageAgent** | Marten configuration, all repository implementations, indexes, BlockJsonConverter |
| **RazorRuntimeAgent** | DatabaseTemplateFileProvider, InMemoryFileInfo, TemplateChangeTokenRegistry, TemplateRepository |
| **CachingAgent** | OutputCache setup, ETagMiddleware, CacheInvalidationHook, PageCacheKeyService, BlockPageCachePolicy |
| **LocalizationAgent** | RequestLocalization setup, CultureRouteConstraint, CultureRedirectMiddleware, hreflang rendering, RTL support |
| **ModuleSystemAgent** | IModule, IModuleBuilder, ModuleRegistry, ModuleLoader, CmsAssemblyLoadContext, ModuleBootstrapService, AdminMenuRegistry |
| **AdminShellAgent** | Admin layout, AdminNavViewComponent, all admin controllers, area routing, EmbeddedFileProvider wiring |
| **WorkflowAgent** | ContentWorkflowService, status state machine, PreviewTokenService, ContentLockDocument, ScheduledPublishJob |
| **MediaAgent** | MediaItem model, IMediaStorage, ImageProcessingService, ImageSharp integration, admin media library UI |
| **SearchAgent** | PageSearchDocument, SearchIndexHandler, Meilisearch integration, search API endpoint |
| **FormsAgent** | CmsForm model, FormField hierarchy, FormSubmission, FormBlockRenderer, submission endpoint |
| **TaxonomyAgent** | TaxonomyVocabulary, TaxonomyTerm, term archive pages, block filtering by term |
| **UsersAgent** | CmsUser, CmsRole, PermissionService, admin user management UI, policy registration |
| **TenancyAgent** | Tenant model, TenantMiddleware, tenant-scoped Marten sessions, admin tenant management |
| **AuditAgent** | AuditEntry, AuditLogHook, audit admin UI with filtering |
| **NotificationsAgent** | NotificationMessage, INotificationChannel, InAppNotification, email channel via SMTP |
| **WebhooksAgent** | WebhookSubscription, WebhookDispatcher, delivery log, HMAC signing, admin UI |
| **ImportExportAgent** | SiteExportService, SiteImportService, admin UI for backup/restore |
| **SeoModuleAgent** | Full CmsSeo module: SeoMetaBlock, sitemap, SchemaOrgBlock, robots.txt, admin section |
| **EventBusAgent** | CmsEventBus, all event records, handler registration wiring |
| **AbTestingAgent** | AbTestBlock, variant assignment, impression/conversion tracking |
| **PersonalisationAgent** | PersonalisedBlock, PersonalisationRule hierarchy, GeoRule, CookieRule, etc. |
| **HeadlessApiAgent** | PagesApiController, all API response DTOs, API versioning, auth |
| **NavigationAgent** | NavigationMenu, NavigationItem, NavigationTarget hierarchy, RedirectRule, RedirectMiddleware |
| **RecycleBinAgent** | Soft-delete patterns, RecycleBinService, purge Hangfire job, admin restore UI |

### Implementation Order

```
Phase 1 — Foundation
  CoreDomainAgent → StorageAgent → PipelineAgent → RenderingAgent → EventBusAgent

Phase 2 — Core CMS
  RazorRuntimeAgent → CachingAgent → LocalizationAgent → WorkflowAgent → ModuleSystemAgent

Phase 3 — Admin
  AdminShellAgent → UsersAgent → AuditAgent → NavigationAgent → MediaAgent

Phase 4 — Content Features
  TaxonomyAgent → FormsAgent → SearchAgent → RecycleBinAgent → ImportExportAgent

Phase 5 — Platform
  TenancyAgent → WebhooksAgent → NotificationsAgent → HeadlessApiAgent

Phase 6 — Modules & Enhancements
  SeoModuleAgent → AbTestingAgent → PersonalisationAgent → AnalyticsAgent
```

### Inter-Agent Contracts

- All agents must consume `IPageRepository`, `ICmsEventBus`, `IBlockRendererRegistry` via DI — never concrete types
- Block types added by modules register via `IModuleBuilder.AddBlock<>()` — never by directly modifying `BlockRendererRegistry`
- Pipeline hooks use `Order` convention: negative = before core, zero = core, positive = after core
- All Marten documents include `TenantId? TenantId` for multi-tenancy readiness from day one
- Events are immutable records — handlers never mutate event data
- Admin controllers live in `Area = "ModuleName"` — the shell never references them directly
- `LocalizedString` is used for all user-facing content fields — never raw `string` for authored content

### Key Invariants

1. Adding a new block type = new record + new renderer + DI registration. Nothing else changes.
2. Adding a new module = new assembly implementing `IModule`. No host app changes.
3. All page mutations flow through `PageService` — never direct repository writes outside of it.
4. The rendering pipeline is stateless — `ViewContext` flows through but no shared mutable state.
5. Marten documents are append-friendly — `PageRevision` is the audit trail for content; `AuditEntry` for actions.
6. Culture is always explicit — no ambient `Thread.CurrentCulture` assumptions in domain logic.
