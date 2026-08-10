using Aero.Cms.Hosting;
using Aero.Cms.Hosting.Defaults;
using Aero.Modular;
using Orleans.Hosting;
using Wolverine;

namespace Aero.Cms.Hosting.Tests;

public sealed class AeroCmsHostCatalogBuilderTests
{
    [Test]
    public async Task Default_catalog_is_non_empty_and_exposes_required_host_capabilities()
    {
        var catalog = AeroCmsDefaultCatalog.Catalog;

        await Assert.That(catalog.ModuleDescriptors).IsNotEmpty();
        await Assert.That(catalog.Capabilities.HasFlag(AeroCmsCapabilities.Setup)).IsTrue();
        await Assert.That(catalog.Capabilities.HasFlag(AeroCmsCapabilities.Identity)).IsTrue();
        await Assert.That(catalog.Capabilities.HasFlag(AeroCmsCapabilities.Manager)).IsTrue();
        await Assert.That(catalog.Capabilities.HasFlag(AeroCmsCapabilities.PublicQuery)).IsTrue();
    }

    [Test]
    public async Task Build_orders_registrations_and_dependencies_deterministically()
    {
        var alpha = Descriptor("Alpha");
        var beta = Descriptor("Beta", "Alpha");
        var gamma = Descriptor("Gamma", "Alpha");

        var catalog = new AeroCmsHostCatalogBuilder()
            .Add(Registration("zeta", gamma))
            .Add(Registration("alpha", beta, alpha))
            .Build();

        await Assert.That(string.Join(",", catalog.Registrations.Select(static registration => registration.Id)))
            .IsEqualTo("alpha,zeta");
        await Assert.That(string.Join(",", catalog.ModuleDescriptors.Select(static descriptor => descriptor.Name)))
            .IsEqualTo("Alpha,Beta,Gamma");
    }

    [Test]
    public async Task Catalog_callbacks_follow_cross_registration_dependency_order()
    {
        var callbacks = new List<string>();
        var dependency = Registration("z-dependency", callbacks, Descriptor("Dependency"));
        var consumer = Registration("a-consumer", callbacks, Descriptor("Consumer", "Dependency"));

        var catalog = new AeroCmsHostCatalogBuilder()
            .Add(consumer)
            .Add(dependency)
            .Build();

        catalog.ConfigureWolverine(new WolverineOptions());

        await Assert.That(string.Join(",", catalog.Registrations.Select(static item => item.Id)))
            .IsEqualTo("z-dependency,a-consumer");
        await Assert.That(string.Join(",", callbacks))
            .IsEqualTo("z-dependency,a-consumer");
    }

    [Test]
    [Arguments("duplicate-registration", "AEROCMS_CATALOG_DUPLICATE_REGISTRATION")]
    [Arguments("duplicate-module", "AEROCMS_CATALOG_DUPLICATE_MODULE")]
    [Arguments("missing-dependency", "AEROCMS_CATALOG_MISSING_DEPENDENCY")]
    [Arguments("cycle", "AEROCMS_CATALOG_DEPENDENCY_CYCLE")]
    public async Task Build_rejects_invalid_catalogs(string scenario, string expectedCode)
    {
        var builder = scenario switch
        {
            "duplicate-registration" => new AeroCmsHostCatalogBuilder()
                .Add(Registration("same", Descriptor("Alpha")))
                .Add(Registration("SAME", Descriptor("Beta"))),
            "duplicate-module" => new AeroCmsHostCatalogBuilder()
                .Add(Registration("one", Descriptor("Alpha")))
                .Add(Registration("two", Descriptor("alpha"))),
            "missing-dependency" => new AeroCmsHostCatalogBuilder()
                .Add(Registration("one", Descriptor("Alpha", "Missing"))),
            "cycle" => new AeroCmsHostCatalogBuilder()
                .Add(Registration(
                    "one",
                    Descriptor("Alpha", "Beta"),
                    Descriptor("Beta", "Alpha"))),
            _ => throw new ArgumentOutOfRangeException(nameof(scenario), scenario, null)
        };

        var exception = await Assert.That(builder.Build).Throws<AeroCmsCatalogException>();

        await Assert.That(exception!.Code).IsEqualTo(expectedCode);
    }

    [Test]
    public async Task Build_rejects_empty_registration_set()
    {
        var builder = new AeroCmsHostCatalogBuilder();

        var exception = await Assert.That(builder.Build).Throws<AeroCmsCatalogException>();

        await Assert.That(exception!.Code).IsEqualTo("AEROCMS_CATALOG_EMPTY");
    }

    [Test]
    public async Task RequireCapabilities_fails_closed()
    {
        var catalog = new AeroCmsHostCatalogBuilder()
            .Add(Registration(
                "core",
                AeroCmsCapabilities.ServerComponents,
                Descriptor("Alpha")))
            .Build();

        void RequireWebAssembly() => catalog.RequireCapabilities(
            AeroCmsCapabilities.ServerComponents | AeroCmsCapabilities.WebAssemblyComponents);

        var exception = await Assert.That(RequireWebAssembly).Throws<AeroCmsCatalogException>();
        await Assert.That(exception!.Code).IsEqualTo("AEROCMS_CATALOG_MISSING_CAPABILITY");
    }

    private static AeroCmsModuleRegistration Registration(
        string id,
        params ModuleDescriptor[] descriptors)
        => Registration(id, AeroCmsCapabilities.None, descriptors);

    private static AeroCmsModuleRegistration Registration(
        string id,
        AeroCmsCapabilities capabilities,
        params ModuleDescriptor[] descriptors)
        => new(
            id,
            descriptors,
            static (WolverineOptions _) => { },
            static (ISiloBuilder _) => { },
            capabilities: capabilities);

    private static AeroCmsModuleRegistration Registration(
        string id,
        ICollection<string> callbacks,
        params ModuleDescriptor[] descriptors)
        => new(
            id,
            descriptors,
            _ => callbacks.Add(id),
            static (ISiloBuilder _) => { });

    private static ModuleDescriptor Descriptor(string name, params string[] dependencies)
        => new()
        {
            Name = name,
            Version = "1.0.0",
            Author = "Tests",
            ModuleType = typeof(Aero.Cms.Modules.Setup.SetupModule),
            AssemblyName = typeof(Aero.Cms.Modules.Setup.SetupModule).Assembly.GetName().Name!,
            Dependencies = dependencies
        };
}
