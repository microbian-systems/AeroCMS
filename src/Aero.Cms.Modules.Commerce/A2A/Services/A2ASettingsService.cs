using Aero.Cms.Modules.Commerce.A2A.Models;
using FluentValidation;

namespace Aero.Cms.Modules.Commerce.A2A.Services;

/// <summary>Business service for the disabled-by-default per-site A2A setting.</summary>
public sealed class A2ASettingsService(
    IA2ASettingsRepository repository,
    IValidator<UpdateA2ASettingsRequest> validator) : IA2ASettingsService
{
    /// <inheritdoc />
    public async Task<Result<A2ASettingsResponse, AeroError>> GetAsync(long tenantId, long siteId, CancellationToken ct = default)
    {
        if (tenantId <= 0 || siteId <= 0)
        {
            return Prelude.Fail<A2ASettingsResponse, AeroError>(AeroError.NotFoundError("Site not found."));
        }

        var existing = await repository.GetAsync(tenantId, siteId, ct);
        return existing switch
        {
            Result<A2ASettingsDocument?, AeroError>.Ok { Value: { } settings } => Prelude.Ok<A2ASettingsResponse, AeroError>(new A2ASettingsResponse(settings.IsEnabled)),
            Result<A2ASettingsDocument?, AeroError>.Ok => Prelude.Ok<A2ASettingsResponse, AeroError>(new A2ASettingsResponse(false)),
            Result<A2ASettingsDocument?, AeroError>.Failure failure => Prelude.Fail<A2ASettingsResponse, AeroError>(failure.Error),
            _ => Prelude.Fail<A2ASettingsResponse, AeroError>(AeroError.DatabaseError("A2A settings could not be loaded."))
        };
    }

    /// <inheritdoc />
    public async Task<Result<A2ASettingsResponse, AeroError>> UpdateAsync(
        long tenantId,
        long siteId,
        UpdateA2ASettingsRequest request,
        string? actorId,
        CancellationToken ct = default)
    {
        if (tenantId <= 0 || siteId <= 0)
        {
            return Prelude.Fail<A2ASettingsResponse, AeroError>(AeroError.NotFoundError("Site not found."));
        }

        var validation = await validator.ValidateAsync(request, ct);
        if (!validation.IsValid)
        {
            return Prelude.Fail<A2ASettingsResponse, AeroError>(AeroError.ValidationError(validation.Errors.Select(x => x.ErrorMessage)));
        }

        var existing = await repository.GetAsync(tenantId, siteId, ct);
        if (existing is Result<A2ASettingsDocument?, AeroError>.Failure getFailure)
        {
            return Prelude.Fail<A2ASettingsResponse, AeroError>(getFailure.Error);
        }

        var now = DateTimeOffset.UtcNow;
        var settings = existing is Result<A2ASettingsDocument?, AeroError>.Ok { Value: { } value }
            ? value
            : new A2ASettingsDocument
            {
                Id = Snowflake.NewId(),
                TenantId = tenantId,
                SiteId = siteId,
                CreatedOn = now,
                CreatedBy = actorId
            };

        settings.IsEnabled = request.IsEnabled!.Value;
        settings.ModifiedOn = now;
        settings.ModifiedBy = actorId;

        var saved = await repository.SaveAsync(settings, ct);
        return saved switch
        {
            Result<A2ASettingsDocument, AeroError>.Ok ok => Prelude.Ok<A2ASettingsResponse, AeroError>(new A2ASettingsResponse(ok.Value.IsEnabled)),
            Result<A2ASettingsDocument, AeroError>.Failure failure => Prelude.Fail<A2ASettingsResponse, AeroError>(failure.Error),
            _ => Prelude.Fail<A2ASettingsResponse, AeroError>(AeroError.DatabaseError("A2A settings could not be saved."))
        };
    }
}
