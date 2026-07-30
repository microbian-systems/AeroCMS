using Aero.Cms.Core;
using Aero.Cms.Abstractions.Authentication;
using Aero.Cms.Core.Entities;
using Aero.Cms.Web.Core.Modules;
using Aero.Models.Entities;
using AeroDB.AspNetIdentity;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Aero.Modular;
using AeroDB.Sable;
using Microsoft.AspNetCore.Authorization;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.Extensions.Options;
using Aero.Cms.Modules.RateLimiting;

namespace Aero.Cms.Modules.Identity;

/// <summary>
/// Registers the ASP.NET Core Identity services used by AeroCMS with AeroDB-backed
/// user and role stores keyed by <see cref="long"/>.
/// </summary>
/// <remarks>
/// <para>
/// Registration includes Identity Core, roles, a sign-in manager, the default token
/// provider types, and the AeroDB user and role stores. The resulting stores require
/// an <c>IDocumentStore</c> to be registered by the host.
/// </para>
/// <para>
/// This module does not configure authentication schemes, authorization policies,
/// <see cref="IdentityOptions"/> (including claim mappings and unique-email policy),
/// cookies, data-protection key persistence or sharing, token lifetimes, HTTPS
/// enforcement, or endpoint mappings. Those concerns remain host responsibilities. In
/// particular, registering the default token providers does not by itself establish a
/// durable multi-instance key ring or guarantee email uniqueness.
/// </para>
/// </remarks>
[Module(nameof(IdentityModule))]
public class IdentityModule : AeroWebModule, IConfigureAeroDB
{
    /// <summary>
    /// Gets the stable module name used for discovery.
    /// </summary>
    public override string Name => nameof(IdentityModule);

    /// <summary>
    /// Gets the version shared by the current AeroCMS release.
    /// </summary>
    public override string Version => AeroConstants.Version;

    /// <summary>
    /// Gets the author shared by AeroCMS modules.
    /// </summary>
    public override string Author => AeroConstants.Author;

    /// <summary>
    /// Gets the rate-limiting infrastructure dependency.
    /// </summary>
    /// <remarks>
    /// This metadata does not remove the runtime requirement for the host to register
    /// AeroDB and its document store.
    /// </remarks>
    public override IReadOnlyList<string> Dependencies => [nameof(RateLimitingModule)];

    /// <summary>
    /// Gets the categories under which the module is presented.
    /// </summary>
    public override IReadOnlyList<string> Category => ["Identity", "Security"];

    /// <summary>
    /// Gets the discovery tags associated with authentication, users, and roles.
    /// </summary>
    public override IReadOnlyList<string> Tags => ["auth", "identity", "users", "roles"];

