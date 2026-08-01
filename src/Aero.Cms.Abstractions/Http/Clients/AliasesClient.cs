using Aero.Cms.Abstractions.Requests;
using Microsoft.Extensions.Logging;

namespace Aero.Cms.Abstractions.Http.Clients;

/// <summary>
/// HTTP client interface for alias management.
/// NOTE: Backend API endpoints for aliases need to be implemented — see Aero.Cms.Modules.Aliases.
/// </summary>
public interface IAliasHttpClient
{
        /// <summary>
    /// Gets aliases for the site selected by the current request context.
    /// </summary>
Task<Result<IReadOnlyList<AliasViewModel>, AeroError>> GetAllAsync(CancellationToken ct = default);
        /// <summary>
    /// CreateAsync method.
    /// </summary>
Task<Result<AliasViewModel, AeroError>> CreateAsync(CreateAliasRequest request, CancellationToken ct = default);
        /// <summary>
    /// DeleteAsync method.
    /// </summary>
Task<Result<bool, AeroError>> DeleteAsync(long id, CancellationToken ct = default);
}

/// <summary>
/// HTTP client for alias management API.
/// </summary>
public class AliasesHttpClient(HttpClient httpClient, ILogger<AliasesHttpClient> logger)
    : AeroCmsClientBase(httpClient, logger), IAliasHttpClient
{
        /// <summary>
    /// Gets or sets the Path.
    /// </summary>
public override string Path => "admin/aliases";

        /// <summary>
    /// Gets aliases for the site selected by the current request context.
    /// </summary>
public Task<Result<IReadOnlyList<AliasViewModel>, AeroError>> GetAllAsync(CancellationToken ct = default)
        => GetAsync<IReadOnlyList<AliasViewModel>>(string.Empty, ct);

        /// <summary>
    /// CreateAsync method.
    /// </summary>
public Task<Result<AliasViewModel, AeroError>> CreateAsync(CreateAliasRequest request, CancellationToken ct = default)
        => PostAsync<CreateAliasRequest, AliasViewModel>(string.Empty, request, ct);

        /// <summary>
    /// DeleteAsync method.
    /// </summary>
public Task<Result<bool, AeroError>> DeleteAsync(long id, CancellationToken ct = default)
        => MapBoolResult(base.DeleteAsync(id.ToString(), ct));

    private static async Task<Result<bool, AeroError>> MapBoolResult(Task<Result<HttpResponseMessage, AeroError>> task)
    {
        var result = await task;
        return result switch
        {
            Result<HttpResponseMessage, AeroError>.Ok => new Result<bool, AeroError>.Ok(true),
            Result<HttpResponseMessage, AeroError>.Failure f => new Result<bool, AeroError>.Failure(f.Error),
            _ => new Result<bool, AeroError>.Failure(AeroError.CreateError("Unexpected result"))
        };
    }
}
