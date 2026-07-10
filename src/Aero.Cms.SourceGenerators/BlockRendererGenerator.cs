using System.Collections.Immutable;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace Aero.Cms.SourceGenerators;

/// <summary>
/// Represents a class for BlockRendererGenerator.
/// </summary>
[Generator]
public sealed class BlockRendererGenerator : IIncrementalGenerator
{
    private const string RendererAttributeMetadataName = "Aero.Cms.Abstractions.Blocks.Rendering.CmsBlockRendererAttribute";
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

        /// <summary>
    /// Initialize method.
    /// </summary>
public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var blockModels = context.SyntaxProvider.CreateSyntaxProvider(
            static (node, _) => node is ClassDeclarationSyntax declaration && declaration.AttributeLists.Count > 0,
            static (ctx, _) => GetBlockModelCandidate(ctx))
            .Where(static candidate => candidate is not null)
            .Select(static (candidate, _) => candidate!.Value)
            .Collect();

        var assemblyName = context.CompilationProvider
            .Select(static (compilation, _) => compilation.AssemblyName ?? string.Empty);

        var renderers = context.SyntaxProvider.ForAttributeWithMetadataName(
            RendererAttributeMetadataName,
            static (node, _) => node is ClassDeclarationSyntax,
            static (ctx, _) => GetRendererCandidate(ctx))
            .Where(static candidate => candidate is not null)
            .Select(static (candidate, _) => candidate!.Value)
            .Collect();

        // Cross-assembly discovery pipeline: finds BlockBase subclasses from referenced assemblies
        var crossAssemblyBlockData = context.CompilationProvider
            .Select(static (compilation, _) =>
            {
                var blockMetaAttr = compilation.GetTypeByMetadataName(BlockMetadataAttributeMetadataName);
                var blockBaseType = compilation.GetTypeByMetadataName(BlockBaseMetadataName);
                if (blockMetaAttr is null || blockBaseType is null)
                    return new CrossAssemblyBlockData(ImmutableArray<DiscoveredBlockType>.Empty, "");

                var results = new List<DiscoveredBlockType>();

                foreach (var module in compilation.Assembly.Modules)
                {
                    CollectBlockTypes(module.GlobalNamespace, blockMetaAttr, blockBaseType, results);
                }

                foreach (var reference in compilation.References)
                {
                    if (compilation.GetAssemblyOrModuleSymbol(reference) is IAssemblySymbol asm)
                    {
                        CollectBlockTypes(asm.GlobalNamespace, blockMetaAttr, blockBaseType, results);
                    }
                }

                return new CrossAssemblyBlockData(results.ToImmutableArray(), compilation.AssemblyName ?? string.Empty);
            });

        context.RegisterSourceOutput(blockModels.Combine(assemblyName), static (productionContext, source) =>
        {
            var (candidates, assembly) = source;
            if (!string.Equals(assembly, "Aero.Cms.Abstractions", StringComparison.Ordinal))
            {
                return;
            }

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

            // Only emit BlockBase.Polymorphic.g.cs from the LOCAL pipeline so it only
            // fires in the project where BlockBase is declared in source (Aero.Cms.Abstractions).
            // Cross-assembly pipelines see types from DLL references, which can't extend
            // partial classes across assembly boundaries.
            productionContext.AddSource(
                "BlockBase.Polymorphic.g.cs",
                SourceText.From(RenderBlockBasePolymorphic(descriptors), Encoding.UTF8));

            // Emit GeneratedBlockFactory.g.cs — AOT-safe factory that replaces
            // Activator.CreateInstance(Type) for block instantiation.
            // The calling code in BlockEditingService uses this via the block type
            // discriminator string, eliminating runtime reflection.
            productionContext.AddSource(
                "GeneratedBlockFactory.g.cs",
                SourceText.From(RenderBlockFactory(descriptors), Encoding.UTF8));
        });

        context.RegisterSourceOutput(renderers.Combine(assemblyName), static (productionContext, source) =>
        {
            var (candidates, assembly) = source;
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

            if (!string.Equals(assembly, "Aero.Cms.Shared", StringComparison.Ordinal))
            {
                return;
            }

            productionContext.AddSource(
                "CmsBlockRendering.g.cs",
                SourceText.From(RenderGeneratedSource(descriptors), Encoding.UTF8));
        });

        // Neo editor catalog — emitted from the renderers pipeline so it lands
        // in Aero.Cms.Shared where NeoEditorCatalogProvider lives.
        context.RegisterSourceOutput(renderers.Combine(assemblyName), static (productionContext, source) =>
        {
            var (candidates, assembly) = source;
            if (!string.Equals(assembly, "Aero.Cms.Shared", StringComparison.Ordinal))
            {
                return;
            }

            var descriptors = candidates
                .Select(candidate => CreateDescriptor(productionContext, candidate))
                .Where(static descriptor => descriptor is not null)
                .Select(static descriptor => descriptor!.Value)
                .OrderBy(static descriptor => descriptor.BlockType, StringComparer.Ordinal)
                .ToImmutableArray();

            if (descriptors.Length == 0)
            {
                return;
            }

            productionContext.AddSource(
                "GeneratedNeoEditorCatalog.g.cs",
                SourceText.From(RenderNeoEditorCatalog(descriptors), Encoding.UTF8));
        });

