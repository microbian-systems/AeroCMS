# Marten + AOT + Source Generators: Compatibility Guide for AeroCMS

## Executive Summary

**Marten is fully compatible with Native AOT and source generators**, but requires intentional configuration to avoid reflection-based patterns. This document outlines the strategy for using Roslyn source generators to eliminate runtime reflection in AeroCMS's Marten integration, enabling Native AOT deployment.

## Why Source Generators for a Block-Based CMS

### The Problem with Traditional CMS Architecture

Block/component-based CMSs are traditionally reflection-heavy:

1. **Dynamic block discovery and registration** - Assembly scanning for `BlockBase` descendants
2. **Polymorphic deserialization** - JSON polymorphism via reflection for block data from database
3. **Runtime dispatching** - `MethodInfo.Invoke()` to route blocks to renderers/ViewComponents
4. **Pipeline hook execution** - Assembly scanning and dynamic sorting of `IPageReadHook`/`IPageSaveHook`

When rendering a single page with 20-50 blocks across thousands of requests, this reflection overhead compounds significantly.

### The Source Generator Solution

Moving these operations to compile-time provides:

#### 1. Zero Runtime Reflection
Instead of reflection-based lookups for each block's renderer or polymorphic JSON deserialization, the cost moves to the build step. For a page with 50 blocks, this eliminates 50+ reflection operations per request.

#### 2. Native AOT Compatibility
.NET 10+ is moving heavily toward Ahead-of-Time compilation. Native AOT does not support:
- `Assembly.GetTypes()` scanning
- `Activator.CreateInstance()`
- Reflection-based JSON polymorphism
- Dynamic `MethodInfo.Invoke()` dispatching

Source generators ensure the CMS compiles into a single, high-performance binary with minimal footprint.

#### 3. Instant Startup (Fast Shell Rebuilds)
In a multi-tenant CMS, a "Tenant Shell" might reload when a module is enabled or settings change. By eliminating startup assembly scanning, shell rebuilds become nearly instantaneous.

```csharp
// Traditional approach: shell rebuild = full assembly scan (100-500ms)
foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
{
    foreach (var type in assembly.GetTypes().Where(t => typeof(BlockBase).IsAssignableFrom(t)))
    {
        services.AddTransient(type);
    }
}

// Source generator approach: shell rebuild = pre-generated code execution (<10ms)
services.AddCmsBlocks(); // Generated method with explicit registrations
```

#### 4. Compile-Time Validation
If a block is missing a required renderer or a pipeline has a circular dependency, the compiler fails the build rather than the site crashing at runtime when an administrator publishes a page.

**Build-time errors:**
- Missing renderer for a block → Build fails
- Circular dependency in pipeline hooks → Build fails
- Invalid Marten document mapping → Build fails

### Architecture Components

The platform uses Roslyn Incremental Source Generators (targeting .NET 10+) for:

1. **Block Serialization** (`CmsBlockSourceGenerator`)
   - Scans for all `BlockBase` subclasses
   - Generates `[JsonPolymorphic]` and `[JsonDerivedType]` attributes
   - Generates `JsonSerializerContext` for source-generated JSON serialization

2. **Discovery & Registry** (`CmsModuleSourceGenerator`)
   - Builds `IServiceCollection.AddCmsBlocks()` extension method
   - Explicitly registers every discovered type in DI
   - Eliminates assembly scanning

3. **Pipeline Hooks** (`CmsPipelineSourceGenerator`)
   - Generates deterministic, pre-ordered execution chain
   - Replaces runtime sorting of `IPageReadHook`/`IPageSaveHook` implementations

4. **Database Mapping** (`CmsMartenDocumentGenerator`)
   - Generates Marten document mappings at compile time
   - Skips expensive "startup scan" for schema building

5. **ViewComponent Dispatching**
   - Eliminates `MethodInfo.Invoke()` when rendering dynamic blocks

### Why This is Perfect for Multi-Tenant CMS

When a tenant enables a new module in the shell architecture, traditional CMS platforms re-scan assemblies. With source generators, you're activating pre-generated code—a game-changing performance improvement for multi-tenant scenarios.

---

## Marten's AOT Compatibility

### Good News: Marten Actively Supports AOT

The Marten team has been working toward AOT compatibility since .NET 7/8, making strategic decisions to support it.

