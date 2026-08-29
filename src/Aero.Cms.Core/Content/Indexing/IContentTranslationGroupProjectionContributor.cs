using System.Text.Json;
using Aero.Core;
using Aero.Core.Railway;
using AeroDB.Sable;

namespace Aero.Cms.Core.Content.Indexing;

/// <summary>Identifies the translation-group lifecycle change being staged.</summary>
public enum ContentTranslationGroupProjectionChange
{
    Upsert = 0,
    Delete = 1
}

/// <summary>
/// Server-owned snapshot supplied to host projection contributors while the
/// translation-group mutation is still pending in the same document session.
/// </summary>
public sealed record ContentTranslationGroupProjectionContext(
    long SiteId,
    string ContentTypeAlias,
    long TranslationGroupId,
    long SourceItemId,
    int Revision,
    IReadOnlyDictionary<string, JsonElement> SharedFields,
    ContentTranslationGroupProjectionChange Change)
{
    /// <summary>
    /// Tracks relationship-target barriers already staged during this one
    /// projection operation. It prevents multiple declared fields from queuing
    /// duplicate optimistic updates for the same target record.
    /// </summary>
    internal HashSet<long> StagedRelationshipTargetBarriers { get; } = [];
}

/// <summary>
/// Lets a consuming host stage a derived translation-group projection in the
/// caller's transaction without adding product-specific behavior to AeroCMS.
/// </summary>
/// <remarks>
/// Contributors must only queue operations on the supplied <c>session</c>. They
/// must not call <see cref="IDocumentSession.SaveChangesAsync"/> themselves.
/// </remarks>
public interface IContentTranslationGroupProjectionContributor
{
    Task<Result<NoneType, AeroError>> StageAsync(
        IDocumentSession session,
        ContentTranslationGroupProjectionContext context,
        CancellationToken cancellationToken = default);
}
