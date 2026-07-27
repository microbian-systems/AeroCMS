using Aero.Cms.Modules.Commerce.A2A.Models;
using AeroDB.Sable;

namespace Aero.Cms.Modules.Commerce.A2A.Services;

/// <summary>Sable-backed repository for per-site A2A availability documents.</summary>
public sealed class A2ASettingsRepository(IDocumentSession session) : IA2ASettingsRepository
{
    /// <inheritdoc />
    public async Task<Result<A2ASettingsDocument?, AeroError>> GetAsync(long tenantId, long siteId, CancellationToken ct = default)
    {
        try
        {
            var settings = await session.Query<A2ASettingsDocument>()
                .FirstOrDefaultAsync(x => x.TenantId == tenantId && x.SiteId == siteId, ct);
            return Prelude.Ok<A2ASettingsDocument?, AeroError>(settings);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception)
        {
            return Prelude.Fail<A2ASettingsDocument?, AeroError>(AeroError.DatabaseError("A2A settings could not be loaded."));
        }
    }

    /// <inheritdoc />
    public async Task<Result<A2ASettingsDocument, AeroError>> SaveAsync(A2ASettingsDocument settings, CancellationToken ct = default)
    {
        try
        {
            session.Store(settings);
            await session.SaveChangesAsync(ct);
            return Prelude.Ok<A2ASettingsDocument, AeroError>(settings);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (ConcurrencyException)
        {
            session.ClearChanges();
            return Prelude.Fail<A2ASettingsDocument, AeroError>(AeroError.ConflictError("A2A settings changed while they were being saved. Reload and try again."));
        }
        catch (Exception)
        {
            session.ClearChanges();
            return Prelude.Fail<A2ASettingsDocument, AeroError>(AeroError.DatabaseError("A2A settings could not be saved."));
        }
    }
}