### ✅ What Works with AOT

#### 1. System.Text.Json (STJ) Source Generation

Marten supports STJ for document serialization. You can provide a pre-generated `JsonSerializerContext`:

```csharp
var store = DocumentStore.For(opts =>
{
    opts.Connection(connectionString);
    opts.UseSystemTextJsonForSerialization(serializerOptions =>
    {
        serializerOptions.TypeInfoResolver = CmsJsonContext.Default;
    });
});
```

#### 2. Explicit Document Mapping (No Runtime Scanning)

```csharp
// ✅ AOT-friendly: explicit registration
opts.RegisterDocumentType<Page>();
opts.RegisterDocumentType<Block>();
opts.RegisterDocumentType<Site>();
opts.RegisterDocumentType<MediaAsset>();

// ❌ AOT-hostile: assembly scanning (avoid this)
opts.RegisterDocumentTypes(Assembly.GetExecutingAssembly());
```

#### 3. Compiled Queries

Marten's compiled query feature is already code-gen based and works perfectly with AOT:

```csharp
public class PageBySlugQuery : ICompiledQuery<Page, Page?>
{
    public string Slug { get; set; }
    public Guid SiteId { get; set; }
    
    public Expression<Func<IQueryable<Page>, Page?>> QueryIs()
    {
        return q => q.FirstOrDefault(x => 
            x.Slug == Slug && 
            x.SiteId == SiteId &&
            x.IsPublished);
    }
}

// Usage:
var page = await session.QueryAsync(new PageBySlugQuery 
{ 
    Slug = "about-us", 
    SiteId = currentSite.Id 
});
```

### ⚠️ What Needs Careful Handling

#### 1. Dynamic LINQ Queries

```csharp
// ❌ AOT-hostile: dynamic expression compilation
session.Query<Page>()
    .Where(x => x.Title.Contains(searchTerm))
    
// ✅ AOT-friendly: compiled queries
session.Query(new PagesByTitleQuery(searchTerm))
```

#### 2. Event Sourcing Projection Runtime Compilation

- Marten's event projections traditionally use runtime compilation
- Need to use source-generated projections for AOT
- This is a known limitation the team is addressing

#### 3. Schema Auto-Generation

```csharp
// ❌ AOT-hostile: uses reflection
StoreOptions.AutoCreateSchemaObjects = AutoCreate.All

// ✅ AOT-friendly: explicit schema management or generated DDL scripts
```

---

## AeroCMS Source Generator Strategy for Marten

### 1. CmsMartenDocumentGenerator

This generator emits explicit Marten configuration code at compile time.

**Generated Output Example:**

```csharp
// Generated by CmsMartenDocumentGenerator
namespace Aero.Cms.Generated;

public static class GeneratedMartenConfiguration
{
    public static void ConfigureCmsDocuments(this StoreOptions options)
    {
        // Explicit document registrations (AOT-safe)
        options.RegisterDocumentType<Page>();
        options.RegisterDocumentType<Site>();
        options.RegisterDocumentType<MediaAsset>();
        options.RegisterDocumentType<Module>();
        options.RegisterDocumentType<User>();
        
        // Document-specific mappings
        options.Schema.For<Page>()
            .Identity(x => x.Id)
            .Index(x => x.Slug)
            .Index(x => x.SiteId)
            .Index(x => x.PublishedDate)
            .UseOptimisticConcurrency(true);
            
        options.Schema.For<Site>()
            .Identity(x => x.Id)
            .Index(x => x.Domain)
            .UniqueIndex(x => x.Domain);
            
        options.Schema.For<MediaAsset>()
            .Identity(x => x.Id)
            .Index(x => x.SiteId)
            .Index(x => x.ContentType);
            
        // Use pre-generated JsonSerializerContext
        options.UseSystemTextJsonForSerialization(opts =>
        {
            opts.TypeInfoResolver = CmsJsonContext.Default;
        });
    }
}
```

**Generator Implementation Skeleton:**

