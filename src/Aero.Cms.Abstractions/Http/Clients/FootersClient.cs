namespace Aero.Cms.Abstractions.Http.Clients;

using Aero.Core.Railway;
using Microsoft.Extensions.Logging;

public interface IFootersHttpClient
{
    Task<Result<IReadOnlyList<FooterSummary>, AeroError>> GetAllAsync(CancellationToken ct = default);
    Task<Result<FooterDetail, AeroError>> GetByIdAsync(long id, CancellationToken ct = default);
    Task<Result<IReadOnlyList<FooterDetail>, AeroError>> ListCultureVariantsAsync(long id, CancellationToken ct = default);
    Task<Result<FooterDetail, AeroError>> ForkToCultureAsync(long id, ForkFooterCultureRequest request, CancellationToken ct = default);
    Task<Result<AiTranslateFooterResult, AeroError>> TranslateWithAiAsync(long id, AiTranslateFooterRequest request, CancellationToken ct = default);
    Task<Result<FooterDetail, AeroError>> CreateAsync(CreateFooterRequest request, CancellationToken ct = default);
    Task<Result<FooterDetail, AeroError>> UpdateAsync(long id, UpdateFooterRequest request, CancellationToken ct = default);
    Task<Result<FooterDetail, AeroError>> SaveDraftAsync(long id, UpdateFooterRequest request, long expectedVersion, CancellationToken ct = default);
    Task<Result<FooterDetail, AeroError>> PublishAsync(long id, long expectedVersion, CancellationToken ct = default);
    Task<Result<bool, AeroError>> SetDefaultAsync(long id, CancellationToken ct = default);
    Task<Result<bool, AeroError>> DeleteAsync(long id, CancellationToken ct = default);
}

public sealed class FootersHttpClient(HttpClient httpClient, ILogger<FootersHttpClient> logger)
    : AeroCmsClientBase(httpClient, logger), IFootersHttpClient
{
    public override string Path => "admin/footers";

    public Task<Result<IReadOnlyList<FooterSummary>, AeroError>> GetAllAsync(CancellationToken ct = default)
        => GetAsync<IReadOnlyList<FooterSummary>>(string.Empty, ct);

    public Task<Result<FooterDetail, AeroError>> GetByIdAsync(long id, CancellationToken ct = default)
        => GetAsync<FooterDetail>($"details/{id}", ct);

    public Task<Result<IReadOnlyList<FooterDetail>, AeroError>> ListCultureVariantsAsync(long id, CancellationToken ct = default)
        => GetAsync<IReadOnlyList<FooterDetail>>($"{id}/translations", ct);

    public Task<Result<FooterDetail, AeroError>> ForkToCultureAsync(long id, ForkFooterCultureRequest request, CancellationToken ct = default)
        => PostAsync<ForkFooterCultureRequest, FooterDetail>($"{id}/translations", request, ct);

    public Task<Result<AiTranslateFooterResult, AeroError>> TranslateWithAiAsync(long id, AiTranslateFooterRequest request, CancellationToken ct = default)
        => PostAsync<AiTranslateFooterRequest, AiTranslateFooterResult>($"{id}/ai-translate", request, ct);

    public Task<Result<FooterDetail, AeroError>> CreateAsync(CreateFooterRequest request, CancellationToken ct = default)
        => PostAsync<CreateFooterRequest, FooterDetail>(string.Empty, request, ct);

    public Task<Result<FooterDetail, AeroError>> UpdateAsync(long id, UpdateFooterRequest request, CancellationToken ct = default)
        => PutAsync<UpdateFooterRequest, FooterDetail>(id.ToString(), request, ct);

    public Task<Result<FooterDetail, AeroError>> SaveDraftAsync(
        long id,
        UpdateFooterRequest request,
        long expectedVersion,
        CancellationToken ct = default)
        => PutAsync<UpdateFooterRequest, FooterDetail>($"{id}/draft?expectedVersion={expectedVersion}", request, ct);

    public Task<Result<FooterDetail, AeroError>> PublishAsync(long id, long expectedVersion, CancellationToken ct = default)
        => PutAsync<object, FooterDetail>($"{id}/publish?expectedVersion={expectedVersion}", new { }, ct);

    public Task<Result<bool, AeroError>> SetDefaultAsync(long id, CancellationToken ct = default)
        => MapBoolResult(base.PutAsync(CreateUri($"{id}/default"), new { }, ct));

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
    public IReadOnlyList<FooterLinkDetail> LegalLinks { get; init; } = LegalLinks ?? [];
    public IReadOnlyList<FooterComponentDetail> Components { get; init; } = Components ?? [];
    public IReadOnlyList<FooterCanvasRowDetail> Rows { get; init; } = Rows ?? [];
}

