using Aero.Marten.Optional;
using FluentValidation;
using Marten;
using Microsoft.Extensions.Logging;
using Aero.Core.Railway;
using Aero.Core.Entities;
using System.Linq.Expressions;
using Aero.Core;

namespace Aero.Cms.Modules.Aliases;

/// <summary>
/// Represents a URL alias mapping for a site, including the original and new paths, as well as optional notes.
/// </summary>
/// <remarks>Use this class to store or retrieve information about path redirections or rewrites within a site.
/// Each instance associates an old path with a new path for a specific site, which can be useful for managing legacy
/// URLs or implementing custom routing.</remarks>
public class AliasDocument : Entity
{
    /// <summary>
    /// Gets or sets the unique identifier for the site.
    /// </summary>
    public long SiteId { get; set; }
    /// <summary>
    /// Gets or sets the original file or directory path before a rename or move operation.
    /// </summary>
    public string OldPath { get; set; } = null!;
    /// <summary>
    /// Gets or sets the new file or directory path to be used in the operation.
    /// </summary>
    public string NewPath { get; set; } = null!;
    /// <summary>
    /// Gets or sets optional notes or comments associated with the object.
    /// </summary>
    public string? Notes { get; set; } = null!;
}


/// <summary>
/// Provides validation rules for the AliasDocument type, ensuring that required properties meet specified constraints.
/// </summary>
/// <remarks>This validator enforces that both the OldPath and NewPath properties of an AliasDocument are not
/// empty and do not exceed 2000 characters in length. Use this class to validate AliasDocument instances before
/// processing or persisting them.</remarks>
public class AliasValidator : AbstractValidator<AliasDocument>
{
    public AliasValidator()
    {
        RuleFor(x => x.OldPath)
            .NotEmpty().WithMessage("Old path is required.")
            .MaximumLength(2000).WithMessage("Old path cannot exceed 2000 characters.");
        RuleFor(x => x.NewPath)
            .NotEmpty().WithMessage("New path is required.")
            .MaximumLength(2000).WithMessage("New path cannot exceed 2000 characters.");
        RuleFor(x => x.OldPath)
            .NotEqual(x => x.NewPath).WithMessage("Old path and new path cannot be the same.");
        RuleFor(x => x.SiteId)
            .GreaterThan(0).WithMessage("SiteId must be a positive integer.");
    }
}

public interface IAliasService
{
    Task<Result<AliasError, AliasDocument>> CreateAsync(CreateAliasRequest request, CancellationToken ct);
    Task<Result<AliasError, AliasDocument>> UpdateAsync(UpdateAliasRequest request, CancellationToken ct);
    Task<Result<AliasError, AliasDocument>> DeleteAsync(DeleteAliasRequest request, CancellationToken ct);
    Task<Result<AliasError, AliasDocument>> GetByIdAsync(long id, CancellationToken ct);
    Task<Result<AliasError, AliasDocument>> GetByNewPathAsync(string newPath, CancellationToken ct);
    Task<Result<AliasError, AliasDocument>> GetByOldPathAsync(string oldPath, CancellationToken ct);
    Task<IEnumerable<AliasDocument>> GetAllAsync(long siteId, int page=1, int rows=10, CancellationToken ct = default);
    Task<Result<AliasError, AliasDocument>> GeByPathAsync(long siteId, string path, CancellationToken ct);
    Task<Result<AliasError, AliasDocument>> GetSiteId(long siteId, int page = 1, int rows = 10, CancellationToken ct = default);
    Task<IEnumerable<AliasDocument>> FindAsync(Expression<Func<AliasDocument, bool>> predicate, int page =1, int rows=10, CancellationToken ct= default);
}

public class AliasService(IAliasRepository db) : IAliasService
{
    public async Task<Result<AliasError, AliasDocument>> CreateAsync(CreateAliasRequest request, CancellationToken ct)
    {
        return await db.InsertAsync(new AliasDocument
        {
            SiteId = request.siteId,
            OldPath = request.OldPath,
            NewPath = request.NewPath,
            Notes = request.notes
        }, ct);
    }

