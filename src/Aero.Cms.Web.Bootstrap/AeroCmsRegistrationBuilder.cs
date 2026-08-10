using System.Reflection;
using Aero.Cms.Hosting;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Aero.Cms.Web.Bootstrap;

/// <summary>
/// Fluent, language-neutral terminal registration for an explicitly selected Aero CMS catalog.
/// </summary>
public sealed class AeroCmsRegistrationBuilder
{
    private readonly WebApplicationBuilder _builder;
    private readonly AeroCmsHostCatalog _catalog;
    private readonly Action<AeroCmsOptions>? _configure;
    private string? _setupSettingsDirectory;

    internal AeroCmsRegistrationBuilder(
        WebApplicationBuilder builder,
        AeroCmsHostCatalog catalog,
        Action<AeroCmsOptions>? configure)
    {
        _builder = builder;
        _catalog = catalog;
        _configure = configure;
    }

    /// <summary>
    /// Selects the host-owned directory in which the setup wizard may persist
    /// <c>appsettings.{Environment}.json</c>.
    /// </summary>
    /// <remarks>
    /// Setup persistence is never inferred from the Aero CMS package location. A relative path is
    /// resolved beneath the consuming host's content root.
    /// </remarks>
    public AeroCmsRegistrationBuilder WithSetupSettingsDirectory(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        if (_setupSettingsDirectory is not null)
        {
            throw new InvalidOperationException(
                "AEROCMS_SETUP_PERSISTENCE_ALREADY_CONFIGURED: The setup settings directory may be selected only once.");
        }

        _setupSettingsDirectory = Path.GetFullPath(path, _builder.Environment.ContentRootPath);
        return this;
    }

    /// <summary>Registers the consuming C# host by marker type.</summary>
    public Task<WebApplicationBuilder> RegisterHostAsync<THost>()
        where THost : class
        => RegisterHostAsync(typeof(THost).Assembly);

    /// <summary>Registers an F# or other .NET host by its entry assembly.</summary>
    public async Task<WebApplicationBuilder> RegisterHostAsync(Assembly hostAssembly)
    {
        ArgumentNullException.ThrowIfNull(hostAssembly);

        if (_builder.Services.Any(static descriptor =>
                descriptor.ServiceType == typeof(AeroCmsHostRegistrationMarker)))
        {
            throw new InvalidOperationException(
                "AEROCMS_HOST_ALREADY_REGISTERED: AddAeroCms may be completed only once for an application builder.");
        }

        _builder.Services.AddSingleton<AeroCmsHostRegistrationMarker>();

        if (_setupSettingsDirectory is not null)
        {
            _builder.Configuration["AeroCms:Configuration:SettingsDirectory"] = _setupSettingsDirectory;
        }

        var setupSelected = (_catalog.Capabilities & AeroCmsCapabilities.Setup) != 0;
        if (setupSelected &&
            string.IsNullOrWhiteSpace(_builder.Configuration["AeroCms:Configuration:SettingsDirectory"]))
        {
            throw new InvalidOperationException(
                "AEROCMS_SETUP_PERSISTENCE_REQUIRED: A catalog with setup support requires an explicit host-owned settings directory. Call WithSetupSettingsDirectory(path) or provide AeroCms:Configuration:SettingsDirectory from host configuration.");
        }

        if (setupSelected && _builder.Configuration.GetValue("AeroCms:Setup:RunHandoff", true))
        {
            await AeroStartupPipeline.EnsureRuntimeConfigurationAsync(_builder, []);
        }

        var configureWolverine = _catalog.ConfigureWolverine;
        var configureOrleans = _catalog.ConfigureOrleans;

        await AeroCmsExtensions.AddAeroCmsCoreAsync(
            _builder,
            hostAssembly,
            options =>
            {
                var catalogServerAssemblies = _catalog.ServerComponentAssemblies
                    .Append(hostAssembly)
                    .DistinctBy(static assembly => assembly.FullName, StringComparer.Ordinal)
                    .ToArray();
                options.ModuleDescriptors = _catalog.ModuleDescriptors;
                options.SelectedCapabilities = _catalog.Capabilities;
                options.ConfigureWolverine = configureWolverine;
                options.ConfigureGrains = configureOrleans;
                options.ServerComponentAssemblies = catalogServerAssemblies;
                options.WebAssemblyComponentAssemblies = _catalog.WebAssemblyComponentAssemblies;

                _configure?.Invoke(options);
                options.RequiredCapabilities |= AeroCmsCapabilities.ServerComponents;
                if (options.WebAssemblyComponentAssemblies.Count > 0)
                {
                    options.RequiredCapabilities |= AeroCmsCapabilities.WebAssemblyComponents;
                }
                _catalog.RequireCapabilities(options.RequiredCapabilities);

                if (!ReferenceEquals(options.ModuleDescriptors, _catalog.ModuleDescriptors) ||
                    !ReferenceEquals(options.ConfigureWolverine, configureWolverine) ||
                    !ReferenceEquals(options.ConfigureGrains, configureOrleans) ||
                    !ContainsEveryAssembly(options.ServerComponentAssemblies, catalogServerAssemblies) ||
                    !ContainsEveryAssembly(options.WebAssemblyComponentAssemblies, _catalog.WebAssemblyComponentAssemblies))
                {
                    throw new InvalidOperationException(
                        "AEROCMS_CATALOG_OVERRIDE_NOT_ALLOWED: The selected catalog is authoritative for modules, handlers, grains, and required component assemblies.");
                }
            },
            preserveHostPolicies: true,
            addServiceDefaults: false,
            configureAeroLogging: false);

        return _builder;
    }

    private static bool ContainsEveryAssembly(
        IReadOnlyList<Assembly> candidate,
        IReadOnlyList<Assembly> required)
    {
        var names = candidate
            .Select(static assembly => assembly.FullName)
            .ToHashSet(StringComparer.Ordinal);
        return required.All(assembly => names.Contains(assembly.FullName));
    }

    private sealed class AeroCmsHostRegistrationMarker;
}