public sealed record ForkFooterCultureRequest(string Culture);

public sealed record AiTranslateFooterRequest(
    IReadOnlyList<AiTranslateFooterCultureRequest> Targets,
    string? ProviderId = null,
    bool OverwriteExisting = false);

public sealed record AiTranslateFooterCultureRequest(string Culture);

public sealed record AiTranslateFooterResult(
    IReadOnlyList<AiTranslateFooterCultureResult> Results);

public sealed record AiTranslateFooterCultureResult(
    string Culture,
    bool Succeeded,
    FooterDetail? Footer,
    IReadOnlyList<string> Warnings,
    string? Error);

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
    public IReadOnlyList<UpdateFooterComponentRequest> Components { get; init; } = Components ?? [];
    public IReadOnlyList<UpdateFooterCanvasRowRequest> Rows { get; init; } = Rows ?? [];
}

public record FooterLinkGroupDetail(long Id, string Title, IReadOnlyList<FooterLinkDetail> Links, int Order);

public record FooterLinkDetail(long Id, string Label, string Href, int Order, bool OpenInNewTab = false);

public record CreateFooterLinkGroupRequest(string Title, IReadOnlyList<CreateFooterLinkRequest> Links, int Order);

public record CreateFooterLinkRequest(string Label, string Href, int Order, bool OpenInNewTab = false);

public record UpdateFooterLinkGroupRequest(long Id, string Title, IReadOnlyList<UpdateFooterLinkRequest> Links, int Order);

public record UpdateFooterLinkRequest(long Id, string Label, string Href, int Order, bool OpenInNewTab = false);

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
    public IReadOnlyList<FooterLinkDetail> Links { get; init; } = Links ?? [];
    public IReadOnlyList<FooterSocialLinkDetail> SocialLinks { get; init; } = SocialLinks ?? [];
}

public record FooterSocialLinkDetail(string Platform, string Href);

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
    public IReadOnlyList<UpdateFooterLinkRequest> Links { get; init; } = Links ?? [];
    public IReadOnlyList<FooterSocialLinkDetail> SocialLinks { get; init; } = SocialLinks ?? [];
}

public record FooterCanvasRowDetail(
    long Id,
    int Order,
    string? Label,
    string DesktopDisplay = "Grid",
    string TabletDisplay = "Grid",
    string MobileDisplay = "Stack",
    IReadOnlyList<FooterCanvasColumnDetail>? Columns = null)
{
    public IReadOnlyList<FooterCanvasColumnDetail> Columns { get; init; } = Columns ?? [];
}

public record FooterCanvasColumnDetail(
    long Id,
    int Order,
    int DesktopSpan,
    int TabletSpan,
    int MobileSpan,
    IReadOnlyList<FooterComponentDetail>? Blocks = null)
{
    public IReadOnlyList<FooterComponentDetail> Blocks { get; init; } = Blocks ?? [];
}

public record UpdateFooterCanvasRowRequest(
    long Id,
    int Order,
    string? Label,
    string DesktopDisplay = "Grid",
    string TabletDisplay = "Grid",
    string MobileDisplay = "Stack",
    IReadOnlyList<UpdateFooterCanvasColumnRequest>? Columns = null)
{
    public IReadOnlyList<UpdateFooterCanvasColumnRequest> Columns { get; init; } = Columns ?? [];
}

public record UpdateFooterCanvasColumnRequest(
    long Id,
    int Order,
    int DesktopSpan,
    int TabletSpan,
    int MobileSpan,
    IReadOnlyList<UpdateFooterComponentRequest>? Blocks = null)
{
    public IReadOnlyList<UpdateFooterComponentRequest> Blocks { get; init; } = Blocks ?? [];
}
