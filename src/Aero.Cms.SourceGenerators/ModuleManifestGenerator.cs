using System.Collections.Immutable;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace Aero.Cms.SourceGenerators;

/// <summary>
/// Per-module project incremental source generator.
/// Runs in every project that can declare modules.
/// Reads <c>[Module]</c> on <see cref="IAeroModule"/> classes,
/// validates targets, and emits a manifest provider
/// plus an assembly-level <c>ModuleManifestProviderAttribute</c>.
/// </summary>
[Generator]
public sealed class ModuleManifestGenerator : IIncrementalGenerator
{
    private const string ModuleAttributeMetadataName = "Aero.Modular.ModuleAttribute";
    private const string IAeroModuleInterfaceName = "Aero.Modular.IAeroModule";

    // Marker interface full names for detection
    private const string IUiModuleName = "Aero.Modular.IUiModule";
    private const string IApiModuleName = "Aero.Modular.IApiModule";
    private const string IBackgroundModuleName = "Aero.Modular.IBackgroundModule";
    private const string IThemeModuleName = "Aero.Modular.IThemeModule";
    private const string IAdminModuleName = "Aero.Modular.IAdminModule";
    private const string IFilterModuleName = "Aero.Modular.IFilterModule";
    private const string IContentDefinitionModuleName = "Aero.Modular.IContentDefinitionModule";

    // AeroDb configurator interface names
    private const string IConfigureAeroDBName = "AeroDB.Sable.IConfigureAeroDB";
    private const string IAsyncConfigureAeroDBName = "AeroDB.Sable.IAsyncConfigureAeroDB";

