using System.Globalization;
using System.Net;
using System.Text;
using System.Text.Json;
using Aero.Cms.Abstractions.Pages.Composition;
using Aero.Cms.Core.Entities;
using Aero.Cms.Modules.Commerce.Catalog.Models;
using Aero.Cms.Modules.Commerce.Catalog.Services;
using Aero.Cms.Modules.Commerce.Storefront;
using Aero.Cms.Modules.Pages.Rendering;
using Aero.Cms.Shared.Localization;
using Aero.Core;
using Aero.Core.Railway;
using AeroDB.Sable;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace Aero.Cms.Modules.Commerce.PageEditor;

/// <summary>
/// Shared implementation for Commerce's explicit, PageEditor-safe catalog renderers.
/// </summary>
public abstract class CommercePageRegisteredFragmentProvider(
    IServiceScopeFactory scopeFactory,
    IHttpContextAccessor httpContextAccessor) : IPageRegisteredFragmentProvider
{
    protected readonly IServiceScopeFactory ScopeFactory = scopeFactory;
    protected readonly IHttpContextAccessor HttpContextAccessor = httpContextAccessor;

    public abstract PageRegisteredFragmentDescriptor Descriptor { get; }

    public abstract Task<Result<string>> RenderAsync(
        PageRegisteredFragment fragment,
        PageFragmentRenderContext context,
        CancellationToken cancellationToken = default);

    protected static async Task<Result<StorefrontContext>> ResolveStorefrontAsync(
        IServiceProvider serviceProvider,
        long siteId,
        string culture,
        CancellationToken cancellationToken)
    {
        if (siteId <= 0)
        {
            return AeroError.ValidationError(["A Commerce page fragment requires a positive site identifier."]);
        }

        try
        {
            var session = serviceProvider.GetRequiredService<IDocumentSession>();
            var site = await session.LoadAsync<SitesModel>(siteId, cancellationToken);
            if (site is not { TenantId: > 0 })
            {
                return AeroError.NotFoundError("Storefront site not found.");
            }

            return new StorefrontContext(
                site.TenantId,
                AeroCultureRoute.NormalizeCultureOrDefault(culture, site.DefaultCulture ?? SitesModel.DefaultCultureName));
        }
        catch (Exception exception)
        {
            return AeroError.DatabaseError(exception.Message);
        }
    }

    protected static int GetTake(PageRegisteredFragment fragment) => fragment.Parameters.TryGetValue("take", out var take)
        && take.TryGetInt32(out var requested)
        ? Math.Clamp(requested, 1, 24)
        : 12;

    protected static string RenderListingCards(IEnumerable<ProductListingDocument> listings, string culture)
    {
        var markup = new StringBuilder("<section aria-label=\"Published products\"><ul>");
        foreach (var listing in listings)
        {
            var name = WebUtility.HtmlEncode(listing.Name);
            var description = WebUtility.HtmlEncode(ShortDescription(listing));
            markup.Append("<li><article>");
            if (!string.IsNullOrWhiteSpace(listing.ImageUrl))
            {
                markup.Append("<img src=\"")
                    .Append(WebUtility.HtmlEncode(listing.ImageUrl))
                    .Append("\" alt=\"")
                    .Append(name)
                    .Append("\" loading=\"lazy\">");
            }

            var productPath = AeroCultureRoute.BuildCulturePath(culture, $"shop/products/{listing.Slug}");
            markup.Append("<h2><a href=\"")
                .Append(WebUtility.HtmlEncode(productPath))
                .Append("\">")
                .Append(name)
                .Append("</a></h2><p>")
                .Append(description)
                .Append("</p><p><strong>")
                .Append(WebUtility.HtmlEncode(listing.Price.ToString("C", CultureInfo.GetCultureInfo("en-US"))))
                .Append("</strong></p>");
            AppendSubscriptionDisclosure(markup, listing);
            markup.Append("<p><a href=\"")
                .Append(WebUtility.HtmlEncode(productPath))
                .Append("\">View product</a></p></article></li>");
        }

        markup.Append("</ul></section>");
        return markup.ToString();
    }

    protected static string RenderSearchForm(string action, string? search)
        => "<form action=\""
           + WebUtility.HtmlEncode(action)
           + "\" method=\"get\"><label for=\"commerce-search\">Search products</label><input id=\"commerce-search\" name=\"search\" type=\"search\" maxlength=\"100\" value=\""
           + WebUtility.HtmlEncode(search ?? string.Empty)
           + "\"><button type=\"submit\">Search</button></form>";

    protected static string RenderProduct(ProductListingDocument listing, string culture, StorefrontMemberState member)
    {
        var markup = new StringBuilder("<article>");
        if (!string.IsNullOrWhiteSpace(listing.ImageUrl))
        {
            markup.Append("<img src=\"")
                .Append(WebUtility.HtmlEncode(listing.ImageUrl))
                .Append("\" alt=\"")
                .Append(WebUtility.HtmlEncode(listing.Name))
                .Append("\" loading=\"lazy\">");
        }

        markup.Append("<h2>")
            .Append(WebUtility.HtmlEncode(listing.Name))
            .Append("</h2><p>")
            .Append(WebUtility.HtmlEncode(ShortDescription(listing)))
            .Append("</p><p><strong>")
            .Append(WebUtility.HtmlEncode(listing.Price.ToString("C", CultureInfo.GetCultureInfo("en-US"))))
            .Append("</strong></p>");
        AppendSubscriptionDisclosure(markup, listing);
        markup.Append("<p><a href=\"")
            .Append(WebUtility.HtmlEncode(AeroCultureRoute.BuildCulturePath(culture, $"shop/products/{listing.Slug}")))
            .Append("\">View product details</a></p>");

        var cartPath = StorefrontCartPath(culture, "shop/cart");
        if (member.IsAuthorized)
        {
            var addPath = StorefrontCartPath(culture, "shop/cart/add", listing.Id);
            markup.Append("<p><a href=\"")
                .Append(WebUtility.HtmlEncode(addPath))
                .Append("\">Add to shopping bag</a> <a href=\"")
                .Append(WebUtility.HtmlEncode(cartPath))
                .Append("\">View shopping bag</a></p>");
        }
        else if (member.Kind == StorefrontMemberStateKind.NotCurrentSiteMember)
        {
            markup.Append("<p>Your account does not have access to purchase from this store.</p>");
        }
        else
        {
            markup.Append("<p>Customer sign-in is required to add products to the shopping bag.</p>");
        }

        markup.Append("</article>");
        return markup.ToString();
    }

    private static void AppendSubscriptionDisclosure(StringBuilder markup, ProductListingDocument listing)
    {
        if (listing.SubscriptionOffer is { IntervalDays: >= 1 } subscription)
        {
            markup.Append("<p><strong>Subscription:</strong> renews every ")
                .Append(subscription.IntervalDays.ToString(CultureInfo.InvariantCulture))
                .Append(" days until cancelled.</p>");
        }
    }

    private static string StorefrontCartPath(string culture, string path, long? listingId = null)
    {
        var absolutePath = path.StartsWith('/') ? path : string.Concat('/', path);
        var query = new StringBuilder("?culture=")
            .Append(Uri.EscapeDataString(culture));
        if (listingId is > 0)
            query.Append("&listingId=").Append(listingId.Value.ToString(CultureInfo.InvariantCulture));
        return absolutePath + query;
    }

    private static string ShortDescription(ProductListingDocument listing)
    {
        var value = listing.ShortDescription ?? listing.Description ?? string.Empty;
        return value.Length <= 320 ? value : string.Concat(value.AsSpan(0, 317), "...");
    }

    protected sealed record StorefrontContext(long TenantId, string Culture);
}

