using System.Globalization;
using System.Text;
using Aero.Core;
using Aero.Core.Railway;

namespace Aero.Cms.Html;

/// <summary>
/// Validates and normalizes persisted native-CSS style-profile settings.
/// </summary>
public static class NativeStyleProfileFactory
{
    private const int MaximumColorTokens = 64;

    /// <summary>
    /// Creates a runtime profile after applying the same normalization used for persisted settings.
    /// </summary>
    /// <param name="siteId">The positive site identity included in the profile identifier.</param>
    /// <param name="settings">The persisted settings to validate and normalize.</param>
    /// <returns>The runtime profile, or all discovered validation errors.</returns>
    public static Result<NativeStyleProfile, AeroError> Create(
        long siteId,
        StyleProfileSettings settings)
    {
        var normalization = Normalize(siteId, settings);
        return normalization switch
        {
            Result<NormalizedNativeStyleProfile, AeroError>.Ok ok =>
                new Result<NativeStyleProfile, AeroError>.Ok(ok.Value.Profile),
            Result<NormalizedNativeStyleProfile, AeroError>.Failure failure =>
                new Result<NativeStyleProfile, AeroError>.Failure(failure.Error),
            _ => new Result<NativeStyleProfile, AeroError>.Failure(
                AeroError.CreateError("Unexpected style-profile normalization result."))
        };
    }

    /// <summary>
    /// Produces canonical persisted settings and their matching runtime profile as one validation operation.
    /// </summary>
    /// <param name="siteId">The positive site identity included in the profile identifier.</param>
    /// <param name="settings">The persisted settings to validate and normalize.</param>
    /// <returns>Sorted canonical tokens and the resolved profile, or all discovered validation errors.</returns>
    public static Result<NormalizedNativeStyleProfile, AeroError> Normalize(
        long siteId,
        StyleProfileSettings settings)
    {
        if (settings is null)
        {
            return new Result<NormalizedNativeStyleProfile, AeroError>.Failure(
                AeroError.ValidationError(["Style-profile settings are required."]));
        }

        var errors = new List<string>();
        if (siteId <= 0)
            errors.Add("The site id must be greater than zero.");
        if (settings.Revision < 1)
            errors.Add("The style-profile revision must be at least 1.");
        if (settings.SmallScreenBreakpointRem is < 20 or > 120)
            errors.Add("The small-screen breakpoint must be between 20 and 120 rem.");

        var sourceTokens = settings.ColorTokens ?? [];
        if (sourceTokens.Count > MaximumColorTokens)
            errors.Add($"A style profile may define at most {MaximumColorTokens} color tokens.");

        var normalizedTokens = new List<StyleColorToken>(Math.Min(sourceTokens.Count, MaximumColorTokens));
        var resolvedTokens = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var token in sourceTokens.Take(MaximumColorTokens))
        {
            if (token is null)
            {
                errors.Add("Color tokens cannot be null.");
                continue;
            }

            var normalizedName = NormalizeTokenName(token.Name);
            if (!IsValidTokenName(normalizedName))
            {
                errors.Add($"'{token.Name}' is not a valid color-token name.");
                continue;
            }

            if (!TryNormalizeHex(token.HexValue, out var normalizedHex))
            {
                errors.Add($"Color token '{normalizedName}' must use a 3, 4, 6, or 8 digit hexadecimal value.");
                continue;
            }

            if (!resolvedTokens.TryAdd(normalizedName, normalizedHex))
            {
                errors.Add($"Color token '{normalizedName}' is defined more than once.");
                continue;
            }

            normalizedTokens.Add(new StyleColorToken
            {
                Name = normalizedName,
                HexValue = normalizedHex
            });
        }

        if (errors.Count > 0)
            return new Result<NormalizedNativeStyleProfile, AeroError>.Failure(
                AeroError.ValidationError(errors));

        normalizedTokens.Sort(static (left, right) =>
            StringComparer.Ordinal.Compare(left.Name, right.Name));
        var profileTokens = normalizedTokens.ToDictionary(
            static token => token.Name,
            static token => token.HexValue,
            StringComparer.Ordinal);

        var normalizedSettings = new StyleProfileSettings
        {
            Revision = settings.Revision,
            SmallScreenBreakpointRem = settings.SmallScreenBreakpointRem,
            ColorTokens = normalizedTokens
        };

        var profile = new NativeStyleProfile
        {
            ProfileId = $"aero-native/site/{siteId.ToString(CultureInfo.InvariantCulture)}",
            ProfileVersion = settings.Revision.ToString(CultureInfo.InvariantCulture),
            SmallScreenBreakpointRem = settings.SmallScreenBreakpointRem,
            ColorTokens = profileTokens
        };

        return new Result<NormalizedNativeStyleProfile, AeroError>.Ok(
            new NormalizedNativeStyleProfile(normalizedSettings, profile));
    }

    /// <summary>Converts an author token label to lower-case kebab form using ASCII letters and digits.</summary>
    private static string NormalizeTokenName(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        var builder = new StringBuilder(value.Length);
        var pendingSeparator = false;
        foreach (var character in value.Trim())
        {
            if (character is >= 'A' and <= 'Z')
            {
                if (pendingSeparator && builder.Length > 0)
                    builder.Append('-');
                pendingSeparator = false;
                builder.Append((char)(character + ('a' - 'A')));
            }
            else if (character is >= 'a' and <= 'z' or >= '0' and <= '9')
            {
                if (pendingSeparator && builder.Length > 0)
                    builder.Append('-');
                pendingSeparator = false;
                builder.Append(character);
            }
            else
            {
                pendingSeparator = builder.Length > 0;
            }
        }

        return builder.ToString();
    }

    /// <summary>Enforces the bounded CSS-safe token-name grammar after normalization.</summary>
    private static bool IsValidTokenName(string value)
    {
        if (value.Length is < 1 or > 64 || value[0] is < 'a' or > 'z')
            return false;

        return value.All(static character =>
            character is >= 'a' and <= 'z' or >= '0' and <= '9' or '-');
    }

    /// <summary>Normalizes supported hexadecimal forms to lower-case six- or eight-digit values with a hash prefix.</summary>
    private static bool TryNormalizeHex(string? value, out string normalized)
    {
        normalized = string.Empty;
        var digits = value?.Trim().TrimStart('#');
        if (digits is null || digits.Length is not (3 or 4 or 6 or 8) ||
            !digits.All(static character =>
                character is >= '0' and <= '9' or >= 'a' and <= 'f' or >= 'A' and <= 'F'))
        {
            return false;
        }

        digits = digits.ToLowerInvariant();
        if (digits.Length is 3 or 4)
        {
            var expanded = new StringBuilder(digits.Length * 2);
            foreach (var character in digits)
                expanded.Append(character, 2);
            digits = expanded.ToString();
        }

        normalized = $"#{digits}";
        return true;
    }
}

/// <summary>Pairs canonical persisted settings with the runtime profile derived from them.</summary>
/// <param name="Settings">The normalized, sorted settings suitable for persistence.</param>
/// <param name="Profile">The runtime profile resolved from those settings.</param>
public sealed record NormalizedNativeStyleProfile(
    StyleProfileSettings Settings,
    NativeStyleProfile Profile);
