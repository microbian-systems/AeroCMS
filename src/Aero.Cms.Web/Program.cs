using Aero.Cms.Modules.Commerce.Client;
using Aero.Cms.Hosting.Defaults;
using Aero.Cms.Modules.Identity;
using Aero.Cms.Web.Bootstrap;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;

var builder = WebApplication.CreateBuilder(args);

// The standalone Aero CMS executable owns its authentication defaults. Embedded consumers keep
// their existing defaults because the public hosting facade deliberately preserves host policy.
builder.Services.AddAuthentication(authentication =>
{
    authentication.DefaultScheme = ManagerRecoveryDefaults.ManagerScheme;
    authentication.DefaultAuthenticateScheme = ManagerRecoveryDefaults.ManagerScheme;
    authentication.DefaultChallengeScheme = ManagerRecoveryDefaults.ManagerScheme;
    authentication.DefaultSignInScheme = IdentityConstants.ApplicationScheme;
});
builder.Services.AddAuthorization(authorization =>
{
    authorization.DefaultPolicy = new AuthorizationPolicyBuilder(ManagerRecoveryDefaults.ManagerScheme)
        .RequireAuthenticatedUser()
        .Build();
});

await builder
    .AddAeroCms(AeroCmsDefaultCatalog.Catalog)
    .WithSetupSettingsDirectory(builder.Environment.ContentRootPath)
    .RegisterHostAsync<Program>();
builder.Services.AddAeroCommerceClient();

var app = builder.Build();

app.UseAeroCms();
app.MapAeroCms();
app.UseAeroCmsTerminalPipeline();

await app.RunAsync();