/// <summary>Renders published catalog cards scoped to the page's exact site and culture.</summary>
public sealed class CommerceCatalogPageRegisteredFragmentProvider(
    IServiceScopeFactory scopeFactory,
    IHttpContextAccessor httpContextAccessor)
    : CommercePageRegisteredFragmentProvider(scopeFactory, httpContextAccessor)
{
    public override PageRegisteredFragmentDescriptor Descriptor { get; } = new()
    {
        Key = "commerce.catalog",
        DisplayName = "Commerce catalog",
        Description = "Published catalog cards for this page's site and culture.",
        Category = "Commerce",
        Parameters =
        [
            new PageRegisteredFragmentParameterDescriptor
            {
                Name = "take", DisplayName = "Items", Kind = PageRegisteredFragmentParameterKind.Integer,
                Minimum = 1, Maximum = 24, DefaultValue = JsonSerializer.SerializeToElement(12)
            },
            new PageRegisteredFragmentParameterDescriptor
            {
                Name = "featuredOnly", DisplayName = "Featured only", Kind = PageRegisteredFragmentParameterKind.Boolean,
                DefaultValue = JsonSerializer.SerializeToElement(false)
            }
        ]
    };

    public override async Task<Result<string>> RenderAsync(PageRegisteredFragment fragment, PageFragmentRenderContext context, CancellationToken cancellationToken = default)
    {
        var search = HttpContextAccessor.HttpContext?.Request.Query["search"].ToString();
        search = string.IsNullOrWhiteSpace(search) ? null : search.Trim()[..Math.Min(search.Trim().Length, 100)];

        await using var scope = ScopeFactory.CreateAsyncScope();
        var storefront = await ResolveStorefrontAsync(scope.ServiceProvider, context.SiteId, context.Culture, cancellationToken);
        if (storefront is Result<StorefrontContext>.Failure failure)
            return failure.Error;

        var products = scope.ServiceProvider.GetRequiredService<IProductService>();
        var featuredOnly = fragment.Parameters.TryGetValue("featuredOnly", out var featured) && featured.ValueKind is JsonValueKind.True;
        var listings = await products.SearchPublishedAsync(
            ((Result<StorefrontContext>.Ok)storefront).Value.TenantId,
            context.SiteId,
            ((Result<StorefrontContext>.Ok)storefront).Value.Culture,
            search,
            take: GetTake(fragment),
            featuredOnly: featuredOnly && search is null,
            ct: cancellationToken);
        return listings is Result<(IReadOnlyList<ProductListingDocument> Items, long TotalCount), AeroError>.Ok result
            ? Prelude.Ok(
                RenderSearchForm(
                    AeroCultureRoute.BuildCulturePath(((Result<StorefrontContext>.Ok)storefront).Value.Culture, "shop"),
                    search)
                + RenderListingCards(result.Value.Items, ((Result<StorefrontContext>.Ok)storefront).Value.Culture))
            : ((Result<(IReadOnlyList<ProductListingDocument> Items, long TotalCount), AeroError>.Failure)listings).Error;
    }
}