```csharp
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

[Generator]
public class CmsMartenDocumentGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        // Find all types inheriting from CmsDocument base class
        var documentTypes = context.SyntaxProvider
            .CreateSyntaxProvider(
                predicate: static (node, _) => node is ClassDeclarationSyntax,
                transform: static (ctx, _) => GetSemanticTargetForGeneration(ctx))
            .Where(static m => m is not null)
            .Collect();
            
        context.RegisterSourceOutput(documentTypes, (spc, types) =>
        {
            GenerateMartenConfiguration(spc, types!);
        });
    }
    
    private static ClassDeclarationSyntax? GetSemanticTargetForGeneration(
        GeneratorSyntaxContext context)
    {
        var classDeclaration = (ClassDeclarationSyntax)context.Node;
        
        // Check if class inherits from CmsDocument or has [CmsDocument] attribute
        var symbol = context.SemanticModel.GetDeclaredSymbol(classDeclaration);
        if (symbol is null) return null;
        
        // Check for CmsDocument base class or marker attribute
        if (InheritsFromCmsDocument(symbol))
        {
            return classDeclaration;
        }
        
        return null;
    }
    
    private static bool InheritsFromCmsDocument(INamedTypeSymbol symbol)
    {
        var current = symbol.BaseType;
        while (current != null)
        {
            if (current.Name == "CmsDocument" || 
                current.Name == "CmsDocumentBase")
            {
                return true;
            }
            current = current.BaseType;
        }
        return false;
    }
    
    private static void GenerateMartenConfiguration(
        SourceProductionContext context, 
        IEnumerable<ClassDeclarationSyntax> documentTypes)
    {
        var sb = new StringBuilder();
        
        sb.AppendLine("// <auto-generated />");
        sb.AppendLine("using Marten;");
        sb.AppendLine("using Aero.Cms.Core.Documents;");
        sb.AppendLine();
        sb.AppendLine("namespace Aero.Cms.Generated;");
        sb.AppendLine();
        sb.AppendLine("public static class GeneratedMartenConfiguration");
        sb.AppendLine("{");
        sb.AppendLine("    public static void ConfigureCmsDocuments(this StoreOptions options)");
        sb.AppendLine("    {");
        
        // Generate RegisterDocumentType calls
        foreach (var docType in documentTypes)
        {
            var typeName = docType.Identifier.Text;
            sb.AppendLine($"        options.RegisterDocumentType<{typeName}>();");
        }
        
        sb.AppendLine();
        
        // Generate schema mappings (analyze attributes for indexes, etc.)
        foreach (var docType in documentTypes)
        {
            GenerateSchemaMapping(sb, docType);
        }
        
        sb.AppendLine();
        sb.AppendLine("        // Use pre-generated JsonSerializerContext");
        sb.AppendLine("        options.UseSystemTextJsonForSerialization(opts =>");
        sb.AppendLine("        {");
        sb.AppendLine("            opts.TypeInfoResolver = CmsJsonContext.Default;");
        sb.AppendLine("        });");
        
        sb.AppendLine("    }");
        sb.AppendLine("}");
        
        context.AddSource("GeneratedMartenConfiguration.g.cs", sb.ToString());
    }
    
    private static void GenerateSchemaMapping(
        StringBuilder sb, 
        ClassDeclarationSyntax docType)
    {
        var typeName = docType.Identifier.Text;
        
        sb.AppendLine($"        options.Schema.For<{typeName}>()");
        sb.AppendLine($"            .Identity(x => x.Id)");
        
        // Analyze attributes for [Index], [UniqueIndex], etc.
        // This would scan for custom attributes like:
        // [CmsIndex(nameof(Slug))]
        // [CmsIndex(nameof(SiteId))]
        
        sb.AppendLine("            .UseOptimisticConcurrency(true);");
        sb.AppendLine();
    }
}
```

### 2. Block Polymorphism Strategy

This is where `CmsBlockSourceGenerator` + Marten integration shines.

**Domain Model:**

```csharp
// Your blocks stored in Page.Blocks as List<BlockBase>
public class Page
{
    public Guid Id { get; set; }
    public string Slug { get; set; }
    public Guid SiteId { get; set; }
    public bool IsPublished { get; set; }
    public DateTime? PublishedDate { get; set; }
    public List<BlockBase> Blocks { get; set; } = new();
}

// Base class for all blocks
public abstract class BlockBase 
{
    public Guid Id { get; set; }
    public string BlockType { get; set; }
    public int Order { get; set; }
}

// Concrete block types
public class TextBlock : BlockBase
{
    public string Content { get; set; }
}

public class ImageBlock : BlockBase
{
    public string ImageUrl { get; set; }
    public string AltText { get; set; }
}

public class HeroBlock : BlockBase
{
    public string Title { get; set; }
    public string Subtitle { get; set; }
    public string BackgroundImageUrl { get; set; }
}
```

