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

public interface ISiteStyleProfileService
{
    Task<Result<SiteStyleProfileViewModel, AeroError>> UpdateAsync(
        long siteId,
        UpdateSiteStyleProfileRequest request,
        CancellationToken cancellationToken = default);
}

public sealed class SiteStyleProfileResolver(IDocumentStore store) : ISiteStyleProfileResolver
{
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

public sealed class SiteStyleProfileService(
    IDocumentStore store,
    IMessageBus messageBus) : ISiteStyleProfileService
{
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

    private static bool SemanticallyEquals(
        StyleProfileSettings current,
        StyleProfileSettings proposed)
    {
        return current.SmallScreenBreakpointRem == proposed.SmallScreenBreakpointRem &&
               current.ColorTokens.SequenceEqual(
                   proposed.ColorTokens,
                   StyleColorTokenComparer.Instance);
    }

    private sealed class StyleColorTokenComparer : IEqualityComparer<StyleColorToken>
    {
        public static StyleColorTokenComparer Instance { get; } = new();

        public bool Equals(StyleColorToken? left, StyleColorToken? right)
        {
            return ReferenceEquals(left, right) ||
                   left is not null &&
                   right is not null &&
                   string.Equals(left.Name, right.Name, StringComparison.Ordinal) &&
                   string.Equals(left.HexValue, right.HexValue, StringComparison.Ordinal);
        }

        public int GetHashCode(StyleColorToken value)
        {
            return HashCode.Combine(
                StringComparer.Ordinal.GetHashCode(value.Name),
                StringComparer.Ordinal.GetHashCode(value.HexValue));
        }
    }
}
