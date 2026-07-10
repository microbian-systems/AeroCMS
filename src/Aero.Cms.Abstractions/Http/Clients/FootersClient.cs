namespace Aero.Cms.Abstractions.Http.Clients;

using Aero.Core.Railway;
using Microsoft.Extensions.Logging;

/// <summary>
/// Defines an interface for IFootersHttpClient.
/// </summary>
public interface IFootersHttpClient
{
        /// <summary>
    /// GetAllAsync method.
    /// </summary>
Task<Result<IReadOnlyList<FooterSummary>, AeroError>> GetAllAsync(CancellationToken ct = default);
        /// <summary>
    /// GetByIdAsync method.
    /// </summary>
Task<Result<FooterDetail, AeroError>> GetByIdAsync(long id, CancellationToken ct = default);
        /// <summary>
    /// ListCultureVariantsAsync method.
    /// </summary>
Task<Result<IReadOnlyList<FooterDetail>, AeroError>> ListCultureVariantsAsync(long id, CancellationToken ct = default);
        /// <summary>
    /// ForkToCultureAsync method.
    /// </summary>
Task<Result<FooterDetail, AeroError>> ForkToCultureAsync(long id, ForkFooterCultureRequest request, CancellationToken ct = default);
        /// <summary>
    /// TranslateWithAiAsync method.
    /// </summary>
Task<Result<AiTranslateFooterResult, AeroError>> TranslateWithAiAsync(long id, AiTranslateFooterRequest request, CancellationToken ct = default);
        /// <summary>
    /// CreateAsync method.
    /// </summary>
Task<Result<FooterDetail, AeroError>> CreateAsync(CreateFooterRequest request, CancellationToken ct = default);
        /// <summary>
    /// UpdateAsync method.
    /// </summary>
Task<Result<FooterDetail, AeroError>> UpdateAsync(long id, UpdateFooterRequest request, CancellationToken ct = default);
        /// <summary>
    /// SaveDraftAsync method.
    /// </summary>
Task<Result<FooterDetail, AeroError>> SaveDraftAsync(long id, UpdateFooterRequest request, long expectedVersion, CancellationToken ct = default);
        /// <summary>
    /// PublishAsync method.
    /// </summary>
Task<Result<FooterDetail, AeroError>> PublishAsync(long id, long expectedVersion, CancellationToken ct = default);
        /// <summary>
    /// SetDefaultAsync method.
    /// </summary>
Task<Result<bool, AeroError>> SetDefaultAsync(long id, CancellationToken ct = default);
        /// <summary>
    /// DeleteAsync method.
    /// </summary>
Task<Result<bool, AeroError>> DeleteAsync(long id, CancellationToken ct = default);
}