    private static readonly DiagnosticDescriptor InvalidModuleTarget = new(
        "AERO010",
        "Invalid module target",
        "Type '{0}' has [Module] but must be a public or internal, concrete, non-abstract, non-generic class implementing IAeroModule",
        "AeroCMS.ModuleManifest",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor MissingIAeroModule = new(
        "AERO011",
        "Module must implement IAeroModule",
        "Type '{0}' has [Module] but does not implement IAeroModule",
        "AeroCMS.ModuleManifest",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor EmptyModuleName = new(
        "AERO012",
        "Module name cannot be empty",
        "Module name must be a non-empty string",
        "AeroCMS.ModuleManifest",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor DuplicateModuleName = new(
        "AERO013",
        "Duplicate module name within project",
        "Module name '{0}' is used by more than one module class in this project",
        "AeroCMS.ModuleManifest",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var moduleCandidates = context.SyntaxProvider.ForAttributeWithMetadataName(
            ModuleAttributeMetadataName,
            static (node, _) => node is ClassDeclarationSyntax,
            static (ctx, _) => GetModuleCandidate(ctx))
            .Where(static candidate => candidate is not null)
            .Select(static (candidate, _) => candidate!.Value)
            .Collect();

        // Combine with compilation to get assembly name at output time
        var combined = moduleCandidates.Combine(context.CompilationProvider);

        context.RegisterSourceOutput(combined, static (productionContext, tuple) =>
        {
            var (candidates, compilation) = tuple;
            var assemblyName = compilation.AssemblyName ?? "UnknownAssembly";
            var descriptors = new List<ModuleDescriptorInfo>(candidates.Length);

            foreach (var candidate in candidates)
            {
                if (!ValidateCandidate(productionContext, candidate))
                    continue;

                descriptors.Add(candidate);
            }

            // Check for duplicates within the project
            var nameGroups = descriptors
                .GroupBy(static d => d.ModuleName, StringComparer.Ordinal);

            foreach (var group in nameGroups)
            {
                if (group.Count() <= 1) continue;

                foreach (var dup in group)
                {
                    productionContext.ReportDiagnostic(Diagnostic.Create(
                        DuplicateModuleName,
                        dup.Location,
                        dup.ModuleName));
                }
            }

            if (descriptors.Count == 0)
                return;

            // Emit the generated provider source
            productionContext.AddSource(
                "GeneratedModuleManifestProvider.g.cs",
                SourceText.From(RenderProviderSource(assemblyName, descriptors), Encoding.UTF8));
        });
    }

    private static ModuleDescriptorInfo? GetModuleCandidate(GeneratorAttributeSyntaxContext context)
    {
        if (context.TargetSymbol is not INamedTypeSymbol moduleType)
            return null;

        var attribute = context.Attributes.FirstOrDefault(static attr =>
            attr.AttributeClass?.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)
                == "global::" + ModuleAttributeMetadataName);

        if (attribute is null)
            return null;

        // Extract attribute constructor args
        var name = attribute.ConstructorArguments.Length > 0
            ? attribute.ConstructorArguments[0].Value as string ?? moduleType.Name
            : moduleType.Name;

        var version = attribute.ConstructorArguments.Length > 1
            ? attribute.ConstructorArguments[1].Value as string
            : null;

        var author = attribute.ConstructorArguments.Length > 2
            ? attribute.ConstructorArguments[2].Value as string
            : null;

        // Extract named args
        var order = GetNamedShort(attribute, "Order", 0);
        var dependencies = GetNamedStringArray(attribute, "Dependencies");
        var category = GetNamedStringArray(attribute, "Category");
        var tags = GetNamedStringArray(attribute, "Tags");
        var disabledInProduction = GetNamedBool(attribute, "DisabledInProduction");
        var description = GetNamedString(attribute, "Description");

        // Detect marker interfaces using AllInterfaces
        // This is acceptable because we only check types that already matched [Module]
        var isUiModule = ImplementsInterface(moduleType, IUiModuleName);
        var isApiModule = ImplementsInterface(moduleType, IApiModuleName);
        var isBackgroundModule = ImplementsInterface(moduleType, IBackgroundModuleName);
        var isThemeModule = ImplementsInterface(moduleType, IThemeModuleName);
        var isAdminModule = ImplementsInterface(moduleType, IAdminModuleName);
        var isFilterModule = ImplementsInterface(moduleType, IFilterModuleName);
        var isContentDefinitionModule = ImplementsInterface(moduleType, IContentDefinitionModuleName);
        var isAeroDbConfigurator = ImplementsInterface(moduleType, IConfigureAeroDBName);
        var isAsyncAeroDbConfigurator = ImplementsInterface(moduleType, IAsyncConfigureAeroDBName);

        var fullTypeName = moduleType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);

        return new ModuleDescriptorInfo(
            name: name,
            version: version,
            author: author,
            order: order,
            dependencies: dependencies,
            category: category,
            tags: tags,
            disabledInProduction: disabledInProduction,
            description: description,
            fullTypeName: fullTypeName,
            assemblyName: moduleType.ContainingAssembly?.Name ?? "Unknown",
            isUiModule: isUiModule,
            isApiModule: isApiModule,
            isBackgroundModule: isBackgroundModule,
            isThemeModule: isThemeModule,
            isAdminModule: isAdminModule,
            isFilterModule: isFilterModule,
            isContentDefinitionModule: isContentDefinitionModule,
            isAeroDbConfigurator: isAeroDbConfigurator,
            isAsyncAeroDbConfigurator: isAsyncAeroDbConfigurator,
            location: moduleType.Locations.FirstOrDefault());
    }

    private static bool ValidateCandidate(SourceProductionContext context, ModuleDescriptorInfo candidate)
    {
        if (string.IsNullOrWhiteSpace(candidate.ModuleName))
        {
            context.ReportDiagnostic(Diagnostic.Create(
                EmptyModuleName, candidate.Location));
            return false;
        }

        return true;
    }

    private static bool ImplementsInterface(INamedTypeSymbol type, string interfaceFullName)
    {
        foreach (var iface in type.AllInterfaces)
        {
            if (iface.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat) == "global::" + interfaceFullName)
                return true;
        }
        return false;
    }

