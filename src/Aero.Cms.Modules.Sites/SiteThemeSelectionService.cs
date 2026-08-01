using Aero.Cms.Abstractions.Events;
using Aero.Cms.Abstractions.Models;
using Aero.Cms.Abstractions.Requests;
using Aero.Cms.Abstractions.Theming;
using Aero.Cms.Core.Entities;
using Aero.Core;
using Aero.Core.Railway;
using AeroDB.Sable;
using Microsoft.Extensions.Logging;
using Wolverine;

namespace Aero.Cms.Modules.Sites;

/// <summary>Updates a site's exact deployment-installed theme with optimistic concurrency.</summary>
public interface ISiteThemeSelectionService
{
    /// <summary>Validates and conditionally persists an exact theme selection.</summary>
    Task<Result<SiteThemeSelectionViewModel, AeroError>> UpdateAsync(
        long siteId,
        UpdateSiteThemeRequest request,
        CancellationToken cancellationToken = default);
}

/// <summary>Persists exact installed-theme selection and publishes post-commit notifications.</summary>
public sealed class SiteThemeSelectionService(
    IDocumentStore store,
    IThemeCatalog themeCatalog,
    IMessageBus messageBus,
    ILogger<SiteThemeSelectionService> logger) : ISiteThemeSelectionService
{
    /// <inheritdoc />
    public async Task<Result<SiteThemeSelectionViewModel, AeroError>> UpdateAsync(
        long siteId,
        UpdateSiteThemeRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request.ExpectedRevision <= 0)
            return Failure(AeroError.ValidationError(["Expected theme revision must be positive."]));
        if (string.IsNullOrWhiteSpace(request.ThemeId) || string.IsNullOrWhiteSpace(request.ThemeVersion))
            return Failure(AeroError.ValidationError(["An exact theme id and version are required."]));
        if (themeCatalog.Find(request.ThemeId, request.ThemeVersion) is null)
        {
            return Failure(AeroError.ValidationError(
                [$"Theme '{request.ThemeId}@{request.ThemeVersion}' is not installed."]));
        }

        try
        {
            await using var session = await store.LightweightSessionAsync(cancellationToken).ConfigureAwait(false);
            var site = await session.LoadAsync<SitesModel>(siteId, cancellationToken).ConfigureAwait(false);
            if (site is null)
                return Failure(AeroError.NotFoundError($"Site {siteId} was not found."));

            if (request.ExpectedRevision != site.ThemeRevision)
            {
                return Failure(AeroError.ConflictError(
                    $"Theme revision {request.ExpectedRevision} is stale; current revision is {site.ThemeRevision}."));
            }

            if (string.Equals(site.ThemeId, request.ThemeId, StringComparison.Ordinal) &&
                string.Equals(site.ThemeVersion, request.ThemeVersion, StringComparison.Ordinal))
            {
                return Success(site);
            }

            site.ThemeId = request.ThemeId;
            site.ThemeVersion = request.ThemeVersion;
            site.ThemeRevision = checked(site.ThemeRevision + 1);
            site.ModifiedOn = DateTimeOffset.UtcNow;
            session.Store(site);
            await session.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

            try
            {
                await messageBus.PublishAsync(new SiteThemeChangedEvent(
                    site.Id,
                    site.ThemeId,
                    site.ThemeVersion,
                    site.ThemeRevision,
                    site.ModifiedOn.Value));
            }
            catch (Exception exception)
            {
                logger.LogError(
                    exception,
                    "Site {SiteId} theme {ThemeId}@{ThemeVersion} revision {ThemeRevision} was saved, but its change notification could not be published.",
                    site.Id,
                    site.ThemeId,
                    site.ThemeVersion,
                    site.ThemeRevision);
            }

            return Success(site);
        }
        catch (OverflowException)
        {
            return Failure(AeroError.ConflictError("The theme revision cannot be incremented."));
        }
        catch (ConcurrencyException)
        {
            return ConcurrencyFailure();
        }
        catch (InvalidOperationException exception) when (IsTransactionConflict(exception))
        {
            return ConcurrencyFailure();
        }
        catch (Exception exception)
        {
            return Failure(AeroError.DatabaseError($"Failed to update the site theme: {exception.Message}"));
        }
    }

    private static Result<SiteThemeSelectionViewModel, AeroError> Success(SitesModel site)
        => new Result<SiteThemeSelectionViewModel, AeroError>.Ok(
            new SiteThemeSelectionViewModel(site.ThemeId, site.ThemeVersion, site.ThemeRevision));

    private static Result<SiteThemeSelectionViewModel, AeroError> Failure(AeroError error)
        => new Result<SiteThemeSelectionViewModel, AeroError>.Failure(error);

    private static Result<SiteThemeSelectionViewModel, AeroError> ConcurrencyFailure()
        => Failure(AeroError.ConflictError(
            "The site theme changed while it was being saved. Reload it and try again."));

    private static bool IsTransactionConflict(Exception exception)
    {
        for (var current = exception; current is not null; current = current.InnerException)
        {
            if (current.Message.StartsWith("Transaction conflict:", StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }
}
