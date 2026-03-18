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
