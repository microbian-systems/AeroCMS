using Aero.Cms.Core.Entities;
using Aero.Cms.Data.Repositories;
using Aero.Cms.Modules.Aliases.Events;
using AeroDB.Sable;
using Wolverine;

namespace Aero.Cms.Modules.Aliases;

/// <summary>
/// Defines the repository-backed alias mutation API registered by
/// <see cref="AliasModule"/>.
/// <para>
/// Mutation methods persist through the supplied document session before they
/// publish their corresponding Wolverine event. Callers retain ownership of
/// the supplied <see cref="AliasDocument"/> and must provide values that meet
/// its persistence constraints; this service does not normalize or validate it.
/// </para>
/// </summary>
public interface IAliasService
{
        /// <summary>
    /// Gets aliases for a site through the repository.
    /// </summary>
Task<IList<AliasDocument>> GetBySiteIdAsync(long siteId, CancellationToken ct = default);
        /// <summary>
    /// Gets an alias using the repository's old-path lookup semantics.
    /// </summary>
Task<AliasDocument?> GetByOldPathAsync(string oldPath, CancellationToken ct = default);
        /// <summary>
    /// Persists <paramref name="document"/>, commits the current session, then
    /// publishes <see cref="AliasCreated"/>. A failed commit prevents publication.
    /// </summary>
Task<AliasDocument> CreateAsync(AliasDocument document, CancellationToken ct = default);
        /// <summary>
    /// Stages an update, synchronously commits the current session, and starts
    /// publication of <see cref="AliasUpdated"/>. This synchronous API does not
    /// expose a cancellation token or await the returned publication task.
    /// </summary>
AliasDocument Update(AliasDocument document);
        /// <summary>
    /// Stages deletion, synchronously commits the current session, and starts
    /// publication of <see cref="AliasDeleted"/> without awaiting that task.
    /// </summary>
void Delete(AliasDocument document);
        /// <summary>
    /// Deletes an existing alias by ID, commits it, and then publishes
    /// <see cref="AliasDeleted"/>. A missing ID is a successful no-op.
    /// </summary>
Task DeleteAsync(long id, CancellationToken ct = default);
}

/// <summary>
/// Repository-backed implementation of <see cref="IAliasService"/>. It observes
/// the commit-before-publish sequence, so an event is not published when its
/// persistence commit fails; a publication failure can occur after the persisted
/// mutation and is surfaced to asynchronous callers.
/// </summary>
public class AliasService : IAliasService
{
    private readonly IAliasRepository _repo;
    private readonly IDocumentSession _session;
    private readonly IMessageBus _bus;

    /// <summary>Initializes the service with the scoped repository, session, and message bus.</summary>
public AliasService(IAliasRepository repo, IDocumentSession session, IMessageBus bus)
    {
        _repo = repo;
        _session = session;
        _bus = bus;
    }

    /// <inheritdoc />
public Task<IList<AliasDocument>> GetBySiteIdAsync(long siteId, CancellationToken ct = default)
        => _repo.GetBySiteIdAsync(siteId, ct);

    /// <inheritdoc />
public Task<AliasDocument?> GetByOldPathAsync(string oldPath, CancellationToken ct = default)
        => _repo.GetByOldPathAsync(oldPath, ct);

    /// <inheritdoc />
public async Task<AliasDocument> CreateAsync(AliasDocument document, CancellationToken ct = default)
    {
        await _repo.AddAsync(document, ct);
        await _session.SaveChangesAsync(ct);
        await _bus.PublishAsync(new AliasCreated(document));
        return document;
    }

    /// <inheritdoc />
public AliasDocument Update(AliasDocument document)
    {
        _repo.Update(document);
        _session.SaveChangesAsync().GetAwaiter().GetResult();
        _bus.PublishAsync(new AliasUpdated(document));
        return document;
    }

    /// <inheritdoc />
public void Delete(AliasDocument document)
    {
        _repo.Delete(document);
        _session.SaveChangesAsync().GetAwaiter().GetResult();
        _bus.PublishAsync(new AliasDeleted(document));
    }

    /// <inheritdoc />
public async Task DeleteAsync(long id, CancellationToken ct = default)
    {
        var existing = await _repo.GetByIdAsync(id, ct);
        if (existing is not null)
        {
            _repo.Delete(existing);
            await _session.SaveChangesAsync(ct);
            await _bus.PublishAsync(new AliasDeleted(existing));
        }
    }
}
