using Aero.Cms.Core.Modules;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;

namespace Aero.Cms.Core.Tests.Models;

public class ModuleBuilderTests
{
    [Test]
    public async Task ModuleBuilder_AddStringRegistrations_ShouldTrackValuesAndRejectDuplicates()
    {
        var builder = CreateBuilder();

        builder.AddPermission("ManageContent");
        builder.AddContentType("Article");

        builder.Permissions.Should().ContainSingle().Which.Should().Be("ManageContent");
        builder.ContentTypes.Should().ContainSingle().Which.Should().Be("Article");

        var duplicatePermission = () => builder.AddPermission("managecontent");
        var duplicateContentType = () => builder.AddContentType("article");

        duplicatePermission.Should().Throw<InvalidOperationException>()
            .WithMessage("*ManageContent*");
        duplicateContentType.Should().Throw<InvalidOperationException>()
            .WithMessage("*Article*");

        await Task.CompletedTask;
    }

    [Test]
    public async Task ModuleBuilder_AddTypedRegistrations_ShouldExposeTypesAndRegisterServices()
    {
        var services = new ServiceCollection();
        var builder = CreateBuilder(services);

        builder.AddAdminMenuContributor<TestAdminMenuContributor>();
        builder.AddShapeContributor<TestShapeContributor>();
        builder.AddDashboardWidget<TestDashboardWidget>();
        builder.AddContentPart<TestContentPart>();
        builder.AddFieldEditor<TestFieldEditor>();
        builder.AddSearchIndexer<TestSearchIndexer>();

        builder.AdminMenuContributors.Should().ContainSingle().Which.Should().Be(typeof(TestAdminMenuContributor));
        builder.ShapeContributors.Should().ContainSingle().Which.Should().Be(typeof(TestShapeContributor));
        builder.DashboardWidgets.Should().ContainSingle().Which.Should().Be(typeof(TestDashboardWidget));
        builder.ContentParts.Should().ContainSingle().Which.Should().Be(typeof(TestContentPart));
        builder.FieldEditors.Should().ContainSingle().Which.Should().Be(typeof(TestFieldEditor));
        builder.SearchIndexers.Should().ContainSingle().Which.Should().Be(typeof(TestSearchIndexer));

        services.Should().Contain(descriptor => descriptor.ServiceType == typeof(TestAdminMenuContributor) && descriptor.Lifetime == ServiceLifetime.Scoped);
        services.Should().Contain(descriptor => descriptor.ServiceType == typeof(TestShapeContributor) && descriptor.Lifetime == ServiceLifetime.Scoped);
        services.Should().Contain(descriptor => descriptor.ServiceType == typeof(TestDashboardWidget) && descriptor.Lifetime == ServiceLifetime.Scoped);
        services.Should().Contain(descriptor => descriptor.ServiceType == typeof(TestContentPart) && descriptor.Lifetime == ServiceLifetime.Scoped);
        services.Should().Contain(descriptor => descriptor.ServiceType == typeof(TestFieldEditor) && descriptor.Lifetime == ServiceLifetime.Scoped);
        services.Should().Contain(descriptor => descriptor.ServiceType == typeof(TestSearchIndexer) && descriptor.Lifetime == ServiceLifetime.Scoped);

        var duplicateAdminMenuContributor = () => builder.AddAdminMenuContributor<TestAdminMenuContributor>();
        duplicateAdminMenuContributor.Should().Throw<InvalidOperationException>()
            .WithMessage("*TestAdminMenuContributor*");

        await Task.CompletedTask;
    }

    private static ModuleBuilder CreateBuilder(IServiceCollection? services = null)
    {
        return new ModuleBuilder(
            services ?? new ServiceCollection(),
            new ConfigurationBuilder().Build(),
            new FakeHostEnvironment());
    }

    private sealed class FakeHostEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = Environments.Development;
        public string ApplicationName { get; set; } = "Aero.Cms.Core.Tests";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public IFileProvider ContentRootFileProvider { get; set; } = new PhysicalFileProvider(AppContext.BaseDirectory);
    }

    private sealed class TestAdminMenuContributor : IAdminMenuContributor;

    private sealed class TestShapeContributor : IShapeContributor;

    private sealed class TestDashboardWidget : IDashboardWidget;

    private sealed class TestContentPart : IContentPart;

    private sealed class TestFieldEditor : IFieldEditor;

    private sealed class TestSearchIndexer : ISearchIndexer;
}