/// <summary>Renders a read-only search form and published search results for the current storefront request.</summary>
public sealed class CommerceSearchPageRegisteredFragmentProvider(
    IServiceScopeFactory scopeFactory,
    IHttpContextAccessor httpContextAccessor)
    : CommercePageRegisteredFragmentProvider(scopeFactory, httpContextAccessor)
{
    public override PageRegisteredFragmentDescriptor Descriptor { get; } = new()
    {
        Key = "commerce.search",
        DisplayName = "Commerce search",
        Description = "A catalog search form and published results for this page's site and culture.",
        Category = "Commerce",
        Parameters =
        [
            new PageRegisteredFragmentParameterDescriptor
            {
                Name = "take", DisplayName = "Items", Kind = PageRegisteredFragmentParameterKind.Integer,
                Minimum = 1, Maximum = 24, DefaultValue = JsonSerializer.SerializeToElement(12)
            }
        ]
    };

    public override async Task<Result<string>> RenderAsync(PageRegisteredFragment fragment, PageFragmentRenderContext context, CancellationToken cancellationToken = default)
    {
        var search = HttpContextAccessor.HttpContext?.Request.Query["search"].ToString();
        var category = HttpContextAccessor.HttpContext?.Request.Query["category"].ToString();
        search = string.IsNullOrWhiteSpace(search) ? null : search.Trim()[..Math.Min(search.Trim().Length, 100)];
        category = string.IsNullOrWhiteSpace(category) ? null : category.Trim()[..Math.Min(category.Trim().Length, 100)];

        await using var scope = ScopeFactory.CreateAsyncScope();
        var storefront = await ResolveStorefrontAsync(scope.ServiceProvider, context.SiteId, context.Culture, cancellationToken);
        if (storefront is Result<StorefrontContext>.Failure failure)
            return failure.Error;

        var products = scope.ServiceProvider.GetRequiredService<IProductService>();
        var listings = await products.SearchPublishedAsync(
            ((Result<StorefrontContext>.Ok)storefront).Value.TenantId, context.SiteId, ((Result<StorefrontContext>.Ok)storefront).Value.Culture, search, category, take: GetTake(fragment), ct: cancellationToken);
        if (listings is Result<(IReadOnlyList<ProductListingDocument> Items, long TotalCount), AeroError>.Failure listingFailure)
            return listingFailure.Error;

        var form = RenderSearchForm(
            AeroCultureRoute.BuildCulturePath(((Result<StorefrontContext>.Ok)storefront).Value.Culture, "shop/search"),
            search);
        return Prelude.Ok(form + RenderListingCards(
            ((Result<(IReadOnlyList<ProductListingDocument> Items, long TotalCount), AeroError>.Ok)listings).Value.Items,
            ((Result<StorefrontContext>.Ok)storefront).Value.Culture));
    }
}