**CmsBlockSourceGenerator Output:**

```csharp
// Generated by CmsBlockSourceGenerator
using System.Text.Json.Serialization;

[JsonPolymorphic(TypeDiscriminatorPropertyName = "$type")]
[JsonDerivedType(typeof(TextBlock), "text")]
[JsonDerivedType(typeof(ImageBlock), "image")]
[JsonDerivedType(typeof(HeroBlock), "hero")]
public abstract partial class BlockBase 
{
    // Original class definition remains
}
```

**JsonSerializerContext Generation:**

```csharp
// Generated by CmsBlockSourceGenerator
using System.Text.Json.Serialization;

[JsonSerializable(typeof(Page))]
[JsonSerializable(typeof(Site))]
[JsonSerializable(typeof(MediaAsset))]
[JsonSerializable(typeof(List<BlockBase>))]
[JsonSerializable(typeof(TextBlock))]
[JsonSerializable(typeof(ImageBlock))]
[JsonSerializable(typeof(HeroBlock))]
[JsonSourceGenerationOptions(
    WriteIndented = false,
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
internal partial class CmsJsonContext : JsonSerializerContext 
{
}
```

**Result:** Marten serializes polymorphic blocks **without any reflection** because STJ source generation handles polymorphism at compile time.

### 3. Compiled Query Pattern for Headless API

For your headless API and rendering pipeline, use generated compiled queries:

```csharp
// Generated or hand-written compiled queries (AOT-safe)
public class PageBySlugQuery : ICompiledQuery<Page, Page?>
{
    public string Slug { get; set; }
    public Guid SiteId { get; set; }
    
    public Expression<Func<IQueryable<Page>, Page?>> QueryIs()
    {
        return q => q.FirstOrDefault(x => 
            x.Slug == Slug && 
            x.SiteId == SiteId &&
            x.IsPublished);
    }
}

public class PagesByTagQuery : ICompiledQuery<Page, IEnumerable<Page>>
{
    public string Tag { get; set; }
    public Guid SiteId { get; set; }
    public int PageSize { get; set; } = 20;
    
    public Expression<Func<IQueryable<Page>, IEnumerable<Page>>> QueryIs()
    {
        return q => q
            .Where(x => x.SiteId == SiteId && x.Tags.Contains(Tag))
            .OrderByDescending(x => x.PublishedDate)
            .Take(PageSize);
    }
}

// Usage in your rendering pipeline:
var page = await session.QueryAsync(new PageBySlugQuery 
{ 
    Slug = routeData.Slug, 
    SiteId = currentSite.Id 
});
```

---

## Known Marten + AOT Gotchas

### 1. Lazy Loading / Include() Operations

```csharp
// ❌ Reflection-based (AOT-hostile):
var page = session.Query<Page>()
    .Include(x => x.AuthorId, out Author author)
    .FirstOrDefault();

// ✅ Explicit loading (AOT-friendly):
var page = session.Query<Page>().FirstOrDefault();
if (page != null)
{
    var author = session.Load<Author>(page.AuthorId);
}
```

### 2. Multi-Tenancy via Database-per-Tenant

Your tenant shell architecture should work fine, but avoid dynamic tenant resolution:

```csharp
// ❌ Dynamic tenant resolution at runtime (uses reflection)
var tenantId = httpContext.GetTenantId();
var session = store.LightweightSession(tenantId);

// ✅ Pre-configured tenant stores
var session = tenantStoreRegistry.GetSession(tenantId);
```

**Better approach for multi-tenant shells:**

```csharp
public interface ITenantStoreRegistry
{
    IDocumentStore GetStore(Guid tenantId);
    IDocumentSession GetSession(Guid tenantId);
}

public class TenantStoreRegistry : ITenantStoreRegistry
{
    private readonly ConcurrentDictionary<Guid, IDocumentStore> _stores = new();
    private readonly Func<Guid, IDocumentStore> _storeFactory;
    
    public TenantStoreRegistry(Func<Guid, IDocumentStore> storeFactory)
    {
        _storeFactory = storeFactory;
    }
    
    public IDocumentStore GetStore(Guid tenantId)
    {
        return _stores.GetOrAdd(tenantId, _storeFactory);
    }
    
    public IDocumentSession GetSession(Guid tenantId)
    {
        return GetStore(tenantId).LightweightSession();
    }
}
```

