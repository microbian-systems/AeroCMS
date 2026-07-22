# Aero.Cms.Modules.EntraExternalId

This class-library module implements the Microsoft Entra External ID provider strategy for external storefront members.

It registers `EntraExternalIdProviderStrategy`, a direct OpenID Connect authorization-code/PKCE adapter. The adapter obtains its request-scoped client ID and client secret only through the provider-neutral `IExternalProviderSecretSource` bundle supplied by the Identity module. It does not register an ASP.NET authentication scheme, create a provider cookie, persist provider tokens, or read credentials from configuration or environment variables.

The module uses bounded, redirect-disabled HTTP clients, OpenID Connect discovery through `ConfigurationManager<OpenIdConnectConfiguration>`, RS256 ID-token validation, and time-limited Data Protection correlation. Tenant bindings and public login/callback/logout endpoints remain owned by `Aero.Cms.Modules.Identity`; this module has no database model, Marten configuration, or API endpoints.

Live tenant validation requires an Entra External ID tenant/app registration and a production Aero.Vault credential source. Offline tests use deterministic discovery, HTTP handlers, and signing keys.
