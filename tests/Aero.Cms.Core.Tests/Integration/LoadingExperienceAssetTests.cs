using System.Diagnostics;
using Aero.Cms.Shared.Services;
using Microsoft.JSInterop;
using NSubstitute;

namespace Aero.Cms.Core.Tests.Integration;

public sealed class LoadingExperienceAssetTests
{
    [Test]
    public async Task Manager_theme_defaults_to_light_when_no_preference_is_available()
    {
        var jsRuntime = Substitute.For<IJSRuntime>();
        var theme = new ManagerThemeService(jsRuntime);

        await Assert.That(theme.IsDarkMode).IsFalse();
        await Assert.That(theme.Theme).IsEqualTo("light");
    }

    [Test]
    public async Task Manager_boot_splash_is_branded_and_never_uses_a_forced_hide_timeout()
    {
        var root = RepositoryRoot();
        var app = await File.ReadAllTextAsync(Path.Combine(root, "src", "Aero.Cms.Web", "Components", "App.razor"));
        var script = await File.ReadAllTextAsync(Path.Combine(root, "src", "Aero.Cms.Shared", "wwwroot", "splash.js"));

        await Assert.That(app).Contains("/img/aero-logo-new.png");
        await Assert.That(app).Contains("@Assets[\"_content/Aero.Cms.Shared/splash.js\"]");
        await Assert.That(app).DoesNotContain("        startAeroApp();");
        await Assert.That(app.IndexOf("_framework/blazor.web.js", StringComparison.Ordinal))
            .IsLessThan(app.IndexOf("_content/Aero.Cms.Shared/splash.js", StringComparison.Ordinal));
        await Assert.That(app).DoesNotContain("setTimeout(hideAppSplash, 20000)");
        await Assert.That(script).Contains("window.__aeroAppStartPromise");
        await Assert.That(script).Contains("window.startAeroApp().catch");
        await Assert.That(script).Contains("Blazor.start().then");
        await Assert.That(script).Contains("Aero Manager couldn\\'t start");
    }

    [Test]
    public async Task Manager_splash_defines_and_starts_the_app_once_in_the_same_script()
    {
        var root = RepositoryRoot();
        var harness = Path.Combine(root, "tests", "Aero.Cms.Core.Tests", "JavaScript", "manager-splash.test.mjs");
        var script = Path.Combine(root, "src", "Aero.Cms.Shared", "wwwroot", "splash.js");

        await RunNodeHarnessAsync(harness, script);
    }

    [Test]
    public async Task Setup_handoff_waits_for_terminal_bootstrap_then_probes_the_readiness_gated_homepage()
    {
        var root = RepositoryRoot();
        var markup = await File.ReadAllTextAsync(Path.Combine(root, "src", "Aero.Cms.Modules.Setup", "Areas", "Setup", "Pages", "SetupRoot.razor"));
        var script = await File.ReadAllTextAsync(Path.Combine(root, "src", "Aero.Cms.Modules.Setup", "wwwroot", "setup-handoff.js"));

        await Assert.That(markup).Contains("id=\"setup-handoff\"");
        await Assert.That(markup).Contains("setup-handoff.js");
        await Assert.That(script).Contains("status.setupComplete === true");
        await Assert.That(script).Contains("status.seedComplete === true");
        await Assert.That(script).Contains("persistCreatedSite(status)");
        await Assert.That(script).Contains("'/',");
        await Assert.That(script).Contains("redirect: 'follow'");
        await Assert.That(script).Contains("window.location.replace('/')");
        await Assert.That(script).Contains("fetchWithTimeout");
    }

    [Test]
    public async Task Setup_handoff_begins_polling_and_resumes_a_pending_browser_session()
    {
        var root = RepositoryRoot();
        var harness = Path.Combine(root, "tests", "Aero.Cms.Core.Tests", "JavaScript", "setup-handoff.test.mjs");
        var script = Path.Combine(root, "src", "Aero.Cms.Modules.Setup", "wwwroot", "setup-handoff.js");

        await RunNodeHarnessAsync(harness, script);
    }

    private static async Task RunNodeHarnessAsync(string harness, string script)
    {
        var startInfo = new ProcessStartInfo("node")
        {
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            UseShellExecute = false
        };
        startInfo.ArgumentList.Add(harness);
        startInfo.ArgumentList.Add(script);

        using var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException("Could not start Node.js for the browser startup regression test.");
        var standardOutput = process.StandardOutput.ReadToEndAsync();
        var standardError = process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();

        await Assert.That(process.ExitCode).IsEqualTo(0)
            .Because($"JavaScript harness failed.{Environment.NewLine}{await standardOutput}{await standardError}");
    }

    private static string RepositoryRoot() => Path.GetFullPath(
        Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
}
