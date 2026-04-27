using Aero.Cms.Core.Entities;
using Aero.Marten;
using Marten;
using Microsoft.Extensions.Logging;

namespace Aero.Cms.Modules.Tenant;


public interface ITenantRepository : IMartenGenericRepositoryOption<TenantModel>
{
    // Define any additional methods specific to tenant management if needed
}


/// <summary>
/// Provides data access and management operations for tenant entities using a Marten document session.
/// </summary>
/// <param name="session">The document session used to interact with the underlying data store. Cannot be null.</param>
/// <param name="log">The logger instance used for logging repository operations. Cannot be null.</param>
public class TenantRepository(IDocumentSession session, ILogger<TenantRepository> log) 
    : MartenGenericRepositoryOption<TenantModel>(session, log), ITenantRepository;