        // NOTE: GeneratedBlockJsonContext.g.cs emission is DEFERRED.
        // The RenderGeneratedContext method and crossAssemblyBlockData pipeline
        // are kept as infrastructure for future use when the Roslyn source
        // generator chaining limitation is resolved (dotnet/roslyn#57239).
        // Currently, STJ's JsonSourceGenerator cannot see [JsonSerializable]
        // attributes emitted by another generator in the same compilation.
        // See docs/source-generator-chaining-limitation.md for details.
        // 
        // To re-enable context emission:
        // 1. Remove/comment the return statement below
        // 2. Ensure types in the shim project can be resolved
        // 3. Verify STJ's source generator produces the implementation
        context.RegisterSourceOutput(crossAssemblyBlockData, static (spc, data) =>
        {
            if (data.Types.IsDefaultOrEmpty) return;
            // Deferred: uncomment when generator chaining is fixed
            // spc.AddSource("GeneratedBlockJsonContext.g.cs",
            //     SourceText.From(RenderGeneratedContext(data.Types), Encoding.UTF8));
        });
    }

    private static BlockModelCandidate? GetBlockModelCandidate(GeneratorSyntaxContext context)
    {
        if (context.Node is not ClassDeclarationSyntax classDeclaration ||
            context.SemanticModel.GetDeclaredSymbol(classDeclaration) is not INamedTypeSymbol modelType ||
            !IsBlockModel(modelType))
        {
            return null;
        }

        var metadata = modelType.GetAttributes().FirstOrDefault(static attr =>
            attr.AttributeClass?.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)
                == "global::" + BlockMetadataAttributeMetadataName);

        if (metadata is null || metadata.ConstructorArguments.Length < 1)
        {
            return null;
        }

        // Extract public settable properties
        var properties = ExtractPropertyDescriptors(modelType);

        // Resolve editor preview + property editor types by naming convention.
        // All current Neo editor previews live under AeroUi.Hero01.
        var compilation = context.SemanticModel.Compilation;
        var modelName = modelType.Name; // e.g. "Hero01Block"
        var editorPreviewName = modelName + "EditorPreview";
        var propertyEditorName = modelName + "Editor";
        var previewBase = "Aero.Cms.Shared.Pages.Manager.PageEditor.AeroUi.Hero01.";

        var editorPreviewType = compilation.GetTypeByMetadataName(previewBase + editorPreviewName);
        var propertyEditorType = compilation.GetTypeByMetadataName(previewBase + propertyEditorName);

        return new BlockModelCandidate(
            modelType,
            metadata,
            properties.ToImmutableArray(),
            editorPreviewType?.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
            propertyEditorType?.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat));
    }

    private static ImmutableArray<PropertyDescriptor>.Builder ExtractPropertyDescriptors(INamedTypeSymbol modelType)
    {
        var builder = ImmutableArray.CreateBuilder<PropertyDescriptor>();

        foreach (var member in modelType.GetMembers())
        {
            if (member is not IPropertySymbol prop)
                continue;

            // Skip BlockType, Order, Id (inherited)
            if (prop is { IsStatic: true } or { SetMethod: null } or { IsIndexer: true })
                continue;
            if (prop.Name is "Id" or "Order")
                continue;
            // Skip visitor + abstract members
            if (prop is { IsAbstract: true } or { IsOverride: true } or { IsVirtual: true })
                continue;

            var label = PascalToLabel(prop.Name);
            var propertyTypeName = prop.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
            var fieldType = MapClrTypeToFieldType(prop);
            builder.Add(new PropertyDescriptor(prop.Name, label, propertyTypeName, fieldType));
        }

        return builder;
    }

    private static string MapClrTypeToFieldType(IPropertySymbol prop)
    {
        var type = prop.Type;
        var typeName = type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        var name = prop.Name;

        // Check for URL-named properties
        if (name.EndsWith("Url", StringComparison.OrdinalIgnoreCase) ||
            name.EndsWith("Uri", StringComparison.OrdinalIgnoreCase) ||
            typeName is "string" && (name.Contains("url", StringComparison.OrdinalIgnoreCase)))
            return "Url";

        // Simple type mapping
        return type.SpecialType switch
        {
            SpecialType.System_String
                when name.Contains("escription", StringComparison.OrdinalIgnoreCase)
                  || name.Contains("ummary", StringComparison.OrdinalIgnoreCase)
                  || name.Contains("ontent", StringComparison.OrdinalIgnoreCase)
                  || name.Contains("emplate", StringComparison.OrdinalIgnoreCase)
                  || name.Contains("html", StringComparison.OrdinalIgnoreCase)
                  || name.Contains("Html", StringComparison.Ordinal)
                => "TextArea",
            SpecialType.System_String
                => "Text",
            SpecialType.System_Boolean => "Boolean",
            SpecialType.System_Int32 or SpecialType.System_Int64
                or SpecialType.System_Double or SpecialType.System_Decimal
                or SpecialType.System_Single
                => "Number",
            _ => typeName switch
            {
                "System.DateTime" or "System.DateTimeOffset" => "DateTime",
                "System.Text.Json.JsonDocument" or "System.Text.Json.JsonElement" => "Json",
                _ when IsStringListType(type) => "StringList",
                _ => "Text"
            }
        };
    }

    private static bool IsStringListType(ITypeSymbol type)
    {
        var typeName = type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        // string[]
        if (type is IArrayTypeSymbol ats
            && ats.ElementType.SpecialType == SpecialType.System_String)
            return true;
        // List<string>, IList<string>, IReadOnlyList<string>, IEnumerable<string>, ICollection<string>
        if (type is INamedTypeSymbol nts
            && nts.IsGenericType
            && nts.TypeArguments.Length == 1
            && nts.TypeArguments[0].SpecialType == SpecialType.System_String)
            return true;
        return false;
    }

    private static string PascalToLabel(string name)
    {
        if (string.IsNullOrEmpty(name)) return name;
        var builder = new StringBuilder(name.Length + 4);
        builder.Append(name[0]);
        for (int i = 1; i < name.Length; i++)
        {
            // Insert space before uppercase that follows lowercase or before last uppercase in acronym at end
            var prev = name[i - 1];
            var cur = name[i];
            if (char.IsUpper(cur) && (char.IsLower(prev) || (i + 1 < name.Length && char.IsLower(name[i + 1]))))
            {
                builder.Append(' ');
            }
            builder.Append(cur);
        }
        return builder.ToString();
    }

    private static RendererCandidate? GetRendererCandidate(GeneratorAttributeSyntaxContext context)
    {
        if (context.TargetSymbol is not INamedTypeSymbol rendererType)
        {
            return null;
        }

        var attribute = context.Attributes.FirstOrDefault(static attr =>
        {
            var attributeName = attr.AttributeClass?.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
            return attributeName == "global::" + RendererAttributeMetadataName;
        });

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
            candidate.ModelType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
            candidate.PropertyDescriptors,
            candidate.EditorPreviewTypeName,
            candidate.PropertyEditorTypeName);
    }

    private static string RenderGeneratedSource(ImmutableArray<BlockRendererDescriptor> descriptors)
    {
        var source = new StringBuilder();
        source.AppendLine("// <auto-generated />");
        source.AppendLine("#pragma warning disable CS1591");
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
        source.AppendLine("#pragma warning restore CS1591");

        return source.ToString();
    }

    private static string RenderBlockModelManifestSource(ImmutableArray<BlockModelDescriptor> descriptors)
    {
        var source = new StringBuilder();
        source.AppendLine("// <auto-generated />");
        source.AppendLine("#pragma warning disable CS1591");
        source.AppendLine("#nullable enable");
        source.AppendLine("using System.Collections.Generic;");
        source.AppendLine();
        source.AppendLine("namespace Aero.Cms.Abstractions.Blocks;");
        source.AppendLine();
        source.AppendLine("/// <summary>");
        source.AppendLine("/// Provides source-generated metadata for CMS block models.");
        source.AppendLine("/// </summary>");
        source.AppendLine("public static partial class GeneratedBlockModelManifest");
        source.AppendLine("{");
        source.AppendLine("    static partial void Populate(Dictionary<string, GeneratedBlockModelDescriptor> blocks)");
        source.AppendLine("    {");

        foreach (var descriptor in descriptors)
        {
            source.AppendLine(
                $"        blocks[\"{Escape(descriptor.BlockType)}\"] = new GeneratedBlockModelDescriptor(\"{Escape(descriptor.BlockType)}\", \"{Escape(descriptor.DisplayName)}\", {Literal(descriptor.Description)}, {Literal(descriptor.Category)}, {Literal(descriptor.Icon)}, {descriptor.SortOrder}, {descriptor.SchemaVersion}, typeof({descriptor.ModelTypeName}));");
        }

        source.AppendLine("    }");
        source.AppendLine("}");
        source.AppendLine("#pragma warning restore CS1591");

        return source.ToString();
    }

    private static string RenderBlockJsonRegistrationSource(ImmutableArray<BlockModelDescriptor> descriptors)
    {
        var source = new StringBuilder();
        source.AppendLine("// <auto-generated />");
        source.AppendLine("#pragma warning disable CS1591");
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
        source.AppendLine("#pragma warning restore CS1591");

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
        source.AppendLine("    static partial void Populate(Dictionary<string, CmsBlockDescriptor> blocks)");
        source.AppendLine("    {");

        foreach (var descriptor in descriptors)
        {
            source.AppendLine(
                $"        blocks[\"{Escape(descriptor.BlockType)}\"] = new CmsBlockDescriptor(\"{Escape(descriptor.BlockType)}\", \"{Escape(descriptor.DisplayName)}\", {Literal(descriptor.Description)}, {Literal(descriptor.Category)}, {Literal(descriptor.Icon)}, {descriptor.SortOrder}, {descriptor.SchemaVersion}, typeof({descriptor.ModelTypeName}), typeof({descriptor.RendererTypeName}), \"{Escape(descriptor.ParameterName)}\");");
        }

        source.AppendLine("    }");
        source.AppendLine("}");
        source.AppendLine();
    }

    private static void RenderRegistrySource(
        StringBuilder source,
        ImmutableArray<BlockRendererDescriptor> descriptors)
    {
        foreach (var descriptor in descriptors)
        {
            source.AppendLine($"internal sealed class {descriptor.AdapterName} : ICmsBlockRenderAdapter, ICmsBlockRenderAdapter<{descriptor.ModelTypeName}>");
            source.AppendLine("{");
            source.AppendLine($"    public string BlockType => \"{Escape(descriptor.BlockType)}\";");
            source.AppendLine($"    public Type ModelType => typeof({descriptor.ModelTypeName});");
            source.AppendLine();
            source.AppendLine($"    RenderFragment ICmsBlockRenderAdapter<{descriptor.ModelTypeName}>.Render({descriptor.ModelTypeName} block, BlockRenderContext context)");
            source.AppendLine("    {");
            source.AppendLine("        return builder =>");
            source.AppendLine("        {");
            source.AppendLine($"            builder.OpenComponent<{descriptor.RendererTypeName}>(1);");
            source.AppendLine($"            builder.AddAttribute(2, \"{Escape(descriptor.ParameterName)}\", block);");
            if (descriptor.HasNavigationParameter)
            {
                source.AppendLine("            builder.AddAttribute(3, \"Navigation\", context.Navigation);");
            }
            source.AppendLine("            builder.CloseComponent();");
            source.AppendLine("        };");
            source.AppendLine("    }");
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
        source.AppendLine("    static partial void PopulateAdapters(Dictionary<string, ICmsBlockRenderAdapter> adapters)");
        source.AppendLine("    {");

        foreach (var descriptor in descriptors)
        {
            source.AppendLine($"        adapters[\"{Escape(descriptor.BlockType)}\"] = new {descriptor.AdapterName}();");
        }

        source.AppendLine("    }");
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

        // Include the containing namespace to avoid collisions when
        // multiple types share the same short name (e.g. Neo.ImageBlock
        // vs common ImageBlock).
        var ns = modelType.ContainingNamespace?.ToDisplayString() ?? "";
        if (!string.IsNullOrEmpty(ns) && ns != "<global namespace>")
        {
            var nsPart = ns.Replace(".", "_");
            name = $"{nsPart}_{name}";
        }

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

    private static void CollectBlockTypes(
        INamespaceSymbol ns,
        INamedTypeSymbol blockMetaAttr,
        INamedTypeSymbol blockBaseType,
        List<DiscoveredBlockType> results)
    {
        foreach (var member in ns.GetTypeMembers())
        {
            if (member.IsAbstract) continue;
            if (IsDerivedFromBlockBase(member, blockBaseType) && HasAttribute(member, blockMetaAttr))
            {
                var metadata = member.GetAttributes()
                    .First(a => SymbolEqualityComparer.Default.Equals(a.AttributeClass, blockMetaAttr));

                results.Add(new DiscoveredBlockType(
                    member.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
                    (string)metadata.ConstructorArguments[0].Value!,
                    metadata.ConstructorArguments.Length > 1 ? (string)metadata.ConstructorArguments[1].Value! : ""));
            }
        }

        foreach (var nested in ns.GetNamespaceMembers())
            CollectBlockTypes(nested, blockMetaAttr, blockBaseType, results);
    }

    private static bool IsDerivedFromBlockBase(ITypeSymbol type, INamedTypeSymbol blockBaseType)
    {
        for (var current = type.BaseType; current is not null; current = current.BaseType)
        {
            if (SymbolEqualityComparer.Default.Equals(current, blockBaseType))
                return true;
        }
        return false;
    }

    private static bool HasAttribute(ISymbol symbol, INamedTypeSymbol attrType)
    {
        return symbol.GetAttributes().Any(a =>
            SymbolEqualityComparer.Default.Equals(a.AttributeClass, attrType));
    }

    private static string RenderGeneratedContext(ImmutableArray<DiscoveredBlockType> types)
    {
        var sb = new StringBuilder();
        sb.AppendLine("// <auto-generated />");
        sb.AppendLine("#pragma warning disable CS1591");
        sb.AppendLine("#nullable enable");
        sb.AppendLine("using System;");
        sb.AppendLine("using System.Collections.Generic;");
        sb.AppendLine("using System.Text.Json;");
        sb.AppendLine("using System.Text.Json.Serialization;");
        sb.AppendLine();
        sb.AppendLine("namespace Aero.Cms.Abstractions.Blocks.Serialization;");
        sb.AppendLine();
        sb.AppendLine("/// <summary>");
        sb.AppendLine("/// Source-generated JSON serializer context for all CMS block models.");
        sb.AppendLine("/// Replaces the hand-maintained BlockJsonContext.cs.");
        sb.AppendLine("/// </summary>");
        sb.AppendLine("[JsonSourceGenerationOptions(");
        sb.AppendLine("    WriteIndented = false,");
        sb.AppendLine("    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,");
        sb.AppendLine("    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,");
        sb.AppendLine("    GenerationMode = JsonSourceGenerationMode.Default | JsonSourceGenerationMode.Metadata)]");
        sb.AppendLine("[JsonSerializable(typeof(BlockBase))]");
        sb.AppendLine("[JsonSerializable(typeof(List<BlockBase>))]");

        foreach (var type in types)
        {
            sb.AppendLine($"[JsonSerializable(typeof({type.FullyQualifiedName}))]");
            sb.AppendLine($"[JsonSerializable(typeof(List<{type.FullyQualifiedName}>))]");
        }

        sb.AppendLine("[JsonSerializable(typeof(Dictionary<string, string>))]");
        sb.AppendLine("[JsonSerializable(typeof(JsonElement))]");
        sb.AppendLine("[JsonSerializable(typeof(JsonDocument))]");
        sb.AppendLine("[JsonSerializable(typeof(string))]");
        sb.AppendLine("[JsonSerializable(typeof(int))]");
        sb.AppendLine("[JsonSerializable(typeof(long))]");
        sb.AppendLine("[JsonSerializable(typeof(bool))]");
        sb.AppendLine("[JsonSerializable(typeof(DateTime))]");
        sb.AppendLine();
            sb.AppendLine("public partial class GeneratedBlockJsonContext : JsonSerializerContext");
        sb.AppendLine("{");
        sb.AppendLine("}");
        sb.AppendLine("#pragma warning restore CS1591");

        return sb.ToString();
    }

    private static string RenderBlockBasePolymorphic(ImmutableArray<BlockModelDescriptor> descriptors)
    {
        var sb = new StringBuilder();
        sb.AppendLine("// <auto-generated />");
        sb.AppendLine("#pragma warning disable CS1591");
        sb.AppendLine("#nullable enable");
        sb.AppendLine("using System.Text.Json.Serialization;");
        sb.AppendLine();
        sb.AppendLine("namespace Aero.Cms.Abstractions.Blocks;");
        sb.AppendLine();
        sb.AppendLine("/// <summary>");
        sb.AppendLine("/// Source-generated polymorphic type discriminators for all CMS block models.");
        sb.AppendLine("/// Replaces the hand-maintained [JsonDerivedType] list on BlockBase.cs.");
        sb.AppendLine("/// </summary>");
        sb.AppendLine("[JsonPolymorphic(TypeDiscriminatorPropertyName = \"$blockType\")]");

        foreach (var desc in descriptors)
        {
            sb.AppendLine($"[JsonDerivedType(typeof({desc.ModelTypeName}), \"{Escape(desc.BlockType)}\")]");
        }

        sb.AppendLine("public abstract partial class BlockBase");
        sb.AppendLine("{");
        sb.AppendLine("}");
        sb.AppendLine("#pragma warning restore CS1591");

        return sb.ToString();
    }

    private static string RenderBlockFactory(ImmutableArray<BlockModelDescriptor> descriptors)
    {
        var sb = new StringBuilder();
        sb.AppendLine("// <auto-generated />");
        sb.AppendLine("#pragma warning disable CS1591");
        sb.AppendLine("#nullable enable");
        sb.AppendLine("using Aero.Cms.Abstractions.Blocks;");
        sb.AppendLine();
        sb.AppendLine("namespace Aero.Cms.Abstractions.Blocks.Editing;");
        sb.AppendLine();
        sb.AppendLine("/// <summary>");
        sb.AppendLine("/// Source-generated AOT-safe factory for creating block instances.");
        sb.AppendLine("/// Replaces Activator.CreateInstance-based reflection in BlockEditingService.");
        sb.AppendLine("/// </summary>");
        sb.AppendLine("public static partial class GeneratedBlockFactory");
        sb.AppendLine("{");
        sb.AppendLine("    static partial void CreateGeneratedByTypeName(string typeName, ref BlockBase? block)");
        sb.AppendLine("    {");
        sb.AppendLine("        block = typeName switch");
        sb.AppendLine("    {");

        foreach (var desc in descriptors)
        {
            sb.AppendLine($"            \"{Escape(desc.BlockType)}\" => new {desc.ModelTypeName}(),");
        }

        sb.AppendLine("            _ => null");
        sb.AppendLine("        };");
        sb.AppendLine("    }");
        sb.AppendLine("}");
        sb.AppendLine("#pragma warning restore CS1591");

        return sb.ToString();
    }

    private static string RenderNeoEditorCatalog(
        ImmutableArray<BlockRendererDescriptor> descriptors)
    {
        var sb = new StringBuilder();
        sb.AppendLine("// <auto-generated />");
        sb.AppendLine("#pragma warning disable CS1591");
        sb.AppendLine("#nullable enable");
        sb.AppendLine("using System;");
        sb.AppendLine("using System.Collections.Generic;");
        sb.AppendLine("using Aero.Cms.Shared.Pages.Manager.PageEditor.Catalog;");
        sb.AppendLine();
        sb.AppendLine("namespace Aero.Cms.Shared.Pages.Manager.PageEditor.Catalog;");
        sb.AppendLine();
        sb.AppendLine("/// <summary>");
        sb.AppendLine("/// Source-generated catalog population for NeoEditorCatalogProvider.");
        sb.AppendLine("/// Replaces the hand-maintained constructor with auto-discovered block metadata.");
        sb.AppendLine("/// Each adapter implementation also implements ICmsBlockRenderAdapter<TBlock> for typed access.");
        sb.AppendLine("/// </summary>");
        sb.AppendLine("public partial class NeoEditorCatalogProvider");
        sb.AppendLine("{");
        sb.AppendLine("    partial void PopulateGeneratedCatalog(List<NeoEditorCatalogItem> items)");
        sb.AppendLine("    {");

        foreach (var rd in descriptors)
        {
            var section = MapCatalogSection(rd.Category);

            sb.AppendLine($"        // {rd.DisplayName}");
            sb.AppendLine($"        items.Add(new NeoEditorCatalogItem");
            sb.AppendLine("        {");
            sb.AppendLine($"            CatalogId = \"{Escape(rd.BlockType)}\",");
            sb.AppendLine($"            DisplayName = \"{Escape(rd.DisplayName)}\",");
            sb.AppendLine($"            Description = {Literal(rd.Description)},");
            sb.AppendLine($"            Section = NeoEditorCatalogSection.{section},");
            sb.AppendLine($"            Kind = NeoEditorCatalogKind.Block,");
            sb.AppendLine($"            IconName = \"{Escape(rd.Icon ?? "box")}\",");
            sb.AppendLine($"            SortOrder = {rd.SortOrder},");
            sb.AppendLine($"            PublicStaticSsrSafe = true,");
            // Editor preview types are deferred — source generator can't reliably
            // detect which editor previews exist via naming convention alone.
            // The hand-coded NeoEditorCatalogProvider.cs is now partial;
            // add EditorPreviewComponentType / PropertyEditorComponentType
            // in a separate partial file or use a naming convention at runtime.
            sb.AppendLine($"            EditorPreviewComponentType = null,");
            sb.AppendLine($"            PropertyEditorComponentType = null,");

            sb.AppendLine($"            PublicRendererComponentType = typeof({rd.RendererTypeName}),");
            sb.AppendLine("            PropertyDefinitions = new List<NeoPropertyDefinition>()");
            sb.AppendLine("        });");
            sb.AppendLine();
        }

        sb.AppendLine("    }");
        sb.AppendLine("}");
        sb.AppendLine("#pragma warning restore CS1591");

        return sb.ToString();
    }

    private static string MapCatalogSection(string? category)
    {
        if (category is null) return "AeroUi";
        return category.Trim() switch
        {
            "Aero UI" => "AeroUi",
            "Primitives" => "Primitives",
            "Components" => "Components",
            "Hyper" => "Hyper",
            "Neo" => "Neo",
            _ => "AeroUi"
        };
    }

    /// <summary>
    /// Derives the editor preview component type name by replacing "Blocks.Rendering"
    /// with "Pages.Manager.PageEditor.AeroUi.Hero01" and "Renderer" with "EditorPreview".
    /// </summary>
    private static string? DeriveEditorPreviewTypeName(string rendererTypeName)
    {
        // e.g. "Aero.Cms.Shared.Blocks.Rendering.Hero01BlockRenderer"
        //   → "Aero.Cms.Shared.Pages.Manager.PageEditor.AeroUi.Hero01.Hero01BlockEditorPreview"
        var index = rendererTypeName.IndexOf(".Blocks.Rendering.", StringComparison.Ordinal);
        if (index < 0) return null;
        var prefix = rendererTypeName.Substring(0, index);
        var typeName = rendererTypeName.Substring(index + ".Blocks.Rendering.".Length);
        if (!typeName.EndsWith("Renderer")) return null;
        var baseName = typeName.Substring(0, typeName.Length - "Renderer".Length);
        return $"{prefix}.Pages.Manager.PageEditor.AeroUi.Hero01.{baseName}EditorPreview";
    }

    /// <summary>
    /// Derives the property editor component type name from the renderer type.
    /// </summary>
    private static string? DerivePropertyEditorTypeName(string rendererTypeName)
    {
        var index = rendererTypeName.IndexOf(".Blocks.Rendering.", StringComparison.Ordinal);
        if (index < 0) return null;
        var prefix = rendererTypeName.Substring(0, index);
        var typeName = rendererTypeName.Substring(index + ".Blocks.Rendering.".Length);
        if (!typeName.EndsWith("Renderer")) return null;
        var baseName = typeName.Substring(0, typeName.Length - "Renderer".Length);
        return $"{prefix}.Pages.Manager.PageEditor.AeroUi.Hero01.{baseName}Editor";
    }

    private readonly struct RendererCandidate
    {
                /// <summary>
        /// Initializes a new instance of the <see cref="RendererCandidate"/> class.
        /// </summary>
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

                /// <summary>
        /// Gets or sets the Renderer Type.
        /// </summary>
public INamedTypeSymbol RendererType { get; }

                /// <summary>
        /// Gets or sets the Model Type.
        /// </summary>
public INamedTypeSymbol ModelType { get; }

                /// <summary>
        /// Gets or sets the Block Parameter.
        /// </summary>
public IPropertySymbol? BlockParameter { get; }

                /// <summary>
        /// Gets or sets the Has Navigation Parameter.
        /// </summary>
public bool HasNavigationParameter { get; }

                /// <summary>
        /// Gets or sets the Location.
        /// </summary>
public Location? Location { get; }
    }

    private readonly struct PropertyDescriptor
    {
                /// <summary>
        /// Initializes a new instance of the <see cref="PropertyDescriptor"/> class.
        /// </summary>
public PropertyDescriptor(string name, string label, string propertyTypeName, string fieldType)
        {
            Name = name;
            Label = label;
            PropertyTypeName = propertyTypeName;
            FieldType = fieldType;
        }

                /// <summary>
        /// Gets or sets the Name.
        /// </summary>
public string Name { get; }
                /// <summary>
        /// Gets or sets the Label.
        /// </summary>
public string Label { get; }
                /// <summary>
        /// Gets or sets the Property Type Name.
        /// </summary>
public string PropertyTypeName { get; }
                /// <summary>
        /// Gets or sets the Field Type.
        /// </summary>
public string FieldType { get; }
    }

    private readonly struct BlockModelCandidate
    {
                /// <summary>
        /// Initializes a new instance of the <see cref="BlockModelCandidate"/> class.
        /// </summary>
public BlockModelCandidate(
            INamedTypeSymbol modelType,
            AttributeData metadata,
            ImmutableArray<PropertyDescriptor> propertyDescriptors,
            string? editorPreviewTypeName,
            string? propertyEditorTypeName)
        {
            ModelType = modelType;
            Metadata = metadata;
            PropertyDescriptors = propertyDescriptors;
            EditorPreviewTypeName = editorPreviewTypeName;
            PropertyEditorTypeName = propertyEditorTypeName;
        }

                /// <summary>
        /// Gets or sets the Model Type.
        /// </summary>
public INamedTypeSymbol ModelType { get; }

                /// <summary>
        /// Gets or sets the Metadata.
        /// </summary>
public AttributeData Metadata { get; }

                /// <summary>
        /// Gets or sets the Property Descriptors.
        /// </summary>
public ImmutableArray<PropertyDescriptor> PropertyDescriptors { get; }

                /// <summary>
        /// Gets or sets the Editor Preview Type Name.
        /// </summary>
public string? EditorPreviewTypeName { get; }

                /// <summary>
        /// Gets or sets the Property Editor Type Name.
        /// </summary>
public string? PropertyEditorTypeName { get; }
    }

    private readonly struct BlockModelDescriptor
    {
                /// <summary>
        /// Initializes a new instance of the <see cref="BlockModelDescriptor"/> class.
        /// </summary>
public BlockModelDescriptor(
            string blockType,
            string displayName,
            string? description,
            string? category,
            string? icon,
            int sortOrder,
            int schemaVersion,
            string modelTypeName,
            ImmutableArray<PropertyDescriptor> propertyDescriptors,
            string? editorPreviewTypeName,
            string? propertyEditorTypeName)
        {
            BlockType = blockType;
            DisplayName = displayName;
            Description = description;
            Category = category;
            Icon = icon;
            SortOrder = sortOrder;
            SchemaVersion = schemaVersion;
            ModelTypeName = modelTypeName;
            PropertyDescriptors = propertyDescriptors;
            EditorPreviewTypeName = editorPreviewTypeName;
            PropertyEditorTypeName = propertyEditorTypeName;
        }

                /// <summary>
        /// Gets or sets the Block Type.
        /// </summary>
public string BlockType { get; }

                /// <summary>
        /// Gets or sets the Display Name.
        /// </summary>
public string DisplayName { get; }

                /// <summary>
        /// Gets or sets the Description.
        /// </summary>
public string? Description { get; }

                /// <summary>
        /// Gets or sets the Category.
        /// </summary>
public string? Category { get; }

                /// <summary>
        /// Gets or sets the Icon.
        /// </summary>
public string? Icon { get; }

                /// <summary>
        /// Gets or sets the Sort Order.
        /// </summary>
public int SortOrder { get; }

                /// <summary>
        /// Gets or sets the Schema Version.
        /// </summary>
public int SchemaVersion { get; }

                /// <summary>
        /// Gets or sets the Model Type Name.
        /// </summary>
public string ModelTypeName { get; }

                /// <summary>
        /// Gets or sets the Property Descriptors.
        /// </summary>
public ImmutableArray<PropertyDescriptor> PropertyDescriptors { get; }

                /// <summary>
        /// Gets or sets the Editor Preview Type Name.
        /// </summary>
public string? EditorPreviewTypeName { get; }

                /// <summary>
        /// Gets or sets the Property Editor Type Name.
        /// </summary>
public string? PropertyEditorTypeName { get; }
    }

    private readonly struct BlockRendererDescriptor
    {
                /// <summary>
        /// Initializes a new instance of the <see cref="BlockRendererDescriptor"/> class.
        /// </summary>
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

                /// <summary>
        /// Gets or sets the Block Type.
        /// </summary>
public string BlockType { get; }

                /// <summary>
        /// Gets or sets the Adapter Name.
        /// </summary>
public string AdapterName { get; }

                /// <summary>
        /// Gets or sets the Model Type Name.
        /// </summary>
public string ModelTypeName { get; }

                /// <summary>
        /// Gets or sets the Renderer Type Name.
        /// </summary>
public string RendererTypeName { get; }

                /// <summary>
        /// Gets or sets the Parameter Name.
        /// </summary>
public string ParameterName { get; }

                /// <summary>
        /// Gets or sets the Display Name.
        /// </summary>
public string DisplayName { get; }

                /// <summary>
        /// Gets or sets the Description.
        /// </summary>
public string? Description { get; }

                /// <summary>
        /// Gets or sets the Category.
        /// </summary>
public string? Category { get; }

                /// <summary>
        /// Gets or sets the Icon.
        /// </summary>
public string? Icon { get; }

                /// <summary>
        /// Gets or sets the Sort Order.
        /// </summary>
public int SortOrder { get; }

                /// <summary>
        /// Gets or sets the Schema Version.
        /// </summary>
public int SchemaVersion { get; }

                /// <summary>
        /// Gets or sets the Has Navigation Parameter.
        /// </summary>
public bool HasNavigationParameter { get; }

                /// <summary>
        /// Gets or sets the Location.
        /// </summary>
public Location? Location { get; }
    }

    private readonly struct CrossAssemblyBlockData
    {
                /// <summary>
        /// Types.
        /// </summary>
public readonly ImmutableArray<DiscoveredBlockType> Types;
                /// <summary>
        /// AssemblyName.
        /// </summary>
public readonly string AssemblyName;

                /// <summary>
        /// Initializes a new instance of the <see cref="CrossAssemblyBlockData"/> class.
        /// </summary>
public CrossAssemblyBlockData(ImmutableArray<DiscoveredBlockType> types, string assemblyName)
        {
            Types = types;
            AssemblyName = assemblyName;
        }
    }

    private readonly struct DiscoveredBlockType
    {
                /// <summary>
        /// Initializes a new instance of the <see cref="DiscoveredBlockType"/> class.
        /// </summary>
public DiscoveredBlockType(
            string fullyQualifiedName,
            string blockType,
            string displayName)
        {
            FullyQualifiedName = fullyQualifiedName;
            BlockType = blockType;
            DisplayName = displayName;
        }

        /// <summary>
        /// Fully qualified type name, e.g. "Aero.Cms.Abstractions.Blocks.Common.RichTextBlock".
        /// </summary>
        public string FullyQualifiedName { get; }

        /// <summary>
        /// Block type discriminator, e.g. "rich_text".
        /// </summary>
        public string BlockType { get; }

        /// <summary>
        /// Display name, e.g. "Rich Text".
        /// </summary>
        public string DisplayName { get; }
    }
}