/// <summary>
/// Represents a class for FootersHttpClient.
/// </summary>
public sealed class FootersHttpClient(HttpClient httpClient, ILogger<FootersHttpClient> logger)
    : AeroCmsClientBase(httpClient, logger), IFootersHttpClient
{
        /// <summary>
    /// Gets or sets the Path.
    /// </summary>
public override string Path => "admin/footers";

        /// <summary>
    /// GetAllAsync method.
    /// </summary>
public Task<Result<IReadOnlyList<FooterSummary>, AeroError>> GetAllAsync(CancellationToken ct = default)
        => GetAsync<IReadOnlyList<FooterSummary>>(string.Empty, ct);

        /// <summary>
    /// GetByIdAsync method.
    /// </summary>
public Task<Result<FooterDetail, AeroError>> GetByIdAsync(long id, CancellationToken ct = default)
        => GetAsync<FooterDetail>($"details/{id}", ct);

        /// <summary>
    /// ListCultureVariantsAsync method.
    /// </summary>
public Task<Result<IReadOnlyList<FooterDetail>, AeroError>> ListCultureVariantsAsync(long id, CancellationToken ct = default)
        => GetAsync<IReadOnlyList<FooterDetail>>($"{id}/translations", ct);

        /// <summary>
    /// ForkToCultureAsync method.
    /// </summary>
public Task<Result<FooterDetail, AeroError>> ForkToCultureAsync(long id, ForkFooterCultureRequest request, CancellationToken ct = default)
        => PostAsync<ForkFooterCultureRequest, FooterDetail>($"{id}/translations", request, ct);

        /// <summary>
    /// TranslateWithAiAsync method.
    /// </summary>
public Task<Result<AiTranslateFooterResult, AeroError>> TranslateWithAiAsync(long id, AiTranslateFooterRequest request, CancellationToken ct = default)
        => PostAsync<AiTranslateFooterRequest, AiTranslateFooterResult>($"{id}/ai-translate", request, ct);

        /// <summary>
    /// CreateAsync method.
    /// </summary>
public Task<Result<FooterDetail, AeroError>> CreateAsync(CreateFooterRequest request, CancellationToken ct = default)
        => PostAsync<CreateFooterRequest, FooterDetail>(string.Empty, request, ct);

        /// <summary>
    /// UpdateAsync method.
    /// </summary>
public Task<Result<FooterDetail, AeroError>> UpdateAsync(long id, UpdateFooterRequest request, CancellationToken ct = default)
        => PutAsync<UpdateFooterRequest, FooterDetail>(id.ToString(), request, ct);

        /// <summary>
    /// SaveDraftAsync method.
    /// </summary>
public Task<Result<FooterDetail, AeroError>> SaveDraftAsync(
        long id,
        UpdateFooterRequest request,
        long expectedVersion,
        CancellationToken ct = default)
        => PutAsync<UpdateFooterRequest, FooterDetail>($"{id}/draft?expectedVersion={expectedVersion}", request, ct);

        /// <summary>
    /// PublishAsync method.
    /// </summary>
public Task<Result<FooterDetail, AeroError>> PublishAsync(long id, long expectedVersion, CancellationToken ct = default)
        => PutAsync<object, FooterDetail>($"{id}/publish?expectedVersion={expectedVersion}", new { }, ct);

        /// <summary>
    /// SetDefaultAsync method.
    /// </summary>
public Task<Result<bool, AeroError>> SetDefaultAsync(long id, CancellationToken ct = default)
        => MapBoolResult(base.PutAsync(CreateUri($"{id}/default"), new { }, ct));

        /// <summary>
    /// DeleteAsync method.
    /// </summary>
public Task<Result<bool, AeroError>> DeleteAsync(long id, CancellationToken ct = default)
        => MapBoolResult(base.DeleteAsync(id.ToString(), ct));

    private static async Task<Result<bool, AeroError>> MapBoolResult(Task<Result<HttpResponseMessage, AeroError>> task)
    {
        var response = await task;
        return response switch
        {
            Result<HttpResponseMessage, AeroError>.Ok => true,
            Result<HttpResponseMessage, AeroError>.Failure(var error) => error,
            _ => AeroError.CreateError("Unexpected result from HTTP operation")
        };
    }
}

#pragma warning disable SA1402
#pragma warning disable SA1649

/// <summary>
/// Represents a record for FooterSummary.
/// </summary>
public record FooterSummary(
    long Id,
    string Name,
    string? Description,
    int LinkGroupCount,
    DateTime CreatedAt,
    long Version = 0,
    string? State = null,
    string Culture = "en-US",
    long? TranslationGroupId = null);

/// <summary>
/// Represents a record for FooterDetail.
/// </summary>
public record FooterDetail(
    long Id,
    string Name,
    string? Description,
    IReadOnlyList<FooterLinkGroupDetail> LinkGroups,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    long Version = 0,
    string? State = null,
    string CompanyName = "Aero CMS",
    string? Tagline = null,
    string? LogoUrl = null,
    string? BackgroundImageUrl = null,
    decimal OverlayOpacity = 0.35m,
    string? CopyrightText = null,
    string Culture = "en-US",
    long? TranslationGroupId = null,
    IReadOnlyList<FooterLinkDetail> LegalLinks = null,
    IReadOnlyList<FooterComponentDetail>? Components = null,
    IReadOnlyList<FooterCanvasRowDetail>? Rows = null)
{
        /// <summary>
    /// Gets or sets the Legal Links.
    /// </summary>
public IReadOnlyList<FooterLinkDetail> LegalLinks { get; init; } = LegalLinks ?? [];
        /// <summary>
    /// Gets or sets the Components.
    /// </summary>
public IReadOnlyList<FooterComponentDetail> Components { get; init; } = Components ?? [];
        /// <summary>
    /// Gets or sets the Rows.
    /// </summary>
public IReadOnlyList<FooterCanvasRowDetail> Rows { get; init; } = Rows ?? [];
}

