using FluentValidation;
using Aero.Cms.Modules.Commerce.Client.Services;
using System.Text;

namespace Aero.Cms.Modules.Commerce.Client.Models;

public sealed class ProductEditorModel
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string Sku { get; set; } = string.Empty;
    public int StockQuantity { get; set; }
    public bool IsActive { get; set; } = true;
    public string TagsText { get; set; } = string.Empty;
    public string AttributesText { get; set; } = string.Empty;
    public long Version { get; set; }

    public ManagerProductRequest ToRequest() => new(
        Name.Trim(), Description?.Trim(), Sku.Trim(), StockQuantity, IsActive,
        ParseAttributes(AttributesText),
        TagsText.Split([',', '\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Distinct(StringComparer.OrdinalIgnoreCase).ToList(),
        Version);

    public static ProductEditorModel From(ManagerProductDto value) => new()
    {
        Name = value.Name,
        Description = value.Description,
        Sku = value.Sku,
        StockQuantity = value.StockQuantity,
        IsActive = value.IsActive,
        TagsText = string.Join(", ", value.Tags),
        AttributesText = string.Join(Environment.NewLine, value.Attributes.Select(pair => $"{pair.Key}={pair.Value}")),
        Version = value.Version
    };

    internal static bool HasValidAttributes(string? text)
        => string.IsNullOrWhiteSpace(text) || text.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .All(line => { var separator = line.IndexOf('='); return separator > 0 && separator < line.Length - 1; });

    private static Dictionary<string, string> ParseAttributes(string? text)
    {
        var attributes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(text)) return attributes;

        foreach (var line in text.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var separator = line.IndexOf('=');
            attributes[line[..separator].Trim()] = line[(separator + 1)..].Trim();
        }

        return attributes;
    }
}

public sealed class ProductEditorModelValidator : AbstractValidator<ProductEditorModel>
{
    public ProductEditorModelValidator()
    {
        RuleFor(model => model.Name).NotEmpty().MaximumLength(500);
        RuleFor(model => model.Sku).NotEmpty().MaximumLength(128).Matches("^[A-Za-z0-9._-]+$");
        RuleFor(model => model.StockQuantity).GreaterThanOrEqualTo(0);
        RuleFor(model => model.AttributesText).Must(ProductEditorModel.HasValidAttributes)
            .WithMessage("Each attribute must use key=value on its own line.");
    }
}

public sealed class ListingEditorModel
{
    public long ProductId { get; set; }
    public string Culture { get; set; } = "en-US";
    public string Slug { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? ShortDescription { get; set; }
    public string? Description { get; set; }
    public string? Category { get; set; }
    public string? ImageUrl { get; set; }
    public decimal Price { get; set; }
    public decimal? CompareAtPrice { get; set; }
    public bool IsPublished { get; set; }
    public bool IsFeatured { get; set; }
    public long Version { get; set; }

    public ManagerListingRequest ToRequest() => new(
        ProductId, Culture.Trim(), NormalizeSlug(Slug), Name.Trim(), ShortDescription?.Trim(), Description?.Trim(),
        Category?.Trim(), ImageUrl?.Trim(), Price, CompareAtPrice, IsPublished, IsFeatured, Version);

    public static string NormalizeSlug(string? value)
    {
        var source = (value ?? string.Empty).Trim().ToLowerInvariant();
        var result = new StringBuilder(source.Length);
        var pendingSeparator = false;
        foreach (var character in source)
        {
            if (character is >= 'a' and <= 'z' or >= '0' and <= '9')
            {
                if (pendingSeparator && result.Length > 0) result.Append('-');
                result.Append(character);
                pendingSeparator = false;
            }
            else
            {
                pendingSeparator = result.Length > 0;
            }
        }
        return result.ToString();
    }

    public static ListingEditorModel From(ManagerListingDto value) => new()
    {
        ProductId = value.ProductId,
        Culture = value.Culture,
        Slug = value.Slug,
        Name = value.Name,
        ShortDescription = value.ShortDescription,
        Description = value.Description,
        Category = value.Category,
        ImageUrl = value.ImageUrl,
        Price = value.Price,
        CompareAtPrice = value.CompareAtPrice,
        IsPublished = value.IsPublished,
        IsFeatured = value.IsFeatured,
        Version = value.Version
    };
}

public sealed class ListingEditorModelValidator : AbstractValidator<ListingEditorModel>
{
    public ListingEditorModelValidator()
    {
        RuleFor(model => model.ProductId).GreaterThan(0);
        RuleFor(model => model.Culture).NotEmpty().MaximumLength(32).Matches("^[a-zA-Z]{2,8}(-[a-zA-Z0-9]{2,8})*$");
        RuleFor(model => model.Slug).Must(value =>
            !string.IsNullOrWhiteSpace(ListingEditorModel.NormalizeSlug(value)) &&
            ListingEditorModel.NormalizeSlug(value).Length <= 256)
            .WithMessage("Slug must produce a route-safe value of 256 characters or fewer.");
        RuleFor(model => model.Name).NotEmpty().MaximumLength(500);
        RuleFor(model => model.ShortDescription).MaximumLength(1_000);
        RuleFor(model => model.Description).MaximumLength(10_000);
        RuleFor(model => model.Category).MaximumLength(256);
        RuleFor(model => model.Price).Must(IsValidUsd)
            .WithMessage("Price must be a positive USD amount with no more than two decimal places.");
        RuleFor(model => model.CompareAtPrice).Must((model, value) => value is null || (IsValidUsd(value.Value) && value >= model.Price));
        RuleFor(model => model.ImageUrl).MaximumLength(2_048).Must(value => string.IsNullOrWhiteSpace(value) || Uri.TryCreate(value, UriKind.RelativeOrAbsolute, out _))
            .WithMessage("Image URL must be a valid relative or absolute URL.");
    }

    private static bool IsValidUsd(decimal amount)
        => amount > 0m && amount <= 1_000_000_000m && decimal.Round(amount, 2, MidpointRounding.ToZero) == amount;
}
