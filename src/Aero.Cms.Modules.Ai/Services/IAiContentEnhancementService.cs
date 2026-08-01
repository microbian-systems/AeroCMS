using Aero.Cms.Abstractions.Ai;
using Aero.Core;
using Aero.Core.Railway;

namespace Aero.Cms.Modules.Ai.Services;

/// <summary>
/// Enhances one CMS content field through a configured AI provider.
/// </summary>
public interface IAiContentEnhancementService
{
        /// <summary>
    /// Validates and submits content to an AI provider for enhancement.
    /// </summary>
    /// <param name="request">The content, context, provider selection, and user instructions.</param>
    /// <param name="cancellationToken">A token that requests cancellation of validation, settings lookup, and provider access.</param>
    /// <returns>
    /// A successful result containing provider-generated text for review, or a failure describing validation,
    /// configuration, provider-client creation, timeout, empty output, truncation, parsing, or invocation failure.
    /// </returns>
    /// <remarks>
    /// The request can be transmitted to an external provider and may incur usage charges. The result is
    /// provider-generated content; callers remain responsible for review and for deciding whether to persist it.
    /// </remarks>
Task<Result<EnhanceContentResponse>> EnhanceAsync(
        EnhanceContentRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Streams a content suggestion while retaining a typed, fully parsed completion event.
    /// </summary>
    /// <param name="request">The content, context, provider selection, and user instructions.</param>
    /// <param name="correlationId">The request correlation identifier exposed in stream events.</param>
    /// <param name="cancellationToken">A token that observes request abortion and provider cancellation.</param>
    /// <returns>
    /// A successful result containing the event stream, or a failure encountered before the SSE response starts.
    /// Failures after streaming starts are represented by an <see cref="EnhanceContentEventKind.Error"/> event.
    /// </returns>
    Task<Result<IAsyncEnumerable<EnhanceContentEvent>>> StreamAsync(
        EnhanceContentRequest request,
        string correlationId,
        CancellationToken cancellationToken = default);
}
