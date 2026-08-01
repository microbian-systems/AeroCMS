using Aero.Marten;
using Aero.Models.Entities;
using Marten;
using Microsoft.Extensions.Logging;

namespace Aero.Cms.Data.Repositories;

/// <summary>
/// Represents a class for UserProfileRepository.
/// </summary>
public class UserProfileRepository(IDocumentSession session, ILogger<UserProfileRepository> log)
    : GenericMartenRepository<AeroUserProfile>(session, log), IUserProfileRepository
{
        /// <summary>
    /// DeleteUserProfileAsync method.
    /// </summary>
public Task DeleteUserProfileAsync(long userId)
    {
        throw new NotImplementedException();
    }

        /// <summary>
    /// GetUserProfileAsync method.
    /// </summary>
public Task<Aero.Core.Railway.Option<AeroUserProfile>> GetUserProfileAsync(long userId)
    {
        throw new NotImplementedException();
    }

        /// <summary>
    /// SaveUserProfileAsync method.
    /// </summary>
public Task SaveUserProfileAsync(AeroUserProfile user)
    {
        throw new NotImplementedException();
    }
}