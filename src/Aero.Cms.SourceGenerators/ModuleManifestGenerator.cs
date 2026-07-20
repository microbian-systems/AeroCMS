using System.Collections.Immutable;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace Aero.Cms.SourceGenerators;

/// <summary>
/// Per-module project incremental source generator.
/// Runs in every project that can declare modules.
/// Reads <c>[Module]</c> from attributed classes and emits a manifest provider
/// plus an assembly-level <c>ModuleManifestProviderAttribute</c>.
/// </summary>
/// <remarks>
/// The current validation rejects only empty module names and exact duplicate names. Although
/// descriptors exist for invalid targets and missing <c>IAeroModule</c>, those diagnostics are not
/// reported by this implementation.
/// </remarks>
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

    /// <summary>
    /// Builds the attributed-module pipeline, reports name diagnostics, and emits one provider per project.
    /// </summary>
    /// <param name="context">The incremental generator registration context.</param>
    /// <remarks>
    /// Names are compared using ordinal case-sensitive equality. A project with no valid non-empty
    /// candidate emits no source. Duplicate candidates remain in generated output after AERO013 is
    /// reported, relying on the error diagnostic to prevent use of an ambiguous catalog.
    /// </remarks>
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

    /// <summary>
    /// Projects an attributed class symbol and its marker interfaces into descriptor metadata.
    /// </summary>
    /// <param name="context">The attribute syntax context.</param>
    /// <returns>The descriptor metadata, or <see langword="null"/> when no matching attribute/type is available.</returns>
    /// <remarks>
    /// Marker interfaces are checked through <c>INamedTypeSymbol.AllInterfaces</c> only after
    /// the syntax provider has matched <c>[Module]</c>.
    /// </remarks>
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

    /// <summary>
    /// Reports AERO012 and rejects a descriptor whose module name is empty or whitespace.
    /// </summary>
    /// <param name="context">The source-production context used to report diagnostics.</param>
    /// <param name="candidate">The descriptor to validate.</param>
    /// <returns><see langword="true"/> only when the module name is non-empty.</returns>
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

    /// <summary>
    /// Determines whether an attributed module implements a marker interface directly or transitively.
    /// </summary>
    /// <param name="type">The module type symbol.</param>
    /// <param name="interfaceFullName">The metadata name without a <c>global::</c> prefix.</param>
    /// <returns><see langword="true"/> when the interface appears in <c>INamedTypeSymbol.AllInterfaces</c>.</returns>
    private static bool ImplementsInterface(INamedTypeSymbol type, string interfaceFullName)
    {
        foreach (var iface in type.AllInterfaces)
        {
            if (iface.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat) == "global::" + interfaceFullName)
                return true;
        }
        return false;
    }

    /// <summary>
    /// Renders a manifest provider and its assembly-level aggregation attribute.
    /// </summary>
    /// <param name="assemblyName">The consuming compilation assembly name.</param>
    /// <param name="descriptors">The validated descriptors in discovery order.</param>
    /// <returns>C# source for <c>GeneratedModuleManifestProvider.g.cs</c>.</returns>
    private static string RenderProviderSource(string assemblyName, List<ModuleDescriptorInfo> descriptors)
    {
        var source = new StringBuilder();
        source.AppendLine("// <auto-generated />");
        source.AppendLine("#pragma warning disable CS1591");
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
        source.AppendLine("#pragma warning restore CS1591");

        return source.ToString();
    }

    /// <summary>
    /// Appends one generated <c>ModuleDescriptor</c> initializer.
    /// </summary>
    /// <param name="source">The generated source buffer.</param>
    /// <param name="descriptor">The compile-time module metadata.</param>
    /// <remarks>
    /// Missing version and author values become <c>0.0.0</c> and <c>AeroCMS Team</c>.
    /// Physical path is <see langword="null"/> and runtime <c>Disabled</c> is always false.
    /// </remarks>
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

    /// <summary>
    /// Appends an empty or populated collection expression for a string-array property.
    /// </summary>
    /// <param name="source">The generated source buffer.</param>
    /// <param name="propertyName">The descriptor property name.</param>
    /// <param name="values">The optional attribute values.</param>
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

    /// <summary>
    /// Replaces characters that cannot appear in a generated C# identifier.
    /// </summary>
    /// <returns>An alphanumeric/underscore identifier, or <c>Generated</c> when empty.</returns>
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

    /// <summary>
    /// Replaces characters that cannot appear in a generated dotted namespace.
    /// </summary>
    /// <returns>A namespace containing only letters, digits, underscores, and periods.</returns>
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

    /// <summary>
    /// Escapes backslashes and quotation marks for generated string literal content.
    /// </summary>
    /// <returns>The escaped value without surrounding quotation marks.</returns>
    /// <remarks>Control characters and newlines are not escaped by this helper.</remarks>
    private static string Escape(string value)
        => value.Replace("\\", "\\\\").Replace("\"", "\\\"");

    /// <summary>
    /// Formats an optional value as a generated C# string or null literal.
    /// </summary>
    /// <returns><c>null</c> or a quoted escaped string.</returns>
    private static string Literal(string? value)
        => value is null ? "null" : $"\"{Escape(value)}\"";

    /// <summary>
    /// Formats a Boolean as a lowercase C# literal.
    /// </summary>
    /// <returns><c>true</c> or <c>false</c>.</returns>
    private static string BoolLiteral(bool value)
        => value ? "true" : "false";

    /// <summary>
    /// Reads a named string argument from a module attribute.
    /// </summary>
    /// <returns>The string value, or <see langword="null"/> when absent or differently typed.</returns>
    private static string? GetNamedString(AttributeData attribute, string name)
    {
        foreach (var kvp in attribute.NamedArguments)
        {
            if (kvp.Key == name && kvp.Value.Value is string s)
                return s;
        }
        return null;
    }

    /// <summary>
    /// Reads a named integral order argument as a 16-bit value.
    /// </summary>
    /// <returns>The short value, an unchecked cast from an integer value, or the supplied default.</returns>
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

    /// <summary>
    /// Reads a named Boolean argument from a module attribute.
    /// </summary>
    /// <returns>The Boolean value, or <see langword="false"/> when absent or differently typed.</returns>
    private static bool GetNamedBool(AttributeData attribute, string name)
    {
        foreach (var kvp in attribute.NamedArguments)
        {
            if (kvp.Key == name && kvp.Value.Value is bool b)
                return b;
        }
        return false;
    }

    /// <summary>
    /// Reads non-null string elements from a named attribute array.
    /// </summary>
    /// <returns>The filtered immutable array, or an empty array when absent.</returns>
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

    /// <summary>
    /// Carries compile-time values used to render a module descriptor.
    /// </summary>
    /// <param name="name">The declared module name.</param>
    /// <param name="version">The optional declared version.</param>
    /// <param name="author">The optional declared author.</param>
    /// <param name="order">The module ordering value.</param>
    /// <param name="dependencies">The declared module dependencies.</param>
    /// <param name="category">The declared categories.</param>
    /// <param name="tags">The declared tags.</param>
    /// <param name="disabledInProduction">Whether the descriptor disables the module in production.</param>
    /// <param name="description">The optional module description.</param>
    /// <param name="fullTypeName">The fully qualified module type name.</param>
    /// <param name="assemblyName">The module's containing assembly name.</param>
    /// <param name="isUiModule">Whether the UI marker is implemented.</param>
    /// <param name="isApiModule">Whether the API marker is implemented.</param>
    /// <param name="isBackgroundModule">Whether the background marker is implemented.</param>
    /// <param name="isThemeModule">Whether the theme marker is implemented.</param>
    /// <param name="isAdminModule">Whether the admin marker is implemented.</param>
    /// <param name="isFilterModule">Whether the filter marker is implemented.</param>
    /// <param name="isContentDefinitionModule">Whether the content-definition marker is implemented.</param>
    /// <param name="isAeroDbConfigurator">Whether synchronous AeroDB configuration is implemented.</param>
    /// <param name="isAsyncAeroDbConfigurator">Whether asynchronous AeroDB configuration is implemented.</param>
    /// <param name="location">The module source location used by diagnostics.</param>
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
        /// <summary>
        /// Gets the declared module name.
        /// </summary>