    /// <summary>
    /// Adds the long-keyed AeroCMS Identity managers and AeroDB stores to the service
    /// collection.
    /// </summary>
    /// <param name="services">The collection to which the Identity services are added.</param>
    /// <param name="config">
    /// The host configuration. The module reads its named authentication rate-limit profiles from it.
    /// </param>
    /// <param name="env">
    /// The host environment. This implementation does not read it.
    /// </param>
    /// <remarks>
    /// <para>
    /// The active store registrations are <c>AeroDBUserStore&lt;AeroUser, AeroRole, long&gt;</c>
    /// and <c>AeroDBRoleStore&lt;AeroRole, long&gt;</c>. The similarly named wrapper
    /// types in this assembly are not registered here.
    /// </para>
    /// <para>
    /// Store operations may open independent AeroDB sessions. Callers must not infer
    /// cross-operation atomicity, optimistic-concurrency handling, immediate session
    /// revocation, tenant isolation, or encrypted token storage from this registration.
    /// </para>
    /// </remarks>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="services"/> is <see langword="null"/>.
    /// </exception>
    public override void ConfigureServices(IServiceCollection services, IConfiguration? config = null, IHostEnvironment? env = null)
    {
        services.AddAeroFixedWindowRateLimitPolicy(
            config,
            ManagerRecoveryDefaults.RateLimitPolicy,
            "ManagerRecovery",
            new AeroFixedWindowRateLimitOptions
            {
                PermitLimit = 5,
                WindowSeconds = 900,
                QueueLimit = 0
            });
        services.AddAeroFixedWindowRateLimitPolicy(
            config,
            LocalExternalMemberAuthentication.LoginRateLimitPolicy,
            "ExternalMemberLogin",
            new AeroFixedWindowRateLimitOptions
            {
                PermitLimit = 5,
                WindowSeconds = 900,
                QueueLimit = 0
            });
        services.AddAeroFixedWindowRateLimitPolicy(
            config,
            LocalExternalMemberAuthentication.PasswordResetRateLimitPolicy,
            "ExternalMemberPasswordReset",
            new AeroFixedWindowRateLimitOptions
            {
                PermitLimit = 5,
                WindowSeconds = 900,
                QueueLimit = 0
            });
        services.AddAeroFixedWindowRateLimitPolicy(
            config,
            LocalExternalMemberAuthentication.ActivationRateLimitPolicy,
            "ExternalMemberActivation",
            new AeroFixedWindowRateLimitOptions
            {
                PermitLimit = 10,
                WindowSeconds = 900,
                QueueLimit = 0
            });

        services.AddIdentityCore<AeroUser>()
            .AddRoles<AeroRole>()
            .AddSignInManager()
            .AddDefaultTokenProviders()
            .AddAeroDBStores<AeroUser, AeroRole, long>();

        services.AddScoped<ICurrentPrincipal, CurrentPrincipal>();
        services.AddScoped<ExternalMemberCookieValidator>();
        services.AddScoped<ManagerFederationCookieValidator>();
        services.AddScoped<IAuthorizationHandler, ExternalMemberSiteAuthorizationHandler>();
        services.TryAddSingleton(TimeProvider.System);
        services.AddScoped<IValidator<CreateExternalMemberInvitationRequest>, CreateExternalMemberInvitationRequestValidator>();
        services.AddScoped<IValidator<BeginExternalMemberSignInRequest>, BeginExternalMemberSignInRequestValidator>();
        services.AddScoped<IValidator<CompleteExternalMemberSignInRequest>, CompleteExternalMemberSignInRequestValidator>();
        services.AddScoped<IExternalMemberIssuanceService, ExternalMemberIssuanceService>();
        services.AddScoped<IValidator<CreateLocalExternalMemberInvitationRequest>, CreateLocalExternalMemberInvitationRequestValidator>();
        services.AddScoped<IValidator<ActivateLocalExternalMemberInvitationRequest>, ActivateLocalExternalMemberInvitationRequestValidator>();
        services.AddScoped<IValidator<LoginLocalExternalMemberRequest>, LoginLocalExternalMemberRequestValidator>();
        services.AddScoped<IValidator<ResetLocalExternalMemberPasswordRequest>, ResetLocalExternalMemberPasswordRequestValidator>();
        services.AddScoped<IValidator<IssueLocalExternalMemberPasswordResetRequest>, IssueLocalExternalMemberPasswordResetRequestValidator>();
        services.AddSingleton<IPasswordHasher<ExternalMemberLocalCredential>, PasswordHasher<ExternalMemberLocalCredential>>();
        services.AddSingleton<LocalExternalMemberPasswordSentinel>();
        services.AddScoped<ILocalExternalMemberAuthenticationService, LocalExternalMemberAuthenticationService>();
        services.AddSingleton<ManagerLocalPasswordResetRateLimiter>();
        services.AddSingleton<ExternalMemberProviderBeginRateLimiter>();
        services.AddSingleton<ManagerAuthenticationRateLimiter>();
        var developmentExternalSecretsEnabled = env?.IsDevelopment() == true &&
            config?.GetValue<bool>(DevelopmentExternalProviderSecretSource.EnabledConfigurationKey) == true;
        if (developmentExternalSecretsEnabled)
            services.TryAddSingleton<IExternalProviderSecretSource, DevelopmentExternalProviderSecretSource>();
        else
            services.TryAddSingleton<IExternalProviderSecretSource, UnavailableExternalProviderSecretSource>();
        services.AddScoped<IExternalIdentityManagerScopeResolver, ExternalIdentityManagerScopeResolver>();
        services.AddScoped<IValidator<ConfigureExternalIdentityAuthorityRequest>, ConfigureExternalIdentityAuthorityRequestValidator>();
        services.AddScoped<IExternalIdentityAuthorityService, ExternalIdentityAuthorityService>();
        services.AddScoped<IExternalMemberProviderStrategyFactory, ExternalMemberProviderStrategyFactory>();
        services.AddScoped<IExternalMemberAuthenticationCoordinator, ExternalMemberAuthenticationCoordinator>();
        services.AddScoped<IExternalMemberSessionRevocationService, ExternalMemberSessionRevocationService>();
        services.AddScoped<ExternalMemberCookieIssuer>();
        services.AddScoped<IManagerRecoveryAuthenticationService, ManagerRecoveryAuthenticationService>();
        services.AddScoped<IValidator<ConfigureManagerIdentityAuthorityRequest>, ConfigureManagerIdentityAuthorityRequestValidator>();
        services.AddScoped<IManagerIdentityAuthorityService, ManagerIdentityAuthorityService>();
        services.AddScoped<IManagerFederationStateService, ManagerFederationStateService>();
        services.AddScoped<IManagerFederationLinkService, ManagerFederationLinkService>();
        services.AddScoped<IManagerIdentityProviderStrategyFactory, ManagerIdentityProviderStrategyFactory>();
        services.AddScoped<IManagerFederationCoordinator, ManagerFederationCoordinator>();
        services.TryAddScoped<IManagerAuthenticationModeResolver, UnavailableManagerAuthenticationModeResolver>();
        services.TryAddEnumerable(ServiceDescriptor.Singleton<
            IPostConfigureOptions<CookieAuthenticationOptions>,
            ManagerApiCookieRedirectPostConfigureOptions>());

        var developmentSecretsEnabled = env?.IsDevelopment() == true &&
            config?.GetValue<bool>(DevelopmentManagerProviderSecretSource.EnabledConfigurationKey) == true;
        if (developmentSecretsEnabled)
            services.TryAddSingleton<IManagerProviderSecretSource, DevelopmentManagerProviderSecretSource>();
        else
            services.TryAddSingleton<IManagerProviderSecretSource, UnavailableManagerProviderSecretSource>();
    }

