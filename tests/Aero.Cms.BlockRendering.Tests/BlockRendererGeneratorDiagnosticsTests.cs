using Aero.Cms.SourceGenerators;
using FluentAssertions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using TUnit.Core;

namespace Aero.Cms.BlockRendering.Tests;

public sealed class BlockRendererGeneratorDiagnosticsTests
{
    [Test]
    public void Generator_EmitsTypedRenderTreeBuilderAdapter()
    {
        var result = RunGeneratorWithResult(
            """
            namespace Demo;

            [Aero.Cms.Abstractions.Blocks.BlockMetadata("demo", "Demo")]
            public sealed class DemoBlock : Aero.Cms.Abstractions.Blocks.BlockBase
            {
                public override string BlockType => "demo";
            }

            [Aero.Cms.Shared.Blocks.Rendering.CmsBlockRenderer(typeof(DemoBlock))]
            public sealed class DemoRenderer
            {
                public DemoBlock Block { get; set; } = default!;
            }
            """);

        result.Diagnostics.Should().BeEmpty();
        result.GetGeneratedSource("CmsBlockRendering.g.cs").Should().Contain("public static partial class CmsBlockManifest");
        result.GetGeneratedSource("CmsBlockRendering.g.cs").Should().Contain("new CmsBlockDescriptor(\"demo\", \"Demo\"");
        result.GetGeneratedSource("CmsBlockRendering.g.cs").Should().Contain("builder.OpenComponent<global::Demo.DemoRenderer>(1);");
        result.GetGeneratedSource("CmsBlockRendering.g.cs").Should().Contain("builder.AddAttribute(2, \"Block\", typedBlock);");
        result.GetGeneratedSource("CmsBlockRendering.g.cs").Should().Contain("[\"demo\"] = new DemoBlockRenderAdapter()");
    }

    [Test]
    public void Generator_EmitsMetadataOnlyBlockArtifacts()
    {
        var result = RunGeneratorWithResult(
            """
            namespace Demo;

            [Aero.Cms.Abstractions.Blocks.BlockMetadata("demo", "Demo", Category = "General", SortOrder = 10, SchemaVersion = 3)]
            public sealed class DemoBlock : Aero.Cms.Abstractions.Blocks.BlockBase
            {
                public override string BlockType => "demo";
            }
            """);

        result.Diagnostics.Should().BeEmpty();

        var manifestSource = result.GetGeneratedSource("GeneratedBlockModelManifest.g.cs");
        manifestSource.Should().Contain("public static partial class GeneratedBlockModelManifest");
        manifestSource.Should().Contain("new GeneratedBlockModelDescriptor(\"demo\", \"Demo\"");
        manifestSource.Should().Contain("10, 3, typeof(global::Demo.DemoBlock)");
        manifestSource.Should().Contain("typeof(global::Demo.DemoBlock)");

        var jsonSource = result.GetGeneratedSource("GeneratedBlockJsonRegistration.g.cs");
        jsonSource.Should().Contain("public static partial class GeneratedBlockJsonRegistration");
        jsonSource.Should().Contain("typeof(global::Demo.DemoBlock)");
        jsonSource.Should().Contain("typeof(List<global::Demo.DemoBlock>)");
    }

    [Test]
    public void Generator_ReportsDuplicateBlockType()
    {
        var diagnostics = RunGenerator(
            """
            namespace Demo;

            [Aero.Cms.Abstractions.Blocks.BlockMetadata("demo", "Demo")]
            public sealed class FirstBlock : Aero.Cms.Abstractions.Blocks.BlockBase
            {
                public override string BlockType => "demo";
            }

            [Aero.Cms.Abstractions.Blocks.BlockMetadata("demo", "Demo")]
            public sealed class SecondBlock : Aero.Cms.Abstractions.Blocks.BlockBase
            {
                public override string BlockType => "demo";
            }

            [Aero.Cms.Shared.Blocks.Rendering.CmsBlockRenderer(typeof(FirstBlock))]
            public sealed class FirstRenderer
            {
                public FirstBlock Block { get; set; } = default!;
            }

            [Aero.Cms.Shared.Blocks.Rendering.CmsBlockRenderer(typeof(SecondBlock))]
            public sealed class SecondRenderer
            {
                public SecondBlock Block { get; set; } = default!;
            }
            """);

        diagnostics.Should().Contain(diagnostic => diagnostic.Id == "AERO001");
    }

    [Test]
    public void Generator_ReportsDuplicateBlockModelMetadata()
    {
        var diagnostics = RunGenerator(
            """
            namespace Demo;

            [Aero.Cms.Abstractions.Blocks.BlockMetadata("demo", "Demo")]
            public sealed class FirstBlock : Aero.Cms.Abstractions.Blocks.BlockBase
            {
                public override string BlockType => "demo";
            }

            [Aero.Cms.Abstractions.Blocks.BlockMetadata("demo", "Duplicate Demo")]
            public sealed class SecondBlock : Aero.Cms.Abstractions.Blocks.BlockBase
            {
                public override string BlockType => "demo";
            }
            """);

        diagnostics.Should().Contain(diagnostic => diagnostic.Id == "AERO006");
    }

