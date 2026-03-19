using System.Security.Claims;
using Aero.Core.Identity;
using JasperFx;
using Marten;
using Marten.Exceptions;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace Aero.MartenDB.Identity;


/// <summary>
/// Creates a new instance of a persistence store for roles.
/// </summary>
/// <typeparam name="TRole">The type of the class representing a role.</typeparam>
public class RoleStore<TRole> : RoleStore<TRole, IdentityRoleClaim<ulong>>
    where TRole : AeroRole, new()
{
    /// <summary>
    /// Constructs a new instance of <see cref="RoleStore{TRole}"/>.
    /// </summary>
    /// <param name="db">The <see cref="IDocumentSession"/>.</param>
    /// <param name="options"></param>
    /// <param name="describer">The <see cref="IdentityErrorDescriber"/>.</param>
    public RoleStore(IDocumentSession db, IOptions<AeroDbIdentityOptions> options,
        IdentityErrorDescriber? describer = null)
        : base(db, options, describer)
    {
    }

    /// <summary>
    /// Creates a entity representing a role claim.
    /// </summary>
    /// <param name="role">The associated role.</param>
    /// <param name="claim">The associated claim.</param>
    /// <returns>The role claim entity.</returns>
    protected override IdentityRoleClaim<ulong> CreateRoleClaim(TRole role, Claim claim)
    {
        return new IdentityRoleClaim<ulong> { ClaimType = claim.Type, ClaimValue = claim.Value };
    }
}

