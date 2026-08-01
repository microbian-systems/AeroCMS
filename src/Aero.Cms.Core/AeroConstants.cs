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
/// Defines stable database schema and table identifiers shared by AeroCMS modules.
/// </summary>
public static class Schemas
{
    /// <summary>The AeroCMS database schema name.</summary>
    public const string Database = "aero";
    /// <summary>The conventional embedded-database user name.</summary>
    public const string EmbeddedUser = "aero";
    /// <summary>Defines physical table names and retained document aliases.</summary>
    public static class Tables
    {
        public const string A2ASettings = "a2a_settings";
        public const string AiConversationMessages = "ai_conversation_messages";
        public const string AiConversations = "ai_conversations";
        public const string AiKnowledgeChunks = "ai_knowledge_chunks";
        public const string AiManagerDocumentationChunks = "ai_manager_documentation_chunks";
        public const string AiManagerDocumentationCorpusStates = "ai_manager_documentation_corpus_states";
        public const string AiMemories = "ai_memories";
        public const string Aliases = "aliases";
        public const string ApiAccounts = "api_accounts";
        public const string ApiKeys = "api_keys";
        public const string Banners = "banners";
        public const string Baskets = "baskets";
        public const string Blocks = "blocks";
        public const string Buyers = "buyers";
        public const string Categories = "categories";
        public const string CategoryTranslations = "category_translations";
        public const string ContentItemVersions = "content_item_versions";
        public const string ContentItems = "content_items";
        public const string ContentSearchFacets = "content_search_facets";
        public const string ContentSearchIndex = "content_search_index";
        public const string ContentSemanticIndex = "content_semantic_index";
        public const string ContentSlugs = "content_slugs";
        public const string ContentTypes = "content_types";
        public const string Docs = "docs";
        public const string ExternalAuthenticationStates = "external_authentication_states";
        public const string ExternalIdentityLinks = "external_identity_links";
        public const string ExternalMemberInvitations = "external_member_invitations";
        public const string ExternalMemberLocalAuthorities = "external_member_local_authorities";
        public const string ExternalMemberLocalCredentials = "external_member_local_credentials";
        public const string ExternalMemberPasswordResets = "external_member_password_resets";
        public const string ExternalMemberSessions = "external_member_sessions";
        public const string ExternalMemberSiteAssignments = "external_member_site_assignments";
        public const string ExternalMembers = "external_members";
        public const string ExternalOrganizationBindings = "external_organization_bindings";
        public const string Files = "files";
        public const string Footers = "footers";
        public const string ManagerAuthenticationStates = "manager_authentication_states";
        public const string ManagerFederatedSessions = "manager_federated_sessions";
        public const string ManagerFederationLinkIntents = "manager_federation_link_intents";
        public const string ManagerIdentityAuthorityBindings = "manager_identity_authority_bindings";
        public const string ManagerRecoverySecurityAudits = "manager_recovery_security_audits";
        public const string Media = "media";
        public const string MediaAssets = "media_assets";
        public const string Modules = "modules";
        public const string NavigationMenus = "navigation_menus";
        public const string Orders = "orders";
        public const string OrderItems = "order_items";
        public const string Pages = "pages";
        public const string PageSourceVersions = "page_source_versions";
        public const string PaymentAttempts = "payment_attempts";
        public const string PaymentWebhookReceipts = "payment_webhook_receipts";
        public const string Posts = "posts";
        public const string ProductListings = "product_listings";
        public const string Products = "products";
        public const string Roles = "roles";
        public const string Series = "series";
        public const string SeriesTranslations = "series_translations";
        public const string Settings = "settings";
        public const string SetupState = "setup_state";
        public const string SiteFooterSettings = "site_footer_settings";
        public const string SiteHosts = "site_hosts";
        public const string SiteNavigationSettings = "site_navigation_settings";
        public const string Sites = "sites";
        public const string SitePerms = "site_perms";
        public const string SlugRegistry = "slugs";
        public const string Subscriptions = "subscriptions";
        public const string SubscriptionCycles = "subscription_cycles";
        public const string SubscriptionWebhookReceipts = "subscription_webhook_receipts";
        public const string Tags = "tags";
        public const string TagTranslations = "tag_translations";
        public const string ThemeDefinitions = "theme_definitions";
        public const string ThemeVersions = "theme_versions";
        public const string SiteThemePublications = "site_theme_publications";
        public const string Tenants = "tenants";
        public const string Users = "users";
        public const string UserSiteAssignments = "user_site_assignments";
    }

}