    [Test]
    public void Generator_ReportsBlockParameterTypeMismatch()
    {
        var diagnostics = RunGenerator(
            """
            namespace Demo;

            [Aero.Cms.Abstractions.Blocks.BlockMetadata("demo", "Demo")]
            public sealed class DemoBlock : Aero.Cms.Abstractions.Blocks.BlockBase
            {
                public override string BlockType => "demo";
            }

            [Aero.Cms.Abstractions.Blocks.BlockMetadata("other", "Other")]
            public sealed class OtherBlock : Aero.Cms.Abstractions.Blocks.BlockBase
            {
                public override string BlockType => "other";
            }

            [Aero.Cms.Shared.Blocks.Rendering.CmsBlockRenderer(typeof(DemoBlock))]
            public sealed class DemoRenderer
            {
                public OtherBlock Block { get; set; } = default!;
            }
            """);

        diagnostics.Should().Contain(diagnostic => diagnostic.Id == "AERO003");
    }

    [Test]
    public void Generator_ReportsInvalidModelType()
    {
        var diagnostics = RunGenerator(
            """
            namespace Demo;

            public sealed class NotABlock
            {
            }

            [Aero.Cms.Shared.Blocks.Rendering.CmsBlockRenderer(typeof(NotABlock))]
            public sealed class DemoRenderer
            {
                public NotABlock Block { get; set; } = default!;
            }
            """);

        diagnostics.Should().Contain(diagnostic => diagnostic.Id == "AERO004");
    }

    private static IReadOnlyList<Diagnostic> RunGenerator(string source)
        => RunGeneratorWithResult(source).Diagnostics;

    private static GeneratorTestResult RunGeneratorWithResult(string source)
    {
        var syntaxTrees = new[]
        {
            CSharpSyntaxTree.ParseText(ContractsSource),
            CSharpSyntaxTree.ParseText(source)
        };

        var references = GetReferences();
        var compilation = CSharpCompilation.Create(
            "Aero.Cms.BlockRendererGenerator.Tests",
            syntaxTrees,
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        GeneratorDriver driver = CSharpGeneratorDriver.Create(new BlockRendererGenerator());
        driver = driver.RunGeneratorsAndUpdateCompilation(
            compilation,
            out var outputCompilation,
            out var generatorDiagnostics);

        var diagnostics = generatorDiagnostics
            .Concat(outputCompilation.GetDiagnostics())
            .Where(diagnostic => diagnostic.Id.StartsWith("AERO", StringComparison.Ordinal))
            .ToArray();

        var generatedSources = driver
            .GetRunResult()
            .GeneratedTrees
            .ToDictionary(
                tree => Path.GetFileName(tree.FilePath),
                tree => tree.GetText().ToString(),
                StringComparer.Ordinal);

        return new GeneratorTestResult(diagnostics, generatedSources);
    }

    private static MetadataReference[] GetReferences()
    {
        var trustedPlatformAssemblies = (string?)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES");
        var references = trustedPlatformAssemblies?
            .Split(Path.PathSeparator)
            .Select(path => MetadataReference.CreateFromFile(path))
            .ToList() ?? [];

        references.Add(MetadataReference.CreateFromFile(typeof(Microsoft.AspNetCore.Components.RenderFragment).Assembly.Location));
        references.Add(MetadataReference.CreateFromFile(typeof(Microsoft.AspNetCore.Html.IHtmlContent).Assembly.Location));

        return references
            .GroupBy(reference => reference.Display, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .ToArray();
    }

    private const string ContractsSource =
        """
        using System;
        using System.Text.Json.Serialization;
        using Microsoft.AspNetCore.Components;
        using Microsoft.AspNetCore.Html;

        namespace Aero.Cms.Abstractions.Blocks
        {
            [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
            public sealed class BlockMetadataAttribute : Attribute
            {
                public BlockMetadataAttribute(string blockType, string displayName)
                {
                    BlockType = blockType;
                    DisplayName = displayName;
                }

                public string BlockType { get; }

                public string DisplayName { get; }

                public string? Description { get; set; }

                public string? Category { get; set; }

                public string? Icon { get; set; }

                public int SortOrder { get; set; }

                public int SchemaVersion { get; set; } = 1;
            }

            public interface IBlock
            {
                string BlockType { get; }
            }

            public interface IBlockVisitor
            {
            }

            public abstract class BlockBase : IBlock
            {
                public abstract string BlockType { get; }

                public virtual IHtmlContent Accept(IBlockVisitor visitor) => default!;
            }
        }

        namespace Aero.Cms.Shared.Blocks.Rendering
        {
            [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
            public sealed class CmsBlockRendererAttribute : Attribute
            {
                public CmsBlockRendererAttribute(Type modelType)
                {
                    ModelType = modelType;
                }

                public Type ModelType { get; }
            }

            public sealed record BlockRenderContext;

            public interface ICmsBlockRenderAdapter
            {
                string BlockType { get; }

                Type ModelType { get; }

                RenderFragment Render(Aero.Cms.Abstractions.Blocks.IBlock block, BlockRenderContext context);
            }

            public sealed record CmsBlockDescriptor(
                string BlockType,
                string DisplayName,
                string? Description,
                string? Category,
                string? Icon,
                int SortOrder,
                int SchemaVersion,
                Type ModelType,
                Type RendererType,
                string RendererParameterName);
        }
        """;

    private sealed record GeneratorTestResult(
        IReadOnlyList<Diagnostic> Diagnostics,
        IReadOnlyDictionary<string, string> GeneratedSources)
    {
        public string GetGeneratedSource(string hintName)
            => GeneratedSources.TryGetValue(hintName, out var source) ? source : string.Empty;
    }
}
