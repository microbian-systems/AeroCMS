using Aero.Cms.Abstractions.Events;
using Aero.Cms.Abstractions.Interfaces;
using Aero.Cms.Abstractions.Models;
using Aero.Cms.Abstractions.Requests;
using Aero.Cms.Core.Entities;
using Aero.Cms.Html;
using Aero.Core;
using Aero.Core.Railway;
using AeroDB.Sable;
using Wolverine;

namespace Aero.Cms.Modules.Sites;

/// <summary>
/// Updates a site's native style-profile settings with optimistic revision checks.
/// </summary>
/// <remarks>
/// This contract addresses a site by identifier and does not enforce caller authorization,
/// tenant ownership, or current-site membership.
/// </remarks>
public interface ISiteStyleProfileService
{
    /// <summary>
    /// Normalizes and conditionally persists a replacement style profile.
    /// </summary>
    /// <param name="siteId">The site document identifier.</param>
    /// <param name="request">The expected revision and proposed breakpoint and color tokens.</param>
    /// <param name="cancellationToken">The token used by document operations.</param>
    /// <returns>
    /// The current or newly persisted profile, or a not-found, validation, conflict, or database failure.
    /// </returns>
    /// <remarks>
    /// Semantically unchanged settings do not increment the revision or publish a change event.
    /// A successful mutation commits the site before publishing its notification.
    /// </remarks>
    Task<Result<SiteStyleProfileViewModel, AeroError>> UpdateAsync(
        long siteId,
        UpdateSiteStyleProfileRequest request,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Loads and normalizes the native style profile for a site.
/// </summary>
/// <param name="store">The document store used to create a short-lived query session.</param>
/// <remarks>
/// Resolution is identifier-based and does not apply authorization or tenant/current-site checks.
/// All exceptions, including cancellation, are returned as database failures.
/// </remarks>
public sealed class SiteStyleProfileResolver(IDocumentStore store) : ISiteStyleProfileResolver
{
    /// <inheritdoc />
    public async Task<Result<IStyleProfile, AeroError>> ResolveAsync(
        long siteId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await using var session = await store.QuerySessionAsync(cancellationToken).ConfigureAwait(false);
            var site = await session.LoadAsync<SitesModel>(siteId, cancellationToken).ConfigureAwait(false);
            if (site is null)
                return new Result<IStyleProfile, AeroError>.Failure(
                    AeroError.NotFoundError($"Site {siteId} was not found."));

            if (site.StyleProfile is null)
                return new Result<IStyleProfile, AeroError>.Failure(
                    AeroError.ValidationError([$"Site {siteId} has no style-profile settings."]));

            var profile = NativeStyleProfileFactory.Create(siteId, site.StyleProfile);
            return profile switch
            {
                Result<NativeStyleProfile, AeroError>.Ok ok =>
                    new Result<IStyleProfile, AeroError>.Ok(ok.Value),
                Result<NativeStyleProfile, AeroError>.Failure failure =>
                    new Result<IStyleProfile, AeroError>.Failure(failure.Error),
                _ => new Result<IStyleProfile, AeroError>.Failure(
                    AeroError.CreateError("Unexpected style-profile resolution result."))
            };
        }
        catch (Exception exception)
        {
            return new Result<IStyleProfile, AeroError>.Failure(
                AeroError.DatabaseError($"Failed to resolve the site style profile: {exception.Message}"));
        }
    }
}

/// <summary>
/// Persists normalized native style settings and publishes profile-change notifications.
/// </summary>
/// <param name="store">The document store used to create a short-lived mutation session.</param>
/// <param name="messageBus">The bus used to publish post-commit change notifications.</param>
/// <remarks>
/// Site lookup is identifier-based. Callers are responsible for authorization and site ownership.
/// Exceptions, including cancellation, are translated to result failures.
/// </remarks>
public sealed class SiteStyleProfileService(
    IDocumentStore store,
    IMessageBus messageBus) : ISiteStyleProfileService
{
    /// <inheritdoc />
    /// <remarks>
    /// The request revision is compared with the normalized stored revision before proposed settings
    /// are normalized. On change, the site commit completes before event publication; a publication
    /// failure is therefore reported as a database failure after the updated profile is durable.
    /// </remarks>
    public async Task<Result<SiteStyleProfileViewModel, AeroError>> UpdateAsync(
        long siteId,
        UpdateSiteStyleProfileRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await using var session = await store.LightweightSessionAsync(cancellationToken).ConfigureAwait(false);
            var site = await session.LoadAsync<SitesModel>(siteId, cancellationToken).ConfigureAwait(false);
            if (site is null)
                return new Result<SiteStyleProfileViewModel, AeroError>.Failure(
                    AeroError.NotFoundError($"Site {siteId} was not found."));

            if (site.StyleProfile is null)
                return new Result<SiteStyleProfileViewModel, AeroError>.Failure(
                    AeroError.ValidationError([$"Site {siteId} has no style-profile settings."]));

            var currentResult = NativeStyleProfileFactory.Normalize(siteId, site.StyleProfile);
            if (currentResult is Result<NormalizedNativeStyleProfile, AeroError>.Failure currentFailure)
                return new Result<SiteStyleProfileViewModel, AeroError>.Failure(currentFailure.Error);

            var current = ((Result<NormalizedNativeStyleProfile, AeroError>.Ok)currentResult).Value;
            if (request.ExpectedRevision != current.Settings.Revision)
            {
                return new Result<SiteStyleProfileViewModel, AeroError>.Failure(
                    AeroError.ConflictError(
                        $"Style-profile revision {request.ExpectedRevision} is stale; current revision is {current.Settings.Revision}."));
            }

            var proposedSettings = new StyleProfileSettings
            {
                Revision = current.Settings.Revision,
                SmallScreenBreakpointRem = request.SmallScreenBreakpointRem,
                ColorTokens = request.ColorTokens?
                    .Select(static token => new StyleColorToken
                    {
                        Name = token.Name,
                        HexValue = token.HexValue
                    })
                    .ToList() ?? []
            };

            var proposedResult = NativeStyleProfileFactory.Normalize(siteId, proposedSettings);
            if (proposedResult is Result<NormalizedNativeStyleProfile, AeroError>.Failure proposedFailure)
                return new Result<SiteStyleProfileViewModel, AeroError>.Failure(proposedFailure.Error);

            var proposed = ((Result<NormalizedNativeStyleProfile, AeroError>.Ok)proposedResult).Value;
            if (SemanticallyEquals(current.Settings, proposed.Settings))
            {
                return new Result<SiteStyleProfileViewModel, AeroError>.Ok(
                    SiteStyleProfileMapper.ToViewModel(current.Settings));
            }

            proposed.Settings.Revision = checked(current.Settings.Revision + 1);
            site.StyleProfile = proposed.Settings;
            site.ModifiedOn = DateTimeOffset.UtcNow;
            session.Store(site);
            await session.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

            await messageBus.PublishAsync(new SiteStyleProfileChangedEvent(
                siteId,
                proposed.Settings.Revision,
                site.ModifiedOn.Value));

            return new Result<SiteStyleProfileViewModel, AeroError>.Ok(
                SiteStyleProfileMapper.ToViewModel(proposed.Settings));
        }
        catch (OverflowException)
        {
            return new Result<SiteStyleProfileViewModel, AeroError>.Failure(
                AeroError.ConflictError("The style-profile revision cannot be incremented."));
        }
        catch (ConcurrencyException)
        {
            return new Result<SiteStyleProfileViewModel, AeroError>.Failure(
                AeroError.ConflictError(
                    "The site style profile changed while it was being saved. Reload it and try again."));
        }
        catch (InvalidOperationException exception) when (IsTransactionConflict(exception))
        {
            return new Result<SiteStyleProfileViewModel, AeroError>.Failure(
                AeroError.ConflictError(
                    "The site style profile changed while it was being saved. Reload it and try again."));
        }
        catch (Exception exception)
        {
            return new Result<SiteStyleProfileViewModel, AeroError>.Failure(
                AeroError.DatabaseError($"Failed to update the site style profile: {exception.Message}"));
        }
    }