### 3. Custom ISerializer Implementations

```csharp
// ❌ AOT-hostile: custom serializer with reflection
opts.Serializer<CustomJsonNetSerializer>();

// ✅ AOT-friendly: STJ source generation
opts.UseSystemTextJsonForSerialization();
```

---

## Testing AOT Compatibility

Create a dedicated AOT test project to validate compatibility early:

**Project File:**

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net10.0</TargetFramework>
    <PublishAot>true</PublishAot>
    <InvariantGlobalization>false</InvariantGlobalization>
  </PropertyGroup>
  
  <ItemGroup>
    <PackageReference Include="Marten" Version="7.*" />
    <ProjectReference Include="..\Aero.Cms.Core\Aero.Cms.Core.csproj" />
    <ProjectReference Include="..\Aero.Cms.Generated\Aero.Cms.Generated.csproj" />
  </ItemGroup>
</Project>
```

**Program.cs - AOT Smoke Test:**

```csharp
using Aero.Cms.Core.Documents;
using Aero.Cms.Generated;
using Marten;

// AOT smoke test for Marten integration
var store = DocumentStore.For(opts =>
{
    opts.Connection("Host=localhost;Database=aerocms_aot_test;Username=postgres;Password=postgres");
    opts.ConfigureCmsDocuments(); // Your generated configuration
});

await using var session = store.LightweightSession();

// Test: Query a page
var page = await session.QueryAsync(new PageBySlugQuery 
{ 
    Slug = "test-page",
    SiteId = Guid.NewGuid()
});

Console.WriteLine(page?.Title ?? "Not found");

// Test: Save a page with polymorphic blocks
var newPage = new Page
{
    Id = Guid.NewGuid(),
    Slug = "aot-test",
    SiteId = Guid.NewGuid(),
    Blocks = new List<BlockBase>
    {
        new TextBlock { Id = Guid.NewGuid(), Content = "Hello from AOT!" },
        new ImageBlock { Id = Guid.NewGuid(), ImageUrl = "/test.jpg", AltText = "Test" }
    }
};

session.Store(newPage);
await session.SaveChangesAsync();

Console.WriteLine("AOT test completed successfully!");
```

**Run the test:**

```bash
cd tests/Aero.Cms.AotTest
dotnet publish -c Release

# If it compiles to native AOT without warnings, you're good
# The executable will be in bin/Release/net10.0/linux-x64/publish/ (or your platform)
./bin/Release/net10.0/linux-x64/publish/Aero.Cms.AotTest
```

**Common AOT warnings to watch for:**

- `IL2026`: Members annotated with RequiresUnreferencedCodeAttribute may break when trimming
- `IL2087`: Target parameter may have annotations that are not compatible with the flow of type
- `IL3050`: Using member which has RequiresDynamicCodeAttribute can break functionality when AOT compiling

If you see these warnings related to Marten operations, it indicates a reflection path that needs to be replaced with source-generated code.

---

## Integration with Tenant Shell Architecture

Your multi-tenant shell architecture benefits massively from this approach:

### Traditional Shell Reload (Reflection-Based)

```csharp
// When a tenant enables a module:
// 1. Assembly scan for new block types (50-200ms)
// 2. Assembly scan for renderers (50-150ms)
// 3. Assembly scan for pipeline hooks (30-100ms)
// 4. Marten schema introspection (100-300ms)
// Total: 230-750ms per shell reload
```

### Source-Generated Shell Reload

```csharp
// When a tenant enables a module:
// 1. Execute pre-generated AddCmsBlocks() (<5ms)
// 2. Execute pre-generated pipeline chain (<5ms)
// 3. Execute pre-generated Marten config (<10ms)
// Total: <20ms per shell reload

public class TenantShell
{
    private readonly IServiceProvider _services;
    
