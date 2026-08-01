using System.Collections.Immutable;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

namespace Aero.Cms.SourceGenerators;

/// <summary>
/// Host-project incremental source generator.
/// Runs in every project but emits output only for the exact <c>Aero.Cms.Web</c> assembly name.
/// Reads assembly-level attributes from referenced assemblies:
/// <list type="bullet">
///   <item><c>ModuleManifestProviderAttribute</c> — discovers module manifest providers</item>
///   <item><c>WolverineHandlersRegistrationAttribute</c> — discovers Wolverine handler registrations</item>
/// </list>
/// </summary>
[Generator]
public sealed class HostModuleCatalogGenerator : IIncrementalGenerator
{
    private const string HostAssemblyName = "Aero.Cms.Web";
    private const string ModuleManifestProviderAttributeName = "Aero.Modular.ModuleManifestProviderAttribute";
    private const string WolverineHandlersRegistrationAttributeName = "Aero.Modular.WolverineHandlersRegistrationAttribute";
    private static readonly DiagnosticDescriptor EmptyHostModuleCatalog = new(
        "AERO050",
        "Generated host module catalog is empty",
        "Host project '{0}' did not discover any generated module manifest providers. Ensure module projects reference Aero.Cms.SourceGenerators as an analyzer and the host references the module projects.",
        "AeroCMS.ModuleManifest",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    /// <summary>
    /// Builds host catalog metadata from direct referenced-assembly attributes and emits host partials.
    /// </summary>
    /// <param name="context">The incremental generator registration context.</param>
    /// <remarks>
    /// Module providers are discovered only when <c>Aero.Modular</c> is directly referenced. Wolverine
    /// output additionally requires a direct <c>Wolverine</c> or <c>WolverineFx</c> reference. The host
    /// receives AERO050 as an error when Aero.Modular is present but no provider attribute is found.
    /// Handler registrations may be empty without a diagnostic.
    /// </remarks>
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        // Pipeline: read assembly attributes + check if host compilation has required types
        var hostCatalog = context.CompilationProvider.Select(static (compilation, _) =>
        {
            var moduleProviders = new List<string>();
            var handlerRegistrations = new List<string>();

            // Check if the current compilation has the required types
            var hasAeroModular = false;
            var hasWolverine = false;

            // Check current assembly's own references for Aero.Modular types
            foreach (var referencedAssembly in compilation.SourceModule.ReferencedAssemblySymbols)
            {
                if (referencedAssembly.Name == "Aero.Modular")
                    hasAeroModular = true;

                if (referencedAssembly.Identity.Name is "Wolverine" or "WolverineFx")
                    hasWolverine = true;
            }

            // Only read provider attributes if Aero.Modular is referenced
            if (hasAeroModular)
            {
                foreach (var referencedAssembly in compilation.SourceModule.ReferencedAssemblySymbols)
                {
                    var attributes = referencedAssembly.GetAttributes();

                    foreach (var attr in attributes)
                    {
                        var attrFullName = attr.AttributeClass?.ToDisplayString(
                            SymbolDisplayFormat.FullyQualifiedFormat);

                        if (attrFullName == "global::" + ModuleManifestProviderAttributeName &&
                            attr.ConstructorArguments.Length == 1 &&
                            attr.ConstructorArguments[0].Kind == TypedConstantKind.Type &&
                            attr.ConstructorArguments[0].Value is ITypeSymbol providerType)
                        {
                            var fullName = providerType.ToDisplayString(
                                SymbolDisplayFormat.FullyQualifiedFormat);
                            if (!moduleProviders.Contains(fullName))
                                moduleProviders.Add(fullName);
                        }

                        if (attrFullName == "global::" + WolverineHandlersRegistrationAttributeName &&
                            attr.ConstructorArguments.Length == 1 &&
                            attr.ConstructorArguments[0].Kind == TypedConstantKind.Type &&
                            attr.ConstructorArguments[0].Value is ITypeSymbol handlerRegType)
                        {
                            var fullName = handlerRegType.ToDisplayString(
                                SymbolDisplayFormat.FullyQualifiedFormat);
                            if (!handlerRegistrations.Contains(fullName))
                                handlerRegistrations.Add(fullName);
                        }
                    }
                }
            }

            return new HostCatalogInfo(
                assemblyName: compilation.AssemblyName ?? string.Empty,
                moduleProviders: moduleProviders.ToImmutableArray(),
                handlerRegistrations: handlerRegistrations.ToImmutableArray(),
                hasAeroModular: hasAeroModular,
                hasWolverine: hasWolverine);
        });

        context.RegisterSourceOutput(hostCatalog, static (productionContext, catalog) =>
        {
            if (!string.Equals(catalog.AssemblyName, HostAssemblyName, StringComparison.Ordinal))
                return;

            if (catalog.HasAeroModular)
            {
                if (catalog.ModuleProviders.IsDefaultOrEmpty)
                {
                    productionContext.ReportDiagnostic(Diagnostic.Create(
                        EmptyHostModuleCatalog,
                        Location.None,
                        catalog.AssemblyName));
                }

                productionContext.AddSource(
                    "GeneratedAeroModuleCatalog.g.cs",
                    SourceText.From(RenderModuleCatalogSource(catalog.ModuleProviders), Encoding.UTF8));

                productionContext.AddSource(
                    "GeneratedHostCatalogMarker.g.cs",
                    SourceText.From(
                        "// <auto-generated />\n" +
                        "#pragma warning disable CS1591\n" +
                        "namespace Aero.Cms.Web.Generated;\n" +
                        "public static class GeneratedHostCatalogMarker { }\n" +
                        "#pragma warning restore CS1591\n",
                        Encoding.UTF8));
            }

            if (catalog.HasWolverine)
            {
                productionContext.AddSource(
                    "GeneratedWolverineHandlerCatalog.g.cs",
                    SourceText.From(RenderHandlerCatalogSource(catalog.HandlerRegistrations), Encoding.UTF8));
            }
        });
    }

