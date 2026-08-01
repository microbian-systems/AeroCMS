using System.Security.Cryptography;
using System.Text;
using Aero.Cms.Abstractions.Pages.Rendering;
using Aero.Core;
using Aero.Core.Railway;

namespace Aero.Cms.Modules.Pages.Rendering;

internal static class SourcePageRendererValidation
{
    public static Result<PageRenderSource> Validate(
        PageRenderRequest request,
        string rendererId,
        string displayName)
    {
        var selectedRendererId =
            PageRendererIds.NormalizeOrDefault(request.Metadata.RendererId);
        if (!string.Equals(selectedRendererId, rendererId, StringComparison.Ordinal))
        {
            return AeroError.ValidationError(
                [$"The page metadata does not select the {displayName} renderer."]);
        }

        if (request.Source is not { } source)
        {
            return AeroError.ValidationError(
                [$"A {displayName} page requires an exact source version."]);
        }

        if (!string.Equals(
                PageRendererIds.NormalizeOrDefault(source.RendererId),
                selectedRendererId,
                StringComparison.Ordinal))
        {
            return AeroError.ValidationError(
                ["The page source version does not match the selected renderer."]);
        }

        if (source.VersionId < 0 || source.VersionId == 0 && !request.IsPreview)
        {
            return AeroError.ValidationError(
                ["Only preview rendering may use an unpersisted source version."]);
        }

        if (string.IsNullOrWhiteSpace(source.Source))
        {
            return AeroError.ValidationError(
                [$"{displayName} page source cannot be blank."]);
        }

        var expectedHash = Convert.ToHexString(
                SHA256.HashData(Encoding.UTF8.GetBytes(source.Source)))
            .ToLowerInvariant();
        if (!string.Equals(source.SourceHash, expectedHash, StringComparison.Ordinal))
        {
            return AeroError.ValidationError(
                [$"The {displayName} page source hash does not match its exact source."]);
        }

        return source;
    }
}
