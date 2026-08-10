using Aero.Cms.SourceGenerators;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Aero.Cms.Hosting.Tests;

public sealed class HostModuleCatalogGeneratorTests
{
    private static readonly MetadataReference[] PlatformReferences =
        ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!)
        .Split(Path.PathSeparator)
        .Where(static path => Path.GetFileName(path) is
            "System.Private.CoreLib.dll" or
            "System.Runtime.dll" or
            "netstandard.dll")
        .Select(static path => MetadataReference.CreateFromFile(path))
        .ToArray();

    [Test]
    public async Task Generator_is_inert_without_explicit_host_opt_in()
    {
        var result = RunGenerator(Compilation("Consumer", "public sealed class Program { }"));

        await Assert.That(result.Results.Single().GeneratedSources).IsEmpty();
        await Assert.That(result.Diagnostics).IsEmpty();
    }

    [Test]
    public async Task Opted_in_empty_catalog_reports_fail_closed_diagnostic()
    {
        var compilation = Compilation("Consumer", OptedInHostSource, CreateOptInReference());
        var attributeNames = compilation.Assembly.GetAttributes()
            .Select(static attribute => attribute.AttributeClass?.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat))
            .ToArray();
        await Assert.That(string.Join(",", attributeNames))
            .Contains("global::Aero.Cms.Hosting.AeroCmsHostCatalogGenerationAttribute");
        var result = RunGenerator(compilation);

        var diagnosticIds = result.Diagnostics
            .Concat(result.Results.Single().Diagnostics)
            .Select(static diagnostic => diagnostic.Id)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        var generatedNames = result.Results.Single().GeneratedSources
            .Select(static source => source.HintName)
            .ToArray();
        await Assert.That($"diagnostics={string.Join(",", diagnosticIds)};sources={string.Join(",", generatedNames)}")
            .IsEqualTo("diagnostics=AERO050;sources=");
        await Assert.That(result.Results.Single().GeneratedSources).IsEmpty();
    }

    [Test]
    public async Task Generated_catalog_orders_referenced_manifests_and_handlers_deterministically()
    {
        var featureReference = EmitReference(Compilation("Feature.Package", FeaturePackageSource));
        var result = RunGenerator(Compilation("Consumer.Host", OptedInHostSource, CreateOptInReference(), featureReference));
        var generated = result.Results.Single().GeneratedSources
            .ToDictionary(static source => source.HintName, static source => source.SourceText.ToString());

        await Assert.That(generated.Keys).Contains("GeneratedAeroModuleCatalog.g.cs");
        await Assert.That(generated.Keys).Contains("GeneratedWolverineHandlerCatalog.g.cs");
        await Assert.That(generated["GeneratedAeroModuleCatalog.g.cs"].IndexOf("AProvider", StringComparison.Ordinal))
            .IsLessThan(generated["GeneratedAeroModuleCatalog.g.cs"].IndexOf("ZProvider", StringComparison.Ordinal));
        await Assert.That(generated["GeneratedWolverineHandlerCatalog.g.cs"].IndexOf("AHandlers", StringComparison.Ordinal))
            .IsLessThan(generated["GeneratedWolverineHandlerCatalog.g.cs"].IndexOf("ZHandlers", StringComparison.Ordinal));
    }

    private static GeneratorDriverRunResult RunGenerator(CSharpCompilation compilation)
    {
        GeneratorDriver driver = CSharpGeneratorDriver.Create(new HostModuleCatalogGenerator());
        driver = driver.RunGeneratorsAndUpdateCompilation(
            compilation,
            out _,
            out _);
        return driver.GetRunResult();
    }

    private static CSharpCompilation Compilation(
        string assemblyName,
        string source,
        params MetadataReference[] additionalReferences)
        => CSharpCompilation.Create(
            assemblyName,
            [CSharpSyntaxTree.ParseText(source)],
            PlatformReferences.Concat(additionalReferences),
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

    private static MetadataReference EmitReference(CSharpCompilation compilation)
    {
        using var stream = new MemoryStream();
        var result = compilation.Emit(stream);
        if (!result.Success)
        {
            throw new InvalidOperationException(string.Join(Environment.NewLine, result.Diagnostics));
        }

        return MetadataReference.CreateFromImage(stream.ToArray());
    }

    private static MetadataReference CreateOptInReference()
        => EmitReference(Compilation("Aero.Cms.Hosting.Contracts", """
            using System;
            namespace Aero.Cms.Hosting
            {
                [AttributeUsage(AttributeTargets.Assembly)]
                public sealed class AeroCmsHostCatalogGenerationAttribute : Attribute { }
            }
            """));

    private const string OptedInHostSource = """
        [assembly: Aero.Cms.Hosting.AeroCmsHostCatalogGenerationAttribute]
        public sealed class Program { }
        """;

    private const string FeaturePackageSource = """
        using System;
        [assembly: Aero.Modular.ModuleManifestProvider(typeof(Feature.ZProvider))]
        [assembly: Aero.Modular.ModuleManifestProvider(typeof(Feature.AProvider))]
        [assembly: Aero.Modular.WolverineHandlersRegistration(typeof(Feature.ZHandlers))]
        [assembly: Aero.Modular.WolverineHandlersRegistration(typeof(Feature.AHandlers))]
        namespace Aero.Modular
        {
            [AttributeUsage(AttributeTargets.Assembly, AllowMultiple = true)]
            public sealed class ModuleManifestProviderAttribute(Type type) : Attribute { }
            [AttributeUsage(AttributeTargets.Assembly, AllowMultiple = true)]
            public sealed class WolverineHandlersRegistrationAttribute(Type type) : Attribute { }
        }
        namespace Feature
        {
            public sealed class AProvider { }
            public sealed class ZProvider { }
            public sealed class AHandlers { }
            public sealed class ZHandlers { }
        }
        """;
}
