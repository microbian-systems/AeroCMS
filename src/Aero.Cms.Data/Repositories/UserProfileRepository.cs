using System.Linq.Expressions;
using Aero.Core.Data;
using Aero.Core.Extensions;
using Aero.Core.Railway;
using Aero.Models.Entities;
using AeroDB.Sable;
using Microsoft.Extensions.Logging;

namespace Aero.Cms.Data.Repositories;

public class UserProfileRepository(
    IDocumentSession session,
    ILogger<UserProfileRepository> log
) : IUserProfileRepository
{

    // ===== Sync Read (IReadonlyRepositorySync<AeroUserProfile, long>) =====
    public IEnumerable<AeroUserProfile> GetAll() =>
        GetAllAsync().GetAwaiter().GetResult();

    public AeroUserProfile FindById(long id) =>
        FindByIdAsync(id).GetAwaiter().GetResult();

    public IEnumerable<AeroUserProfile> Find(Expression<Func<AeroUserProfile, bool>> predicate) =>
        FindAsync(predicate).GetAwaiter().GetResult();

    // ===== Async Read (IReadonlyRepositoryAsync<AeroUserProfile, long>) =====
    public async Task<IEnumerable<AeroUserProfile>> GetAllAsync() =>
        await session.Query<AeroUserProfile>().ToListAsync(CancellationToken.None);

    public async Task<long> CountAsync() =>
        await session.Query<AeroUserProfile>().CountAsync();

    public async Task<bool> ExistsAsync(long id) =>
        await session.Query<AeroUserProfile>().Where(x => x.Id == id).AnyAsync();

    public async Task<AeroUserProfile> GetByIdAsync(long id) =>
        await session.LoadAsync<AeroUserProfile>(id);

    public async Task<IReadOnlyCollection<AeroUserProfile>> GetByIdsAsync(IEnumerable<long> ids) =>
        await session.Query<AeroUserProfile>().Where(x => ids.Contains(x.Id)).ToListAsync();

    public async Task<AeroUserProfile> FindByIdAsync(long id)
    {
        log.LogInformation("search for entity with id {Id}", id);
        return await session.Query<AeroUserProfile>()
            .Where(x => x.Id == id).SingleOrDefaultAsync()
            ?? throw new InvalidOperationException($"Expected one {nameof(AeroUserProfile)} with id {id}, none found.");
    }

    public async Task<IEnumerable<AeroUserProfile>> FindAsync(Expression<Func<AeroUserProfile, bool>> predicate)
    {
        log.LogInformation("querying marten store...");
        return await session.Query<AeroUserProfile>()
            .Where(predicate).ToListAsync();
    }

    // ===== Sync Write (IWriteOnlyRepositorySync<AeroUserProfile, long>) =====
    public AeroUserProfile Insert(AeroUserProfile entity) =>
        InsertAsync(entity).GetAwaiter().GetResult();

    public AeroUserProfile Update(AeroUserProfile entity) =>
        UpdateAsync(entity).GetAwaiter().GetResult();

    public AeroUserProfile Upsert(AeroUserProfile entity) =>
        UpsertAsync(entity).GetAwaiter().GetResult();

    public void Delete(long id) =>
        DeleteAsync(id).GetAwaiter().GetResult();

    public void Delete(AeroUserProfile entity) =>
        DeleteAsync(entity).GetAwaiter().GetResult();

    // ===== Async Write (IWriteOnlyRepositoryAsync<AeroUserProfile, long>) =====
    public async Task<AeroUserProfile> InsertAsync(AeroUserProfile entity)
    {
        await Task.CompletedTask;
        log.LogInformation("inserting entity {Entity}", entity.Dump());
        session.Store(entity);
        return entity;
    }

    public async Task<AeroUserProfile> UpdateAsync(AeroUserProfile entity)
    {
        log.LogInformation("updating entity {Entity}", entity.Dump());
        session.Store(entity);
        await session.SaveChangesAsync();
        return entity;
    }

    public async Task<AeroUserProfile> UpsertAsync(AeroUserProfile entity)
    {
        log.LogInformation("upserting entity {Entity}", entity.Dump());
        session.Store(entity);
        await session.SaveChangesAsync();
        return entity;
    }

    public async Task DeleteAsync(long id)
    {
        log.LogInformation("deleting entity with id {Id}", id);
        session.Delete<AeroUserProfile>(id);
    }

    public async Task DeleteAsync(AeroUserProfile entity) =>
        DeleteAsync(entity.Id).GetAwaiter().GetResult();

    // ===== UserProfileRepository-specific methods =====
    public async Task<Option<AeroUserProfile>> GetUserProfileAsync(long userId)
    {
        var profile = await session.Query<AeroUserProfile>()
            .FirstOrDefaultAsync(x => x.Userid == userId);
        return profile is not null
            ? new Option<AeroUserProfile>.Some(profile)
            : new Option<AeroUserProfile>.None();
    }

    public async Task SaveUserProfileAsync(AeroUserProfile user)
    {
        session.Store(user);
        await session.SaveChangesAsync();
    }

    public async Task DeleteUserProfileAsync(long userId)
    {
        var profile = await session.Query<AeroUserProfile>()
            .FirstOrDefaultAsync(x => x.Userid == userId);
        if (profile is not null)
        {
            session.Delete(profile);
        }
    }
}