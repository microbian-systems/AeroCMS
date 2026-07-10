using Aero.Actors;
using Aero.Cms.Abstractions.Actors;
using Aero.Cms.Core.Models;

namespace Aero.Cms.Modules.Settings.Grains;

/// <summary>
/// Orleans grain for settings management — key-value store backed by AeroDB
/// <see cref="Setting"/> documents (keyed by string).
/// </summary>
public sealed class AeroSettingGrain : AeroActor, IAeroSettingActor
{
    private readonly IDocumentStore _store;

        /// <summary>
    /// Initializes a new instance of the <see cref="AeroSettingGrain"/> class.
    /// </summary>
public AeroSettingGrain(
        ILogger<AeroActor> log,
        IDocumentStore store)
        : base(log)
    {
        _store = store;
    }

        /// <summary>
    /// GetAllAsync method.
    /// </summary>
public async Task<List<SettingSummary>> GetAllAsync(CancellationToken ct = default)
    {
        await using var session = await _store.LightweightSessionAsync();
        var settings = await session.Query<Setting>()
            .OrderBy(x => x.Category)
            .ThenBy(x => x.Key)
            .ToListAsync(ct);

        return settings.Select(s => new SettingSummary(s.Key, s.Category, s.Description)).ToList();
    }

        /// <summary>
    /// GetByKeyAsync method.
    /// </summary>
public async Task<SettingDetail?> GetByKeyAsync(string key, CancellationToken ct = default)
    {
        await using var session = await _store.LightweightSessionAsync();
        var s = await session.LoadAsync<Setting>(key, ct);
        return s is null ? null : ToDetail(s);
    }

        /// <summary>
    /// GetByCategoryAsync method.
    /// </summary>
public async Task<List<SettingDetail>> GetByCategoryAsync(string category, CancellationToken ct = default)
    {
        await using var session = await _store.LightweightSessionAsync();
        var settings = await session.Query<Setting>()
            .Where(x => x.Category == category)
            .OrderBy(x => x.Key)
            .ToListAsync(ct);

        return settings.Select(ToDetail).ToList();
    }

        /// <summary>
    /// SetAsync method.
    /// </summary>
public async Task<SettingDetail> SetAsync(string key, string value, string category = "General", string type = "string", CancellationToken ct = default)
    {
        await using var session = await _store.LightweightSessionAsync();
        var setting = await session.LoadAsync<Setting>(key, ct);

        if (setting is null)
        {
            setting = new Setting { Key = key };
        }

        setting.Value = value;
        setting.Category = category;
        setting.Type = type;
        setting.ModifiedOn = DateTimeOffset.UtcNow;

        session.Store(setting);
        await session.SaveChangesAsync(ct);

        return ToDetail(setting);
    }

        /// <summary>
    /// DeleteAsync method.
    /// </summary>
public async Task<bool> DeleteAsync(string key, CancellationToken ct = default)
    {
        await using var session = await _store.LightweightSessionAsync();
        var setting = await session.LoadAsync<Setting>(key, ct);

        if (setting is null)
            return false;

        session.Delete(setting);
        await session.SaveChangesAsync(ct);
        return true;
    }

        /// <summary>
    /// GetCategoriesAsync method.
    /// </summary>
public async Task<List<SettingCategory>> GetCategoriesAsync(CancellationToken ct = default)
    {
        await using var session = await _store.LightweightSessionAsync();
        var settings = await session.Query<Setting>().ToListAsync(ct);

        return settings
            .GroupBy(x => x.Category)
            .Select(g => new SettingCategory(g.Key, g.Count()))
            .ToList();
    }

    private static SettingDetail ToDetail(Setting s) => new(
        s.Key,
        s.Value,
        s.Category,
        s.Description,
        s.Type,
        s.ModifiedOn.GetValueOrDefault().DateTime
    );
}