    /// <summary>Configures local external-member documents and their lookup constraints.</summary>
    public void Configure(AeroDB.Sable.StoreOptions opts)
    {
        var members = opts.Schema.For<ExternalMember>()
            .TableName(Schemas.Tables.ExternalMembers);
        members.UseOptimisticConcurrency = true;
        members.Index(member => member.IsActive);

        var sessions = opts.Schema.For<ExternalMemberSession>()
            .TableName(Schemas.Tables.ExternalMemberSessions);
        sessions.UseOptimisticConcurrency = true;
        sessions.Index(session => session.ExternalMemberId);
        sessions.Index(session => new { session.TenantId, session.SiteId });
        sessions.Index(session => session.ExternalIdentityLinkId);
        sessions.Index(session => session.ExpiresAt);

        var assignments = opts.Schema.For<ExternalMemberSiteAssignment>()
            .TableName(Schemas.Tables.ExternalMemberSiteAssignments);
        assignments.UseOptimisticConcurrency = true;
        assignments
            .UniqueIndex(assignment => new { assignment.ExternalMemberId, assignment.SiteId });
        assignments.Index(assignment => assignment.TenantId);

        var links = opts.Schema.For<ExternalIdentityLink>()
            .TableName(Schemas.Tables.ExternalIdentityLinks);
        links.UseOptimisticConcurrency = true;
        links.UniqueIndex(link => link.IdentityKey);
        links.Index(link => link.ExternalMemberId);

        var bindings = opts.Schema.For<ExternalOrganizationBinding>()
            .TableName(Schemas.Tables.ExternalOrganizationBindings);
        bindings.UseOptimisticConcurrency = true;
        bindings.UniqueIndex(binding => binding.TenantId);
        bindings.UniqueIndex(binding => binding.BindingKey);


        var invitations = opts.Schema.For<ExternalMemberInvitation>()
            .TableName(Schemas.Tables.ExternalMemberInvitations);
        invitations.UseOptimisticConcurrency = true;
        invitations.UniqueIndex(invitation => invitation.TokenDigest);
        invitations.Index(invitation => new { invitation.TenantId, invitation.SiteId });

        var localAuthorities = opts.Schema.For<ExternalMemberLocalAuthority>()
            .TableName(Schemas.Tables.ExternalMemberLocalAuthorities);
        localAuthorities.UseOptimisticConcurrency = true;
        localAuthorities.UniqueIndex(authority => authority.TenantId);

        var localCredentials = opts.Schema.For<ExternalMemberLocalCredential>()
            .TableName(Schemas.Tables.ExternalMemberLocalCredentials);
        localCredentials.UseOptimisticConcurrency = true;
        localCredentials.UniqueIndex(credential => new { credential.TenantId, credential.NormalizedEmail });
        localCredentials.UniqueIndex(credential => new { credential.TenantId, credential.ExternalMemberId });

        var passwordResets = opts.Schema.For<ExternalMemberPasswordReset>()
            .TableName(Schemas.Tables.ExternalMemberPasswordResets);
        passwordResets.UseOptimisticConcurrency = true;
        passwordResets.UniqueIndex(reset => reset.TokenDigest);
        passwordResets.Index(reset => new { reset.TenantId, reset.CredentialId });
        passwordResets.Index(reset => reset.ExpiresAt);

        var states = opts.Schema.For<ExternalAuthenticationState>()
            .TableName(Schemas.Tables.ExternalAuthenticationStates);
        states.UseOptimisticConcurrency = true;
        states.UniqueIndex(state => state.SecretDigest);
        states.Index(state => state.ExpiresAt);

        var recoveryAudits = opts.Schema.For<ManagerRecoverySecurityAudit>()
            .TableName(Schemas.Tables.ManagerRecoverySecurityAudits);
        recoveryAudits.Index(audit => audit.AttemptedAtUtc);
        recoveryAudits.Index(audit => audit.RecoveryAdministratorUserId);

        var managerAuthorities = opts.Schema.For<ManagerIdentityAuthorityBinding>()
            .TableName(Schemas.Tables.ManagerIdentityAuthorityBindings);
        managerAuthorities.UseOptimisticConcurrency = true;
        managerAuthorities.UniqueIndex(binding => binding.SingletonKey);
        managerAuthorities.UniqueIndex(binding => binding.BindingKey);

        var managerLinkIntents = opts.Schema.For<ManagerFederationLinkIntent>()
            .TableName(Schemas.Tables.ManagerFederationLinkIntents);
        managerLinkIntents.UseOptimisticConcurrency = true;
        managerLinkIntents.UniqueIndex(intent => intent.SecretDigest);
        managerLinkIntents.Index(intent => intent.ExpiresAt);

        var managerStates = opts.Schema.For<ManagerAuthenticationState>()
            .TableName(Schemas.Tables.ManagerAuthenticationStates);
        managerStates.UseOptimisticConcurrency = true;
        managerStates.UniqueIndex(state => state.SecretDigest);
        managerStates.Index(state => state.ExpiresAt);

        var managerSessions = opts.Schema.For<ManagerFederatedSession>()
            .TableName(Schemas.Tables.ManagerFederatedSessions);
        managerSessions.UseOptimisticConcurrency = true;
        managerSessions.Index(managerSession => managerSession.UserId);
        managerSessions.Index(managerSession => managerSession.ExpiresAt);
    }

    /// <summary>Applies the external-member schema through the service-aware configuration hook.</summary>
    public void Configure(IServiceProvider? services, AeroDB.Sable.StoreOptions opts) => Configure(opts);
}
