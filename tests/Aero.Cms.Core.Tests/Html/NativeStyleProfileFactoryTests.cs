using Aero.Cms.Html;
using Aero.Core;
using Aero.Core.Railway;

namespace Aero.Cms.Core.Tests.Html;

public sealed class NativeStyleProfileFactoryTests
{
    [Test]
    public async Task Create_normalizes_names_hex_values_and_profile_identity()
    {
        var result = NativeStyleProfileFactory.Normalize(42, new StyleProfileSettings
        {
            Revision = 7,
            SmallScreenBreakpointRem = 52,
            ColorTokens =
            [
                new StyleColorToken { Name = "Accent Color", HexValue = "AbC8" },
                new StyleColorToken { Name = "Brand_Primary", HexValue = "#123456" }
            ]
        });

        var ok = result as Result<NormalizedNativeStyleProfile, AeroError>.Ok;
        await Assert.That(ok).IsNotNull();
        await Assert.That(ok!.Value.Profile.ProfileId).IsEqualTo("aero-native/site/42");
        await Assert.That(ok.Value.Profile.ProfileVersion).IsEqualTo("7");
        await Assert.That(ok.Value.Settings.ColorTokens[0].Name).IsEqualTo("accent-color");
        await Assert.That(ok.Value.Settings.ColorTokens[0].HexValue).IsEqualTo("#aabbcc88");
        await Assert.That(ok.Value.Settings.ColorTokens[1].Name).IsEqualTo("brand-primary");
        await Assert.That(ok.Value.Profile.ColorTokens["brand-primary"]).IsEqualTo("#123456");
    }

    [Test]
    public async Task Create_rejects_duplicate_normalized_names_and_invalid_limits()
    {
        var result = NativeStyleProfileFactory.Create(42, new StyleProfileSettings
        {
            Revision = 0,
            SmallScreenBreakpointRem = 121,
            ColorTokens =
            [
                new StyleColorToken { Name = "Brand Primary", HexValue = "#fff" },
                new StyleColorToken { Name = "brand-primary", HexValue = "#000" },
                new StyleColorToken { Name = "9invalid", HexValue = "#12345" }
            ]
        });

        var failure = result as Result<NativeStyleProfile, AeroError>.Failure;
        await Assert.That(failure).IsNotNull();
        await Assert.That(failure!.Error).IsTypeOf<AeroError.Validation>();
        var validation = (AeroError.Validation)failure.Error;
        await Assert.That(validation.Errors.Any(error => error.Contains("revision", StringComparison.OrdinalIgnoreCase))).IsTrue();
        await Assert.That(validation.Errors.Any(error => error.Contains("breakpoint", StringComparison.OrdinalIgnoreCase))).IsTrue();
        await Assert.That(validation.Errors.Any(error => error.Contains("more than once", StringComparison.OrdinalIgnoreCase))).IsTrue();
    }

    [Test]
    public async Task Create_accepts_all_supported_hex_lengths()
    {
        var result = NativeStyleProfileFactory.Normalize(1, new StyleProfileSettings
        {
            ColorTokens =
            [
                new StyleColorToken { Name = "three", HexValue = "#abc" },
                new StyleColorToken { Name = "four", HexValue = "#abcd" },
                new StyleColorToken { Name = "six", HexValue = "#abcdef" },
                new StyleColorToken { Name = "eight", HexValue = "#abcdef12" }
            ]
        });

        var ok = result as Result<NormalizedNativeStyleProfile, AeroError>.Ok;
        await Assert.That(ok).IsNotNull();
        await Assert.That(ok!.Value.Profile.ColorTokens["three"]).IsEqualTo("#aabbcc");
        await Assert.That(ok.Value.Profile.ColorTokens["four"]).IsEqualTo("#aabbccdd");
        await Assert.That(ok.Value.Profile.ColorTokens["six"]).IsEqualTo("#abcdef");
        await Assert.That(ok.Value.Profile.ColorTokens["eight"]).IsEqualTo("#abcdef12");
    }
}
