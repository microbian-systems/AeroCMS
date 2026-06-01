namespace Aero.Cms.Abstractions.Http.Clients;

using Aero.Core.Railway;
using Microsoft.Extensions.Logging;

public interface IFootersHttpClient
{
    Task<Result<IReadOnlyList<FooterSummary>, AeroError>> GetAllAsync(CancellationToken ct = default);
    Task<Result<FooterDetail, AeroError>> GetByIdAsync(long id, CancellationToken ct = default);
    Task<Result<IReadOnlyList<FooterDetail>, AeroError>> ListCultureVariantsAsync(long id, CancellationToken ct = default);
    Task<Result<FooterDetail, AeroError>> ForkToCultureAsync(long id, ForkFooterCultureRequest request, CancellationToken ct = default);
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
    IReadOnlyList<FooterLinkDetail> LegalLinks = null)
{
    public IReadOnlyList<FooterLinkDetail> LegalLinks { get; init; } = LegalLinks ?? [];
}

public sealed record ForkFooterCultureRequest(string Culture);

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
    IReadOnlyList<UpdateFooterLinkRequest>? LegalLinks = null);

public record FooterLinkGroupDetail(long Id, string Title, IReadOnlyList<FooterLinkDetail> Links, int Order);

public record FooterLinkDetail(long Id, string Label, string Href, int Order, bool OpenInNewTab = false);

public record CreateFooterLinkGroupRequest(string Title, IReadOnlyList<CreateFooterLinkRequest> Links, int Order);

public record CreateFooterLinkRequest(string Label, string Href, int Order, bool OpenInNewTab = false);

public record UpdateFooterLinkGroupRequest(long Id, string Title, IReadOnlyList<UpdateFooterLinkRequest> Links, int Order);

public record UpdateFooterLinkRequest(long Id, string Label, string Href, int Order, bool OpenInNewTab = false);