    private static string RenderProviderSource(string assemblyName, List<ModuleDescriptorInfo> descriptors)
    {
        var source = new StringBuilder();
        source.AppendLine("// <auto-generated />");
        source.AppendLine("#nullable enable");
        source.AppendLine("using System;");
        source.AppendLine("using System.Collections.Generic;");
        source.AppendLine("using System.Linq;");
        source.AppendLine("using Aero.Modular;");
        source.AppendLine();

        // Assembly-level attribute — one per project that has module classes
        var safeNamespace = SanitizeNamespace(assemblyName);
        var providerTypeName = SanitizeIdentifier(assemblyName) + "ModuleManifestProvider";
        var fullyQualifiedProvider = $"{safeNamespace}.Generated.{providerTypeName}";

        source.AppendLine($"[assembly: ModuleManifestProviderAttribute(typeof({fullyQualifiedProvider}))]");
        source.AppendLine();

        source.AppendLine($"namespace {safeNamespace}.Generated;");
        source.AppendLine();
        source.AppendLine($"public sealed class {providerTypeName} : IModuleManifestProvider");
        source.AppendLine("{");
        source.AppendLine("    public IReadOnlyList<ModuleDescriptor> GetDescriptors() =>");
        source.AppendLine("    [");

        foreach (var descriptor in descriptors)
        {
            RenderDescriptor(source, descriptor);
        }

        source.AppendLine("    ];");
        source.AppendLine("}");

        return source.ToString();
    }

    private static void RenderDescriptor(StringBuilder source, ModuleDescriptorInfo descriptor)
    {
        source.AppendLine("        new ModuleDescriptor");
        source.AppendLine("        {");
        source.AppendLine($"            Name = {Literal(descriptor.ModuleName)},");
        source.AppendLine($"            Version = {Literal(descriptor.Version ?? "0.0.0")},");
        source.AppendLine($"            Author = {Literal(descriptor.Author ?? "AeroCMS Team")},");
        source.AppendLine($"            ModuleType = typeof({descriptor.FullTypeName}),");
        source.AppendLine($"            AssemblyName = {Literal(descriptor.AssemblyName)},");
        source.AppendLine($"            PhysicalPath = null,");
        source.AppendLine($"            Order = {descriptor.Order},");

        RenderStringArray(source, "Dependencies", descriptor.Dependencies);
        RenderStringArray(source, "Category", descriptor.Category);
        RenderStringArray(source, "Tags", descriptor.Tags);

        source.AppendLine($"            IsUiModule = {BoolLiteral(descriptor.IsUiModule)},");
        source.AppendLine($"            IsApiModule = {BoolLiteral(descriptor.IsApiModule)},");
        source.AppendLine($"            IsBackgroundModule = {BoolLiteral(descriptor.IsBackgroundModule)},");
        source.AppendLine($"            IsThemeModule = {BoolLiteral(descriptor.IsThemeModule)},");
        source.AppendLine($"            IsAdminModule = {BoolLiteral(descriptor.IsAdminModule)},");
        source.AppendLine($"            IsFilterModule = {BoolLiteral(descriptor.IsFilterModule)},");
        source.AppendLine($"            IsContentDefinitionModule = {BoolLiteral(descriptor.IsContentDefinitionModule)},");
        source.AppendLine($"            IsAeroDbConfigurator = {BoolLiteral(descriptor.IsAeroDbConfigurator)},");
        source.AppendLine($"            IsAsyncAeroDbConfigurator = {BoolLiteral(descriptor.IsAsyncAeroDbConfigurator)},");
        source.AppendLine($"            DisabledInProduction = {BoolLiteral(descriptor.DisabledInProduction)},");
        source.AppendLine($"            Disabled = false,");
        source.AppendLine($"            Description = {Literal(descriptor.Description)},");
        source.AppendLine("        },");
    }

    private static void RenderStringArray(StringBuilder source, string propertyName, ImmutableArray<string>? values)
    {
        if (values is not { Length: > 0 } nonEmpty)
        {
            source.AppendLine($"            {propertyName} = [],");
            return;
        }

        source.AppendLine($"            {propertyName} =");
        source.AppendLine("            [");
        foreach (var value in nonEmpty)
        {
            source.AppendLine($"                {Literal(value)},");
        }
        source.AppendLine("            ],");
    }

