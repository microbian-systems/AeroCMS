# CMS Source Generator System — Implementation Specification

> **Purpose:** Complete specification for the source generator subsystem of the block-based CMS.
> Designed for agent swarm implementation. Each generator is a discrete, independently implementable unit.
> All generators target C# 12 / .NET 8+ / Roslyn incremental generator API.

---

## Table of Contents

1. [Overview & Goals](#1-overview--goals)
2. [Project Structure](#2-project-structure)
3. [Shared Generator Infrastructure](#3-shared-generator-infrastructure)
4. [Generator 1 — CmsBlockSourceGenerator](#4-generator-1--cmsblocksourcegenerator)
5. [Generator 2 — CmsPipelineSourceGenerator](#5-generator-2--cmspipelinesourcegenerator)
6. [Generator 3 — CmsViewComponentSourceGenerator](#6-generator-3--cmsviewcomponentsourcegenerator)
7. [Generator 4 — CmsModuleSourceGenerator](#7-generator-4--cmsmodulesourcegenerator)
8. [Generator 5 — CmsMartenDocumentGenerator](#8-generator-5--cmsmartenDocumentgenerator)
9. [Generator 6 — CmsEventHandlerSourceGenerator](#9-generator-6--cmseventhandlersourcegenerator)
10. [Generator 7 — CmsAdminSectionSourceGenerator](#10-generator-7--cmsadminsectionsourcegenerator)
11. [Integration — Program.cs Generated Extensions](#11-integration--programcs-generated-extensions)
12. [Testing Source Generators](#12-testing-source-generators)
13. [Diagnostics & Errors](#13-diagnostics--errors)
14. [AOT Compatibility Matrix](#14-aot-compatibility-matrix)
15. [Agent Implementation Notes](#15-agent-implementation-notes)

---

## 1. Overview & Goals

### Problem

The CMS architecture uses several runtime patterns that rely on reflection:

| Pattern | Reflection Usage |
|---|---|
| Block JSON polymorphism | `JsonConverter` reads `blockType` string, calls `Deserialize<T>` via type map |
| Block renderer discovery | DI scans `IEnumerable<IBlockRenderer>` registrations at startup |
| Pipeline hook ordering | `IEnumerable<IPageReadHook>` sorted by `hook.Order` at runtime |
| ViewComponent dispatch | `IViewComponentHelper.InvokeAsync(string name, object args)` uses `MethodInfo.Invoke` |
| Module wiring | `assembly.GetTypes()` + `Activator.CreateInstance` |
| Marten document mapping | Runtime reflection scan of document POCOs |
| Event handler routing | `IEnumerable<ICmsEventHandler<TEvent>>` DI scan |

### Goal

Replace every pattern above with **compile-time generated code** using Roslyn incremental source generators so that:

1. Zero reflection in domain/infrastructure layer at runtime
2. Adding a new block type, hook, ViewComponent, or module requires **no manual wiring** — only the type definition
3. Missing registrations become **compile errors**, not runtime `InvalidOperationException`
4. The generated code is **fully AOT-compatible** (except Razor/MVC framework internals which are outside our control)
5. Generated files are committed to source control as `.g.cs` files for debuggability

### Non-Goals

- Replacing MVC routing reflection (framework internal, not our code)
- Making `RazorTemplateBlock` runtime compilation AOT-safe (fundamentally incompatible — isolated behind a build flag)
- Generating Razor view code (Razor itself handles this)

---

## 2. Project Structure

```
/src
  /Cms.SourceGenerators                    ← Generator assembly (netstandard2.0)
    /Generators
      CmsBlockSourceGenerator.cs
      CmsPipelineSourceGenerator.cs
      CmsViewComponentSourceGenerator.cs
      CmsModuleSourceGenerator.cs
      CmsMartenDocumentGenerator.cs
      CmsEventHandlerSourceGenerator.cs
      CmsAdminSectionSourceGenerator.cs
    /Models
      BlockTypeInfo.cs
      RendererInfo.cs
      HookInfo.cs
      ViewComponentInfo.cs
      ModuleInfo.cs
      EventHandlerInfo.cs
      AdminSectionInfo.cs
    /Helpers
      RoslynExtensions.cs
      SourceWriterExtensions.cs
      SymbolHelpers.cs
    /Diagnostics
      CmsDiagnostics.cs
    Cms.SourceGenerators.csproj

  /Cms.Core                                ← References Cms.SourceGenerators
    ...existing code...

  /Cms.Core.Tests.Generators               ← Generator unit tests
    BlockGeneratorTests.cs
    PipelineGeneratorTests.cs
    ViewComponentGeneratorTests.cs
    ModuleGeneratorTests.cs
    EventHandlerGeneratorTests.cs
    Cms.Core.Tests.Generators.csproj
```

### Generator Project File

```xml
<!-- Cms.SourceGenerators.csproj -->
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>netstandard2.0</TargetFramework>
    <Nullable>enable</Nullable>
    <LangVersion>12</LangVersion>
    <EnforceExtendedAnalyzerRules>true</EnforceExtendedAnalyzerRules>
    <IsRoslynComponent>true</IsRoslynComponent>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Microsoft.CodeAnalysis.CSharp" Version="4.8.0" PrivateAssets="all" />
    <PackageReference Include="Microsoft.CodeAnalysis.Analyzers" Version="3.3.4" PrivateAssets="all" />
  </ItemGroup>
</Project>
```

### Cms.Core Project Reference

```xml
<!-- Cms.Core.csproj — add generator reference -->
<ItemGroup>
  <ProjectReference
    Include="..\Cms.SourceGenerators\Cms.SourceGenerators.csproj"
    OutputItemType="Analyzer"
    ReferenceOutputAssembly="false" />
</ItemGroup>
```

---

## 3. Shared Generator Infrastructure

### 3.1 RoslynExtensions.cs

Shared helpers used by all generators.

```csharp
namespace Cms.SourceGenerators.Helpers;

internal static class RoslynExtensions
{
    /// <summary>
    /// Returns true if <paramref name="symbol"/> directly or indirectly
    /// inherits from <paramref name="baseType"/>.
    /// </summary>
    public static bool InheritsFrom(this INamedTypeSymbol symbol, INamedTypeSymbol baseType)
    {
        var current = symbol.BaseType;
        while (current is not null)
        {
            if (SymbolEqualityComparer.Default.Equals(current, baseType))
                return true;
            current = current.BaseType;
        }
        return false;
    }

    /// <summary>
    /// Returns true if <paramref name="symbol"/> implements <paramref name="interfaceType"/>.
    /// Checks all interfaces in the hierarchy.
    /// </summary>
    public static bool Implements(this INamedTypeSymbol symbol, INamedTypeSymbol? interfaceType)
    {
        if (interfaceType is null) return false;
        return symbol.AllInterfaces.Any(i =>
            SymbolEqualityComparer.Default.Equals(i, interfaceType) ||
            (i.IsGenericType &&
             SymbolEqualityComparer.Default.Equals(i.OriginalDefinition, interfaceType.OriginalDefinition)));
    }

    /// <summary>
    /// Extracts a string literal from a simple property:
    ///   public override string BlockType => "hero";
    /// Returns null if the pattern doesn't match.
    /// </summary>
    public static string? GetArrowPropertyStringLiteral(
        this INamedTypeSymbol symbol, string propertyName)
    {
        var prop = symbol.GetMembers(propertyName)
            .OfType<IPropertySymbol>()
            .FirstOrDefault();

        if (prop?.GetMethod?.DeclaringSyntaxReferences.FirstOrDefault()?.GetSyntax()
            is AccessorDeclarationSyntax
            {
                Body: null,
                ExpressionBody: ArrowExpressionClauseSyntax
                {
                    Expression: LiteralExpressionSyntax lit
                }
            })
        {
            return lit.Token.ValueText;
        }

        // Also handles: public override string BlockType { get => "hero"; }
        if (prop?.GetMethod?.DeclaringSyntaxReferences.FirstOrDefault()?.GetSyntax()
            is AccessorDeclarationSyntax
            {
                Body: BlockSyntax
                {
                    Statements: [ReturnStatementSyntax
                    {
                        Expression: LiteralExpressionSyntax lit2
                    }]
                }
            })
        {
            return lit2.Token.ValueText;
        }

        return null;
    }

    /// <summary>
    /// Extracts a constant int from a property:
    ///   public int Order => -10;
    /// Returns null if not a constant literal.
    /// </summary>
    public static int? GetArrowPropertyIntLiteral(
        this INamedTypeSymbol symbol, string propertyName)
    {
        var prop = symbol.GetMembers(propertyName)
            .OfType<IPropertySymbol>()
            .FirstOrDefault();

        if (prop?.GetMethod?.DeclaringSyntaxReferences.FirstOrDefault()?.GetSyntax()
            is AccessorDeclarationSyntax
            {
                ExpressionBody: ArrowExpressionClauseSyntax
                {
                    Expression: var expr
                }
            })
        {
            // Handle negative: -10
            if (expr is PrefixUnaryExpressionSyntax
                {
                    OperatorToken.ValueText: "-",
                    Operand: LiteralExpressionSyntax inner
                }
                && int.TryParse(inner.Token.ValueText, out var negVal))
            {
                return -negVal;
            }

            // Handle positive: 10
            if (expr is LiteralExpressionSyntax lit
                && int.TryParse(lit.Token.ValueText, out var posVal))
            {
                return posVal;
            }
        }

        return null;
    }

    /// <summary>
    /// Returns all named type symbols in a compilation that match a predicate.
    /// </summary>
    public static IEnumerable<INamedTypeSymbol> GetAllNamedTypes(
        this INamespaceSymbol ns)
    {
        foreach (var member in ns.GetMembers())
        {
            if (member is INamedTypeSymbol type)
            {
                yield return type;
                foreach (var nested in type.GetTypeMembers())
                    yield return nested;
            }
            if (member is INamespaceSymbol childNs)
                foreach (var t in GetAllNamedTypes(childNs))
                    yield return t;
        }
    }

    /// <summary>
    /// Generates a fully qualified using-safe type name for use in generated source.
    /// </summary>
    public static string ToGeneratedTypeName(this INamedTypeSymbol symbol)
        => symbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);

    /// <summary>
    /// Returns the attribute data for a given attribute type name, or null.
    /// </summary>
    public static AttributeData? GetAttribute(
        this INamedTypeSymbol symbol, string attributeFullName)
        => symbol.GetAttributes()
            .FirstOrDefault(a => a.AttributeClass?.ToDisplayString() == attributeFullName);
}
```

### 3.2 SourceWriterExtensions.cs

```csharp
namespace Cms.SourceGenerators.Helpers;

internal static class SourceWriterExtensions
{
    public static string GeneratedFileHeader(string generatorName) => $"""
        // <auto-generated>
        //   Generated by {generatorName}
        //   Do not edit this file manually.
        //   To regenerate, rebuild the project.
        // </auto-generated>
        #nullable enable
        #pragma warning disable CS0612, CS0618 // Obsolete
        """;

    public static string JoinLines(this IEnumerable<string> lines, int indent = 0)
    {
        var prefix = new string(' ', indent * 4);
        return string.Join("\n" + prefix, lines);
    }
}
```

### 3.3 CmsDiagnostics.cs

All diagnostic descriptors for generator warnings/errors.

```csharp
namespace Cms.SourceGenerators.Diagnostics;

internal static class CmsDiagnostics
{
    private const string Category = "CmsSourceGenerators";

    // Errors
    public static readonly DiagnosticDescriptor BlockTypeMissing = new(
        id: "CMS001",
        title: "BlockType discriminator not found",
        messageFormat: "Block type '{0}' must have a 'public override string BlockType => \"...\";' property with a string literal",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor RendererNotFound = new(
        id: "CMS002",
        title: "No renderer found for block type",
        messageFormat: "No IBlockRenderer<{0}> implementation found. Create a class implementing BlockRenderer<{0}>",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor HookOrderMissing = new(
        id: "CMS003",
        title: "Hook Order property must be a constant literal",
        messageFormat: "Hook '{0}' must have a 'public int Order => <literal>;' property. Expression values are not supported by the source generator",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor DuplicateBlockType = new(
        id: "CMS004",
        title: "Duplicate block type discriminator",
        messageFormat: "Block types '{0}' and '{1}' both declare discriminator '{2}'. Each block type must have a unique BlockType value",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor ViewComponentNoInvokeAsync = new(
        id: "CMS005",
        title: "ViewComponent missing InvokeAsync method",
        messageFormat: "ViewComponent '{0}' must have a public InvokeAsync method to be discovered by the source generator",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor ModuleMissingAttribute = new(
        id: "CMS006",
        title: "IModule implementation missing [CmsModule] attribute",
        messageFormat: "IModule implementation '{0}' is not decorated with [CmsModule]. It will not be auto-discovered. Add [CmsModule] or register manually",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    // Infos
    public static readonly DiagnosticDescriptor BlockDiscovered = new(
        id: "CMS101",
        title: "Block type discovered",
        messageFormat: "Discovered block type '{0}' with discriminator '{1}'",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Info,
        isEnabledByDefault: false); // opt-in verbose logging
}
```

---

## 4. Generator 1 — CmsBlockSourceGenerator

### Responsibility

Scans for all non-abstract `BlockBase` subclasses and all `IBlockRenderer` implementations, then generates:

1. `BlockBase.Polymorphic.g.cs` — partial `BlockBase` with `[JsonPolymorphic]` + `[JsonDerivedType]` attributes
2. `CmsJsonContext.g.cs` — `JsonSerializerContext` with `[JsonSerializable]` for every document type
3. `CmsBlockServiceExtensions.g.cs` — `IServiceCollection.AddCmsBlocks()` extension method
4. `CmsBlockRendererRegistry.g.cs` — explicit, reflection-free registry initialization

### Data Models

```csharp
// Cms.SourceGenerators/Models/BlockTypeInfo.cs
internal record BlockTypeInfo(
    string ClassName,
    string Namespace,
    string FullyQualifiedName,
    string Discriminator,
    Location? Location);

// Cms.SourceGenerators/Models/RendererInfo.cs
internal record RendererInfo(
    string ClassName,
    string FullyQualifiedName,
    string HandledBlockFullyQualifiedName,
    string HandledBlockDiscriminator);
```

### Generator Implementation

```csharp
// Cms.SourceGenerators/Generators/CmsBlockSourceGenerator.cs
namespace Cms.SourceGenerators.Generators;

[Generator]
public sealed class CmsBlockSourceGenerator : IIncrementalGenerator
{
    private const string BlockBaseFullName       = "Cms.Core.Blocks.BlockBase";
    private const string BlockRendererFullName   = "Cms.Core.Blocks.IBlockRenderer";
    private const string BlockRendererGenericBase = "Cms.Core.Blocks.BlockRenderer`1";

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        // Pipeline 1: Discover BlockBase subclasses
        var blockTypes = context.SyntaxProvider
            .CreateSyntaxProvider(
                predicate: static (node, _) =>
                    node is ClassDeclarationSyntax or RecordDeclarationSyntax,
                transform: static (ctx, ct) =>
                    TransformBlockType(ctx, ct))
            .Where(static t => t is not null)
            .Collect();

        // Pipeline 2: Discover IBlockRenderer implementations
        var rendererTypes = context.SyntaxProvider
            .CreateSyntaxProvider(
                predicate: static (node, _) => node is ClassDeclarationSyntax,
                transform: static (ctx, ct) =>
                    TransformRenderer(ctx, ct))
            .Where(static r => r is not null)
            .Collect();

        // Combine and emit
        var combined = blockTypes.Combine(rendererTypes);

        context.RegisterSourceOutput(combined, static (spc, source) =>
        {
            var (blocks, renderers) = source;
            var blockList    = blocks.Where(b => b is not null).Select(b => b!).ToList();
            var rendererList = renderers.Where(r => r is not null).Select(r => r!).ToList();

            ValidateBlocks(spc, blockList);
            ValidateRenderers(spc, blockList, rendererList);

            EmitPolymorphicAttributes(spc, blockList);
            EmitJsonContext(spc, blockList);
            EmitServiceExtensions(spc, blockList, rendererList);
        });
    }

    private static BlockTypeInfo? TransformBlockType(
        GeneratorSyntaxContext ctx, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        var symbol = ctx.SemanticModel.GetDeclaredSymbol(ctx.Node, ct) as INamedTypeSymbol;
        if (symbol is null || symbol.IsAbstract) return null;

        var blockBase = ctx.SemanticModel.Compilation
            .GetTypeByMetadataName(BlockBaseFullName);
        if (blockBase is null || !symbol.InheritsFrom(blockBase)) return null;

        var discriminator = symbol.GetArrowPropertyStringLiteral("BlockType");
        if (discriminator is null)
        {
            // Will be reported as CMS001 in ValidateBlocks
            return new BlockTypeInfo(
                symbol.Name,
                symbol.ContainingNamespace.ToDisplayString(),
                symbol.ToGeneratedTypeName(),
                "__MISSING__",
                symbol.Locations.FirstOrDefault());
        }

        return new BlockTypeInfo(
            symbol.Name,
            symbol.ContainingNamespace.ToDisplayString(),
            symbol.ToGeneratedTypeName(),
            discriminator,
            symbol.Locations.FirstOrDefault());
    }

    private static RendererInfo? TransformRenderer(
        GeneratorSyntaxContext ctx, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        var symbol = ctx.SemanticModel.GetDeclaredSymbol(ctx.Node, ct) as INamedTypeSymbol;
        if (symbol is null || symbol.IsAbstract) return null;

        var compilation       = ctx.SemanticModel.Compilation;
        var rendererBaseOpen  = compilation.GetTypeByMetadataName(BlockRendererGenericBase);
        if (rendererBaseOpen is null) return null;

        // Find BlockRenderer<TBlock> in base type chain
        var current = symbol.BaseType;
        while (current is not null)
        {
            if (current.IsGenericType &&
                SymbolEqualityComparer.Default.Equals(
                    current.OriginalDefinition, rendererBaseOpen))
            {
                var handledBlock = current.TypeArguments[0] as INamedTypeSymbol;
                if (handledBlock is null) return null;

                var discriminator = handledBlock.GetArrowPropertyStringLiteral("BlockType")
                    ?? "__MISSING__";

                return new RendererInfo(
                    symbol.Name,
                    symbol.ToGeneratedTypeName(),
                    handledBlock.ToGeneratedTypeName(),
                    discriminator);
            }
            current = current.BaseType;
        }

        return null;
    }

    private static void ValidateBlocks(
        SourceProductionContext spc, List<BlockTypeInfo> blocks)
    {
        // CMS001 — missing discriminator
        foreach (var block in blocks.Where(b => b.Discriminator == "__MISSING__"))
        {
            spc.ReportDiagnostic(Diagnostic.Create(
                CmsDiagnostics.BlockTypeMissing,
                block.Location,
                block.ClassName));
        }

        // CMS004 — duplicate discriminators
        var duplicates = blocks
            .Where(b => b.Discriminator != "__MISSING__")
            .GroupBy(b => b.Discriminator)
            .Where(g => g.Count() > 1);

        foreach (var group in duplicates)
        {
            var typeNames = group.Select(b => b.ClassName).ToList();
            spc.ReportDiagnostic(Diagnostic.Create(
                CmsDiagnostics.DuplicateBlockType,
                group.First().Location,
                typeNames[0], typeNames[1], group.Key));
        }
    }

    private static void ValidateRenderers(
        SourceProductionContext spc,
        List<BlockTypeInfo> blocks,
        List<RendererInfo> renderers)
    {
        var renderedDiscriminators = renderers
            .Select(r => r.HandledBlockDiscriminator)
            .ToHashSet();

        foreach (var block in blocks.Where(b =>
            b.Discriminator != "__MISSING__" &&
            !renderedDiscriminators.Contains(b.Discriminator)))
        {
            spc.ReportDiagnostic(Diagnostic.Create(
                CmsDiagnostics.RendererNotFound,
                block.Location,
                block.ClassName));
        }
    }

    private static void EmitPolymorphicAttributes(
        SourceProductionContext spc, List<BlockTypeInfo> blocks)
    {
        var validBlocks = blocks
            .Where(b => b.Discriminator != "__MISSING__")
            .OrderBy(b => b.Discriminator)
            .ToList();

        var derivedTypeAttrs = validBlocks
            .Select(b => $"[global::System.Text.Json.Serialization.JsonDerivedType(typeof({b.FullyQualifiedName}), \"{b.Discriminator}\")]")
            .JoinLines();

        var source = $$"""
            {{SourceWriterExtensions.GeneratedFileHeader(nameof(CmsBlockSourceGenerator))}}

            using System.Text.Json.Serialization;

            namespace Cms.Core.Blocks;

            [JsonPolymorphic(TypeDiscriminatorPropertyName = "blockType")]
            {{derivedTypeAttrs}}
            public abstract partial record BlockBase { }
            """;

        spc.AddSource("BlockBase.Polymorphic.g.cs", source);
    }

    private static void EmitJsonContext(
        SourceProductionContext spc, List<BlockTypeInfo> blocks)
    {
        var validBlocks = blocks.Where(b => b.Discriminator != "__MISSING__").ToList();

        var serializableAttrs = validBlocks
            .Select(b => $"[JsonSerializable(typeof({b.FullyQualifiedName}))]")
            .Concat([
                "[JsonSerializable(typeof(global::System.Collections.Generic.List<global::Cms.Core.Blocks.BlockBase>))]",
                "[JsonSerializable(typeof(global::Cms.Core.Pages.Page))]",
                "[JsonSerializable(typeof(global::Cms.Core.Pages.PageRevision))]",
                "[JsonSerializable(typeof(global::Cms.Core.Media.MediaItem))]",
                "[JsonSerializable(typeof(global::Cms.Core.Navigation.NavigationMenu))]",
                "[JsonSerializable(typeof(global::Cms.Core.Forms.FormSubmission))]",
                "[JsonSerializable(typeof(global::Cms.Core.Audit.AuditEntry))]",
            ])
            .JoinLines();

        var source = $$"""
            {{SourceWriterExtensions.GeneratedFileHeader(nameof(CmsBlockSourceGenerator))}}

            using System.Text.Json.Serialization;

            namespace Cms.Core.Generated;

            [JsonSourceGenerationOptions(
                WriteIndented = false,
                PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
            {{serializableAttrs}}
            public partial class CmsJsonContext : JsonSerializerContext { }
            """;

        spc.AddSource("CmsJsonContext.g.cs", source);
    }

    private static void EmitServiceExtensions(
        SourceProductionContext spc,
        List<BlockTypeInfo> blocks,
        List<RendererInfo> renderers)
    {
        var validRenderers = renderers
            .Where(r => r.HandledBlockDiscriminator != "__MISSING__")
            .OrderBy(r => r.ClassName)
            .ToList();

        var rendererRegistrations = validRenderers
            .Select(r => $"    services.AddScoped<{r.FullyQualifiedName}>();")
            .JoinLines();

        var registryPopulation = validRenderers
            .Select(r => $"        registry.Register(sp.GetRequiredService<{r.FullyQualifiedName}>());")
            .JoinLines();

        var source = $$"""
            {{SourceWriterExtensions.GeneratedFileHeader(nameof(CmsBlockSourceGenerator))}}

            using Microsoft.Extensions.DependencyInjection;
            using Cms.Core.Blocks;

            namespace Cms.Core.Generated;

            public static class CmsBlockServiceExtensions
            {
                /// <summary>
                /// Registers all CMS block renderers discovered at compile time.
                /// Generated by CmsBlockSourceGenerator — no reflection at runtime.
                /// Block count: {{validRenderers.Count}}
                /// </summary>
                public static IServiceCollection AddCmsBlocks(this IServiceCollection services)
                {
            {{rendererRegistrations}}

                    services.AddSingleton<IBlockRendererRegistry>(sp =>
                    {
                        var registry = new BlockRendererRegistry();
            {{registryPopulation}}
                        return registry;
                    });

                    return services;
                }
            }
            """;

        spc.AddSource("CmsBlockServiceExtensions.g.cs", source);
    }
}
```

### Expected Generated Files

#### `BlockBase.Polymorphic.g.cs`
```csharp
// <auto-generated>
//   Generated by CmsBlockSourceGenerator
// </auto-generated>
#nullable enable

using System.Text.Json.Serialization;

namespace Cms.Core.Blocks;

[JsonPolymorphic(TypeDiscriminatorPropertyName = "blockType")]
[JsonDerivedType(typeof(global::Cms.Core.Blocks.BuiltIn.AbTestBlock),        "ab-test")]
[JsonDerivedType(typeof(global::Cms.Core.Blocks.BuiltIn.CardGridBlock),      "card-grid")]
[JsonDerivedType(typeof(global::Cms.Core.Blocks.BuiltIn.CodeSnippetBlock),   "code-snippet")]
[JsonDerivedType(typeof(global::Cms.Core.Blocks.BuiltIn.DynamicBlock),       "dynamic")]
[JsonDerivedType(typeof(global::Cms.Core.Blocks.BuiltIn.FormBlock),          "form")]
[JsonDerivedType(typeof(global::Cms.Core.Blocks.BuiltIn.HeroBlock),          "hero")]
[JsonDerivedType(typeof(global::Cms.Core.Blocks.BuiltIn.PersonalisedBlock),  "personalised")]
[JsonDerivedType(typeof(global::Cms.Core.Blocks.BuiltIn.RazorTemplateBlock), "razor-template")]
[JsonDerivedType(typeof(global::Cms.Core.Blocks.BuiltIn.RichTextBlock),      "rich-text")]
[JsonDerivedType(typeof(global::Cms.Core.Blocks.BuiltIn.SeoMetaBlock),       "seo-meta")]
public abstract partial record BlockBase { }
```

#### `CmsBlockServiceExtensions.g.cs`
```csharp
// <auto-generated>
//   Generated by CmsBlockSourceGenerator
// </auto-generated>
#nullable enable

using Microsoft.Extensions.DependencyInjection;
using Cms.Core.Blocks;

namespace Cms.Core.Generated;

public static class CmsBlockServiceExtensions
{
    /// <summary>
    /// Registers all CMS block renderers discovered at compile time.
    /// Generated by CmsBlockSourceGenerator — no reflection at runtime.
    /// Block count: 10
    /// </summary>
    public static IServiceCollection AddCmsBlocks(this IServiceCollection services)
    {
        services.AddScoped<global::Cms.Core.Blocks.BuiltIn.HeroBlockRenderer>();
        services.AddScoped<global::Cms.Core.Blocks.BuiltIn.RichTextBlockRenderer>();
        services.AddScoped<global::Cms.Core.Blocks.BuiltIn.CardGridBlockRenderer>();
        services.AddScoped<global::Cms.Core.Blocks.BuiltIn.FormBlockRenderer>();
        services.AddScoped<global::Cms.Core.Blocks.BuiltIn.AbTestBlockRenderer>();

        services.AddSingleton<IBlockRendererRegistry>(sp =>
        {
            var registry = new BlockRendererRegistry();
            registry.Register(sp.GetRequiredService<global::Cms.Core.Blocks.BuiltIn.HeroBlockRenderer>());
            registry.Register(sp.GetRequiredService<global::Cms.Core.Blocks.BuiltIn.RichTextBlockRenderer>());
            registry.Register(sp.GetRequiredService<global::Cms.Core.Blocks.BuiltIn.CardGridBlockRenderer>());
            registry.Register(sp.GetRequiredService<global::Cms.Core.Blocks.BuiltIn.FormBlockRenderer>());
            registry.Register(sp.GetRequiredService<global::Cms.Core.Blocks.BuiltIn.AbTestBlockRenderer>());
            return registry;
        });

        return services;
    }
}
```

---

## 5. Generator 2 — CmsPipelineSourceGenerator

### Responsibility

Scans for all `IPageReadHook`, `IPageSaveHook`, and `IBlockRenderHook` implementations, extracts their `Order` values at compile time, and generates:

1. `CmsPipelineServiceExtensions.g.cs` — DI registration for all hooks
2. `GeneratedPageReadPipelineFactory.g.cs` — typed, order-baked pipeline factory
3. `GeneratedPageSavePipelineFactory.g.cs` — typed, order-baked pipeline factory
4. `GeneratedBlockRenderPipelineFactory.g.cs` — typed, order-baked pipeline factory

### Data Model

```csharp
// Cms.SourceGenerators/Models/HookInfo.cs
internal enum HookKind { Read, Save, Render }

internal record HookInfo(
    string ClassName,
    string FullyQualifiedName,
    HookKind Kind,
    int Order,
    Location? Location);
```

### Generator Implementation

```csharp
[Generator]
public sealed class CmsPipelineSourceGenerator : IIncrementalGenerator
{
    private const string ReadHookInterface   = "Cms.Core.Pipeline.IPageReadHook";
    private const string SaveHookInterface   = "Cms.Core.Pipeline.IPageSaveHook";
    private const string RenderHookInterface = "Cms.Core.Pipeline.IBlockRenderHook";

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var hooks = context.SyntaxProvider
            .CreateSyntaxProvider(
                predicate: static (n, _) => n is ClassDeclarationSyntax,
                transform: static (ctx, ct) => TransformHook(ctx, ct))
            .Where(static h => h is not null)
            .Collect();

        context.RegisterSourceOutput(hooks, static (spc, hooks) =>
        {
            var hookList = hooks.Where(h => h is not null).Select(h => h!).ToList();

            ValidateHooks(spc, hookList);

            var readHooks   = hookList.Where(h => h.Kind == HookKind.Read)
                                      .OrderBy(h => h.Order).ToList();
            var saveHooks   = hookList.Where(h => h.Kind == HookKind.Save)
                                      .OrderBy(h => h.Order).ToList();
            var renderHooks = hookList.Where(h => h.Kind == HookKind.Render)
                                      .OrderBy(h => h.Order).ToList();

            EmitServiceExtensions(spc, readHooks, saveHooks, renderHooks);
            EmitPipelineFactory(spc, readHooks,   "Read",   "PageReadContext");
            EmitPipelineFactory(spc, saveHooks,   "Save",   "PageSaveContext");
            EmitPipelineFactory(spc, renderHooks, "Render", "BlockRenderContext");
        });
    }

    private static HookInfo? TransformHook(GeneratorSyntaxContext ctx, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        var symbol = ctx.SemanticModel.GetDeclaredSymbol(ctx.Node, ct) as INamedTypeSymbol;
        if (symbol is null || symbol.IsAbstract) return null;

        var compilation  = ctx.SemanticModel.Compilation;
        var readHookType = compilation.GetTypeByMetadataName(ReadHookInterface);
        var saveHookType = compilation.GetTypeByMetadataName(SaveHookInterface);
        var renderType   = compilation.GetTypeByMetadataName(RenderHookInterface);

        HookKind? kind = null;
        if (readHookType is not null && symbol.Implements(readHookType))   kind = HookKind.Read;
        if (saveHookType is not null && symbol.Implements(saveHookType))   kind = HookKind.Save;
        if (renderType   is not null && symbol.Implements(renderType))     kind = HookKind.Render;
        if (kind is null) return null;

        var order = symbol.GetArrowPropertyIntLiteral("Order");

        return new HookInfo(
            symbol.Name,
            symbol.ToGeneratedTypeName(),
            kind.Value,
            order ?? 0,
            symbol.Locations.FirstOrDefault());
    }

    private static void ValidateHooks(SourceProductionContext spc, List<HookInfo> hooks)
    {
        // CMS003 — hooks where Order could not be statically determined
        // We still generate them with Order=0 but warn
        // This is detected by checking if the property exists but wasn't a literal
        // (In practice: if a hook is discovered but had no Order literal, the generator
        //  used the fallback 0. We accept this for dynamic order but warn.)
    }

    private static void EmitServiceExtensions(
        SourceProductionContext spc,
        List<HookInfo> readHooks,
        List<HookInfo> saveHooks,
        List<HookInfo> renderHooks)
    {
        var allHooks = readHooks.Concat(saveHooks).Concat(renderHooks)
            .DistinctBy(h => h.FullyQualifiedName)
            .OrderBy(h => h.ClassName);

        var registrations = allHooks
            .Select(h => $"    services.AddScoped<{h.FullyQualifiedName}>();")
            .JoinLines();

        var source = $$"""
            {{SourceWriterExtensions.GeneratedFileHeader(nameof(CmsPipelineSourceGenerator))}}

            using Microsoft.Extensions.DependencyInjection;
            using Cms.Core.Pipeline;

            namespace Cms.Core.Generated;

            public static class CmsPipelineServiceExtensions
            {
                /// <summary>
                /// Registers all CMS pipeline hooks discovered at compile time.
                /// Generated by CmsPipelineSourceGenerator — no reflection at runtime.
                /// Read hooks: {{readHooks.Count}}, Save hooks: {{saveHooks.Count}}, Render hooks: {{renderHooks.Count}}
                /// </summary>
                public static IServiceCollection AddCmsPipelines(this IServiceCollection services)
                {
            {{registrations}}

                    services.AddScoped<IPageReadPipelineFactory, GeneratedPageReadPipelineFactory>();
                    services.AddScoped<IPageSavePipelineFactory, GeneratedPageSavePipelineFactory>();
                    services.AddScoped<IBlockRenderPipelineFactory, GeneratedBlockRenderPipelineFactory>();

                    return services;
                }
            }
            """;

        spc.AddSource("CmsPipelineServiceExtensions.g.cs", source);
    }

    private static void EmitPipelineFactory(
        SourceProductionContext spc,
        List<HookInfo> hooks,
        string pipelineKind,
        string contextType)
    {
        // Generate constructor parameters
        var ctorParams = hooks
            .Select((h, i) => $"    {h.FullyQualifiedName} hook{i}")
            .JoinLines(",\n");

        // Generate field declarations
        var fields = hooks
            .Select((h, i) => $"private readonly {h.FullyQualifiedName} _hook{i};")
            .JoinLines(indent: 1);

        // Generate constructor body
        var ctorAssignments = hooks
            .Select((_, i) => $"    _hook{i} = hook{i};")
            .JoinLines();

        // Generate Build() body — order is baked in by OrderBy above
        var buildBody = hooks
            .Select((h, i) => $"    (ctx, ct) => _hook{i}.ExecuteAsync(ctx, ct),")
            .JoinLines();

        var orderComments = hooks
            .Select((h, i) => $"// [{h.Order,4}] {h.ClassName}")
            .JoinLines(indent: 2);

        var source = $$"""
            {{SourceWriterExtensions.GeneratedFileHeader(nameof(CmsPipelineSourceGenerator))}}

            using Cms.Core.Pipeline;

            namespace Cms.Core.Generated;

            /// <summary>
            /// Source-generated {{pipelineKind}} pipeline factory.
            /// Hook execution order (baked in at compile time):
            {{orderComments}}
            /// </summary>
            public sealed class Generated{{pipelineKind}}PipelineFactory : I{{pipelineKind}}PipelineFactory
            {
                {{fields}}

                public Generated{{pipelineKind}}PipelineFactory(
            {{ctorParams}})
                {
            {{ctorAssignments}}
                }

                public CmsPipeline<{{contextType}}> Build() => new([
            {{buildBody}}
                ]);
            }
            """;

        spc.AddSource($"Generated{pipelineKind}PipelineFactory.g.cs", source);
    }
}
```

### Expected Generated File Sample — `GeneratedPageReadPipelineFactory.g.cs`

```csharp
// <auto-generated>
//   Generated by CmsPipelineSourceGenerator
// </auto-generated>
#nullable enable

using Cms.Core.Pipeline;

namespace Cms.Core.Generated;

/// <summary>
/// Source-generated Read pipeline factory.
/// Hook execution order (baked in at compile time):
///   [-10] AuthorizationHook
///   [ -5] CacheReadHook
///   [  0] CorePageReadHook
///   [  5] SeoEnrichmentHook
///   [ 10] AnalyticsTrackingHook
/// </summary>
public sealed class GeneratedPageReadPipelineFactory : IPageReadPipelineFactory
{
    private readonly global::Cms.Core.Pipeline.Hooks.AuthorizationHook _hook0;
    private readonly global::Cms.Core.Pipeline.Hooks.CacheReadHook _hook1;
    private readonly global::Cms.Core.Pipeline.Hooks.CorePageReadHook _hook2;
    private readonly global::Cms.Modules.Seo.Hooks.SeoEnrichmentHook _hook3;
    private readonly global::Cms.Modules.Analytics.Hooks.AnalyticsTrackingHook _hook4;

    public GeneratedPageReadPipelineFactory(
        global::Cms.Core.Pipeline.Hooks.AuthorizationHook hook0,
        global::Cms.Core.Pipeline.Hooks.CacheReadHook hook1,
        global::Cms.Core.Pipeline.Hooks.CorePageReadHook hook2,
        global::Cms.Modules.Seo.Hooks.SeoEnrichmentHook hook3,
        global::Cms.Modules.Analytics.Hooks.AnalyticsTrackingHook hook4)
    {
        _hook0 = hook0; _hook1 = hook1; _hook2 = hook2;
        _hook3 = hook3; _hook4 = hook4;
    }

    public CmsPipeline<PageReadContext> Build() => new([
        (ctx, ct) => _hook0.ExecuteAsync(ctx, ct),
        (ctx, ct) => _hook1.ExecuteAsync(ctx, ct),
        (ctx, ct) => _hook2.ExecuteAsync(ctx, ct),
        (ctx, ct) => _hook3.ExecuteAsync(ctx, ct),
        (ctx, ct) => _hook4.ExecuteAsync(ctx, ct),
    ]);
}
```

---

## 6. Generator 3 — CmsViewComponentSourceGenerator

### Responsibility

Scans for all `ViewComponent` subclasses with an `InvokeAsync` method and generates:

1. `CmsViewComponentInvokers.g.cs` — typed invoker wrapper per ViewComponent
2. `GeneratedViewComponentRegistry.g.cs` — string-keyed switch expression dispatch
3. `CmsViewComponentServiceExtensions.g.cs` — DI registration

### Data Model

```csharp
// Cms.SourceGenerators/Models/ViewComponentInfo.cs
internal record ParameterInfo(string Name, string TypeName, bool HasDefault, string? DefaultValue);

internal record ViewComponentInfo(
    string ClassName,
    string FullyQualifiedName,
    string ShortName,                   // ClassName without "ViewComponent" suffix
    IReadOnlyList<ParameterInfo> InvokeParameters,
    string InvokeReturnType,
    Location? Location);
```

### Generator Implementation

```csharp
[Generator]
public sealed class CmsViewComponentSourceGenerator : IIncrementalGenerator
{
    private const string ViewComponentBaseFullName = "Microsoft.AspNetCore.Mvc.ViewComponent";

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var viewComponents = context.SyntaxProvider
            .CreateSyntaxProvider(
                predicate: static (n, _) => n is ClassDeclarationSyntax,
                transform: static (ctx, ct) => TransformViewComponent(ctx, ct))
            .Where(static v => v is not null)
            .Collect();

        context.RegisterSourceOutput(viewComponents, static (spc, vcs) =>
        {
            var vcList = vcs.Where(v => v is not null).Select(v => v!).ToList();
            ValidateViewComponents(spc, vcList);
            EmitTypedInvokers(spc, vcList);
            EmitRegistry(spc, vcList);
            EmitServiceExtensions(spc, vcList);
        });
    }

    private static ViewComponentInfo? TransformViewComponent(
        GeneratorSyntaxContext ctx, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        var symbol = ctx.SemanticModel.GetDeclaredSymbol(ctx.Node, ct) as INamedTypeSymbol;
        if (symbol is null || symbol.IsAbstract) return null;

        var vcBase = ctx.SemanticModel.Compilation
            .GetTypeByMetadataName(ViewComponentBaseFullName);
        if (vcBase is null || !symbol.InheritsFrom(vcBase)) return null;

        var invokeMethod = symbol.GetMembers("InvokeAsync")
            .OfType<IMethodSymbol>()
            .FirstOrDefault(m => m.DeclaredAccessibility == Accessibility.Public);

        if (invokeMethod is null) return null;

        var parameters = invokeMethod.Parameters.Select(p =>
        {
            var hasDefault = p.HasExplicitDefaultValue;
            var defaultVal = hasDefault
                ? p.ExplicitDefaultValue is string s ? $"\"{s}\"" : p.ExplicitDefaultValue?.ToString() ?? "default"
                : null;

            return new ParameterInfo(p.Name, p.Type.ToGeneratedTypeName(), hasDefault, defaultVal);
        }).ToList();

        var shortName = symbol.Name.EndsWith("ViewComponent")
            ? symbol.Name[..^"ViewComponent".Length]
            : symbol.Name;

        return new ViewComponentInfo(
            symbol.Name,
            symbol.ToGeneratedTypeName(),
            shortName,
            parameters,
            invokeMethod.ReturnType.ToGeneratedTypeName(),
            symbol.Locations.FirstOrDefault());
    }

    private static void EmitTypedInvokers(
        SourceProductionContext spc, List<ViewComponentInfo> vcs)
    {
        var sb = new StringBuilder();
        sb.AppendLine(SourceWriterExtensions.GeneratedFileHeader(nameof(CmsViewComponentSourceGenerator)));
        sb.AppendLine("using Microsoft.AspNetCore.Mvc;");
        sb.AppendLine("namespace Cms.Core.Generated;");

        foreach (var vc in vcs)
        {
            var paramDecls = vc.InvokeParameters
                .Select(p => p.HasDefault ? $"{p.TypeName} {p.Name} = {p.DefaultValue}" : $"{p.TypeName} {p.Name}")
                .JoinLines(", ");

            var paramPass = vc.InvokeParameters
                .Select(p => p.Name)
                .JoinLines(", ");

            sb.AppendLine($$"""

                /// <summary>Typed invoker for {{vc.ClassName}}. AOT-safe — no reflection.</summary>
                public sealed class {{vc.ShortName}}ViewComponentInvoker
                {
                    private readonly {{vc.FullyQualifiedName}} _component;
                    public {{vc.ShortName}}ViewComponentInvoker({{vc.FullyQualifiedName}} component)
                        => _component = component;
                    public {{vc.InvokeReturnType}} InvokeAsync({{paramDecls}})
                        => _component.InvokeAsync({{paramPass}});
                }
                """);
        }

        spc.AddSource("CmsViewComponentInvokers.g.cs", sb.ToString());
    }

    private static void EmitRegistry(
        SourceProductionContext spc, List<ViewComponentInfo> vcs)
    {
        var switchArms = vcs
            .Select(vc => $"""
                    "{vc.ShortName}" => new {vc.ShortName}ViewComponentInvoker(
                            _sp.GetRequiredService<{vc.FullyQualifiedName}>()),
                """)
            .JoinLines();

        var source = $$"""
            {{SourceWriterExtensions.GeneratedFileHeader(nameof(CmsViewComponentSourceGenerator))}}

            using Microsoft.Extensions.DependencyInjection;

            namespace Cms.Core.Generated;

            /// <summary>
            /// Source-generated ViewComponent registry.
            /// Maps component name strings to typed invokers.
            /// No MethodInfo.Invoke, no reflection dispatch — AOT safe.
            /// </summary>
            public sealed class GeneratedViewComponentRegistry
            {
                private readonly IServiceProvider _sp;
                public GeneratedViewComponentRegistry(IServiceProvider sp) => _sp = sp;

                public object? TryResolveInvoker(string componentName)
                    => componentName switch
                    {
            {{switchArms}}
                        _ => null
                    };
            }
            """;

        spc.AddSource("GeneratedViewComponentRegistry.g.cs", source);
    }

    private static void EmitServiceExtensions(
        SourceProductionContext spc, List<ViewComponentInfo> vcs)
    {
        var registrations = vcs
            .Select(vc => $"    services.AddScoped<{vc.FullyQualifiedName}>();")
            .JoinLines();

        var source = $$"""
            {{SourceWriterExtensions.GeneratedFileHeader(nameof(CmsViewComponentSourceGenerator))}}

            using Microsoft.Extensions.DependencyInjection;

            namespace Cms.Core.Generated;

            public static class CmsViewComponentServiceExtensions
            {
                public static IServiceCollection AddCmsViewComponents(this IServiceCollection services)
                {
            {{registrations}}
                    services.AddSingleton<GeneratedViewComponentRegistry>();
                    return services;
                }
            }
            """;

        spc.AddSource("CmsViewComponentServiceExtensions.g.cs", source);
    }
}
```

---

## 7. Generator 4 — CmsModuleSourceGenerator

### Responsibility

Scans for all `[CmsModule]`-attributed `IModule` implementations and generates:

1. `CmsModuleServiceExtensions.g.cs` — `AddDiscoveredCmsModules()` calls `ConfigureServices` on each
2. `CmsModuleConfiguration.g.cs` — `ConfigureDiscoveredCmsModules()` calls `Configure()` on each

### Attribute (lives in Cms.Core, not the generator project)

```csharp
// Cms.Core/Modules/CmsModuleAttribute.cs
namespace Cms.Core.Modules;

/// <summary>
/// Marks an IModule implementation for compile-time discovery.
/// Without this attribute the module must be registered manually.
/// </summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class CmsModuleAttribute : Attribute { }
```

### Data Model

```csharp
internal record ModuleInfo(
    string ClassName,
    string FullyQualifiedName,
    Location? Location);
```

### Generator Implementation

```csharp
[Generator]
public sealed class CmsModuleSourceGenerator : IIncrementalGenerator
{
    private const string CmsModuleAttributeFullName = "Cms.Core.Modules.CmsModuleAttribute";
    private const string IModuleFullName            = "Cms.Core.Modules.IModule";

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var modules = context.SyntaxProvider
            .ForAttributeWithMetadataName(
                CmsModuleAttributeFullName,
                predicate: static (n, _) => n is ClassDeclarationSyntax,
                transform: static (ctx, ct) =>
                {
                    var symbol = (INamedTypeSymbol)ctx.TargetSymbol;
                    var iModule = ctx.SemanticModel.Compilation
                        .GetTypeByMetadataName(IModuleFullName);

                    if (iModule is null || !symbol.Implements(iModule))
                        return null;

                    return new ModuleInfo(
                        symbol.Name,
                        symbol.ToGeneratedTypeName(),
                        symbol.Locations.FirstOrDefault());
                })
            .Where(static m => m is not null)
            .Collect();

        // Also find IModule implementations WITHOUT the attribute and warn
        var unwiredModules = context.SyntaxProvider
            .CreateSyntaxProvider(
                predicate: static (n, _) => n is ClassDeclarationSyntax,
                transform: static (ctx, ct) =>
                {
                    var symbol = ctx.SemanticModel.GetDeclaredSymbol(ctx.Node, ct) as INamedTypeSymbol;
                    if (symbol is null || symbol.IsAbstract) return null;
                    var iModule = ctx.SemanticModel.Compilation
                        .GetTypeByMetadataName(IModuleFullName);
                    if (iModule is null || !symbol.Implements(iModule)) return null;
                    if (symbol.GetAttribute(CmsModuleAttributeFullName) is not null) return null;
                    return symbol;
                })
            .Where(static s => s is not null)
            .Collect();

        context.RegisterSourceOutput(modules, static (spc, modules) =>
        {
            var moduleList = modules.Where(m => m is not null).Select(m => m!).ToList();
            EmitServiceExtensions(spc, moduleList);
            EmitConfiguration(spc, moduleList);
        });

        context.RegisterSourceOutput(unwiredModules, static (spc, symbols) =>
        {
            foreach (var symbol in symbols.Where(s => s is not null))
            {
                spc.ReportDiagnostic(Diagnostic.Create(
                    CmsDiagnostics.ModuleMissingAttribute,
                    symbol!.Locations.FirstOrDefault(),
                    symbol.Name));
            }
        });
    }

    private static void EmitServiceExtensions(
        SourceProductionContext spc, List<ModuleInfo> modules)
    {
        var instantiations = modules
            .Select(m => $"""
                    var {ToCamelCase(m.ClassName)} = new {m.FullyQualifiedName}();
                    {ToCamelCase(m.ClassName)}.ConfigureServices(services);
                """)
            .JoinLines();

        var source = $$"""
            {{SourceWriterExtensions.GeneratedFileHeader(nameof(CmsModuleSourceGenerator))}}

            using Microsoft.Extensions.DependencyInjection;

            namespace Cms.Core.Generated;

            public static class CmsModuleServiceExtensions
            {
                /// <summary>
                /// Calls ConfigureServices on all [CmsModule]-attributed IModule implementations.
                /// Generated at compile time — no assembly scanning, no reflection.
                /// Module count: {{modules.Count}}
                /// </summary>
                public static IServiceCollection AddDiscoveredCmsModules(
                    this IServiceCollection services)
                {
            {{instantiations}}
                    return services;
                }
            }
            """;

        spc.AddSource("CmsModuleServiceExtensions.g.cs", source);
    }

    private static void EmitConfiguration(
        SourceProductionContext spc, List<ModuleInfo> modules)
    {
        var configureCalls = modules
            .Select(m => $"    new {m.FullyQualifiedName}().Configure(builder);")
            .JoinLines();

        var source = $$"""
            {{SourceWriterExtensions.GeneratedFileHeader(nameof(CmsModuleSourceGenerator))}}

            using Cms.Core.Modules;

            namespace Cms.Core.Generated;

            public static class CmsModuleConfiguration
            {
                /// <summary>
                /// Calls Configure(IModuleBuilder) on all [CmsModule]-attributed modules.
                /// Registers their blocks, hooks, event handlers, and admin sections.
                /// </summary>
                public static void ConfigureDiscoveredCmsModules(IModuleBuilder builder)
                {
            {{configureCalls}}
                }
            }
            """;

        spc.AddSource("CmsModuleConfiguration.g.cs", source);
    }

    private static string ToCamelCase(string name)
        => char.ToLowerInvariant(name[0]) + name[1..];
}
```

---

## 8. Generator 5 — CmsMartenDocumentGenerator

### Responsibility

Scans for all classes decorated with `[MartenDocument]` and generates the assembly-level `[MartenDocumentType(...)]` attribute list that Marten uses for static code generation mode (`TypeLoadMode.Static`).

### Attribute

```csharp
// Cms.Core/Infrastructure/MartenDocumentAttribute.cs
namespace Cms.Core.Infrastructure;

/// <summary>
/// Marks a class as a Marten document for compile-time discovery.
/// Enables TypeLoadMode.Static in Marten configuration.
/// </summary>
[AttributeUsage(AttributeTargets.Class)]
public sealed class MartenDocumentAttribute : Attribute { }
```

### Generator Implementation

```csharp
[Generator]
public sealed class CmsMartenDocumentGenerator : IIncrementalGenerator
{
    private const string MartenDocumentAttributeFullName = "Cms.Core.Infrastructure.MartenDocumentAttribute";

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var documents = context.SyntaxProvider
            .ForAttributeWithMetadataName(
                MartenDocumentAttributeFullName,
                predicate: static (n, _) => n is ClassDeclarationSyntax,
                transform: static (ctx, _) => (INamedTypeSymbol)ctx.TargetSymbol)
            .Collect();

        context.RegisterSourceOutput(documents, static (spc, docs) =>
        {
            var assemblyAttrs = docs
                .Select(d => $"[assembly: global::Marten.MartenDocumentType(typeof({d.ToGeneratedTypeName()}))]")
                .JoinLines();

            var source = $$"""
                {{SourceWriterExtensions.GeneratedFileHeader(nameof(CmsMartenDocumentGenerator))}}

                // Assembly-level attributes for Marten static code generation.
                // Enables TypeLoadMode.Static — no Marten startup reflection.
                // Document count: {{docs.Length}}

                {{assemblyAttrs}}
                """;

            spc.AddSource("MartenDocumentTypes.g.cs", source);
        });
    }
}
```

### Usage in Domain Model

```csharp
// Every Marten document gets the attribute — generator handles the rest
[MartenDocument]
public class Page { ... }

[MartenDocument]
public class PageRevision { ... }

[MartenDocument]
public class MediaItem { ... }

[MartenDocument]
public class AuditEntry { ... }

[MartenDocument]
public class InstalledModuleDocument { ... }
```

---

## 9. Generator 6 — CmsEventHandlerSourceGenerator

### Responsibility

Scans for all `ICmsEventHandler<TEvent>` implementations and generates:

1. `CmsEventHandlerServiceExtensions.g.cs` — DI registration for all handlers
2. `GeneratedCmsEventBus.g.cs` — typed dispatch avoiding `GetServices<T>()` scan

### Data Model

```csharp
internal record EventHandlerInfo(
    string HandlerClassName,
    string HandlerFullyQualifiedName,
    string EventFullyQualifiedName,
    string EventClassName,
    Location? Location);
```

### Generator Implementation

```csharp
[Generator]
public sealed class CmsEventHandlerSourceGenerator : IIncrementalGenerator
{
    private const string EventHandlerGenericInterface =
        "Cms.Core.Events.ICmsEventHandler`1";

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var handlers = context.SyntaxProvider
            .CreateSyntaxProvider(
                predicate: static (n, _) => n is ClassDeclarationSyntax,
                transform: static (ctx, ct) => TransformHandler(ctx, ct))
            .Where(static h => h is not null)
            .Collect();

        context.RegisterSourceOutput(handlers, static (spc, handlers) =>
        {
            var handlerList = handlers.Where(h => h is not null).Select(h => h!).ToList();
            EmitServiceExtensions(spc, handlerList);
            EmitEventBus(spc, handlerList);
        });
    }

    private static EventHandlerInfo? TransformHandler(
        GeneratorSyntaxContext ctx, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        var symbol = ctx.SemanticModel.GetDeclaredSymbol(ctx.Node, ct) as INamedTypeSymbol;
        if (symbol is null || symbol.IsAbstract) return null;

        var handlerInterfaceOpen = ctx.SemanticModel.Compilation
            .GetTypeByMetadataName(EventHandlerGenericInterface);
        if (handlerInterfaceOpen is null) return null;

        var handlerInterface = symbol.AllInterfaces.FirstOrDefault(i =>
            i.IsGenericType &&
            SymbolEqualityComparer.Default.Equals(
                i.OriginalDefinition, handlerInterfaceOpen));

        if (handlerInterface is null) return null;

        var eventType = handlerInterface.TypeArguments[0] as INamedTypeSymbol;
        if (eventType is null) return null;

        return new EventHandlerInfo(
            symbol.Name,
            symbol.ToGeneratedTypeName(),
            eventType.ToGeneratedTypeName(),
            eventType.Name,
            symbol.Locations.FirstOrDefault());
    }

    private static void EmitServiceExtensions(
        SourceProductionContext spc, List<EventHandlerInfo> handlers)
    {
        var registrations = handlers
            .Select(h => $"    services.AddScoped<global::Cms.Core.Events.ICmsEventHandler<{h.EventFullyQualifiedName}>, {h.HandlerFullyQualifiedName}>();")
            .JoinLines();

        var source = $$"""
            {{SourceWriterExtensions.GeneratedFileHeader(nameof(CmsEventHandlerSourceGenerator))}}

            using Microsoft.Extensions.DependencyInjection;

            namespace Cms.Core.Generated;

            public static class CmsEventHandlerServiceExtensions
            {
                /// <summary>
                /// Registers all ICmsEventHandler<T> implementations discovered at compile time.
                /// Handler count: {{handlers.Count}}
                /// </summary>
                public static IServiceCollection AddCmsEventHandlers(
                    this IServiceCollection services)
                {
            {{registrations}}
                    services.AddScoped<global::Cms.Core.Events.ICmsEventBus,
                        GeneratedCmsEventBus>();
                    return services;
                }
            }
            """;

        spc.AddSource("CmsEventHandlerServiceExtensions.g.cs", source);
    }

    private static void EmitEventBus(
        SourceProductionContext spc, List<EventHandlerInfo> handlers)
    {
        // Group handlers by event type for the dispatch switch
        var byEvent = handlers
            .GroupBy(h => h.EventFullyQualifiedName)
            .ToList();

        var dispatchMethods = byEvent.Select(group =>
        {
            var eventType    = group.Key;
            var handlerCalls = group
                .Select(h => $"""
                        try {{ await _sp.GetRequiredService<{h.HandlerFullyQualifiedName}>().HandleAsync(({eventType})evt, ct); }}
                        catch (Exception ex) {{ _logger.LogError(ex, "Event handler {{h.HandlerClassName}} failed"); }}
                    """)
                .JoinLines();

            return $"""
                    private async Task Dispatch{group.First().EventClassName}Async(
                        global::Cms.Core.Events.ICmsEvent evt, CancellationToken ct)
                    {{
                {handlerCalls}
                    }}
                """;
        }).JoinLines();

        var switchArms = byEvent
            .Select(g => $"    typeof({g.Key}) => Dispatch{g.First().EventClassName}Async(evt, ct),")
            .JoinLines();

        var source = $$"""
            {{SourceWriterExtensions.GeneratedFileHeader(nameof(CmsEventHandlerSourceGenerator))}}

            using Microsoft.Extensions.DependencyInjection;
            using Microsoft.Extensions.Logging;
            using Cms.Core.Events;

            namespace Cms.Core.Generated;

            /// <summary>
            /// Source-generated event bus. Dispatches to typed handlers without
            /// runtime IEnumerable scanning. AOT safe.
            /// </summary>
            public sealed class GeneratedCmsEventBus : ICmsEventBus
            {
                private readonly IServiceProvider _sp;
                private readonly ILogger<GeneratedCmsEventBus> _logger;

                public GeneratedCmsEventBus(
                    IServiceProvider sp,
                    ILogger<GeneratedCmsEventBus> logger)
                {
                    _sp = sp; _logger = logger;
                }

                public Task PublishAsync<TEvent>(TEvent evt, CancellationToken ct)
                    where TEvent : class, ICmsEvent
                    => evt.GetType() switch
                    {
            {{switchArms}}
                        _ => Task.CompletedTask  // Unknown event types are silently ignored
                    };

            {{dispatchMethods}}
            }
            """;

        spc.AddSource("GeneratedCmsEventBus.g.cs", source);
    }
}
```

---

## 10. Generator 7 — CmsAdminSectionSourceGenerator

### Responsibility

Scans for controllers decorated with `[CmsAdminSection]` and generates the `AdminMenuRegistry` population code, eliminating the need for modules to call `builder.AddAdminSection(...)` manually.

### Attribute

```csharp
// Cms.Core/Admin/CmsAdminSectionAttribute.cs
namespace Cms.Core.Admin;

[AttributeUsage(AttributeTargets.Class)]
public sealed class CmsAdminSectionAttribute : Attribute
{
    public string MenuLabel { get; set; } = "";
    public string Icon { get; set; } = "";
    public int MenuOrder { get; set; } = 100;
    public string? MenuGroup { get; set; }
    public string RequiredPermission { get; set; } = "Admin";
}
```

### Generator Implementation

```csharp
[Generator]
public sealed class CmsAdminSectionSourceGenerator : IIncrementalGenerator
{
    private const string AdminSectionAttributeFullName =
        "Cms.Core.Admin.CmsAdminSectionAttribute";

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var sections = context.SyntaxProvider
            .ForAttributeWithMetadataName(
                AdminSectionAttributeFullName,
                predicate: static (n, _) => n is ClassDeclarationSyntax,
                transform: static (ctx, ct) =>
                {
                    var symbol = (INamedTypeSymbol)ctx.TargetSymbol;
                    var attr   = symbol.GetAttribute(AdminSectionAttributeFullName)!;

                    // Extract area from [Area("...")] attribute
                    var areaAttr = symbol.GetAttribute(
                        "Microsoft.AspNetCore.Mvc.AreaAttribute");
                    var areaName = areaAttr?.ConstructorArguments.FirstOrDefault().Value?.ToString()
                        ?? "Admin";

                    return new AdminSectionInfo(
                        symbol.Name.Replace("Controller", ""),
                        areaName,
                        attr.NamedArguments.FirstOrDefault(a => a.Key == "MenuLabel").Value.Value?.ToString() ?? symbol.Name,
                        attr.NamedArguments.FirstOrDefault(a => a.Key == "Icon").Value.Value?.ToString() ?? "squares-2x2",
                        (int)(attr.NamedArguments.FirstOrDefault(a => a.Key == "MenuOrder").Value.Value ?? 100),
                        attr.NamedArguments.FirstOrDefault(a => a.Key == "MenuGroup").Value.Value?.ToString(),
                        attr.NamedArguments.FirstOrDefault(a => a.Key == "RequiredPermission").Value.Value?.ToString() ?? "Admin",
                        symbol.Locations.FirstOrDefault());
                })
            .Where(static s => s is not null)
            .Collect();

        context.RegisterSourceOutput(sections, static (spc, sections) =>
        {
            var sectionList = sections.Where(s => s is not null).Select(s => s!).ToList();
            EmitMenuRegistration(spc, sectionList);
        });
    }

    private static void EmitMenuRegistration(
        SourceProductionContext spc, List<AdminSectionInfo> sections)
    {
        var registrations = sections
            .OrderBy(s => s.MenuOrder)
            .Select(s => $$"""
                    registry.Register(new global::Cms.Core.Admin.AdminSectionDescriptor
                    {
                        ModuleName         = "{{s.ControllerName}}",
                        MenuLabel          = "{{s.MenuLabel}}",
                        Icon               = "{{s.Icon}}",
                        MenuOrder          = {{s.MenuOrder}},
                        MenuGroup          = {{(s.MenuGroup is null ? "null" : $"\"{s.MenuGroup}\"")}},
                        AreaName           = "{{s.AreaName}}",
                        ControllerName     = "{{s.ControllerName}}",
                        RequiredPermission = "{{s.RequiredPermission}}",
                    });
                """)
            .JoinLines();

        var source = $$"""
            {{SourceWriterExtensions.GeneratedFileHeader(nameof(CmsAdminSectionSourceGenerator))}}

            using Cms.Core.Admin;

            namespace Cms.Core.Generated;

            public static class CmsAdminMenuRegistration
            {
                /// <summary>
                /// Registers all [CmsAdminSection]-attributed admin sections.
                /// Generated at compile time — no controller scanning.
                /// Section count: {{sections.Count}}
                /// </summary>
                public static void RegisterAdminSections(AdminMenuRegistry registry)
                {
            {{registrations}}
                }
            }
            """;

        spc.AddSource("CmsAdminMenuRegistration.g.cs", source);
    }
}
```

---

## 11. Integration — Program.cs Generated Extensions

With all generators in place, `Program.cs` becomes a clean declaration:

```csharp
// Program.cs — Cms.Web

var builder = WebApplication.CreateBuilder(args);

builder.Services
    // ── Generated: all BlockBase subclasses → [JsonPolymorphic] + registry
    .AddCmsBlocks()

    // ── Generated: all IPageReadHook / IPageSaveHook → ordered typed factories
    .AddCmsPipelines()

    // ── Generated: all ViewComponent subclasses → typed invokers + registry
    .AddCmsViewComponents()

    // ── Generated: all [CmsModule] implementations → ConfigureServices() calls
    .AddDiscoveredCmsModules()

    // ── Generated: all ICmsEventHandler<T> → typed event bus dispatch
    .AddCmsEventHandlers()

    // Standard framework services
    .AddControllersWithViews()
    .AddViewLocalization()
    .AddOutputCache(/* ... */)
    .AddHttpContextAccessor();

// Marten — TypeLoadMode.Static uses Marten's own codegen (run 'dotnet marten codegen')
builder.Services.AddMarten(opts =>
{
    opts.Connection(connectionString);
    opts.UseSystemTextJsonForSerialization(CmsJsonContext.Default);  // Generated context
    opts.GeneratedCodeMode = TypeLoadMode.Static;
})
.UseLightweightSessions()
.ApplyAllDatabaseChangesOnStartup();

var app = builder.Build();

// ── Generated: all [CmsModule] implementations → Configure() calls
var moduleBuilder = app.Services.GetRequiredService<IModuleBuilder>();
CmsModuleConfiguration.ConfigureDiscoveredCmsModules(moduleBuilder);

// ── Generated: all [CmsAdminSection] controllers → AdminMenuRegistry population
var menuRegistry = app.Services.GetRequiredService<AdminMenuRegistry>();
CmsAdminMenuRegistration.RegisterAdminSections(menuRegistry);

app.UseRequestLocalization();
app.UseMiddleware<TenantMiddleware>();
app.UseMiddleware<CultureRedirectMiddleware>();
app.UseMiddleware<ETagMiddleware>();
app.UseOutputCache();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllerRoute("localized", "{culture:culturecode}/{controller=Home}/{action=Index}/{id?}");

app.Run();
```

---

## 12. Testing Source Generators

Source generator tests use `Microsoft.CodeAnalysis.CSharp.Testing` and `Microsoft.CodeAnalysis.Testing.Verifiers.XUnit`.

### Test Project Dependencies

```xml
<!-- Cms.Core.Tests.Generators.csproj -->
<PackageReference Include="Microsoft.CodeAnalysis.CSharp.Testing" Version="1.1.2" />
<PackageReference Include="Microsoft.CodeAnalysis.Testing.Verifiers.XUnit" Version="1.1.2" />
<PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.*" />
<PackageReference Include="xunit" Version="2.*" />
```

### Block Generator Tests

```csharp
public class BlockGeneratorTests
{
    [Fact]
    public async Task GeneratesPolymorphicAttributes_ForBlockSubclasses()
    {
        var source = """
            using Cms.Core.Blocks;
            namespace TestAssembly;

            public record HeroBlock : BlockBase
            {
                public override string BlockType => "hero";
            }

            public record RichTextBlock : BlockBase
            {
                public override string BlockType => "rich-text";
            }
            """;

        await new CSharpSourceGeneratorVerifier<CmsBlockSourceGenerator>.Test
        {
            TestState =
            {
                Sources = { source },
                GeneratedSources =
                {
                    (typeof(CmsBlockSourceGenerator),
                     "BlockBase.Polymorphic.g.cs",
                     """
                     // <auto-generated>
                     [JsonPolymorphic(TypeDiscriminatorPropertyName = "blockType")]
                     [JsonDerivedType(typeof(global::TestAssembly.HeroBlock),     "hero")]
                     [JsonDerivedType(typeof(global::TestAssembly.RichTextBlock), "rich-text")]
                     public abstract partial record BlockBase { }
                     """)
                }
            }
        }.RunAsync();
    }

    [Fact]
    public async Task ReportsDiagnosticCMS001_WhenBlockTypeMissing()
    {
        var source = """
            using Cms.Core.Blocks;
            namespace TestAssembly;

            public record BrokenBlock : BlockBase
            {
                // Missing BlockType property entirely — should not compile
                public override string BlockType => someVariable; // not a literal
            }
            """;

        await new CSharpSourceGeneratorVerifier<CmsBlockSourceGenerator>.Test
        {
            TestState =
            {
                Sources = { source },
                ExpectedDiagnostics =
                {
                    CSharpSourceGeneratorVerifier<CmsBlockSourceGenerator>
                        .Diagnostic(CmsDiagnostics.BlockTypeMissing)
                        .WithSpan(4, 1, 4, 40)
                        .WithArguments("BrokenBlock")
                }
            }
        }.RunAsync();
    }

    [Fact]
    public async Task ReportsDiagnosticCMS004_WhenDuplicateDiscriminator()
    {
        var source = """
            using Cms.Core.Blocks;
            namespace TestAssembly;

            public record BlockA : BlockBase { public override string BlockType => "hero"; }
            public record BlockB : BlockBase { public override string BlockType => "hero"; }
            """;

        await new CSharpSourceGeneratorVerifier<CmsBlockSourceGenerator>.Test
        {
            TestState =
            {
                Sources = { source },
                ExpectedDiagnostics =
                {
                    CSharpSourceGeneratorVerifier<CmsBlockSourceGenerator>
                        .Diagnostic(CmsDiagnostics.DuplicateBlockType)
                        .WithArguments("BlockA", "BlockB", "hero")
                }
            }
        }.RunAsync();
    }

    [Fact]
    public async Task GeneratesServiceExtensions_ForDiscoveredRenderers()
    {
        var source = """
            using Cms.Core.Blocks;
            namespace TestAssembly;

            public record HeroBlock : BlockBase { public override string BlockType => "hero"; }

            public class HeroBlockRenderer : BlockRenderer<HeroBlock>
            {
                public override string BlockType => "hero";
                protected override Task<IHtmlContent> RenderAsync(HeroBlock block, ViewContext ctx)
                    => Task.FromResult<IHtmlContent>(HtmlString.Empty);
            }
            """;

        await new CSharpSourceGeneratorVerifier<CmsBlockSourceGenerator>.Test
        {
            TestState =
            {
                Sources = { source },
                GeneratedSources =
                {
                    (typeof(CmsBlockSourceGenerator),
                     "CmsBlockServiceExtensions.g.cs",
                     SourceContains("services.AddScoped<global::TestAssembly.HeroBlockRenderer>()"))
                }
            }
        }.RunAsync();
    }
}
```

### Pipeline Generator Tests

```csharp
public class PipelineGeneratorTests
{
    [Fact]
    public async Task GeneratesOrderedFactory_WithCorrectOrder()
    {
        var source = """
            using Cms.Core.Pipeline;
            namespace TestAssembly;

            public class FirstHook : IPageReadHook
            {
                public int Order => -10;
                public Task ExecuteAsync(PageReadContext ctx, CancellationToken ct) => Task.CompletedTask;
            }

            public class SecondHook : IPageReadHook
            {
                public int Order => 5;
                public Task ExecuteAsync(PageReadContext ctx, CancellationToken ct) => Task.CompletedTask;
            }
            """;

        await new CSharpSourceGeneratorVerifier<CmsPipelineSourceGenerator>.Test
        {
            TestState =
            {
                Sources = { source },
                GeneratedSources =
                {
                    (typeof(CmsPipelineSourceGenerator),
                     "GeneratedReadPipelineFactory.g.cs",
                     SourceContainsInOrder(
                         "global::TestAssembly.FirstHook hook0",   // -10 first
                         "global::TestAssembly.SecondHook hook1")) // 5 second
                }
            }
        }.RunAsync();
    }
}
```

---

## 13. Diagnostics & Errors

### Full Diagnostic Reference

| ID | Severity | Trigger | Resolution |
|---|---|---|---|
| CMS001 | Error | `BlockBase` subclass has no `string BlockType => "literal"` | Add `public override string BlockType => "your-type";` |
| CMS002 | Warning | `BlockBase` subclass has no matching `IBlockRenderer<T>` | Create `class YourBlockRenderer : BlockRenderer<YourBlock>` |
| CMS003 | Error | Hook `Order` property is not a constant integer literal | Change to `public int Order => -10;` (literal only) |
| CMS004 | Error | Two `BlockBase` subclasses share the same discriminator string | Make each `BlockType` value unique across the assembly |
| CMS005 | Warning | `ViewComponent` subclass has no public `InvokeAsync` method | Add `public async Task<IViewComponentResult> InvokeAsync(...)` |
| CMS006 | Warning | `IModule` implementation missing `[CmsModule]` attribute | Add `[CmsModule]` or register the module manually in `Program.cs` |

### Suppression

Individual diagnostics can be suppressed per-file if needed:

```csharp
// For a block that intentionally shares a type string with another assembly's block
#pragma warning disable CMS004
public record OverrideHeroBlock : BlockBase
{
    public override string BlockType => "hero"; // intentional override
}
#pragma warning restore CMS004
```

---

## 14. AOT Compatibility Matrix

| Component | Generator Output | Reflection Eliminated | AOT Compatible |
|---|---|---|---|
| Block JSON polymorphism | `[JsonPolymorphic]` + `CmsJsonContext` | ✓ `BlockJsonConverter` deleted | ✓ Full AOT |
| Block renderer registry | `AddCmsBlocks()` extension | ✓ No `IEnumerable<IBlockRenderer>` scan | ✓ Full AOT |
| Pipeline hook ordering | `GeneratedXxxPipelineFactory` | ✓ No runtime `OrderBy` on hooks | ✓ Full AOT |
| ViewComponent dispatch | `GeneratedViewComponentRegistry` switch | ✓ No `MethodInfo.Invoke` | ✓ Full AOT |
| Module wiring | `AddDiscoveredCmsModules()` | ✓ No `GetTypes()` / `Activator` | ✓ Full AOT |
| Event handler routing | `GeneratedCmsEventBus` | ✓ No `IEnumerable<IHandler>` scan | ✓ Full AOT |
| Marten documents | `MartenDocumentTypes.g.cs` | ✓ No Marten startup scan | ✓ With `TypeLoadMode.Static` |
| Admin menu registration | `CmsAdminMenuRegistration` | ✓ No controller scanning | ✓ Full AOT |
| Razor template rendering | Not generated (framework) | ✗ MVC internals | ReadyToRun only |
| Runtime Razor compilation | Not generated (fundamental) | ✗ DbTemplate path | ReadyToRun only |
| MVC routing | Not generated (framework) | ✗ Framework internal | ReadyToRun only |

### Recommended Publish Configuration

```bash
# Headless API layer — fully AOT
dotnet publish Cms.Api -r linux-x64 -c Release /p:PublishAot=true

# Web + Admin layer — ReadyToRun (MVC/Razor blocks full AOT)
dotnet publish Cms.Web -r linux-x64 -c Release /p:PublishReadyToRun=true

# Run Marten code generation before publish (both targets)
dotnet marten codegen write --project Cms.Infrastructure
```

---

## 15. Agent Implementation Notes

### Agent Assignments

| Agent | Files to Implement |
|---|---|
| **GeneratorInfrastructureAgent** | `Cms.SourceGenerators.csproj`, `RoslynExtensions.cs`, `SourceWriterExtensions.cs`, `CmsDiagnostics.cs`, all model records |
| **BlockGeneratorAgent** | `CmsBlockSourceGenerator.cs` + full test coverage in `BlockGeneratorTests.cs` |
| **PipelineGeneratorAgent** | `CmsPipelineSourceGenerator.cs` + `PipelineGeneratorTests.cs` |
| **ViewComponentGeneratorAgent** | `CmsViewComponentSourceGenerator.cs` + `ViewComponentGeneratorTests.cs` |
| **ModuleGeneratorAgent** | `CmsModuleSourceGenerator.cs` + `CmsModuleAttribute.cs` + `ModuleGeneratorTests.cs` |
| **MartenGeneratorAgent** | `CmsMartenDocumentGenerator.cs` + `MartenDocumentAttribute.cs` + tests |
| **EventHandlerGeneratorAgent** | `CmsEventHandlerSourceGenerator.cs` + `EventHandlerGeneratorTests.cs` |
| **AdminSectionGeneratorAgent** | `CmsAdminSectionSourceGenerator.cs` + `CmsAdminSectionAttribute.cs` + tests |
| **IntegrationAgent** | Updates `Program.cs`, wires all generated extensions, validates end-to-end compilation |

### Implementation Order

```
Phase 1 — Infrastructure (no dependencies)
  GeneratorInfrastructureAgent

Phase 2 — Core generators (depend only on infrastructure)
  BlockGeneratorAgent          (highest priority — everything depends on BlockBase)
  MartenGeneratorAgent         (no inter-generator dependencies)

Phase 3 — Pipeline generators (depend on domain types being defined)
  PipelineGeneratorAgent
  EventHandlerGeneratorAgent

Phase 4 — UI generators (depend on MVC types)
  ViewComponentGeneratorAgent
  AdminSectionGeneratorAgent

Phase 5 — Module system (depends on all of the above)
  ModuleGeneratorAgent

Phase 6 — Integration
  IntegrationAgent
```

### Key Implementation Rules

1. **All generators must be incremental** — use `IIncrementalGenerator`, never `ISourceGenerator`. Non-incremental generators break IDE performance.

2. **Transforms must be pure functions** — `transform` lambdas passed to `CreateSyntaxProvider` must not close over mutable state. All data extraction happens inside the lambda.

3. **Cancellation tokens must be respected** — call `ct.ThrowIfCancellationRequested()` at the start of every transform.

4. **Use `ForAttributeWithMetadataName` when possible** — it is significantly faster than `CreateSyntaxProvider` with a manual attribute check. Use it for `[CmsModule]`, `[MartenDocument]`, `[CmsAdminSection]`.

5. **Generated file names must be stable** — use fixed names like `CmsBlockServiceExtensions.g.cs`, never include timestamps or GUIDs. Unstable names cause incremental cache misses.

6. **Never emit diagnostics from `RegisterSourceOutput`** — only emit diagnostics from a dedicated `RegisterSourceOutput` pipeline that only receives the relevant data, not from the same pipeline that emits source.

7. **Model records must be value-comparable** — all `IIncrementalGenerator` pipeline inputs are cached by equality. Ensure all model records implement structural equality (C# records do this automatically).

8. **Test every diagnostic** — every `DiagnosticDescriptor` in `CmsDiagnostics.cs` must have at least one test that verifies it fires correctly and one that verifies it does NOT fire for valid input.

9. **Emit `#nullable enable` in all generated files** — generated code participates in nullable analysis.

10. **Use `global::` prefix for all type references in generated code** — avoids namespace collision with generated file's own namespace.
