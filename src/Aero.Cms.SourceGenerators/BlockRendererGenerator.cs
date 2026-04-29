using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace Aero.Cms.SourceGenerators;

[Generator]
public sealed class BlockRendererGenerator : IIncrementalGenerator
{
    private const string RendererAttributeMetadataName = "Aero.Cms.Shared.Blocks.Rendering.CmsBlockRendererAttribute";
    private const string BlockMetadataAttributeMetadataName = "Aero.Cms.Abstractions.Blocks.BlockMetadataAttribute";
    private const string BlockBaseMetadataName = "Aero.Cms.Abstractions.Blocks.BlockBase";
    private const string IBlockMetadataName = "Aero.Cms.Abstractions.Blocks.IBlock";

    private static readonly DiagnosticDescriptor DuplicateBlockType = new(
        "AERO001",
        "Duplicate CMS block renderer",
        "Block type '{0}' has more than one CMS block renderer",
        "AeroCMS.BlockRendering",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor BlockParameterTypeMismatch = new(
        "AERO003",
        "CMS block renderer parameter type does not match model type",
        "Renderer '{0}' declares model type '{1}' but its Block parameter is '{2}'",
        "AeroCMS.BlockRendering",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor InvalidModelType = new(
        "AERO004",
        "CMS block renderer model type is not a block",
        "Renderer '{0}' declares model type '{1}', which must derive from BlockBase or implement IBlock",
        "AeroCMS.BlockRendering",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor MissingBlockMetadata = new(
        "AERO005",
        "CMS block renderer model type is missing BlockMetadata",
        "Renderer '{0}' declares model type '{1}', which must have BlockMetadataAttribute",
        "AeroCMS.BlockRendering",
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor DuplicateBlockModelType = new(
        "AERO006",
        "Duplicate CMS block metadata",
        "Block type '{0}' is declared by more than one CMS block model",
        "AeroCMS.BlockRendering",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var blockModels = context.SyntaxProvider.ForAttributeWithMetadataName(
            BlockMetadataAttributeMetadataName,
            static (node, _) => node is ClassDeclarationSyntax,
            static (ctx, _) => GetBlockModelCandidate(ctx))
            .Where(static candidate => candidate is not null)
            .Select(static (candidate, _) => candidate!.Value)
            .Collect();

        var renderers = context.SyntaxProvider.ForAttributeWithMetadataName(
            RendererAttributeMetadataName,
            static (node, _) => node is ClassDeclarationSyntax,
            static (ctx, _) => GetRendererCandidate(ctx))
            .Where(static candidate => candidate is not null)
            .Select(static (candidate, _) => candidate!.Value)
            .Collect();

        context.RegisterSourceOutput(blockModels, static (productionContext, candidates) =>
        {
            var descriptors = candidates
                .Select(CreateBlockModelDescriptor)
                .Where(static descriptor => descriptor is not null)
                .Select(static descriptor => descriptor!.Value)
                .OrderBy(static descriptor => descriptor.BlockType, StringComparer.Ordinal)
                .ToImmutableArray();

            ReportDuplicateBlockModelTypes(productionContext, descriptors);

            if (descriptors.Length == 0)
            {
                return;
            }

            productionContext.AddSource(
                "GeneratedBlockModelManifest.g.cs",
                SourceText.From(RenderBlockModelManifestSource(descriptors), Encoding.UTF8));

            productionContext.AddSource(
                "GeneratedBlockJsonRegistration.g.cs",
                SourceText.From(RenderBlockJsonRegistrationSource(descriptors), Encoding.UTF8));
        });

        context.RegisterSourceOutput(renderers, static (productionContext, candidates) =>
        {
            var descriptors = candidates
                .Select(candidate => CreateDescriptor(productionContext, candidate))
                .Where(static descriptor => descriptor is not null)
                .Select(static descriptor => descriptor!.Value)
                .OrderBy(static descriptor => descriptor.BlockType, StringComparer.Ordinal)
                .ToImmutableArray();

            ReportDuplicateBlockTypes(productionContext, descriptors);

            if (descriptors.Length == 0)
            {
                return;
            }

            productionContext.AddSource(
                "CmsBlockRendering.g.cs",
                SourceText.From(RenderGeneratedSource(descriptors), Encoding.UTF8));
        });
    }

    private static BlockModelCandidate? GetBlockModelCandidate(GeneratorAttributeSyntaxContext context)
    {
        if (context.TargetSymbol is not INamedTypeSymbol modelType || !IsBlockModel(modelType))
        {
            return null;
        }

        var metadata = context.Attributes.FirstOrDefault(static attr =>
            attr.AttributeClass?.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)
                == "global::" + BlockMetadataAttributeMetadataName);

        if (metadata is null || metadata.ConstructorArguments.Length < 1)
        {
            return null;
        }

        return new BlockModelCandidate(modelType, metadata);
    }

    private static RendererCandidate? GetRendererCandidate(GeneratorAttributeSyntaxContext context)
    {
        if (context.TargetSymbol is not INamedTypeSymbol rendererType)
        {
            return null;
        }

        var attribute = context.Attributes.FirstOrDefault(static attr =>
            attr.AttributeClass?.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)
                == "global::" + RendererAttributeMetadataName);

        if (attribute?.ConstructorArguments.Length != 1)
        {
            return null;
        }

        if (attribute.ConstructorArguments[0].Value is not INamedTypeSymbol modelType)
        {
            return null;
        }

        var blockParameter = rendererType
            .GetMembers("Block")
            .OfType<IPropertySymbol>()
            .FirstOrDefault(static property => property.SetMethod is not null);

        var hasNavigationParameter = rendererType
            .GetMembers("Navigation")
            .OfType<IPropertySymbol>()
            .Any(static property => property.SetMethod is not null);

        return new RendererCandidate(
            rendererType,
            modelType,
            blockParameter,
            hasNavigationParameter,
            rendererType.Locations.FirstOrDefault());
    }

    private static BlockRendererDescriptor? CreateDescriptor(
        SourceProductionContext context,
        RendererCandidate candidate)
    {
        if (!IsBlockModel(candidate.ModelType))
        {
            context.ReportDiagnostic(Diagnostic.Create(
                InvalidModelType,
                candidate.Location,
                candidate.RendererType.Name,
                candidate.ModelType.ToDisplayString()));

            return null;
        }

        if (candidate.BlockParameter is not null &&
            !SymbolEqualityComparer.Default.Equals(candidate.BlockParameter.Type, candidate.ModelType))
        {
            context.ReportDiagnostic(Diagnostic.Create(
                BlockParameterTypeMismatch,
                candidate.Location,
                candidate.RendererType.Name,
                candidate.ModelType.ToDisplayString(),
                candidate.BlockParameter.Type.ToDisplayString()));

            return null;
        }

        var metadata = candidate.ModelType
            .GetAttributes()
            .FirstOrDefault(static attr =>
                attr.AttributeClass?.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)
                    == "global::" + BlockMetadataAttributeMetadataName);

        if (metadata is null || metadata.ConstructorArguments.Length < 1)
        {
            context.ReportDiagnostic(Diagnostic.Create(
                MissingBlockMetadata,
                candidate.Location,
                candidate.RendererType.Name,
                candidate.ModelType.ToDisplayString()));

            return null;
        }

        if (metadata.ConstructorArguments[0].Value is not string blockType ||
            string.IsNullOrWhiteSpace(blockType))
        {
            return null;
        }

        var displayName = metadata.ConstructorArguments.Length > 1 &&
            metadata.ConstructorArguments[1].Value is string display
                ? display
                : blockType;

        var description = GetNamedString(metadata, "Description");
        var category = GetNamedString(metadata, "Category");
        var icon = GetNamedString(metadata, "Icon");
        var sortOrder = GetNamedInt(metadata, "SortOrder");
        var schemaVersion = GetNamedInt(metadata, "SchemaVersion", 1);
        var rendererName = candidate.RendererType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        var modelName = candidate.ModelType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        var adapterName = ToAdapterName(candidate.ModelType);
        var parameterName = candidate.BlockParameter?.Name ?? "Block";

        return new BlockRendererDescriptor(
            blockType,
            adapterName,
            modelName,
            rendererName,
            parameterName,
            displayName,
            description,
            category,
            icon,
            sortOrder,
            schemaVersion,
            candidate.HasNavigationParameter,
            candidate.Location);
    }

    private static BlockModelDescriptor? CreateBlockModelDescriptor(BlockModelCandidate candidate)
    {
        if (candidate.Metadata.ConstructorArguments[0].Value is not string blockType ||
            string.IsNullOrWhiteSpace(blockType))
        {
            return null;
        }

        var displayName = candidate.Metadata.ConstructorArguments.Length > 1 &&
            candidate.Metadata.ConstructorArguments[1].Value is string display
                ? display
                : blockType;

        return new BlockModelDescriptor(
            blockType,
            displayName,
            GetNamedString(candidate.Metadata, "Description"),
            GetNamedString(candidate.Metadata, "Category"),
            GetNamedString(candidate.Metadata, "Icon"),
            GetNamedInt(candidate.Metadata, "SortOrder"),
            GetNamedInt(candidate.Metadata, "SchemaVersion", 1),
            candidate.ModelType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat));
    }

