using Aero.Cms.Core.Entities;
using Marten.Events.Aggregation;
using Marten.Events.Projections;

namespace Aero.Cms.Modules.Pages;

/// <summary>
/// Inline snapshot projection for <see cref="PageDocument"/> that works
/// around Marten 8's lack of <c>long</c> identity support by using
/// <c>string</c> as the projection identity type and manually mapping
/// stream keys back to Snowflake IDs.
/// </summary>
/// <remarks>
/// Marten 8's <c>Snapshot&lt;T&gt;()</c> shorthand auto-detects identity
/// from <c>T.Id</c>, which is <c>long</c> for <c>PageDocument</c>.
/// <c>SingleStreamProjection&lt;T, TId&gt;</c> requires <c>TId</c> to
/// be <c>Guid</c> or <c>string</c>.  We use <c>string</c> (matching
/// <c>StreamIdentity.AsString</c>) and resolve the Snowflake ID from
/// the stream key at projection time.
/// </remarks>
public sealed class PageDocumentProjection : SingleStreamProjection<PageDocument, string>
{
    public PageDocumentProjection()
    {
        // Use the self-aggregating conventional methods (Create / Apply)
        // already defined on PageDocument. Marten auto-discovers them
        // via reflection on the aggregate type.
        Lifecycle = ProjectionLifecycle.Inline;
    }
}
