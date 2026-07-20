using System.Linq.Expressions;
using Aero.Core.Data;
using Aero.Core.Extensions;
using Aero.Core.Railway;
using Aero.Models.Entities;
using AeroDB.Sable;
using Microsoft.Extensions.Logging;

namespace Aero.Cms.Data.Repositories;

/// <summary>
/// Provides synchronous and asynchronous profile persistence operations through
/// one caller-owned Sable document session.
/// </summary>
/// <param name="session">The session used for all reads, staged writes, and explicit saves.</param>
/// <param name="log">The logger used for query and mutation diagnostics.</param>
/// <remarks>
/// Members without a cancellation-token parameter execute without caller-controlled
/// cancellation. Session and query-provider exceptions are not translated.
/// </remarks>
public class UserProfileRepository(
    IDocumentSession session,
    ILogger<UserProfileRepository> log
) : IUserProfileRepository
{

    // ===== Sync Read (IReadonlyRepositorySync<AeroUserProfile, long>) =====
    /// <summary>Synchronously materializes all profile documents without an explicit ordering.</summary>
    /// <returns>All profiles returned by the session query.</returns>
    /// <remarks>This member blocks the calling thread until <see cref="GetAllAsync"/> completes.</remarks>
public IEnumerable<AeroUserProfile> GetAll() =>
        GetAllAsync().GetAwaiter().GetResult();

    /// <summary>Synchronously retrieves the single profile with a document identifier.</summary>
    /// <param name="id">The document identifier to match.</param>
    /// <returns>The matching profile.</returns>
    /// <exception cref="InvalidOperationException">No profile, or more than one profile, matches the identifier.</exception>
public AeroUserProfile FindById(long id) =>
        FindByIdAsync(id).GetAwaiter().GetResult();

    /// <summary>Synchronously materializes profiles matching a predicate.</summary>
    /// <param name="predicate">The expression passed to the Sable query provider.</param>
    /// <returns>Matching profiles without an explicit ordering.</returns>
    /// <remarks>This member blocks the calling thread until <see cref="FindAsync"/> completes.</remarks>
public IEnumerable<AeroUserProfile> Find(Expression<Func<AeroUserProfile, bool>> predicate) =>
        FindAsync(predicate).GetAwaiter().GetResult();

    // ===== Async Read (IReadonlyRepositoryAsync<AeroUserProfile, long>) =====
    /// <summary>Materializes all profile documents without an explicit ordering.</summary>
    /// <returns>A task containing all profiles returned by the session query.</returns>
    /// <remarks>The query explicitly uses <see cref="CancellationToken.None"/>.</remarks>
public async Task<IEnumerable<AeroUserProfile>> GetAllAsync() =>
        await session.Query<AeroUserProfile>().ToListAsync(CancellationToken.None);

    /// <summary>Counts all profile documents visible to the session.</summary>
    /// <returns>A task containing the document count.</returns>
public async Task<long> CountAsync() =>
        await session.Query<AeroUserProfile>().CountAsync();

    /// <summary>Determines whether any profile has the supplied document identifier.</summary>
    /// <param name="id">The document identifier to match.</param>
    /// <returns><see langword="true"/> when at least one match exists; otherwise <see langword="false"/>.</returns>
public async Task<bool> ExistsAsync(long id) =>
        await session.Query<AeroUserProfile>().Where(x => x.Id == id).AnyAsync();

    /// <summary>Loads a profile by document identifier through the session identity API.</summary>
    /// <param name="id">The document identifier to load.</param>
    /// <returns>
    /// A task whose result is the matching profile, or <see langword="null"/> at
    /// runtime when the document is missing.
    /// </returns>
    /// <remarks>
    /// The nullable result from the <see cref="IDocumentSession"/>
    /// <c>LoadAsync</c> operation is returned unchanged despite this member's non-nullable
    /// <see cref="Task{TResult}"/> signature.
    /// </remarks>
public async Task<AeroUserProfile> GetByIdAsync(long id) =>
        await session.LoadAsync<AeroUserProfile>(id);

    /// <summary>Materializes profiles whose identifiers occur in a supplied sequence.</summary>
    /// <param name="ids">The identifiers used by the membership predicate.</param>
    /// <returns>Matching profiles without an explicit ordering; the collection is empty when none match.</returns>
public async Task<IReadOnlyCollection<AeroUserProfile>> GetByIdsAsync(IEnumerable<long> ids) =>
        await session.Query<AeroUserProfile>().Where(x => ids.Contains(x.Id)).ToListAsync();

    /// <summary>Queries for exactly one profile with a document identifier.</summary>
    /// <param name="id">The document identifier to match.</param>
    /// <returns>The single matching profile.</returns>
    /// <exception cref="InvalidOperationException">No profile, or more than one profile, matches the identifier.</exception>
public async Task<AeroUserProfile> FindByIdAsync(long id)
    {
        log.LogInformation("search for entity with id {Id}", id);
        return await session.Query<AeroUserProfile>()
            .Where(x => x.Id == id).SingleOrDefaultAsync()
            ?? throw new InvalidOperationException($"Expected one {nameof(AeroUserProfile)} with id {id}, none found.");
    }

    /// <summary>Materializes profiles matching a provider-translatable predicate.</summary>
    /// <param name="predicate">The expression passed to the Sable query provider.</param>
    /// <returns>Matching profiles without an explicit ordering.</returns>
public async Task<IEnumerable<AeroUserProfile>> FindAsync(Expression<Func<AeroUserProfile, bool>> predicate)
    {
        log.LogInformation("querying marten store...");
        return await session.Query<AeroUserProfile>()
            .Where(predicate).ToListAsync();
    }

    // ===== Sync Write (IWriteOnlyRepositorySync<AeroUserProfile, long>) =====
    /// <summary>Synchronously stages a profile for storage without saving the session.</summary>
    /// <param name="entity">The profile to stage.</param>
    /// <returns>The same staged instance.</returns>
public AeroUserProfile Insert(AeroUserProfile entity) =>
        InsertAsync(entity).GetAwaiter().GetResult();

    /// <summary>Synchronously stores a profile and saves the session.</summary>
    /// <param name="entity">The profile to store.</param>
    /// <returns>The same instance after persistence completes.</returns>
public AeroUserProfile Update(AeroUserProfile entity) =>
        UpdateAsync(entity).GetAwaiter().GetResult();

    /// <summary>Synchronously stores a profile and saves the session.</summary>
    /// <param name="entity">The profile to store.</param>
    /// <returns>The same instance after persistence completes.</returns>
public AeroUserProfile Upsert(AeroUserProfile entity) =>
        UpsertAsync(entity).GetAwaiter().GetResult();

    /// <summary>Synchronously stages deletion of a profile identifier without saving the session.</summary>
    /// <param name="id">The profile document identifier to stage for deletion.</param>
public void Delete(long id) =>
        DeleteAsync(id).GetAwaiter().GetResult();

    /// <summary>Synchronously stages deletion of a profile without saving the session.</summary>
    /// <param name="entity">The profile whose document identifier is staged for deletion.</param>
public void Delete(AeroUserProfile entity) =>
        DeleteAsync(entity).GetAwaiter().GetResult();

    // ===== Async Write (IWriteOnlyRepositoryAsync<AeroUserProfile, long>) =====
    /// <summary>Stages a profile for storage without saving the session.</summary>
    /// <param name="entity">The profile to stage.</param>
    /// <returns>A task containing the same staged instance.</returns>
public async Task<AeroUserProfile> InsertAsync(AeroUserProfile entity)
    {
        await Task.CompletedTask;
        log.LogInformation("inserting entity {Entity}", entity.Dump());
        session.Store(entity);
        return entity;
    }

    /// <summary>Stores a profile and saves the session.</summary>
    /// <param name="entity">The profile to store.</param>
    /// <returns>A task containing the same instance after persistence completes.</returns>
public async Task<AeroUserProfile> UpdateAsync(AeroUserProfile entity)
    {
        log.LogInformation("updating entity {Entity}", entity.Dump());
        session.Store(entity);
        await session.SaveChangesAsync();
        return entity;
    }

    /// <summary>Stores a profile and saves the session.</summary>
    /// <param name="entity">The profile to store.</param>
    /// <returns>A task containing the same instance after persistence completes.</returns>
public async Task<AeroUserProfile> UpsertAsync(AeroUserProfile entity)
    {
        log.LogInformation("upserting entity {Entity}", entity.Dump());
        session.Store(entity);
        await session.SaveChangesAsync();
        return entity;
    }

    /// <summary>Stages deletion of a profile identifier without saving the session.</summary>
    /// <param name="id">The profile document identifier to stage for deletion.</param>
    /// <returns>A task that completes after the deletion has been staged.</returns>
public async Task DeleteAsync(long id)
    {
        log.LogInformation("deleting entity with id {Id}", id);
        session.Delete<AeroUserProfile>(id);
    }

    /// <summary>Stages deletion of a profile without saving the session.</summary>
    /// <param name="entity">The profile whose document identifier is staged for deletion.</param>
    /// <returns>A task that completes after the deletion has been staged.</returns>
public async Task DeleteAsync(AeroUserProfile entity) =>
        DeleteAsync(entity.Id).GetAwaiter().GetResult();

    // ===== UserProfileRepository-specific methods =====
    /// <summary>Returns the first profile associated with a user identifier.</summary>
    /// <param name="userId">The user identifier matched against <see cref="AeroUserProfile.Userid"/>.</param>
    /// <returns>A populated option for the first match; otherwise an empty option.</returns>
    /// <remarks>The query declares no ordering or uniqueness requirement.</remarks>
public async Task<Option<AeroUserProfile>> GetUserProfileAsync(long userId)
    {
        var profile = await session.Query<AeroUserProfile>()
            .FirstOrDefaultAsync(x => x.Userid == userId);
        return profile is not null
            ? new Option<AeroUserProfile>.Some(profile)
            : new Option<AeroUserProfile>.None();
    }

    /// <summary>Stores a profile and saves the session.</summary>
    /// <param name="user">The profile to store.</param>
    /// <returns>A task that completes after persistence succeeds.</returns>
public async Task SaveUserProfileAsync(AeroUserProfile user)
    {
        session.Store(user);
        await session.SaveChangesAsync();
    }

    /// <summary>Stages deletion of the first profile associated with a user identifier.</summary>
    /// <param name="userId">The user identifier used to locate a profile.</param>
    /// <returns>A task that completes after a matching deletion is staged, or immediately when no profile matches.</returns>
    /// <remarks>This operation does not save the session and declares no ordering or uniqueness requirement.</remarks>
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
