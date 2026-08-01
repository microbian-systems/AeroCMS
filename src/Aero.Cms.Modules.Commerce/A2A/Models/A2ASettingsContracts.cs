namespace Aero.Cms.Modules.Commerce.A2A.Models;

/// <summary>Manager-safe representation of a site's A2A availability.</summary>
public sealed record A2ASettingsResponse(bool IsEnabled);

/// <summary>Manager request for changing a site's A2A availability.</summary>
public sealed record UpdateA2ASettingsRequest(bool? IsEnabled);
