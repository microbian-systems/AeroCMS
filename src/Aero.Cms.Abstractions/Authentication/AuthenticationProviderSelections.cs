namespace Aero.Cms.Abstractions.Authentication;

/// <summary>
/// Defines the canonical authentication-provider values persisted by initial setup.
/// </summary>
public static class AuthenticationProviderSelections
{
    /// <summary>Defines CMS manager authentication-provider values.</summary>
    public static class Manager
    {
        /// <summary>ASP.NET Core Identity authenticates CMS managers locally.</summary>
        public const string Local = "local";

        /// <summary>Microsoft Entra Workforce authenticates CMS managers.</summary>
        public const string EntraWorkforce = "entra_workforce";

        /// <summary>WorkOS authenticates CMS managers.</summary>
        public const string WorkOs = "workos";

        /// <summary>Returns whether a value is an exact canonical manager provider.</summary>
        public static bool IsCanonical(string? value)
            => value is Local or EntraWorkforce or WorkOs;

        /// <summary>Returns whether the manager provider can currently be activated.</summary>
        public static bool IsAvailable(string? value)
            => value is Local or EntraWorkforce or WorkOs;
    }

    /// <summary>Defines storefront member authentication-provider values.</summary>
    public static class Member
    {
        /// <summary>Storefront member authentication is disabled.</summary>
        public const string Disabled = "disabled";

        /// <summary>AeroCMS authenticates storefront members locally.</summary>
        public const string Local = "local";

        /// <summary>Microsoft Entra External ID authenticates storefront members.</summary>
        public const string EntraExternalId = "entra_external_id";

        /// <summary>WorkOS authenticates storefront members.</summary>
        public const string WorkOs = "workos";

        /// <summary>Returns whether a value is an exact canonical member provider.</summary>
        public static bool IsCanonical(string? value)
            => value is Disabled or Local or EntraExternalId or WorkOs;

        /// <summary>Returns whether the member provider can currently be configured.</summary>
        public static bool IsAvailable(string? value)
            => value is Disabled or Local or EntraExternalId or WorkOs;
    }
}
