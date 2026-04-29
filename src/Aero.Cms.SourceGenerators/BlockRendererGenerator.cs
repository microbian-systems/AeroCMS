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

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var renderers = context.SyntaxProvider.ForAttributeWithMetadataName(
            RendererAttributeMetadataName,
            static (node, _) => node is ClassDeclarationSyntax,
            static (ctx, _) => GetRendererCandidate(ctx))
            .Where(static candidate => candidate is not null)
            .Select(static (candidate, _) => candidate!.Value)
            .Collect();

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
            candidate.HasNavigationParameter,
            candidate.Location);
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
                $"            [\"{Escape(descriptor.BlockType)}\"] = new CmsBlockDescriptor(\"{Escape(descriptor.BlockType)}\", \"{Escape(descriptor.DisplayName)}\", {Literal(descriptor.Description)}, {Literal(descriptor.Category)}, {Literal(descriptor.Icon)}, {descriptor.SortOrder}, typeof({descriptor.ModelTypeName}), typeof({descriptor.RendererTypeName}), \"{Escape(descriptor.ParameterName)}\"),");
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

    private static int GetNamedInt(AttributeData metadata, string name)
    {
        foreach (var namedArgument in metadata.NamedArguments)
        {
            if (namedArgument.Key == name && namedArgument.Value.Value is int value)
            {
                return value;
            }
        }

        return 0;
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

        public bool HasNavigationParameter { get; }

        public Location? Location { get; }
    }
}
