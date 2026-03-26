using Aero.Marten;
using Aero.Models.Entities;
using Marten;
using Microsoft.Extensions.Logging;

namespace Aero.Cms.Repositories;

public class UserProfileRepository(IDocumentSession session, ILogger<UserProfileRepository> log)
    : GenericMartenRepository<AeroUserProfile>(session, log), IUserProfileRepository
{
    public Task DeleteUserProfileAsync(long userId)
    {
        throw new NotImplementedException();
    }

    public Task<Aero.Core.Railway.Option<AeroUserProfile>> GetUserProfileAsync(long userId)
    {
        throw new NotImplementedException();
    }

    public Task SaveUserProfileAsync(AeroUserProfile user)
    {
        throw new NotImplementedException();
    }
}