    private static string RenderGeneratedSource(ImmutableArray<BlockRendererDescriptor> descriptors)
    {
        var source = new StringBuilder();
        source.AppendLine("// <auto-generated />");
        source.AppendLine("#nullable enable");
        source.AppendLine("using System;");
        source.AppendLine("using System.Collections.Generic;");
        source.AppendLine("using Aero.Cms.Abstractions.Blocks;");
        source.AppendLine("using Microsoft.AspNetCore.Components;");
        source.AppendLine();
        source.AppendLine("namespace Aero.Cms.Shared.Blocks.Rendering;");
        source.AppendLine();

        RenderManifestSource(source, descriptors);
        RenderRegistrySource(source, descriptors);

        return source.ToString();
    }

    private static string RenderBlockModelManifestSource(ImmutableArray<BlockModelDescriptor> descriptors)
    {
        var source = new StringBuilder();
        source.AppendLine("// <auto-generated />");
        source.AppendLine("#nullable enable");
        source.AppendLine("using System;");
        source.AppendLine("using System.Collections.Generic;");
        source.AppendLine();
        source.AppendLine("namespace Aero.Cms.Abstractions.Blocks;");
        source.AppendLine();
        source.AppendLine("/// <summary>");
        source.AppendLine("/// Describes a source-generated CMS block model registration.");
        source.AppendLine("/// </summary>");
        source.AppendLine("public readonly record struct GeneratedBlockModelDescriptor(");
        source.AppendLine("    string BlockType,");
        source.AppendLine("    string DisplayName,");
        source.AppendLine("    string? Description,");
        source.AppendLine("    string? Category,");
        source.AppendLine("    string? Icon,");
        source.AppendLine("    int SortOrder,");
        source.AppendLine("    int SchemaVersion,");
        source.AppendLine("    Type ModelType);");
        source.AppendLine();
        source.AppendLine("/// <summary>");
        source.AppendLine("/// Provides source-generated metadata for CMS block models.");
        source.AppendLine("/// </summary>");
        source.AppendLine("public static partial class GeneratedBlockModelManifest");
        source.AppendLine("{");
        source.AppendLine("    /// <summary>");
        source.AppendLine("    /// Gets all discovered CMS block models keyed by persisted block type.");
        source.AppendLine("    /// </summary>");
        source.AppendLine("    public static readonly IReadOnlyDictionary<string, GeneratedBlockModelDescriptor> Blocks =");
        source.AppendLine("        new Dictionary<string, GeneratedBlockModelDescriptor>(StringComparer.OrdinalIgnoreCase)");
        source.AppendLine("        {");

        foreach (var descriptor in descriptors)
        {
            source.AppendLine(
                $"            [\"{Escape(descriptor.BlockType)}\"] = new GeneratedBlockModelDescriptor(\"{Escape(descriptor.BlockType)}\", \"{Escape(descriptor.DisplayName)}\", {Literal(descriptor.Description)}, {Literal(descriptor.Category)}, {Literal(descriptor.Icon)}, {descriptor.SortOrder}, {descriptor.SchemaVersion}, typeof({descriptor.ModelTypeName})),");
        }

        source.AppendLine("        };");
        source.AppendLine();
        source.AppendLine("    /// <summary>");
        source.AppendLine("    /// Gets every discovered CMS block model type.");
        source.AppendLine("    /// </summary>");
        source.AppendLine("    public static readonly Type[] ModelTypes =");
        source.AppendLine("    [");

        foreach (var descriptor in descriptors)
        {
            source.AppendLine($"        typeof({descriptor.ModelTypeName}),");
        }

        source.AppendLine("    ];");
        source.AppendLine();
        source.AppendLine("    /// <summary>");
        source.AppendLine("    /// Attempts to resolve source-generated metadata for a persisted block type discriminator.");
        source.AppendLine("    /// </summary>");
        source.AppendLine("    public static bool TryGet(string blockType, out GeneratedBlockModelDescriptor descriptor)");
        source.AppendLine("        => Blocks.TryGetValue(blockType, out descriptor);");
        source.AppendLine("}");

        return source.ToString();
    }