/// <summary>
/// Creates a new instance of a persistence store for roles.
/// </summary>
/// <typeparam name="TRole">The type of the class representing a role.</typeparam>
/// <typeparam name="TRoleClaim">The type of the class representing a role claim.</typeparam>
public abstract class RoleStore<TRole, TRoleClaim> :
    IRoleStore<TRole>,
    IRoleClaimStore<TRole>,
    IQueryableRoleStore<TRole>
    where TRole : AeroRole, new()
    where TRoleClaim : IdentityRoleClaim<ulong>, new()
{
    private readonly IOptions<AeroDbIdentityOptions> options;

    /// <summary>
    /// Constructs a new instance of <see cref="RoleStore{TRole, TRoleClaim}"/>.
    /// </summary>
    /// <param name="db">The <see cref="IDocumentSession"/>.</param>
    /// <param name="options"></param>
    /// <param name="describer">The <see cref="IdentityErrorDescriber"/>.</param>
    public RoleStore(IDocumentSession db, IOptions<AeroDbIdentityOptions> options,
        IdentityErrorDescriber? describer = null)
    {
        this.db = db ?? throw new ArgumentNullException(nameof(db));
        this.options = options ?? throw new ArgumentNullException(nameof(options));
        ErrorDescriber = describer ?? new IdentityErrorDescriber();
    }

    private bool _disposed;


    /// <summary>
    /// Gets the database context for this store.
    /// </summary>
    public IDocumentSession db { get; private set; }

    /// <summary>
    /// Gets or sets the <see cref="IdentityErrorDescriber"/> for any error that occurred with the current operation.
    /// </summary>
    public IdentityErrorDescriber ErrorDescriber { get; set; }

    /// <summary>Saves the current store.</summary>
    /// <param name="cancellationToken">
    ///   The <see cref="CancellationToken" /> used to propagate notifications that the operation
    ///   should be canceled.
    /// </param>
    /// <returns>The <see cref="Task" /> that represents the asynchronous operation.</returns>
    private async Task SaveChanges(CancellationToken cancellationToken)
    {
        if (options.Value.AutoSaveChanges)
        {
            await db.SaveChangesAsync(cancellationToken);
        }
    }

    //#region IQueryableRoleStore

    /// <summary>
    /// Gets the roles as an IQueryable.
    /// </summary>
    public virtual IQueryable<TRole> Roles => this.db.Query<TRole>();

    //#endregion

    /// <summary>
    /// Creates a new role in a store as an asynchronous operation.
    /// </summary>
    /// <param name="role">The role to create in the store.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> used to propagate notifications that the operation should be canceled.</param>
    /// <returns>A <see cref="Task{TResult}"/> that represents the <see cref="IdentityResult"/> of the asynchronous query.</returns>
    public virtual async Task<IdentityResult> CreateAsync(TRole role, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ThrowIfDisposed();
        if (role == null)
        {
            throw new ArgumentNullException(nameof(role));
        }
        if (string.IsNullOrWhiteSpace(role.Name))
        {
            throw new ArgumentNullException(nameof(role.Name));
        }

        db.Store(role);
        await SaveChanges(cancellationToken);
        return IdentityResult.Success;
    }

    /// <summary>
    /// Updates a role in a store as an asynchronous operation.
    /// </summary>
    /// <param name="role">The role to update in the store.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> used to propagate notifications that the operation should be canceled.</param>
    /// <returns>A <see cref="Task{TResult}"/> that represents the <see cref="IdentityResult"/> of the asynchronous query.</returns>
    public virtual async Task<IdentityResult> UpdateAsync(TRole role, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ThrowIfDisposed();
        if (role == null)
        {
            throw new ArgumentNullException(nameof(role));
        }

        db.Store(role);
        try
        {
            await SaveChanges(cancellationToken);
        }
        catch (ConcurrencyException)
        {
            return IdentityResult.Failed(ErrorDescriber.ConcurrencyFailure());
        }
        return IdentityResult.Success;
    }

    /// <summary>
    /// Deletes a role from the store as an asynchronous operation.
    /// </summary>
    /// <param name="role">The role to delete from the store.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> used to propagate notifications that the operation should be canceled.</param>
    /// <returns>A <see cref="Task{TResult}"/> that represents the <see cref="IdentityResult"/> of the asynchronous query.</returns>
    public virtual async Task<IdentityResult> DeleteAsync(TRole role, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ThrowIfDisposed();
        if (role == null)
        {
            throw new ArgumentNullException(nameof(role));
        }
        db.Delete(role);
        try
        {
            await SaveChanges(cancellationToken);
        }
        catch (ConcurrencyException)
        {
            return IdentityResult.Failed(ErrorDescriber.ConcurrencyFailure());
        }
        return IdentityResult.Success;
    }

    /// <summary>
    /// Gets the ID for a role from the store as an asynchronous operation.
    /// </summary>
    /// <param name="role">The role whose ID should be returned.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> used to propagate notifications that the operation should be canceled.</param>
    /// <returns>A <see cref="Task{TResult}"/> that contains the ID of the role.</returns>
    public virtual Task<string> GetRoleIdAsync(TRole role, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ThrowIfDisposed();
        if (role == null)
        {
            throw new ArgumentNullException(nameof(role));
        }

        return Task.FromResult(role.Id.ToString()!);
    }

    /// <summary>
    /// Gets the name of a role from the store as an asynchronous operation.
    /// </summary>
    /// <param name="role">The role whose name should be returned.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> used to propagate notifications that the operation should be canceled.</param>
    /// <returns>A <see cref="Task{TResult}"/> that contains the name of the role.</returns>
    public virtual Task<string?> GetRoleNameAsync(TRole role, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ThrowIfDisposed();
        if (role == null)
        {
            throw new ArgumentNullException(nameof(role));
        }
        return Task.FromResult(role.Name);
    }

    /// <summary>
    /// Sets the name of a role in the store as an asynchronous operation.
    /// </summary>
    /// <param name="role">The role whose name should be set.</param>
    /// <param name="roleName">The name of the role.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> used to propagate notifications that the operation should be canceled.</param>
    /// <returns>The <see cref="Task"/> that represents the asynchronous operation.</returns>
    public virtual Task SetRoleNameAsync(TRole role, string? roleName, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ThrowIfDisposed();
        if (role == null)
        {
            throw new ArgumentNullException(nameof(role));
        }
        role.Name = roleName;
        return Task.CompletedTask;
    }

    /// <summary>
    /// Finds the role who has the specified ID as an asynchronous operation.
    /// </summary>
    /// <param name="id">The role ID to look for.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> used to propagate notifications that the operation should be canceled.</param>
    /// <returns>A <see cref="Task{TResult}"/> that result of the look up.</returns>
    public virtual Task<TRole?> FindByIdAsync(string id, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ThrowIfDisposed();
        if (!ulong.TryParse(id, out var ulongId))
        {
            return Task.FromResult<TRole?>(null);
        }
        return db.LoadAsync<TRole>(ulongId, cancellationToken);
    }

    /// <summary>
    /// Finds the role who has the specified normalized name as an asynchronous operation.
    /// </summary>
    /// <param name="normalizedName">The normalized role name to look for.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> used to propagate notifications that the operation should be canceled.</param>
    /// <returns>A <see cref="Task{TResult}"/> that result of the look up.</returns>
    public virtual Task<TRole?> FindByNameAsync(string normalizedName, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ThrowIfDisposed();

        return db.Query<TRole>().FirstOrDefaultAsync(x => x.Name == normalizedName, cancellationToken);
    }

    /// <summary>
    /// Get a role's normalized name as an asynchronous operation.
    /// </summary>
    /// <param name="role">The role whose normalized name should be retrieved.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> used to propagate notifications that the operation should be canceled.</param>
    /// <returns>A <see cref="Task{TResult}"/> that contains the name of the role.</returns>
    public virtual Task<string?> GetNormalizedRoleNameAsync(TRole role, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ThrowIfDisposed();
        if (role == null)
        {
            throw new ArgumentNullException(nameof(role));
        }

        return Task.FromResult(role.Name?.ToUpperInvariant());
    }

    /// <summary>
    /// Set a role's normalized name as an asynchronous operation.
    /// </summary>
    /// <param name="role">The role whose normalized name should be set.</param>
    /// <param name="normalizedName">The normalized name to set</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> used to propagate notifications that the operation should be canceled.</param>
    /// <returns>The <see cref="Task"/> that represents the asynchronous operation.</returns>
    public virtual Task SetNormalizedRoleNameAsync(TRole role, string? normalizedName, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ThrowIfDisposed();
        if (role == null)
        {
            throw new ArgumentNullException(nameof(role));
        }
        //role.Name = normalizedName;
        return Task.CompletedTask;
    }

    /// <summary>
    /// Throws if this class has been disposed.
    /// </summary>
    protected void ThrowIfDisposed()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(GetType().Name);
        }
    }

    /// <summary>
    /// Dispose the stores
    /// </summary>
    public virtual void Dispose()
    {
        _disposed = true;
    }

    /// <summary>
    /// Get the claims associated with the specified <paramref name="role"/> as an asynchronous operation.
    /// </summary>
    /// <param name="role">The role whose claims should be retrieved.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> used to propagate notifications that the operation should be canceled.</param>
    /// <returns>A <see cref="Task{TResult}"/> that contains the claims granted to a role.</returns>
    public virtual  async Task<IList<Claim>> GetClaimsAsync(TRole role, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        if (role == null)
        {
            throw new ArgumentNullException(nameof(role));
        }

        await Task.FromResult(0);

        return role.Claims
            .Select(c => new Claim(c.ClaimType ?? string.Empty, c.ClaimValue ?? string.Empty))
            .ToList();
    }

    /// <summary>
    /// Adds the <paramref name="claim"/> given to the specified <paramref name="role"/>.
    /// </summary>
    /// <param name="role">The role to add the claim to.</param>
    /// <param name="claim">The claim to add to the role.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> used to propagate notifications that the operation should be canceled.</param>
    /// <returns>The <see cref="Task"/> that represents the asynchronous operation.</returns>
    public virtual async Task AddClaimAsync(TRole role, Claim claim, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        if (role == null)
        {
            throw new ArgumentNullException(nameof(role));
        }
        if (claim == null)
        {
            throw new ArgumentNullException(nameof(claim));
        }
        role.Claims.Add(CreateRoleClaim(role, claim));

        await SaveChanges(cancellationToken);
    }

    /// <summary>
    /// Removes the <paramref name="claim"/> given from the specified <paramref name="role"/>.
    /// </summary>
    /// <param name="role">The role to remove the claim from.</param>
    /// <param name="claim">The claim to remove from the role.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> used to propagate notifications that the operation should be canceled.</param>
    /// <returns>The <see cref="Task"/> that represents the asynchronous operation.</returns>
    public virtual async Task RemoveClaimAsync(TRole role, Claim claim, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        if (role == null)
        {
            throw new ArgumentNullException(nameof(role));
        }
        if (claim == null)
        {
            throw new ArgumentNullException(nameof(claim));
        }

        var claims = role.Claims.Where(c => c.ClaimValue == claim.Value).ToList();
        foreach (var c in claims)
        {
            role.Claims.Remove(c);
        }

        await SaveChanges(cancellationToken);
    }

    /// <summary>
    /// Creates a entity representing a role claim.
    /// </summary>
    /// <param name="role">The associated role.</param>
    /// <param name="claim">The associated claim.</param>
    /// <returns>The role claim entity.</returns>
    protected abstract TRoleClaim CreateRoleClaim(TRole role, Claim claim);
}
