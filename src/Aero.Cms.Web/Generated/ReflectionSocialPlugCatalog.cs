using System.Reflection;
using Aero.Cms.Generated;
using Aero.Social.Abstractions;
using Aero.Social.Plugs;

namespace Aero.Cms.Web.Generated;

/// <summary>
/// Startup-time plug catalog that scans known <see cref="ISocialProvider"/> types
/// for <c>[Plug]</c> and <c>[PostPlug]</c> methods once and caches the results.
/// This eliminates per-instance runtime method scanning from
/// <see cref="SocialProviderBase.DiscoverPlugs"/>.
/// </summary>
/// <remarks>
/// After construction, assign this to <see cref="SocialProviderBase.PlugCatalog"/>
/// during application startup. The scan runs once; subsequent plug discovery
/// uses the cached dictionary.
/// </remarks>
[LegacyReflectionDiscovery(Justification = "One-time startup scan to build cached plug catalog. Replaces per-instance runtime method scanning.")]
public sealed class ReflectionSocialPlugCatalog : ISocialPlugCatalog
{
    private readonly IReadOnlyDictionary<Type, IReadOnlyList<PlugInfo>> _plugs;

    /// <summary>
    /// Creates the catalog by scanning all loaded assemblies for provider types
    /// with <c>[Plug]</c> or <c>[PostPlug]</c> methods.
    /// </summary>
    public ReflectionSocialPlugCatalog()
    {
        var byType = new Dictionary<Type, IReadOnlyList<PlugInfo>>();

        var providerTypes = AppDomain.CurrentDomain.GetAssemblies()
            .SelectMany(a =>
            {
                try { return a.GetTypes(); }
                catch (ReflectionTypeLoadException ex) { return ex.Types.Where(t => t != null)!; }
                catch { return Type.EmptyTypes; }
            })
            .Where(t => t is { IsClass: true, IsAbstract: false } && typeof(ISocialProvider).IsAssignableFrom(t));

        foreach (var providerType in providerTypes)
        {
            var plugs = new List<PlugInfo>();
            var methods = providerType.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);

            foreach (var method in methods)
            {
                var plugAttr = method.GetCustomAttribute<PlugAttribute>();
                if (plugAttr != null)
                {
                    plugs.Add(new PlugInfo
                    {
                        Method = method,
                        Attribute = plugAttr,
                        IsPostPlug = false
                    });
                }

                var postPlugAttr = method.GetCustomAttribute<PostPlugAttribute>();
                if (postPlugAttr != null)
                {
                    plugs.Add(new PlugInfo
                    {
                        Method = method,
                        PostPlugAttribute = postPlugAttr,
                        IsPostPlug = true
                    });
                }
            }

            if (plugs.Count > 0)
            {
                byType[providerType] = plugs.AsReadOnly();
            }
        }

        _plugs = byType;
    }

    /// <inheritdoc />
    public IReadOnlyList<PlugInfo> GetPlugs(Type providerType)
    {
        return _plugs.TryGetValue(providerType, out var plugs) ? plugs : Array.Empty<PlugInfo>();
    }

    /// <inheritdoc />
    public PlugInfo? GetPlug(Type providerType, string identifier)
    {
        if (!_plugs.TryGetValue(providerType, out var plugs))
            return null;
        return plugs.FirstOrDefault(p => p.Identifier == identifier);
    }
}
