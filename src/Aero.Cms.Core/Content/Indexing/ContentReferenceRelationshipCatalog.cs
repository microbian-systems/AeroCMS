using System.Security.Cryptography;
using System.Text;
using Aero.Cms.Abstractions.Content.Views;
using Aero.Cms.Core.Content.Views;
using AeroDB.Sable;

namespace Aero.Cms.Core.Content.Indexing;

/// <summary>
/// Lists native relationships declared by content fields. It is deliberately
/// separate from physical schema discovery: a declaration describes intended
/// semantics, while INFO evidence describes the current database shape.
/// </summary>
public interface IContentDeclaredRelationshipCatalog
{
    Task<IReadOnlyList<ContentRelationshipDefinition>> ListAsync(
        ContentViewScope scope,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Resolves content-field declarations through exactly one code-owned
/// materializer and emits immutable, fingerprinted relationship metadata.
/// </summary>
public sealed class ContentReferenceRelationshipCatalog(
    IDocumentSession session,
    IEnumerable<IContentReferenceRelationshipMaterializer> materializers)
    : IContentDeclaredRelationshipCatalog
{
    private readonly IReadOnlyList<IContentReferenceRelationshipMaterializer> _materializers =
        materializers.ToArray();

    public async Task<IReadOnlyList<ContentRelationshipDefinition>> ListAsync(
        ContentViewScope scope,
        CancellationToken cancellationToken = default)
    {
        if (!scope.IsValid)
        {
            return [];
        }

        var contentTypes = await session.Query<ContentTypeDocument>()
            .Where(candidate => candidate.SiteId == scope.SiteId)
            .ToListAsync(cancellationToken);
        var relationships = new List<ContentRelationshipDefinition>();
        foreach (var contentType in contentTypes.OrderBy(candidate => candidate.Alias, StringComparer.Ordinal))
        {
            foreach (var field in contentType.Fields.OrderBy(candidate => candidate.Name, StringComparer.Ordinal))
            {
                if (!ContentReferenceRelationshipDeclaration.TryCreate(contentType.Alias, field, out var declaration)
                    || declaration is null)
                {
                    continue;
                }

                var handlers = _materializers.Where(candidate => candidate.CanHandle(declaration)).ToArray();
                if (handlers.Length != 1)
                {
                    throw new InvalidOperationException(
                        $"Relationship '{declaration.Alias}' requires exactly one registered native materializer; found {handlers.Length}.");
                }

                var described = await handlers[0].DescribeAsync(
                    session,
                    scope,
                    declaration,
                    cancellationToken);
                if (described is null)
                {
                    throw new InvalidOperationException(
                        $"Relationship '{declaration.Alias}' could not describe its native representation.");
                }

                relationships.Add(FinalizeDescriptor(scope, declaration, described));
            }
        }

        var duplicate = relationships
            .GroupBy(candidate => candidate.Alias, StringComparer.Ordinal)
            .FirstOrDefault(group => group.Count() > 1);
        if (duplicate is not null)
        {
            throw new InvalidOperationException(
                $"Relationship alias '{duplicate.Key}' is declared more than once in this site.");
        }

        return relationships;
    }

    private static ContentRelationshipDefinition FinalizeDescriptor(
        ContentViewScope scope,
        ContentReferenceRelationshipDeclaration declaration,
        ContentRelationshipDefinition described)
    {
        if (described.Scope != scope
            || !string.Equals(described.Alias, declaration.Alias, StringComparison.Ordinal)
            || !string.Equals(described.SourceShapeAlias, declaration.SourceContentTypeAlias, StringComparison.OrdinalIgnoreCase)
            || described.OwnershipState != ContentRelationshipOwnershipState.Derived
            || string.IsNullOrWhiteSpace(described.SourceTable)
            || string.IsNullOrWhiteSpace(described.TargetTable))
        {
            throw new InvalidOperationException(
                $"Relationship '{declaration.Alias}' returned an invalid or cross-scope native descriptor.");
        }

        var fingerprint = ContentRelationshipDdlLifecycle.CreateFingerprint(
            ContentRelationshipDdlLifecycle.CreateStatements(described));
        var identity = string.Join(':',
            scope.TenantId,
            scope.SiteId,
            declaration.Alias,
            fingerprint,
            "content-field");
        var stable = BitConverter.ToInt64(SHA256.HashData(Encoding.UTF8.GetBytes(identity)), 0);
        var id = stable == 0
            ? -1
            : -Math.Abs(stable == long.MinValue ? long.MaxValue : stable);
        return described with
        {
            Id = id,
            SchemaFingerprint = fingerprint,
            OwnershipState = ContentRelationshipOwnershipState.Derived
        };
    }
}