    /// <summary>
    /// Renders the host partial that instantiates each referenced module manifest provider.
    /// </summary>
    /// <param name="moduleProviders">Fully qualified provider type names in reference-discovery order.</param>
    /// <returns>C# source implementing <c>GeneratedAeroModuleCatalog.PopulateProviders</c>.</returns>
    private static string RenderModuleCatalogSource(ImmutableArray<string> moduleProviders)
    {
        var source = new StringBuilder();
        source.AppendLine("// <auto-generated />");
        source.AppendLine("#pragma warning disable CS1591");
        source.AppendLine("#nullable enable");
        source.AppendLine("using System.Collections.Generic;");
        source.AppendLine("using Aero.Modular;");
        source.AppendLine();
        source.AppendLine("namespace Aero.Cms.Web.Generated;");
        source.AppendLine();
        source.AppendLine("/// <summary>");
        source.AppendLine("/// Source-generated module catalog population aggregated from referenced module projects.");
        source.AppendLine("/// </summary>");
        source.AppendLine("public static partial class GeneratedAeroModuleCatalog");
        source.AppendLine("{");
        source.AppendLine("    static partial void PopulateProviders(List<IModuleManifestProvider> providers)");
        source.AppendLine("    {");

        foreach (var provider in moduleProviders)
        {
            source.AppendLine($"        providers.Add(new {provider}());");
        }

        source.AppendLine("    }");
        source.AppendLine("}");
        source.AppendLine("#pragma warning restore CS1591");

        return source.ToString();
    }

    /// <summary>
    /// Renders the host partial that invokes each referenced Wolverine registration catalog.
    /// </summary>
    /// <param name="handlerRegistrations">Fully qualified registration type names in discovery order.</param>
    /// <returns>C# source implementing <c>GeneratedWolverineHandlerCatalog.RegisterGenerated</c>.</returns>
    private static string RenderHandlerCatalogSource(ImmutableArray<string> handlerRegistrations)
    {
        var source = new StringBuilder();
        source.AppendLine("// <auto-generated />");
        source.AppendLine("#pragma warning disable CS1591");
        source.AppendLine("#nullable enable");
        source.AppendLine("using Wolverine;");
        source.AppendLine();
        source.AppendLine("namespace Aero.Cms.Web.Generated;");
        source.AppendLine();
        source.AppendLine("/// <summary>");
        source.AppendLine("/// Source-generated Wolverine handler catalog aggregated from referenced module projects.");
        source.AppendLine("/// </summary>");
        source.AppendLine("public static partial class GeneratedWolverineHandlerCatalog");
        source.AppendLine("{");
        source.AppendLine("    static partial void RegisterGenerated(WolverineOptions opts)");
        source.AppendLine("    {");

        foreach (var registration in handlerRegistrations)
        {
            source.AppendLine($"        {registration}.Register(opts);");
        }

        source.AppendLine("    }");
        source.AppendLine("}");
        source.AppendLine("#pragma warning restore CS1591");

        return source.ToString();
    }

    /// <summary>
    /// Captures reference-derived catalog inputs for one compilation.
    /// </summary>
    /// <param name="assemblyName">The current compilation assembly name.</param>
    /// <param name="moduleProviders">The distinct discovered module provider type names.</param>
    /// <param name="handlerRegistrations">The distinct discovered Wolverine registration type names.</param>
    /// <param name="hasAeroModular">Whether the compilation directly references Aero.Modular.</param>
    /// <param name="hasWolverine">Whether the compilation directly references Wolverine.</param>
    private readonly struct HostCatalogInfo(
        string assemblyName,
        ImmutableArray<string> moduleProviders,
        ImmutableArray<string> handlerRegistrations,
        bool hasAeroModular,
        bool hasWolverine)
    {
        /// <summary>
        /// Gets the current compilation assembly name.
        /// </summary>
        public string AssemblyName { get; } = assemblyName;
        /// <summary>
        /// Gets the discovered module-provider type names.
        /// </summary>
        public ImmutableArray<string> ModuleProviders { get; } = moduleProviders;
        /// <summary>
        /// Gets the discovered Wolverine registration type names.
        /// </summary>
        public ImmutableArray<string> HandlerRegistrations { get; } = handlerRegistrations;
        /// <summary>
        /// Gets whether Aero.Modular is directly referenced.
        /// </summary>
        public bool HasAeroModular { get; } = hasAeroModular;
        /// <summary>
        /// Gets whether Wolverine or WolverineFx is directly referenced.
        /// </summary>
        public bool HasWolverine { get; } = hasWolverine;
    }
}
