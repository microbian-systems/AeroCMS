using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace Aero.Cms.Core.Modules;

/// <summary>
/// A composition surface for modules to contribute metadata and services.
/// </summary>
public interface IModuleBuilder
{
    void AddPermission(string permission);
    void AddAdminMenuContributor<T>() where T : class, IAdminMenuContributor;
    void AddShapeContributor<T>() where T : class, IShapeContributor;
    void AddDashboardWidget<T>() where T : class, IDashboardWidget;
    void AddContentType(string contentType);
    void AddContentPart<TPart>() where TPart : class, IContentPart;
    void AddFieldEditor<TEditor>() where TEditor : class, IFieldEditor;
    void AddSearchIndexer<TIndexer>() where TIndexer : class, ISearchIndexer;
}

public interface IAdminMenuContributor { }
public interface IShapeContributor { }
public interface IDashboardWidget { }
public interface IContentPart { }
public interface IFieldEditor { }
public interface ISearchIndexer { }


public class ModuleBuilder(IServiceCollection services, IConfiguration config, IHostEnvironment env) 
    : IModuleBuilder
{
    public void AddPermission(string permission)
    {
        throw new NotImplementedException();
    }

    public void AddAdminMenuContributor<T>() where T : class, IAdminMenuContributor
    {
        throw new NotImplementedException();
    }

    public void AddShapeContributor<T>() where T : class, IShapeContributor
    {
        throw new NotImplementedException();
    }

    public void AddDashboardWidget<T>() where T : class, IDashboardWidget
    {
        throw new NotImplementedException();
    }

    public void AddContentType(string contentType)
    {
        throw new NotImplementedException();
    }

    public void AddContentPart<TPart>() where TPart : class, IContentPart
    {
        throw new NotImplementedException();
    }

    public void AddFieldEditor<TEditor>() where TEditor : class, IFieldEditor
    {
        throw new NotImplementedException();
    }

    public void AddSearchIndexer<TIndexer>() where TIndexer : class, ISearchIndexer
    {
        throw new NotImplementedException();
    }
}