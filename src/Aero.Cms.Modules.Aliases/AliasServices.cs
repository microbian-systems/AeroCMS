using Aero.Marten.Optional;
using FluentValidation;
using Marten;
using Microsoft.Extensions.Logging;
using Aero.Core.Railway;
using Aero.Core.Entities;
using System.Linq.Expressions;
using Aero.Core;
using Aero.Cms.Core.Entities;

namespace Aero.Cms.Modules.Aliases;




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