    public async Task<Result<AliasError, AliasDocument>> DeleteAsync(DeleteAliasRequest request, CancellationToken ct)
    {
        var res = await db.DeleteAsync(request.id, ct);

        throw new NotImplementedException();
        //return res switch
        //{
        //    true => new Result<AliasError, AliasDocument>.Ok { Value = null! },
        //    false => new Result<AliasError, AliasDocument>.Failure { Error = new AliasError() }
        //};
    }

    public async Task<IEnumerable<AliasDocument>> FindAsync(Expression<Func<AliasDocument, bool>> predicate, int page = 1, int rows = 10, CancellationToken ct = default)
    {
        var results = await db.FindAsync(predicate, ct);
        return results ??  [];
    }

    public Task<Result<AliasError, AliasDocument>> GeByPathAsync(long siteId, string path, CancellationToken ct)
    {
        throw new NotImplementedException();
    }

    public async Task<IEnumerable<AliasDocument>> GetAllAsync(long siteId, int page = 1, int rows = 10, CancellationToken ct = default)
    {
        throw new NotImplementedException() ;
    }

    public Task<Result<AliasError, AliasDocument>> GetByIdAsync(long id, CancellationToken ct)
    {
        throw new NotImplementedException();
    }

    public Task<Result<AliasError, AliasDocument>> GetByNewPathAsync(string newPath, CancellationToken ct)
    {
        throw new NotImplementedException();
    }

    public Task<Result<AliasError, AliasDocument>> GetByOldPathAsync(string oldPath, CancellationToken ct)
    {
        throw new NotImplementedException();
    }

    public Task<Result<AliasError, AliasDocument>> GetSiteId(long siteId, int page = 1, int rows = 10, CancellationToken ct = default)
    {
        throw new NotImplementedException();
    }

    public Task<Result<AliasError, AliasDocument>> UpdateAsync(UpdateAliasRequest request, CancellationToken ct)
    {
        throw new NotImplementedException();
    }
}


public interface IAliasRepository : IMartenGenericRepositoryOption<AliasDocument>
{
    // add specific alias methods here if needed
}

/// <summary>
/// Provides data access and management operations for alias documents using a Marten document session.
/// </summary>
/// <param name="session">The document session used to interact with the underlying data store. Cannot be null.</param>
/// <param name="log">The logger instance used for logging repository operations. Cannot be null.</param>
public class AliasRepository(IDocumentSession session, ILogger<AliasRepository> log)
    : MartenGenericRepositoryOption<AliasDocument>(session, log), IAliasRepository
{

}


/// <summary>
/// Represents a request to create a new alias for a site, specifying the original and new paths.
/// </summary>
/// <param name="siteId">The unique identifier of the site for which the alias is being created.</param>
/// <param name="OldPath">The original path that will be aliased. Must not be null or empty.</param>
/// <param name="NewPath">The new path to which the alias will point. Must not be null or empty.</param>
/// <param name="notes">Optional notes or comments associated with the alias creation. Can be null.</param>
public record CreateAliasRequest(long siteId, string OldPath, string NewPath, string? notes);

/// <summary>
/// Represents a request to update an existing alias with a new path and optional notes.
/// </summary>
/// <param name="id">The unique identifier of the alias to update.</param>
/// <param name="OldPath">The current path associated with the alias. This is used to verify the alias before updating.</param>
/// <param name="NewPath">The new path to assign to the alias.</param>
/// <param name="notes">Optional notes or comments about the update. Can be null.</param>
public record UpdateAliasRequest(long id, string OldPath, string NewPath, string? notes);

/// <summary>
/// Represents a request to delete an alias identified by its unique identifier.
/// </summary>
/// <param name="id">The unique identifier of the alias to delete.</param>
public record DeleteAliasRequest(long id);


/// <summary>
/// Represents an error that occurs when processing an alias operation.
/// </summary>
/// <remarks>This is an abstract base record for errors related to alias handling. Derived types provide specific
/// details about the nature of the alias error. Use pattern matching to handle different error cases
/// appropriately.</remarks>
public abstract record AliasError() : AeroError;