    private static string RenderBlockJsonRegistrationSource(ImmutableArray<BlockModelDescriptor> descriptors)
    {
        var source = new StringBuilder();
        source.AppendLine("// <auto-generated />");
        source.AppendLine("#nullable enable");
        source.AppendLine("using System;");
        source.AppendLine("using System.Collections.Generic;");
        source.AppendLine("using Aero.Cms.Abstractions.Blocks;");
        source.AppendLine();
        source.AppendLine("namespace Aero.Cms.Abstractions.Blocks.Serialization;");
        source.AppendLine();
        source.AppendLine("/// <summary>");
        source.AppendLine("/// Provides source-generated JSON registration metadata for CMS block models.");
        source.AppendLine("/// </summary>");
        source.AppendLine("public static partial class GeneratedBlockJsonRegistration");
        source.AppendLine("{");
        source.AppendLine("    /// <summary>");
        source.AppendLine("    /// Gets every discovered block model type that should be represented in block JSON metadata.");
        source.AppendLine("    /// </summary>");
        source.AppendLine("    public static readonly Type[] ModelTypes =");
        source.AppendLine("    [");

        foreach (var descriptor in descriptors)
        {
            source.AppendLine($"        typeof({descriptor.ModelTypeName}),");
        }

        source.AppendLine("    ];");
        source.AppendLine();
        source.AppendLine("    /// <summary>");
        source.AppendLine("    /// Gets every discovered block collection type that should be represented in block JSON metadata.");
        source.AppendLine("    /// </summary>");
        source.AppendLine("    public static readonly Type[] CollectionTypes =");
        source.AppendLine("    [");
        source.AppendLine("        typeof(List<BlockBase>),");

        foreach (var descriptor in descriptors)
        {
            source.AppendLine($"        typeof(List<{descriptor.ModelTypeName}>),");
        }

        source.AppendLine("    ];");
        source.AppendLine("}");

        return source.ToString();
    }

