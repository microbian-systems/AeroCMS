using Aero.Cms.Abstractions.Requests;
using FluentValidation;

namespace Aero.Cms.Abstractions.Validators;

/// <summary>
/// FluentValidation rules for alias create/update/delete requests.
/// Ensures paths follow URL-safe format: starts with '/', no consecutive
/// slashes, no trailing slash, only valid URL-path characters.
/// </summary>
public sealed class CreateAliasRequestValidator : AbstractValidator<CreateAliasRequest>
{
    public CreateAliasRequestValidator()
    {
        RuleFor(x => x.SiteId)
            .GreaterThan(0).WithMessage("SiteId must be a positive integer.");

        RuleFor(x => x.OldPath)
            .NotEmpty().WithMessage("Old path is required.")
            .MaximumLength(2000).WithMessage("Old path cannot exceed 2000 characters.")
            .Must(BeAValidUrlPath).WithMessage("Old path must be a valid URL path (e.g. /old-page or /category/page).");

        RuleFor(x => x.NewPath)
            .NotEmpty().WithMessage("New path is required.")
            .MaximumLength(2000).WithMessage("New path cannot exceed 2000 characters.")
            .Must(BeAValidUrlPath).WithMessage("New path must be a valid URL path (e.g. /new-page).");

        RuleFor(x => x.OldPath)
            .NotEqual(x => x.NewPath).WithMessage("Old path and new path cannot be the same.");
    }

    /// <summary>
    /// Validates that the path looks like <c>/valid-url-path</c>:
    /// starts with '/', contains only valid URL-path characters,
    /// no consecutive slashes, no trailing slash.
    /// </summary>
    internal static bool BeAValidUrlPath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return false;

        // Must start with /
        if (!path.StartsWith('/'))
            return false;

        // No trailing slash (except for root "/")
        if (path.Length > 1 && path.EndsWith('/'))
            return false;

        // No consecutive slashes
        if (path.Contains("//"))
            return false;

        // Only valid URL-path characters
        foreach (var c in path)
        {
            if (!IsValidPathChar(c))
                return false;
        }

        return true;
    }

    private static bool IsValidPathChar(char c) => c switch
    {
        '/' => true,
        '-' => true,
        '_' => true,
        '.' => true,
        '~' => true,
        _ => char.IsLetterOrDigit(c)
    };
}

public sealed class UpdateAliasRequestValidator : AbstractValidator<UpdateAliasRequest>
{
    public UpdateAliasRequestValidator()
    {
        RuleFor(x => x.Id)
            .GreaterThan(0).WithMessage("Id must be a positive integer.");

        RuleFor(x => x.OldPath)
            .NotEmpty().WithMessage("Old path is required.")
            .MaximumLength(2000).WithMessage("Old path cannot exceed 2000 characters.")
            .Must(BeAValidUrlPath).WithMessage("Old path must be a valid URL path.");

        RuleFor(x => x.NewPath)
            .NotEmpty().WithMessage("New path is required.")
            .MaximumLength(2000).WithMessage("New path cannot exceed 2000 characters.")
            .Must(BeAValidUrlPath).WithMessage("New path must be a valid URL path.");

        RuleFor(x => x.OldPath)
            .NotEqual(x => x.NewPath).WithMessage("Old path and new path cannot be the same.");
    }

    private static bool BeAValidUrlPath(string? path) =>
        CreateAliasRequestValidator.BeAValidUrlPath(path); // reuse logic
}

public sealed class DeleteAliasRequestValidator : AbstractValidator<DeleteAliasRequest>
{
    public DeleteAliasRequestValidator()
    {
        RuleFor(x => x.Id)
            .GreaterThan(0).WithMessage("Id must be a positive integer.");
    }
}
