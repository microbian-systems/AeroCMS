using Aero.Cms.Abstractions.Http.Clients;
using FluentValidation;

namespace Aero.Cms.Modules.Footer.Validators;

/// <summary>
/// Represents a class for CreateFooterRequestValidator.
/// </summary>
public sealed class CreateFooterRequestValidator : AbstractValidator<CreateFooterRequest>
{
        /// <summary>
    /// Initializes a new instance of the <see cref="CreateFooterRequestValidator"/> class.
    /// </summary>
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
/// Represents a class for UpdateFooterRequestValidator.
/// </summary>
public sealed class UpdateFooterRequestValidator : AbstractValidator<UpdateFooterRequest>
{
        /// <summary>
    /// Initializes a new instance of the <see cref="UpdateFooterRequestValidator"/> class.
    /// </summary>
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
/// Represents a class for CreateFooterLinkGroupRequestValidator.
/// </summary>
public sealed class CreateFooterLinkGroupRequestValidator : AbstractValidator<CreateFooterLinkGroupRequest>
{
        /// <summary>
    /// Initializes a new instance of the <see cref="CreateFooterLinkGroupRequestValidator"/> class.
    /// </summary>
public CreateFooterLinkGroupRequestValidator()
    {
        RuleFor(x => x.Title).NotEmpty().MaximumLength(120);
        RuleForEach(x => x.Links).SetValidator(new CreateFooterLinkRequestValidator());
    }
}

/// <summary>
/// Represents a class for UpdateFooterLinkGroupRequestValidator.
/// </summary>
public sealed class UpdateFooterLinkGroupRequestValidator : AbstractValidator<UpdateFooterLinkGroupRequest>
{
        /// <summary>
    /// Initializes a new instance of the <see cref="UpdateFooterLinkGroupRequestValidator"/> class.
    /// </summary>
public UpdateFooterLinkGroupRequestValidator()
    {
        RuleFor(x => x.Title).NotEmpty().MaximumLength(120);
        RuleForEach(x => x.Links).SetValidator(new UpdateFooterLinkRequestValidator());
    }
}

/// <summary>
/// Represents a class for CreateFooterLinkRequestValidator.
/// </summary>
public sealed class CreateFooterLinkRequestValidator : AbstractValidator<CreateFooterLinkRequest>
{
        /// <summary>
    /// Initializes a new instance of the <see cref="CreateFooterLinkRequestValidator"/> class.
    /// </summary>
public CreateFooterLinkRequestValidator()
    {
        RuleFor(x => x.Label).NotEmpty().MaximumLength(120);
        RuleFor(x => x.Href).NotEmpty().MaximumLength(2048);
    }
}

/// <summary>
/// Represents a class for UpdateFooterLinkRequestValidator.
/// </summary>
public sealed class UpdateFooterLinkRequestValidator : AbstractValidator<UpdateFooterLinkRequest>
{
        /// <summary>
    /// Initializes a new instance of the <see cref="UpdateFooterLinkRequestValidator"/> class.
    /// </summary>
public UpdateFooterLinkRequestValidator()
    {
        RuleFor(x => x.Label).NotEmpty().MaximumLength(120);
        RuleFor(x => x.Href).NotEmpty().MaximumLength(2048);
    }
}