/// <summary>
/// Represents a record for ForkFooterCultureRequest.
/// </summary>
public sealed record ForkFooterCultureRequest(string Culture);

/// <summary>
/// Represents a record for AiTranslateFooterRequest.
/// </summary>
public sealed record AiTranslateFooterRequest(
    IReadOnlyList<AiTranslateFooterCultureRequest> Targets,
    string? ProviderId = null,
    bool OverwriteExisting = false);

/// <summary>
/// Represents a record for AiTranslateFooterCultureRequest.
/// </summary>
public sealed record AiTranslateFooterCultureRequest(string Culture);

/// <summary>
/// Represents a record for AiTranslateFooterResult.
/// </summary>
public sealed record AiTranslateFooterResult(
    IReadOnlyList<AiTranslateFooterCultureResult> Results);

/// <summary>
/// Represents a record for AiTranslateFooterCultureResult.
/// </summary>
public sealed record AiTranslateFooterCultureResult(
    string Culture,
    bool Succeeded,
    FooterDetail? Footer,
    IReadOnlyList<string> Warnings,
    string? Error);

/// <summary>
/// Represents a record for CreateFooterRequest.
/// </summary>
public record CreateFooterRequest(
    string Name,
    string? Description,
    string CompanyName = "Aero CMS",
    IReadOnlyList<CreateFooterLinkGroupRequest>? LinkGroups = null,
    string? Tagline = null,
    string? LogoUrl = null,
    string? BackgroundImageUrl = null,
    decimal OverlayOpacity = 0.35m,
    string? CopyrightText = null,
    IReadOnlyList<CreateFooterLinkRequest>? LegalLinks = null);

/// <summary>
/// Represents a record for UpdateFooterRequest.
/// </summary>
public record UpdateFooterRequest(
    string Name,
    string? Description,
    string CompanyName,
    IReadOnlyList<UpdateFooterLinkGroupRequest> LinkGroups,
    string? Tagline = null,
    string? LogoUrl = null,
    string? BackgroundImageUrl = null,
    decimal OverlayOpacity = 0.35m,
    string? CopyrightText = null,
    IReadOnlyList<UpdateFooterLinkRequest>? LegalLinks = null,
    IReadOnlyList<UpdateFooterComponentRequest>? Components = null,
    IReadOnlyList<UpdateFooterCanvasRowRequest>? Rows = null)
{
        /// <summary>
    /// Gets or sets the Components.
    /// </summary>
public IReadOnlyList<UpdateFooterComponentRequest> Components { get; init; } = Components ?? [];
        /// <summary>
    /// Gets or sets the Rows.
    /// </summary>
public IReadOnlyList<UpdateFooterCanvasRowRequest> Rows { get; init; } = Rows ?? [];
}

/// <summary>
/// Represents a record for FooterLinkGroupDetail.
/// </summary>
public record FooterLinkGroupDetail(long Id, string Title, IReadOnlyList<FooterLinkDetail> Links, int Order);

/// <summary>
/// Represents a record for FooterLinkDetail.
/// </summary>
public record FooterLinkDetail(long Id, string Label, string Href, int Order, bool OpenInNewTab = false);

/// <summary>
/// Represents a record for CreateFooterLinkGroupRequest.
/// </summary>
public record CreateFooterLinkGroupRequest(string Title, IReadOnlyList<CreateFooterLinkRequest> Links, int Order);

/// <summary>
/// Represents a record for CreateFooterLinkRequest.
/// </summary>
public record CreateFooterLinkRequest(string Label, string Href, int Order, bool OpenInNewTab = false);

/// <summary>
/// Represents a record for UpdateFooterLinkGroupRequest.
/// </summary>
public record UpdateFooterLinkGroupRequest(long Id, string Title, IReadOnlyList<UpdateFooterLinkRequest> Links, int Order);

/// <summary>
/// Represents a record for UpdateFooterLinkRequest.
/// </summary>
public record UpdateFooterLinkRequest(long Id, string Label, string Href, int Order, bool OpenInNewTab = false);

