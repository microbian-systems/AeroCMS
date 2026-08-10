using System.Collections.Immutable;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

namespace Aero.Cms.SourceGenerators;

/// <summary>
/// Generates deterministic catalog helpers for C# assemblies that explicitly opt in with
/// <c>AeroCmsHostCatalogGenerationAttribute</c>.
/// </summary>
[Generator]
public sealed class HostModuleCatalogGenerator : IIncrementalGenerator
{
    private const string OptInAttributeName = "Aero.Cms.Hosting.AeroCmsHostCatalogGenerationAttribute";
    private const string ModuleManifestProviderAttributeName = "Aero.Modular.ModuleManifestProviderAttribute";
    private const string WolverineHandlersRegistrationAttributeName = "Aero.Modular.WolverineHandlersRegistrationAttribute";

    private static readonly DiagnosticDescriptor EmptyHostModuleCatalog = new(
        "AERO050",
        "Generated host module catalog is empty",
        "Catalog project '{0}' did not discover any generated module manifest providers. Reference at least one Aero CMS module package or remove AeroCmsHostCatalogGenerationAttribute.",
        "AeroCMS.ModuleManifest",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    /// <inheritdoc />
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var hostCatalog = context.CompilationProvider.Select(static (compilation, _) =>
        {
            var optedIn = compilation.Assembly.GetAttributes().Any(static attribute =>
                attribute.AttributeClass?.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)
                    == "global::" + OptInAttributeName);

            if (!optedIn)
            {
                return new HostCatalogInfo(
                    compilation.AssemblyName ?? string.Empty,
                    false,
                    ImmutableArray<string>.Empty,
                    ImmutableArray<string>.Empty);
            }

            var moduleProviders = new SortedSet<string>(StringComparer.Ordinal);
            var handlerRegistrations = new SortedSet<string>(StringComparer.Ordinal);

            foreach (var referencedAssembly in compilation.SourceModule.ReferencedAssemblySymbols)
            {
                foreach (var attribute in referencedAssembly.GetAttributes())
                {
                    var attributeName = attribute.AttributeClass?.ToDisplayString(
                        SymbolDisplayFormat.FullyQualifiedFormat);

                    if (attributeName == "global::" + ModuleManifestProviderAttributeName &&
                        TryGetTypeName(attribute, out var providerType))
                    {
                        moduleProviders.Add(providerType);
                    }

                    if (attributeName == "global::" + WolverineHandlersRegistrationAttributeName &&
                        TryGetTypeName(attribute, out var registrationType))
                    {
                        handlerRegistrations.Add(registrationType);
                    }
                }
            }

            return new HostCatalogInfo(
                compilation.AssemblyName ?? string.Empty,
                true,
                moduleProviders.ToImmutableArray(),
                handlerRegistrations.ToImmutableArray());
        });

        context.RegisterSourceOutput(hostCatalog, static (productionContext, catalog) =>
        {
            if (!catalog.OptedIn)
            {
                return;
            }

            if (catalog.ModuleProviders.IsDefaultOrEmpty)
            {
                productionContext.ReportDiagnostic(Diagnostic.Create(
                    EmptyHostModuleCatalog,
                    Location.None,
                    catalog.AssemblyName));
                return;
            }

            var generatedNamespace = SanitizeNamespace(catalog.AssemblyName) + ".Generated";
            productionContext.AddSource(
                "GeneratedAeroModuleCatalog.g.cs",
                SourceText.From(
                    RenderModuleCatalogSource(generatedNamespace, catalog.ModuleProviders),
                    Encoding.UTF8));
            productionContext.AddSource(
                "GeneratedWolverineHandlerCatalog.g.cs",
                SourceText.From(
                    RenderHandlerCatalogSource(generatedNamespace, catalog.HandlerRegistrations),
                    Encoding.UTF8));
        });
    }

    private static bool TryGetTypeName(AttributeData attribute, out string typeName)
    {
        if (attribute.ConstructorArguments.Length == 1 &&
            attribute.ConstructorArguments[0].Kind == TypedConstantKind.Type &&
            attribute.ConstructorArguments[0].Value is ITypeSymbol type)
        {
            typeName = type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
            return true;
        }

        typeName = string.Empty;
        return false;
    }

    private static string RenderModuleCatalogSource(
        string generatedNamespace,
        ImmutableArray<string> moduleProviders)
    {
        var source = new StringBuilder();
        source.AppendLine("// <auto-generated />");
        source.AppendLine("#pragma warning disable CS1591");
        source.AppendLine("#nullable enable");
        source.AppendLine("using System.Collections.Generic;");
        source.AppendLine("using System.Linq;");
        source.AppendLine("using Aero.Modular;");
        source.AppendLine();
        source.AppendLine($"namespace {generatedNamespace};");
        source.AppendLine();
        source.AppendLine("public static class GeneratedAeroModuleCatalog");
        source.AppendLine("{");
        source.AppendLine("    public static IReadOnlyList<IModuleManifestProvider> Providers { get; } =");
        source.AppendLine("    [");

        foreach (var provider in moduleProviders)
        {
            source.AppendLine($"        new {provider}(),");
        }

        source.AppendLine("    ];");
        source.AppendLine();
        source.AppendLine("    public static IReadOnlyList<ModuleDescriptor> Descriptors { get; } =");
        source.AppendLine("        Providers.SelectMany(static provider => provider.GetDescriptors()).ToArray();");
        source.AppendLine("}");
        source.AppendLine("#pragma warning restore CS1591");
        return source.ToString();
    }

    private static string RenderHandlerCatalogSource(
        string generatedNamespace,
        ImmutableArray<string> handlerRegistrations)
    {
        var source = new StringBuilder();
        source.AppendLine("// <auto-generated />");
        source.AppendLine("#pragma warning disable CS1591");
        source.AppendLine("#nullable enable");
        source.AppendLine("using Wolverine;");
        source.AppendLine();
        source.AppendLine($"namespace {generatedNamespace};");
        source.AppendLine();
        source.AppendLine("public static class GeneratedWolverineHandlerCatalog");
        source.AppendLine("{");
        source.AppendLine("    public static void Register(WolverineOptions options)");
        source.AppendLine("    {");
        source.AppendLine("        global::System.ArgumentNullException.ThrowIfNull(options);");
        source.AppendLine("        options.Discovery.DisableConventionalDiscovery();");

        foreach (var registration in handlerRegistrations)
        {
            source.AppendLine($"        {registration}.Register(options);");
        }

        source.AppendLine("    }");
        source.AppendLine("}");
        source.AppendLine("#pragma warning restore CS1591");
        return source.ToString();
    }

    private static string SanitizeNamespace(string value)
    {
        var builder = new StringBuilder(value.Length);
        foreach (var character in value)
        {
            builder.Append(char.IsLetterOrDigit(character) || character is '_' or '.' ? character : '_');
        }

        return builder.Length == 0 ? "Generated" : builder.ToString();
    }

    private readonly struct HostCatalogInfo(
        string assemblyName,
        bool optedIn,
        ImmutableArray<string> moduleProviders,
        ImmutableArray<string> handlerRegistrations)
    {
        public string AssemblyName { get; } = assemblyName;
        public bool OptedIn { get; } = optedIn;
        public ImmutableArray<string> ModuleProviders { get; } = moduleProviders;
        public ImmutableArray<string> HandlerRegistrations { get; } = handlerRegistrations;
    }
}