public string ModuleName { get; } = name;
        /// <summary>
        /// Gets the optional declared version.
        /// </summary>
public string? Version { get; } = version;
        /// <summary>
        /// Gets the optional declared author.
        /// </summary>
public string? Author { get; } = author;
        /// <summary>
        /// Gets the module ordering value.
        /// </summary>
public short Order { get; } = order;
        /// <summary>
        /// Gets the declared dependency names.
        /// </summary>
public ImmutableArray<string> Dependencies { get; } = dependencies;
        /// <summary>
        /// Gets the declared category values.
        /// </summary>
public ImmutableArray<string> Category { get; } = category;
        /// <summary>
        /// Gets the declared tag values.
        /// </summary>
public ImmutableArray<string> Tags { get; } = tags;
        /// <summary>
        /// Gets whether the module is declared disabled in production.
        /// </summary>
public bool DisabledInProduction { get; } = disabledInProduction;
        /// <summary>
        /// Gets the optional module description.
        /// </summary>
public string? Description { get; } = description;
        /// <summary>
        /// Gets the fully qualified module type name.
        /// </summary>
public string FullTypeName { get; } = fullTypeName;
        /// <summary>
        /// Gets the containing assembly name.
        /// </summary>
public string AssemblyName { get; } = assemblyName;
        /// <summary>
        /// Gets whether the UI-module marker is implemented.
        /// </summary>
public bool IsUiModule { get; } = isUiModule;
        /// <summary>
        /// Gets whether the API-module marker is implemented.
        /// </summary>
public bool IsApiModule { get; } = isApiModule;
        /// <summary>
        /// Gets whether the background-module marker is implemented.
        /// </summary>
public bool IsBackgroundModule { get; } = isBackgroundModule;
        /// <summary>
        /// Gets whether the theme-module marker is implemented.
        /// </summary>
public bool IsThemeModule { get; } = isThemeModule;
        /// <summary>
        /// Gets whether the admin-module marker is implemented.
        /// </summary>
public bool IsAdminModule { get; } = isAdminModule;
        /// <summary>
        /// Gets whether the filter-module marker is implemented.
        /// </summary>
public bool IsFilterModule { get; } = isFilterModule;
        /// <summary>
        /// Gets whether the content-definition-module marker is implemented.
        /// </summary>
public bool IsContentDefinitionModule { get; } = isContentDefinitionModule;
        /// <summary>
        /// Gets whether synchronous AeroDB configuration is implemented.
        /// </summary>
public bool IsAeroDbConfigurator { get; } = isAeroDbConfigurator;
        /// <summary>
        /// Gets whether asynchronous AeroDB configuration is implemented.
        /// </summary>
public bool IsAsyncAeroDbConfigurator { get; } = isAsyncAeroDbConfigurator;
        /// <summary>
        /// Gets the source location used for generator diagnostics.
        /// </summary>
public Location? Location { get; } = location;
    }
}