    private static void RenderManifestSource(
        StringBuilder source,
        ImmutableArray<BlockRendererDescriptor> descriptors)
    {
        source.AppendLine("/// <summary>");
        source.AppendLine("/// Provides source-generated metadata for compiled CMS block renderers.");
        source.AppendLine("/// </summary>");
        source.AppendLine("public static partial class CmsBlockManifest");
        source.AppendLine("{");
        source.AppendLine("    /// <summary>");
        source.AppendLine("    /// Gets all compiled CMS block descriptors keyed by persisted block type.");
        source.AppendLine("    /// </summary>");
        source.AppendLine("    public static readonly IReadOnlyDictionary<string, CmsBlockDescriptor> Blocks =");
        source.AppendLine("        new Dictionary<string, CmsBlockDescriptor>(StringComparer.OrdinalIgnoreCase)");
        source.AppendLine("        {");

        foreach (var descriptor in descriptors)
        {
            source.AppendLine(
                $"            [\"{Escape(descriptor.BlockType)}\"] = new CmsBlockDescriptor(\"{Escape(descriptor.BlockType)}\", \"{Escape(descriptor.DisplayName)}\", {Literal(descriptor.Description)}, {Literal(descriptor.Category)}, {Literal(descriptor.Icon)}, {descriptor.SortOrder}, {descriptor.SchemaVersion}, typeof({descriptor.ModelTypeName}), typeof({descriptor.RendererTypeName}), \"{Escape(descriptor.ParameterName)}\"),");
        }

        source.AppendLine("        };");
        source.AppendLine();
        source.AppendLine("    /// <summary>");
        source.AppendLine("    /// Attempts to resolve metadata for a persisted block type discriminator.");
        source.AppendLine("    /// </summary>");
        source.AppendLine("    public static bool TryGet(string blockType, out CmsBlockDescriptor descriptor)");
        source.AppendLine("        => Blocks.TryGetValue(blockType, out descriptor!);");
        source.AppendLine("}");
        source.AppendLine();
    }

