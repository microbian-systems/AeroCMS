using Aero.Cms.Contracts.Abstractions;
using Aero.Cms.Shared.Services;
using Shouldly;

namespace Aero.Cms.Core.Tests.Services;

public sealed class AdminStateContainerTests
{
    [Test]
    public void Site_selection_round_trips_a_snowflake_as_a_string()
    {
        const long siteId = 1_530_221_140_281_556_994;
        var storage = new FakeAdminStorage();
        var state = new AdminStateContainer(storage);

        state.SetSite(siteId, "Contoso");

        storage.Values["aero-admin-state.siteId"].ShouldBe(siteId.ToString());
        storage.Values["aero-admin-state.siteName"].ShouldBe("Contoso");

        var restored = new AdminStateContainer(storage);
        restored.LoadFromStorage();

        restored.CurrentSiteId.ShouldBe(siteId);
        restored.CurrentSiteName.ShouldBe("Contoso");
        restored.IsInitialized.ShouldBeTrue();
    }

    [Test]
    public void Clear_site_removes_both_browser_storage_values()
    {
        var storage = new FakeAdminStorage();
        var state = new AdminStateContainer(storage);
        state.SetSite(42, "Old Site");

        state.ClearSite();

        state.CurrentSiteId.ShouldBeNull();
        state.CurrentSiteName.ShouldBeNull();
        storage.Values.ContainsKey("aero-admin-state.siteId").ShouldBeFalse();
        storage.Values.ContainsKey("aero-admin-state.siteName").ShouldBeFalse();
    }

    private sealed class FakeAdminStorage : IAdminStorage
    {
        public Dictionary<string, object?> Values { get; } = new(StringComparer.Ordinal);

        public T? GetItem<T>(string key)
        {
            if (!Values.TryGetValue(key, out var value))
                return default;

            return (T?)value;
        }

        public void SetItem<T>(string key, T value)
        {
            Values[key] = value;
        }

        public void RemoveItem(string key)
        {
            Values.Remove(key);
        }
    }
}
