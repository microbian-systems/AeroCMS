using System.Globalization;
using Aero.Cms.Abstractions.Theming;
using Aero.Cms.Modules.Theming;

namespace Aero.Cms.Core.Tests.Theming;

public sealed class ThemeCssCompilerTests
{
    [Test]
    public async Task Compiler_is_deterministic_culture_invariant_and_emits_shape_variables()
    {
        var tokens = new ThemeTokenSet { Shape = new ThemeShapeTokens { RadiusSelectorRem = 1.25m, RadiusFieldRem = .5m, RadiusBoxRem = .75m, SizeSelectorRem = 1m, SizeFieldRem = 1.5m, BorderRem = .125m, Depth = 1, Noise = 0 } };
        var compiler = new ThemeCssCompiler();
        var original = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("fr-FR");
            var first = compiler.Compile("tenant-theme", tokens);
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("en-US");
            var second = compiler.Compile("tenant-theme", tokens);
            await Assert.That(first.Css).IsEqualTo(second.Css);
            await Assert.That(first.Sha256).IsEqualTo(second.Sha256);
            await Assert.That(first.Css).Contains("--radius-selector:1.25rem;--radius-field:0.5rem;--radius-box:0.75rem;--size-selector:1rem;--size-field:1.5rem;--border:0.125rem;--depth:1;--noise:0;");
        }
        finally { CultureInfo.CurrentCulture = original; }
    }

    [Test]
    public async Task Structural_validation_allows_draft_warning_but_publish_blocks_low_contrast()
    {
        var tokens = new ThemeTokenSet(); tokens.Light.Primary = "#ffffff"; tokens.Light.PrimaryContent = "#ffffff";
        ThemeTokenValidator.ThrowIfInvalid(tokens);
        await Assert.That(ThemeTokenValidator.GetContrastWarnings(tokens)).IsNotEmpty();
        await Assert.That(() => new ThemeCssCompiler().Compile("tenant-theme", tokens)).Throws<ArgumentException>();
    }

    [Test]
    public async Task Publish_validation_checks_all_base_surfaces_against_base_content()
    {
        var tokens = new ThemeTokenSet();
        tokens.Light.Base200 = "#ffffff";
        tokens.Light.Base300 = "#ffffff";
        tokens.Light.BaseContent = "#ffffff";

        var warnings = ThemeTokenValidator.GetContrastWarnings(tokens);

        await Assert.That(warnings.Any(warning => warning.Message.Contains("base 200", StringComparison.OrdinalIgnoreCase))).IsTrue();
        await Assert.That(warnings.Any(warning => warning.Message.Contains("base 300", StringComparison.OrdinalIgnoreCase))).IsTrue();
        await Assert.That(() => new ThemeCssCompiler().Compile("tenant-theme", tokens)).Throws<ArgumentException>();
    }

    [Test]
    public async Task Strict_theme_json_rejects_unknown_fields()
    {
        var json = "{\"schemaVersion\":1,\"theme\":{\"name\":\"A\",\"slug\":\"a\",\"tokens\":{}},\"unexpected\":true}";
        await Assert.That(() => System.Text.Json.JsonSerializer.Deserialize(json, ThemeJsonContext.Default.ThemeImportEnvelope)).Throws<System.Text.Json.JsonException>();
    }

    [Test]
    public async Task Strict_theme_json_rejects_missing_token_groups()
    {
        var json = "{\"schemaVersion\":1,\"theme\":{\"name\":\"A\",\"slug\":\"a\",\"tokens\":{\"defaultMode\":0}}}";

        await Assert.That(() => System.Text.Json.JsonSerializer.Deserialize(
                json,
                ThemeJsonContext.Default.ThemeImportEnvelope))
            .Throws<System.Text.Json.JsonException>();
    }

    [Test]
    public async Task Preview_compilation_allows_low_contrast_while_publish_compilation_blocks_it()
    {
        var tokens = new ThemeTokenSet();
        tokens.Light.Primary = "#ffffff";
        tokens.Light.PrimaryContent = "#ffffff";
        var compiler = new ThemeCssCompiler();

        var preview = compiler.CompilePreview("tenant-preview", tokens);

        await Assert.That(preview.Css).Contains("[data-theme=tenant-preview]");
        await Assert.That(() => compiler.Compile("tenant-theme", tokens)).Throws<ArgumentException>();
    }
}