    private static void RenderRegistrySource(
        StringBuilder source,
        ImmutableArray<BlockRendererDescriptor> descriptors)
    {
        foreach (var descriptor in descriptors)
        {
            source.AppendLine($"internal sealed class {descriptor.AdapterName} : ICmsBlockRenderAdapter");
            source.AppendLine("{");
            source.AppendLine($"    public string BlockType => \"{Escape(descriptor.BlockType)}\";");
            source.AppendLine($"    public Type ModelType => typeof({descriptor.ModelTypeName});");
            source.AppendLine();
            source.AppendLine("    public RenderFragment Render(IBlock block, BlockRenderContext context)");
            source.AppendLine("    {");
            source.AppendLine("        return builder =>");
            source.AppendLine("        {");
            source.AppendLine($"            if (block is not {descriptor.ModelTypeName} typedBlock)");
            source.AppendLine("            {");
            source.AppendLine($"                builder.AddContent(0, $\"Invalid block model for '{descriptor.BlockType}'.\");");
            source.AppendLine("                return;");
            source.AppendLine("            }");
            source.AppendLine();
            source.AppendLine($"            builder.OpenComponent<{descriptor.RendererTypeName}>(1);");
            source.AppendLine($"            builder.AddAttribute(2, \"{Escape(descriptor.ParameterName)}\", typedBlock);");

            if (descriptor.HasNavigationParameter)
            {
                source.AppendLine("            builder.AddAttribute(3, \"Navigation\", context.Navigation);");
            }

            source.AppendLine("            builder.CloseComponent();");
            source.AppendLine("        };");
            source.AppendLine("    }");
            source.AppendLine("}");
            source.AppendLine();
        }

        source.AppendLine("/// <summary>");
        source.AppendLine("/// Provides source-generated block renderer adapter lookup.");
        source.AppendLine("/// </summary>");
        source.AppendLine("public static partial class CmsBlockRenderRegistry");
        source.AppendLine("{");
        source.AppendLine("    private static readonly IReadOnlyDictionary<string, ICmsBlockRenderAdapter> Adapters =");
        source.AppendLine("        new Dictionary<string, ICmsBlockRenderAdapter>(StringComparer.OrdinalIgnoreCase)");
        source.AppendLine("        {");

        foreach (var descriptor in descriptors)
        {
            source.AppendLine($"            [\"{Escape(descriptor.BlockType)}\"] = new {descriptor.AdapterName}(),");
        }

        source.AppendLine("        };");
        source.AppendLine();
        source.AppendLine("    /// <summary>");
        source.AppendLine("    /// Attempts to resolve the compiled render adapter for a block type discriminator.");
        source.AppendLine("    /// </summary>");
        source.AppendLine("    public static bool TryGet(string blockType, out ICmsBlockRenderAdapter adapter)");
        source.AppendLine("        => Adapters.TryGetValue(blockType, out adapter!);");
        source.AppendLine("}");
    }

