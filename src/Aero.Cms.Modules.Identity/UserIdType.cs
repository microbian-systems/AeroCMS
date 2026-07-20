namespace Aero.Cms.Modules.Identity;

/// <summary>
/// Lists historical string-oriented user identifier strategies retained by this
/// assembly.
/// </summary>
/// <remarks>
/// <para>
/// No current code in this module consumes this enum. Active AeroCMS Identity
/// registration uses <see cref="long"/> user and role keys, so the string and
/// document-key strategies named here are not compatible with that registration.
/// </para>
/// <para>
/// This legacy artifact should be excluded from the curated DocFX API reference until
/// it is removed or redesigned for the current key model.
/// </para>
/// </remarks>
public enum UserIdType
{
    /// <summary>
    /// Identifies the historical behavior of retaining a caller-supplied identifier or
    /// deferring identifier generation.
    /// </summary>
    None,

    /// <summary>
    /// Identifies the historical strategy of embedding an email address in a string
    /// document identifier.
    /// </summary>
    Email,

    /// <summary>
    /// Identifies the historical strategy of embedding a user name in a string document
    /// identifier.
    /// </summary>
    UserName,

    /// <summary>
    /// Identifies the historical strategy of requesting a server-generated string
    /// document identifier.
    /// </summary>
    ServerGenerated,

    /// <summary>
    /// Identifies the historical collection-and-number-tag string identifier strategy.
    /// </summary>
    NumberTag,

    /// <summary>
    /// Identifies the historical consecutive string identifier strategy.
    /// </summary>
    Consecutive
}