    private static string SanitizeIdentifier(string value)
    {
        var builder = new StringBuilder(value.Length);
        foreach (var ch in value)
        {
            if (char.IsLetterOrDigit(ch) || ch == '_')
                builder.Append(ch);
            else
                builder.Append('_');
        }
        return builder.Length == 0 ? "Generated" : builder.ToString();
    }

    private static string SanitizeNamespace(string value)
    {
        var builder = new StringBuilder(value.Length);
        foreach (var ch in value)
        {
            if (char.IsLetterOrDigit(ch) || ch == '_' || ch == '.')
                builder.Append(ch);
            else
                builder.Append('_');
        }
        return builder.Length == 0 ? "Generated" : builder.ToString();
    }

    private static string Escape(string value)
        => value.Replace("\\", "\\\\").Replace("\"", "\\\"");

    private static string Literal(string? value)
        => value is null ? "null" : $"\"{Escape(value)}\"";

    private static string BoolLiteral(bool value)
        => value ? "true" : "false";

    private static string? GetNamedString(AttributeData attribute, string name)
    {
        foreach (var kvp in attribute.NamedArguments)
        {
            if (kvp.Key == name && kvp.Value.Value is string s)
                return s;
        }
        return null;
    }

    private static short GetNamedShort(AttributeData attribute, string name, short defaultValue)
    {
        foreach (var kvp in attribute.NamedArguments)
        {
            if (kvp.Key == name)
            {
                if (kvp.Value.Value is short s)
                    return s;
                if (kvp.Value.Value is int i)
                    return (short)i;
            }
        }
        return defaultValue;
    }

    private static bool GetNamedBool(AttributeData attribute, string name)
    {
        foreach (var kvp in attribute.NamedArguments)
        {
            if (kvp.Key == name && kvp.Value.Value is bool b)
                return b;
        }
        return false;
    }

    private static ImmutableArray<string> GetNamedStringArray(AttributeData attribute, string name)
    {
        foreach (var kvp in attribute.NamedArguments)
        {
            if (kvp.Key == name && kvp.Value.Value is ImmutableArray<TypedConstant> array)
            {
                return array
                    .Select(static tc => tc.Value as string)
                    .Where(static s => s is not null)
                    .Select(static s => s!)
                    .ToImmutableArray();
            }
        }
        return [];
    }

    private readonly struct ModuleDescriptorInfo(
        string name,
        string? version,
        string? author,
        short order,
        ImmutableArray<string> dependencies,
        ImmutableArray<string> category,
        ImmutableArray<string> tags,
        bool disabledInProduction,
        string? description,
        string fullTypeName,
        string assemblyName,
        bool isUiModule,
        bool isApiModule,
        bool isBackgroundModule,
        bool isThemeModule,
        bool isAdminModule,
        bool isFilterModule,
        bool isContentDefinitionModule,
        bool isAeroDbConfigurator,
        bool isAsyncAeroDbConfigurator,
        Location? location)
    {
        public string ModuleName { get; } = name;
        public string? Version { get; } = version;
        public string? Author { get; } = author;
        public short Order { get; } = order;
        public ImmutableArray<string> Dependencies { get; } = dependencies;
        public ImmutableArray<string> Category { get; } = category;
        public ImmutableArray<string> Tags { get; } = tags;
        public bool DisabledInProduction { get; } = disabledInProduction;
        public string? Description { get; } = description;
        public string FullTypeName { get; } = fullTypeName;
        public string AssemblyName { get; } = assemblyName;
        public bool IsUiModule { get; } = isUiModule;
        public bool IsApiModule { get; } = isApiModule;
        public bool IsBackgroundModule { get; } = isBackgroundModule;
        public bool IsThemeModule { get; } = isThemeModule;
        public bool IsAdminModule { get; } = isAdminModule;
        public bool IsFilterModule { get; } = isFilterModule;
        public bool IsContentDefinitionModule { get; } = isContentDefinitionModule;
        public bool IsAeroDbConfigurator { get; } = isAeroDbConfigurator;
        public bool IsAsyncAeroDbConfigurator { get; } = isAsyncAeroDbConfigurator;
        public Location? Location { get; } = location;
    }
}
