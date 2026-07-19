namespace Aero.Cms.Abstractions.Enums;

/// <summary>
/// Defines how a previously published URL is handled when a page route changes.
/// </summary>
public enum PreviousPathBehavior
{
    /// <summary>
    /// Preserve every affected previously published URL as a permanent redirect.
    /// </summary>
    CreatePermanentRedirect,

    /// <summary>
    /// Change the route without preserving the affected previously published URLs.
    /// </summary>
    Discard
}