    public void Reload()
    {
        var services = new ServiceCollection();
        
        // All generated - zero reflection
        services.AddCmsBlocks();           // Generated by CmsModuleSourceGenerator
        services.AddCmsPipeline();         // Generated by CmsPipelineSourceGenerator
        services.AddSingleton(sp =>        
        {
            return DocumentStore.For(opts =>
            {
                opts.Connection(GetConnectionString());
                opts.ConfigureCmsDocuments(); // Generated by CmsMartenDocumentGenerator
            });
        });
        
        _services = services.BuildServiceProvider();
    }
}
```

---

## Deployment Strategy: Split Publish Model

Based on your architecture, use a split publish strategy:

### Cms.Api (Headless/Admin API)
```xml
<PropertyGroup>
  <PublishAot>true</PublishAot>
  <InvariantGlobalization>false</InvariantGlobalization>
</PropertyGroup>
```

**Benefits:**
- Minimal container size (~15-30MB vs 200MB+)
- Instant cold start (<100ms)
- Lower memory footprint
- Perfect for serverless/container density

### Cms.Web (Razor Pages Rendering)
```xml
<PropertyGroup>
  <PublishReadyToRun>true</PublishReadyToRun>
</PropertyGroup>
```

**Why not AOT:**
- Razor runtime compilation from `DatabaseTemplateFileProvider`
- May need dynamic view compilation
- ReadyToRun provides 80% of the startup benefit

---

## Checklist: AOT-Ready Marten Integration

Use this checklist to validate your implementation:

- [ ] All document types explicitly registered (no `Assembly.GetTypes()`)
- [ ] Using STJ source generation with `JsonSerializerContext`
- [ ] Block polymorphism handled via `[JsonPolymorphic]` + source generation
- [ ] All queries use compiled query pattern (`ICompiledQuery<T, TResult>`)
- [ ] No dynamic LINQ queries (`Where(x => x.Property.Contains(...))`)
- [ ] Schema generation is explicit or script-based (no `AutoCreate.All`)
- [ ] No custom `ISerializer` implementations (STJ only)
- [ ] Tenant stores pre-configured (no runtime tenant resolution)
- [ ] No reflection-based includes or lazy loading
- [ ] AOT test project builds without warnings
- [ ] `CmsMartenDocumentGenerator` emits complete configuration
- [ ] `CmsBlockSourceGenerator` generates `[JsonDerivedType]` attributes
- [ ] `CmsJsonContext` includes all document and block types

---

## Compatibility Matrix

| Feature | AOT Compatible | Notes |
|---------|----------------|-------|
| Document storage/retrieval | ✅ Yes | With explicit registration |
| Compiled queries | ✅ Yes | Preferred query method |
| Dynamic LINQ queries | ❌ No | Use compiled queries instead |
| STJ serialization | ✅ Yes | With source generation |
| Newtonsoft.Json | ❌ No | Use STJ instead |
| Polymorphic documents | ✅ Yes | With `[JsonPolymorphic]` + source gen |
| Event sourcing | ⚠️ Partial | Projections need source generation |
| Multi-tenancy (DB-per-tenant) | ✅ Yes | With pre-configured stores |
| Schema auto-creation | ❌ No | Use explicit schema management |
| Optimistic concurrency | ✅ Yes | Works with explicit mapping |
| Indexes | ✅ Yes | With explicit configuration |
| Full-text search | ✅ Yes | With compiled queries |
| Lazy loading / Include | ❌ No | Use explicit loads instead |

---

## Summary

**Marten + AOT + Source Generators = Fully Compatible** ✅

**Requirements:**
1. Use STJ source generation (not Newtonsoft.Json)
2. Avoid dynamic LINQ queries (use compiled queries)
3. Register documents explicitly (no assembly scanning)
4. Handle schema generation externally or via generated scripts
5. Use explicit includes/loads (not reflection-based lazy loading)

Your architecture with source generators (`CmsBlockSourceGenerator`, `CmsMartenDocumentGenerator`, `CmsPipelineSourceGenerator`) is **exactly the right approach** for building a modern, high-performance, AOT-compatible CMS.

The Marten team is actively supporting the AOT ecosystem. Stay on recent versions (7.x+) and test your AOT builds early and often.

For questions or issues, the Marten team is responsive on their GitHub repository and has extensive documentation on AOT compatibility in their official docs.