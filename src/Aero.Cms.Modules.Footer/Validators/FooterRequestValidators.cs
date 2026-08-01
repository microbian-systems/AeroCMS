using Aero.Cms.Abstractions.Http.Clients;
using FluentValidation;

namespace Aero.Cms.Modules.Footer.Validators;

/// <summary>
/// Validates required text, length, opacity, and nested link-group fields for footer creation.
/// </summary>
/// <remarks>URL values are length-checked here; URL-shape validation occurs when the mapped snapshot is validated.</remarks>
public sealed class CreateFooterRequestValidator : AbstractValidator<CreateFooterRequest>
{
    /// <summary>Initializes the create-footer validation rules.</summary>
    public CreateFooterRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(160);
        RuleFor(x => x.Description).MaximumLength(512);
        RuleFor(x => x.CompanyName).NotEmpty().MaximumLength(160);
        RuleFor(x => x.LogoUrl).MaximumLength(2048);
        RuleFor(x => x.BackgroundImageUrl).MaximumLength(2048);
        RuleFor(x => x.OverlayOpacity).InclusiveBetween(0m, 1m);
        When(x => x.LinkGroups is not null, () =>
        {
            RuleForEach(x => x.LinkGroups!).SetValidator(new CreateFooterLinkGroupRequestValidator());
        });
    }
}

/// <summary>
/// Validates required text, length, opacity, and nested link-group fields for footer updates.
/// </summary>
/// <remarks>The link-group collection is required. URL values are length-checked but are not sanitized by this validator.</remarks>
public sealed class UpdateFooterRequestValidator : AbstractValidator<UpdateFooterRequest>
{
    /// <summary>Initializes the update-footer validation rules.</summary>
    public UpdateFooterRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(160);
        RuleFor(x => x.Description).MaximumLength(512);
        RuleFor(x => x.CompanyName).NotEmpty().MaximumLength(160);
        RuleFor(x => x.LogoUrl).MaximumLength(2048);
        RuleFor(x => x.BackgroundImageUrl).MaximumLength(2048);
        RuleFor(x => x.OverlayOpacity).InclusiveBetween(0m, 1m);
        RuleFor(x => x.LinkGroups).NotNull();
        RuleForEach(x => x.LinkGroups).SetValidator(new UpdateFooterLinkGroupRequestValidator());
    }
}

/// <summary>
/// Validates the title and nested links of a link group in a create request.
/// </summary>
public sealed class CreateFooterLinkGroupRequestValidator : AbstractValidator<CreateFooterLinkGroupRequest>
{
    /// <summary>Initializes the create-link-group validation rules.</summary>
    public CreateFooterLinkGroupRequestValidator()
    {
        RuleFor(x => x.Title).NotEmpty().MaximumLength(120);
        RuleForEach(x => x.Links).SetValidator(new CreateFooterLinkRequestValidator());
    }
}

/// <summary>
/// Validates the title and nested links of a link group in an update request.
/// </summary>
public sealed class UpdateFooterLinkGroupRequestValidator : AbstractValidator<UpdateFooterLinkGroupRequest>
{
    /// <summary>Initializes the update-link-group validation rules.</summary>
    public UpdateFooterLinkGroupRequestValidator()
    {
        RuleFor(x => x.Title).NotEmpty().MaximumLength(120);
        RuleForEach(x => x.Links).SetValidator(new UpdateFooterLinkRequestValidator());
    }
}

/// <summary>
/// Validates the required label and destination lengths of a link in a create request.
/// </summary>
public sealed class CreateFooterLinkRequestValidator : AbstractValidator<CreateFooterLinkRequest>
{
    /// <summary>Initializes the create-link validation rules.</summary>
    public CreateFooterLinkRequestValidator()
    {
        RuleFor(x => x.Label).NotEmpty().MaximumLength(120);
        RuleFor(x => x.Href).NotEmpty().MaximumLength(2048);
    }
}

/// <summary>
/// Validates the required label and destination lengths of a link in an update request.
/// </summary>
public sealed class UpdateFooterLinkRequestValidator : AbstractValidator<UpdateFooterLinkRequest>
{
    /// <summary>Initializes the update-link validation rules.</summary>
    public UpdateFooterLinkRequestValidator()
    {
        RuleFor(x => x.Label).NotEmpty().MaximumLength(120);
        RuleFor(x => x.Href).NotEmpty().MaximumLength(2048);
    }
}
