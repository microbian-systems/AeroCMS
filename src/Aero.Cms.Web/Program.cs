using Aero.Cms.Modules.Commerce.Client;
using Aero.Cms.Web.Areas.Api.V1;
using Aero.Cms.Web.Bootstrap;
using Aero.Cms.Web.Components;
using Aero.Cms.Web.Generated;

var builder = WebApplication.CreateBuilder(args);

await builder.AddAeroCmsAsync<Program>(args, GeneratedAeroCmsHostCatalog.Configure);
builder.Services.AddAeroCommerceClient();
builder.Services.AddPublicCmsQueryApi();

var app = builder.Build();

app.UseAeroCms();
app.MapPublicCmsQueryApi();
app.MapAeroCms<App>(components => components
    .AddAdditionalAssemblies(typeof(Aero.Cms.Web.Client._Imports).Assembly)
    .AddAdditionalAssemblies(typeof(Aero.Cms.Modules.Commerce.Client._Imports).Assembly));

await app.RunAsync();