/// <summary>
/// Represents a record for FooterComponentDetail.
/// </summary>
public record FooterComponentDetail(
    long Id,
    string Kind,
    int Order,
    string Placement,
    string? Title = null,
    string? Text = null,
    IReadOnlyList<FooterLinkDetail>? Links = null,
    IReadOnlyList<FooterSocialLinkDetail>? SocialLinks = null,
    string? EndpointKey = null,
    string? Placeholder = null,
    string? ButtonLabel = null,
    string? SearchAction = null,
    string? SizeToken = null)
{
        /// <summary>
    /// Gets or sets the Links.
    /// </summary>
public IReadOnlyList<FooterLinkDetail> Links { get; init; } = Links ?? [];
        /// <summary>
    /// Gets or sets the Social Links.
    /// </summary>
public IReadOnlyList<FooterSocialLinkDetail> SocialLinks { get; init; } = SocialLinks ?? [];
}

/// <summary>
/// Represents a record for FooterSocialLinkDetail.
/// </summary>
public record FooterSocialLinkDetail(string Platform, string Href);

/// <summary>
/// Represents a record for UpdateFooterComponentRequest.
/// </summary>
public record UpdateFooterComponentRequest(
    long Id,
    string Kind,
    int Order,
    string Placement,
    string? Title = null,
    string? Text = null,
    IReadOnlyList<UpdateFooterLinkRequest>? Links = null,
    IReadOnlyList<FooterSocialLinkDetail>? SocialLinks = null,
    string? EndpointKey = null,
    string? Placeholder = null,
    string? ButtonLabel = null,
    string? SearchAction = null,
    string? SizeToken = null)
{
        /// <summary>
    /// Gets or sets the Links.
    /// </summary>
public IReadOnlyList<UpdateFooterLinkRequest> Links { get; init; } = Links ?? [];
        /// <summary>
    /// Gets or sets the Social Links.
    /// </summary>
public IReadOnlyList<FooterSocialLinkDetail> SocialLinks { get; init; } = SocialLinks ?? [];
}

/// <summary>
/// Represents a record for FooterCanvasRowDetail.
/// </summary>
public record FooterCanvasRowDetail(
    long Id,
    int Order,
    string? Label,
    string DesktopDisplay = "Grid",
    string TabletDisplay = "Grid",
    string MobileDisplay = "Stack",
    IReadOnlyList<FooterCanvasColumnDetail>? Columns = null)
{
        /// <summary>
    /// Gets or sets the Columns.
    /// </summary>
public IReadOnlyList<FooterCanvasColumnDetail> Columns { get; init; } = Columns ?? [];
}

/// <summary>
/// Represents a record for FooterCanvasColumnDetail.
/// </summary>
public record FooterCanvasColumnDetail(
    long Id,
    int Order,
    int DesktopSpan,
    int TabletSpan,
    int MobileSpan,
    IReadOnlyList<FooterComponentDetail>? Blocks = null)
{
        /// <summary>
    /// Gets or sets the Blocks.
    /// </summary>
public IReadOnlyList<FooterComponentDetail> Blocks { get; init; } = Blocks ?? [];
}

/// <summary>
/// Represents a record for UpdateFooterCanvasRowRequest.
/// </summary>
public record UpdateFooterCanvasRowRequest(
    long Id,
    int Order,
    string? Label,
    string DesktopDisplay = "Grid",
    string TabletDisplay = "Grid",
    string MobileDisplay = "Stack",
    IReadOnlyList<UpdateFooterCanvasColumnRequest>? Columns = null)
{
        /// <summary>
    /// Gets or sets the Columns.
    /// </summary>
public IReadOnlyList<UpdateFooterCanvasColumnRequest> Columns { get; init; } = Columns ?? [];
}

/// <summary>
/// Represents a record for UpdateFooterCanvasColumnRequest.
/// </summary>
public record UpdateFooterCanvasColumnRequest(
    long Id,
    int Order,
    int DesktopSpan,
    int TabletSpan,
    int MobileSpan,
    IReadOnlyList<UpdateFooterComponentRequest>? Blocks = null)
{
        /// <summary>
    /// Gets or sets the Blocks.
    /// </summary>
public IReadOnlyList<UpdateFooterComponentRequest> Blocks { get; init; } = Blocks ?? [];
}
