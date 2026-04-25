using Aero.Cms.Abstractions.Services;
using Aero.Cms.Core.Entities;
using Marten;
using System.Security.Cryptography;
using System.Text;

namespace Aero.Cms.Modules.Security;

/// <summary>
/// Implementation of IApiKeyService using Marten for persistence.
/// </summary>
public sealed class ApiKeyService : IApiKeyService
{
    private readonly IQuerySession _session;

    public ApiKeyService(IQuerySession session)
    {
        _session = session;
    }

    public async Task<long?> ValidateAsync(string apiKey, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(apiKey)) return null;

        // Hash the incoming key to compare with stored hash
        // The generator uses SHA256.HashData(Encoding.UTF8.GetBytes(rawApiKey))
        var hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(apiKey));
        var hash = Convert.ToHexString(hashBytes).ToLowerInvariant();

        var document = await _session.Query<ApiKeyDocument>()
            .FirstOrDefaultAsync(x => x.SecretHash == hash, cancellationToken);

        if (document != null && document.IsActive)
        {
            return document.UserId;
        }

        return null;
    }
}
