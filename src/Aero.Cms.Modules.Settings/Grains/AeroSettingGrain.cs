using Aero.Actors;
using Aero.Cms.Abstractions.Actors;
using Aero.Cms.Core.Models;

namespace Aero.Cms.Modules.Settings.Grains;

/// <summary>
/// Orleans grain for settings management — key-value store backed by AeroDB
/// <see cref="Setting"/> documents (keyed by string).
/// </summary>
/// <remarks>
/// Settings are global to the configured document store: operations do not apply site or tenant
/// predicates. Authorization and isolation must therefore be enforced by callers and deployment
/// configuration.
/// </remarks>
public sealed class AeroSettingGrain : AeroActor, IAeroSettingActor
{
    private readonly IDocumentStore _store;

        /// <summary>
    /// Initializes the grain with its actor logger and document store.
    /// </summary>
    /// <param name="log">The logger forwarded to the actor base.</param>
    /// <param name="store">The store used to open a session per operation.</param>
public AeroSettingGrain(
        ILogger<AeroActor> log,
        IDocumentStore store)
        : base(log)
    {
        _store = store;
    }

        /// <summary>
    /// Lists all settings ordered by category and then key.
    /// </summary>
    /// <param name="ct">The token used for the store query.</param>
    /// <returns>Setting summaries that intentionally omit values and types.</returns>
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
    /// Loads a setting by its document key.
    /// </summary>
    /// <param name="key">The exact string document identifier.</param>
    /// <param name="ct">The token used for the store load.</param>
    /// <returns>The mapped detail, or <see langword="null"/> when no document exists.</returns>
public async Task<SettingDetail?> GetByKeyAsync(string key, CancellationToken ct = default)
    {
        await using var session = await _store.LightweightSessionAsync();
        var s = await session.LoadAsync<Setting>(key, ct);
        return s is null ? null : ToDetail(s);
    }

        /// <summary>
    /// Lists settings whose category exactly matches the requested value.
    /// </summary>
    /// <param name="category">The category value used in the persistence predicate.</param>
    /// <param name="ct">The token used for the query.</param>
    /// <returns>Matching details ordered by key.</returns>
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
    /// Creates or updates a setting and persists the session.
    /// </summary>
    /// <param name="key">The string document identifier to create or update.</param>
    /// <param name="value">The unencrypted value stored in the setting document.</param>
    /// <param name="category">The category that replaces any existing category.</param>
    /// <param name="type">The type label that replaces any existing label.</param>
    /// <param name="ct">The token used for loading and committing.</param>
    /// <returns>The persisted setting detail with its modification timestamp.</returns>
    /// <remarks>
    /// The operation overwrites value, category, and type but preserves an existing description.
    /// Store and cancellation exceptions propagate to the caller.
    /// </remarks>
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
    /// Deletes a setting document when its key exists.
    /// </summary>
    /// <param name="key">The exact string document identifier.</param>
    /// <param name="ct">The token used for loading and committing.</param>
    /// <returns><see langword="true"/> after a committed delete; otherwise <see langword="false"/>.</returns>
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
    /// Groups all settings by category and counts each group.
    /// </summary>
    /// <param name="ct">The token used for the store query.</param>
    /// <returns>Category aggregates in provider-defined group order.</returns>
public async Task<List<SettingCategory>> GetCategoriesAsync(CancellationToken ct = default)
    {
        await using var session = await _store.LightweightSessionAsync();
        var settings = await session.Query<Setting>().ToListAsync(ct);

        return settings
            .GroupBy(x => x.Category)
            .Select(g => new SettingCategory(g.Key, g.Count()))
            .ToList();
    }

    /// <summary>
    /// Projects a persisted setting into the actor contract.
    /// </summary>
    /// <param name="s">The setting document to project.</param>
    /// <returns>A detail whose missing modification time becomes <see cref="DateTime.MinValue"/>.</returns>
    private static SettingDetail ToDetail(Setting s) => new(
        s.Key,
        s.Value,
        s.Category,
        s.Description,
        s.Type,
        s.ModifiedOn.GetValueOrDefault().DateTime
    );
}
