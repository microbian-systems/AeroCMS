using Aero.Cms.Hosting.Defaults;
using Aero.Cms.Web.Bootstrap;

var builder = WebApplication.CreateBuilder(args);

await builder
    .AddAeroCms(AeroCmsDefaultCatalog.Catalog)
    .WithSetupSettingsDirectory(builder.Environment.ContentRootPath)
    .RegisterHostAsync<Program>();

var app = builder.Build();

app.UseAeroCmsRouting();
app.UseAeroCmsSiteAndLocalization();
app.UseAuthentication();
app.UseRateLimiter();
app.UseAuthorization();
app.UseAeroCmsRequestPipeline();
app.UseAntiforgery();

app.MapGet("/consumer/health", () => Results.Ok("C# consumer is running."));
app.MapAeroCms();
app.UseAeroCmsTerminalPipeline();

app.Run();
