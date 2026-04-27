using Aero.Modular;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Aero.Cms.Modules.Modules.Services;

/// <summary>
/// Default implementation of IModuleBuilder that stores metadata contributions.
/// </summary>
public class AeroModuleBuilder : IAeroModuleBuilder
{
    private readonly HashSet<string> _permissions = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _contentTypes = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<Type> _adminMenuContributors = new();
    private readonly List<Type> _shapeContributors = new();
    private readonly List<Type> _dashboardWidgets = new();
    private readonly List<Type> _contentParts = new();
    private readonly List<Type> _fieldEditors = new();
    private readonly List<Type> _searchIndexers = new();
    private readonly List<Type> _martenConfigurations = new();

    /// <summary>
    /// The service collection for DI registrations.
    /// </summary>
    public IServiceCollection Services { get; }

    /// <summary>
    /// The application configuration.
    /// </summary>
    public IConfiguration Configuration { get; }

    /// <summary>
    /// The host environment.
    /// </summary>
    public IHostEnvironment Environment { get; }

    /// <summary>
    /// Creates a new ModuleBuilder instance.
    /// </summary>
    public AeroModuleBuilder(IServiceCollection services, IConfiguration configuration, IHostEnvironment environment)
    {
        Services = services;
        Configuration = configuration;
        Environment = environment;
    }

    /// <inheritdoc/>
    public IReadOnlySet<string> Permissions => _permissions;

    /// <inheritdoc/>
    public IReadOnlySet<string> ContentTypes => _contentTypes;

    /// <inheritdoc/>
    public IReadOnlyList<Type> AdminMenuContributors => _adminMenuContributors.AsReadOnly();

    /// <inheritdoc/>
    public IReadOnlyList<Type> ShapeContributors => _shapeContributors.AsReadOnly();

    /// <inheritdoc/>
    public IReadOnlyList<Type> DashboardWidgets => _dashboardWidgets.AsReadOnly();

    /// <inheritdoc/>
    public IReadOnlyList<Type> ContentParts => _contentParts.AsReadOnly();

    /// <inheritdoc/>
    public IReadOnlyList<Type> FieldEditors => _fieldEditors.AsReadOnly();

    /// <inheritdoc/>
    public IReadOnlyList<Type> SearchIndexers => _searchIndexers.AsReadOnly();

    /// <inheritdoc/>
    public IReadOnlyList<Type> MartenConfigurations => _martenConfigurations.AsReadOnly();

    /// <inheritdoc/>
    public void AddPermission(string permission)
    {
        ArgumentException.ThrowIfNullOrEmpty(permission);

        if (!_permissions.Add(permission))
        {
            throw new InvalidOperationException($"Permission '{permission}' has already been registered.");
        }
    }

    /// <inheritdoc/>
    public void AddAdminMenuContributor<T>() where T : class, IAdminMenuContributor
    {
        var type = typeof(T);
        if (_adminMenuContributors.Contains(type))
        {
            throw new InvalidOperationException($"Admin menu contributor '{type.FullName}' has already been registered.");
        }

        _adminMenuContributors.Add(type);
        Services.AddScoped<T>();
    }

    /// <inheritdoc/>
    public void AddShapeContributor<T>() where T : class, IShapeContributor
    {
        var type = typeof(T);
        if (_shapeContributors.Contains(type))
        {
            throw new InvalidOperationException($"Shape contributor '{type.FullName}' has already been registered.");
        }

        _shapeContributors.Add(type);
        Services.AddScoped<T>();
    }

    /// <inheritdoc/>
    public void AddDashboardWidget<T>() where T : class, IDashboardWidget
    {
        var type = typeof(T);
        if (_dashboardWidgets.Contains(type))
        {
            throw new InvalidOperationException($"Dashboard widget '{type.FullName}' has already been registered.");
        }

        _dashboardWidgets.Add(type);
        Services.AddScoped<T>();
    }

    /// <inheritdoc/>
    public void AddContentType(string contentType)
    {
        ArgumentException.ThrowIfNullOrEmpty(contentType);

        if (!_contentTypes.Add(contentType))
        {
            throw new InvalidOperationException($"Content type '{contentType}' has already been registered.");
        }
    }

    /// <inheritdoc/>
    public void AddContentPart<TPart>() where TPart : class, IContentPart
    {
        var type = typeof(TPart);
        if (_contentParts.Contains(type))
        {
            throw new InvalidOperationException($"Content part '{type.FullName}' has already been registered.");
        }

        _contentParts.Add(type);
        Services.AddScoped<TPart>();
    }

    /// <inheritdoc/>
    public void AddFieldEditor<TEditor>() where TEditor : class, IFieldEditor
    {
        var type = typeof(TEditor);
        if (_fieldEditors.Contains(type))
        {
            throw new InvalidOperationException($"Field editor '{type.FullName}' has already been registered.");
        }

        _fieldEditors.Add(type);
        Services.AddScoped<TEditor>();
    }

    /// <inheritdoc/>
    public void AddSearchIndexer<TIndexer>() where TIndexer : class, ISearchIndexer
    {
        var type = typeof(TIndexer);
        if (_searchIndexers.Contains(type))
        {
            throw new InvalidOperationException($"Search indexer '{type.FullName}' has already been registered.");
        }

        _searchIndexers.Add(type);
        Services.AddScoped<TIndexer>();
    }

    /// <inheritdoc/>
    public void AddMartenConfiguration<T>() where T : class, global::Marten.IConfigureMarten
    {
        var type = typeof(T);
        if (_martenConfigurations.Contains(type))
        {
            throw new InvalidOperationException($"Marten configuration contributor '{type.FullName}' has already been registered.");
        }

        _martenConfigurations.Add(type);
        Services.AddSingleton<global::Marten.IConfigureMarten, T>();
    }
}