/// <summary>Renders one published listing by its canonical slug in the page composition.</summary>
public sealed class CommerceProductPageRegisteredFragmentProvider(
    IServiceScopeFactory scopeFactory,
    IHttpContextAccessor httpContextAccessor)
    : CommercePageRegisteredFragmentProvider(scopeFactory, httpContextAccessor)
{
    public override PageRegisteredFragmentDescriptor Descriptor { get; } = new()
    {
        Key = "commerce.product",
        DisplayName = "Commerce product",
        Description = "One published storefront product scoped to this page's site and culture.",
        Category = "Commerce",
        Parameters =
        [
            new PageRegisteredFragmentParameterDescriptor
            {
                Name = "slug", DisplayName = "Product slug", Kind = PageRegisteredFragmentParameterKind.String,
                Required = true, MaximumLength = 256
            }
        ]
    };

    public override async Task<Result<string>> RenderAsync(PageRegisteredFragment fragment, PageFragmentRenderContext context, CancellationToken cancellationToken = default)
    {
        var slug = fragment.Parameters["slug"].GetString() ?? string.Empty;
        if (!CatalogSlug.IsCanonical(slug))
            return AeroError.ValidationError(["The Commerce product fragment slug is invalid."]);

        await using var scope = ScopeFactory.CreateAsyncScope();
        var storefront = await ResolveStorefrontAsync(scope.ServiceProvider, context.SiteId, context.Culture, cancellationToken);
        if (storefront is Result<StorefrontContext>.Failure failure)
            return failure.Error;

        var products = scope.ServiceProvider.GetRequiredService<IProductService>();
        var listing = await products.GetPublishedListingBySlugAsync(
            ((Result<StorefrontContext>.Ok)storefront).Value.TenantId, context.SiteId, ((Result<StorefrontContext>.Ok)storefront).Value.Culture, slug, cancellationToken);
        if (listing is Result<ProductListingDocument?, AeroError>.Ok { Value: { } value })
        {
            var memberAccessor = scope.ServiceProvider.GetService<IStorefrontMemberAccessor>();
            var member = memberAccessor is null
                ? new StorefrontMemberState(StorefrontMemberStateKind.Unauthenticated)
                : await memberAccessor.GetAsync(cancellationToken);
            return Prelude.Ok(RenderProduct(
                value,
                ((Result<StorefrontContext>.Ok)storefront).Value.Culture,
                member));
        }

        return listing switch
        {
            Result<ProductListingDocument?, AeroError>.Ok => Prelude.Ok("<p>Product unavailable.</p>"),
            Result<ProductListingDocument?, AeroError>.Failure productFailure => productFailure.Error,
            _ => AeroError.DatabaseError("Catalog data could not be loaded.")
        };
    }
}
