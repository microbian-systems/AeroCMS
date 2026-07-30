using System.Text.RegularExpressions;
using Aero.Cms.Abstractions.Ai.Assistant;
using Aero.Cms.Abstractions.Ai.Pipeline;
using Aero.Core;
using Aero.Core.Railway;

namespace Aero.Cms.Modules.AiAssistant;

/// <summary>Immutable input to the assistant's final output-security boundary.</summary>
public sealed record AeroCmsAssistantOutputPolicyContext(
    AeroAiAudience Audience,
    string Text,
    IReadOnlyList<AeroCmsAssistantCitation> Citations);

/// <summary>Checks a complete provider response before it is persisted or returned.</summary>
public interface IAeroCmsAssistantOutputPolicy
{
    Result<string> Evaluate(AeroCmsAssistantOutputPolicyContext context);
}

/// <summary>
/// Rejects likely secrets, high-risk identifiers, invalid citations, and ungrounded public answers.
/// The policy intentionally evaluates a complete response so streamed fragments cannot bypass it.
/// </summary>
public sealed partial class AeroCmsAssistantOutputPolicy : IAeroCmsAssistantOutputPolicy
{
    private const string SafeFailure = "Assistant output did not satisfy the server output policy.";

    public Result<string> Evaluate(AeroCmsAssistantOutputPolicyContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (string.IsNullOrWhiteSpace(context.Text) ||
            context.Text.Length > AeroCmsAssistantLimits.MaxOutputCharacters)
        {
            return AeroError.ValidationError(["Assistant output was empty or exceeded the allowed size."]);
        }

        if (ContainsSensitiveValue(context.Text))
            return AeroError.ForbiddenError(SafeFailure);

        var citationIds = context.Citations
            .Select(citation => citation.Id)
            .ToHashSet(StringComparer.Ordinal);
        var references = CitationRegex()
            .Matches(context.Text)
            .Select(match => match.Value[1..^1])
            .ToArray();
        if (references.Any(reference => !citationIds.Contains(reference)))
            return AeroError.ForbiddenError(SafeFailure);

        if (context.Audience is AeroAiAudience.Public or AeroAiAudience.Member &&
            (citationIds.Count == 0 || references.Length == 0))
        {
            return AeroError.ForbiddenError(SafeFailure);
        }

        return context.Text;
    }

    private static bool ContainsSensitiveValue(string text)
    {
        if (text.Contains("-----BEGIN PRIVATE KEY-----", StringComparison.OrdinalIgnoreCase) ||
            text.Contains("-----BEGIN RSA PRIVATE KEY-----", StringComparison.OrdinalIgnoreCase) ||
            BearerTokenRegex().IsMatch(text) ||
            CredentialAssignmentRegex().IsMatch(text) ||
            CommonSecretPrefixRegex().IsMatch(text) ||
            SocialSecurityNumberRegex().IsMatch(text))
        {
            return true;
        }

        foreach (Match match in PaymentCardCandidateRegex().Matches(text))
        {
            var digits = new string(match.Value.Where(char.IsAsciiDigit).ToArray());
            if (digits.Length is >= 13 and <= 19 && PassesLuhn(digits))
                return true;
        }

        return false;
    }

    private static bool PassesLuhn(string digits)
    {
        var sum = 0;
        var doubleDigit = false;
        for (var index = digits.Length - 1; index >= 0; index--)
        {
            var value = digits[index] - '0';
            if (doubleDigit)
            {
                value *= 2;
                if (value > 9)
                    value -= 9;
            }

            sum += value;
            doubleDigit = !doubleDigit;
        }

        return sum % 10 == 0;
    }

    [GeneratedRegex(@"\[CMS-[1-9][0-9]{0,2}\]", RegexOptions.CultureInvariant)]
    private static partial Regex CitationRegex();

    [GeneratedRegex(@"\bBearer\s+[A-Za-z0-9\-._~+/]+=*\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex BearerTokenRegex();

    [GeneratedRegex(
        @"\b(?:api[_-]?key|client[_-]?secret|password|connection[_-]?string)\s*[:=]\s*['""]?[^\s,'""]{8,}",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex CredentialAssignmentRegex();

    [GeneratedRegex(
        @"\b(?:sk-(?:proj-)?[A-Za-z0-9_-]{16,}|gh[pousr]_[A-Za-z0-9]{20,}|AIza[A-Za-z0-9_-]{20,})\b",
        RegexOptions.CultureInvariant)]
    private static partial Regex CommonSecretPrefixRegex();

    [GeneratedRegex(@"\b(?!000|666|9\d\d)\d{3}[- ](?!00)\d{2}[- ](?!0000)\d{4}\b", RegexOptions.CultureInvariant)]
    private static partial Regex SocialSecurityNumberRegex();

    [GeneratedRegex(@"(?<!\d)(?:\d[ -]?){13,19}(?!\d)", RegexOptions.CultureInvariant)]
    private static partial Regex PaymentCardCandidateRegex();
}