    /// <summary>
    /// Detects the provider's message-based transaction-conflict signal in an exception chain.
    /// </summary>
    /// <param name="exception">The exception whose inner chain is inspected.</param>
    /// <returns><see langword="true"/> when any message begins with the transaction-conflict marker.</returns>
    private static bool IsTransactionConflict(Exception exception)
    {
        for (var current = exception; current is not null; current = current.InnerException)
        {
            if (current.Message.StartsWith(
                    "Transaction conflict:",
                    StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Compares normalized settings using exact breakpoint and ordered token equality.
    /// </summary>
    /// <param name="current">The currently persisted normalized settings.</param>
    /// <param name="proposed">The proposed normalized settings.</param>
    /// <returns><see langword="true"/> when the breakpoint and token sequence match exactly.</returns>
    private static bool SemanticallyEquals(
        StyleProfileSettings current,
        StyleProfileSettings proposed)
    {
        return current.SmallScreenBreakpointRem == proposed.SmallScreenBreakpointRem &&
               current.ColorTokens.SequenceEqual(
                   proposed.ColorTokens,
                   StyleColorTokenComparer.Instance);
    }

    /// <summary>
    /// Compares normalized style tokens by ordinal name and hexadecimal value.
    /// </summary>
    private sealed class StyleColorTokenComparer : IEqualityComparer<StyleColorToken>
    {
        /// <summary>
        /// Gets the stateless comparer instance used for ordered token-sequence comparisons.
        /// </summary>
        public static StyleColorTokenComparer Instance { get; } = new();

        /// <summary>
        /// Tests two tokens for reference equality or exact ordinal value equality.
        /// </summary>
        /// <param name="left">The first token.</param>
        /// <param name="right">The second token.</param>
        /// <returns><see langword="true"/> when both tokens represent the same normalized values.</returns>
        public bool Equals(StyleColorToken? left, StyleColorToken? right)
        {
            return ReferenceEquals(left, right) ||
                   left is not null &&
                   right is not null &&
                   string.Equals(left.Name, right.Name, StringComparison.Ordinal) &&
                   string.Equals(left.HexValue, right.HexValue, StringComparison.Ordinal);
        }

        /// <summary>
        /// Computes an ordinal hash from a token's name and hexadecimal value.
        /// </summary>
        /// <param name="value">The non-null token to hash.</param>
        /// <returns>A combined ordinal hash code.</returns>
        public int GetHashCode(StyleColorToken value)
        {
            return HashCode.Combine(
                StringComparer.Ordinal.GetHashCode(value.Name),
                StringComparer.Ordinal.GetHashCode(value.HexValue));
        }
    }
}