    private static void ReportDuplicateBlockTypes(
        SourceProductionContext context,
        ImmutableArray<BlockRendererDescriptor> descriptors)
    {
        foreach (var duplicateGroup in descriptors
            .GroupBy(static descriptor => descriptor.BlockType, StringComparer.OrdinalIgnoreCase)
            .Where(static group => group.Count() > 1))
        {
            foreach (var descriptor in duplicateGroup)
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    DuplicateBlockType,
                    descriptor.Location,
                    descriptor.BlockType));
            }
        }
    }

    private static void ReportDuplicateBlockModelTypes(
        SourceProductionContext context,
        ImmutableArray<BlockModelDescriptor> descriptors)
    {
        foreach (var duplicateGroup in descriptors
            .GroupBy(static descriptor => descriptor.BlockType, StringComparer.OrdinalIgnoreCase)
            .Where(static group => group.Count() > 1))
        {
            foreach (var descriptor in duplicateGroup)
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    DuplicateBlockModelType,
                    Location.None,
                    descriptor.BlockType));
            }
        }
    }

    private static bool IsBlockModel(INamedTypeSymbol modelType)
    {
        if (modelType.AllInterfaces.Any(static candidate =>
                candidate.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)
                    == "global::" + IBlockMetadataName))
        {
            return true;
        }

        for (var current = modelType.BaseType; current is not null; current = current.BaseType)
        {
            if (current.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)
                == "global::" + BlockBaseMetadataName)
            {
                return true;
            }
        }

        return false;
    }

    private static string? GetNamedString(AttributeData metadata, string name)
    {
        foreach (var namedArgument in metadata.NamedArguments)
        {
            if (namedArgument.Key == name && namedArgument.Value.Value is string value)
            {
                return value;
            }
        }

        return null;
    }

    private static int GetNamedInt(AttributeData metadata, string name, int defaultValue = 0)
    {
        foreach (var namedArgument in metadata.NamedArguments)
        {
            if (namedArgument.Key == name && namedArgument.Value.Value is int value)
            {
                return value;
            }
        }

        return defaultValue;
    }

    private static string ToAdapterName(INamedTypeSymbol modelType)
    {
        var name = modelType.Name.EndsWith("Block", StringComparison.Ordinal)
            ? modelType.Name.Substring(0, modelType.Name.Length - "Block".Length)
            : modelType.Name;

        return SanitizeIdentifier(name) + "BlockRenderAdapter";
    }

    private static string SanitizeIdentifier(string value)
    {
        var builder = new StringBuilder(value.Length);

        foreach (var character in value)
        {
            builder.Append(char.IsLetterOrDigit(character) || character == '_' ? character : '_');
        }

        return builder.Length == 0 ? "Generated" : builder.ToString();
    }

    private static string Escape(string value)
        => value.Replace("\\", "\\\\").Replace("\"", "\\\"");

    private static string Literal(string? value)
        => value is null ? "null" : $"\"{Escape(value)}\"";

    private readonly struct RendererCandidate
    {
        public RendererCandidate(
            INamedTypeSymbol rendererType,
            INamedTypeSymbol modelType,
            IPropertySymbol? blockParameter,
            bool hasNavigationParameter,
            Location? location)
        {
            RendererType = rendererType;
            ModelType = modelType;
            BlockParameter = blockParameter;
            HasNavigationParameter = hasNavigationParameter;
            Location = location;
        }

        public INamedTypeSymbol RendererType { get; }

        public INamedTypeSymbol ModelType { get; }

        public IPropertySymbol? BlockParameter { get; }

        public bool HasNavigationParameter { get; }

        public Location? Location { get; }
    }

    private readonly struct BlockModelCandidate
    {
        public BlockModelCandidate(INamedTypeSymbol modelType, AttributeData metadata)
        {
            ModelType = modelType;
            Metadata = metadata;
        }

        public INamedTypeSymbol ModelType { get; }

        public AttributeData Metadata { get; }
    }

    private readonly struct BlockModelDescriptor
    {
        public BlockModelDescriptor(
            string blockType,
            string displayName,
            string? description,
            string? category,
            string? icon,
            int sortOrder,
            int schemaVersion,
            string modelTypeName)
        {
            BlockType = blockType;
            DisplayName = displayName;
            Description = description;
            Category = category;
            Icon = icon;
            SortOrder = sortOrder;
            SchemaVersion = schemaVersion;
            ModelTypeName = modelTypeName;
        }

        public string BlockType { get; }

        public string DisplayName { get; }

        public string? Description { get; }

        public string? Category { get; }

        public string? Icon { get; }

        public int SortOrder { get; }

        public int SchemaVersion { get; }

        public string ModelTypeName { get; }
    }

    private readonly struct BlockRendererDescriptor
    {
        public BlockRendererDescriptor(
            string blockType,
            string adapterName,
            string modelTypeName,
            string rendererTypeName,
            string parameterName,
            string displayName,
            string? description,
            string? category,
            string? icon,
            int sortOrder,
            int schemaVersion,
            bool hasNavigationParameter,
            Location? location)
        {
            BlockType = blockType;
            AdapterName = adapterName;
            ModelTypeName = modelTypeName;
            RendererTypeName = rendererTypeName;
            ParameterName = parameterName;
            DisplayName = displayName;
            Description = description;
            Category = category;
            Icon = icon;
            SortOrder = sortOrder;
            SchemaVersion = schemaVersion;
            HasNavigationParameter = hasNavigationParameter;
            Location = location;
        }

        public string BlockType { get; }

        public string AdapterName { get; }

        public string ModelTypeName { get; }

        public string RendererTypeName { get; }

        public string ParameterName { get; }

        public string DisplayName { get; }

        public string? Description { get; }

        public string? Category { get; }

        public string? Icon { get; }

        public int SortOrder { get; }

        public int SchemaVersion { get; }

        public bool HasNavigationParameter { get; }

        public Location? Location { get; }
    }
}
