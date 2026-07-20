namespace Aero.Cms.Core;

/// <summary>
/// Defines shared AeroCMS product and module metadata identifiers.
/// </summary>
public static class AeroConstants
{
        /// <summary>The product display name.</summary>
public const string AppName = "AeroCMS";
        /// <summary>The author text used by AeroCMS module metadata.</summary>
public const string Author = "AeroCMS Team";
        /// <summary>The abbreviated author name.</summary>
public const string AuthorShortName = "AeroCMS";
        /// <summary>The product copyright-year text.</summary>
public const string Copyright = "2020 - 2026";
        /// <summary>The conventional AeroCMS connection-string identifier.</summary>
public const string ConnString = "aero";
        /// <summary>The version string exposed by current AeroCMS modules.</summary>
public const string Version = "0.0.5.-alpha";
}

/// <summary>
/// Defines legacy database schema and document-alias identifiers.
/// </summary>
/// <remarks>
/// These names are retained for legacy adapters and do not configure the current Sable
/// document model.
/// </remarks>
public static class Schemas
{
        /// <summary>The legacy AeroCMS database schema name.</summary>
public const string Database = "aero";
        /// <summary>The conventional legacy embedded-database user name.</summary>
public const string EmbeddedUser = "aero";
        /// <summary>Defines legacy table and document aliases.</summary>
public static class Tables
    {
                /// <summary>The legacy alias-record identifier.</summary>
public const string Aliases = "aliases";
                /// <summary>The legacy block-record identifier; it is not a current page-composition API.</summary>
public const string Blocks = "blocks";
                /// <summary>The legacy category-record identifier.</summary>
public const string Categories = "categories";
                /// <summary>The legacy content-item record identifier.</summary>
public const string ContentItems = "content_items";
                /// <summary>The legacy content-item-version record identifier.</summary>
public const string ContentItemVersions = "content_items_versions";
                /// <summary>The legacy content-type record identifier.</summary>
public const string ContentTypes = "content_types";
                /// <summary>The legacy documentation-record identifier.</summary>
public const string Docs = "docs";
                /// <summary>The legacy media-record identifier.</summary>
public const string Media = "media";
                /// <summary>The legacy module-record identifier.</summary>
public const string Modules = "modules";
                /// <summary>The legacy page-record identifier.</summary>
public const string Pages = "pages";
                /// <summary>The legacy post-record identifier.</summary>
public const string Posts = "posts";
                /// <summary>The legacy setting-record identifier.</summary>
public const string Settings = "settings";
                /// <summary>The legacy site-record identifier.</summary>
public const string Sites = "sites";
                /// <summary>The legacy site-host record identifier.</summary>
public const string SiteHosts = "hosts";
                /// <summary>The legacy site-permission record identifier.</summary>
public const string SitePerms = "site_perms";
                /// <summary>The legacy slug-registry record identifier.</summary>
public const string SlugRegistry = "slugs";
                /// <summary>The legacy tag-record identifier.</summary>
public const string Tags = "tags";
                /// <summary>The legacy tenant-record identifier.</summary>
public const string Tenants = "tenants";
                /// <summary>The legacy user-record identifier.</summary>
public const string Users = "users";
                /// <summary>The legacy commerce-basket record identifier.</summary>
public const string Baskets = "baskets";
                /// <summary>The legacy commerce-buyer record identifier.</summary>
public const string Buyers = "buyers";
                /// <summary>The legacy commerce-order record identifier.</summary>
public const string Orders = "orders";
                /// <summary>The legacy commerce-order-item record identifier.</summary>
public const string OrderItems = "order_items";
                /// <summary>The legacy commerce-product record identifier.</summary>
public const string Products = "products";
    }

}
