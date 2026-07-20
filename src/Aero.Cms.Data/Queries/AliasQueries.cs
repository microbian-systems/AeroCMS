using Aero.Cms.Core.Entities;
using Aero.Cms.Data.Queries.Base;
using AeroDB.Sable;
using System.Linq.Expressions;

namespace Aero.Cms.Data.Queries;


/// <inheritdoc cref="EntityByIdQuery{T}"/>
public sealed class AliasByIdQuery : EntityByIdQuery<AliasDocument>;

/// <inheritdoc cref="EntitiesByIdsQuery{T}"/>
public sealed class AliasesByIdsQuery : EntitiesByIdsQuery<AliasDocument>;

/// <summary>Selects aliases owned by one site, ordered by stored old path.</summary>
public sealed class AliasesBySiteIdQuery : ICompiledQuery<AliasDocument, IList<AliasDocument>>
{
    /// <summary>The site identifier that must match <see cref="AliasDocument.SiteId"/>.</summary>
public required long SiteId { get; set; }

    /// <inheritdoc />
public Expression<Func<ISurrealDbQueryable<AliasDocument>, IList<AliasDocument>>> QueryIs()
    {
        return q => q
            .Where(x => x.SiteId == SiteId)
            .OrderBy(x => x.OldPath)
            .ToList();
    }
}

/// <summary>Selects aliases whose stored old path contains a supplied substring.</summary>
/// <remarks>The expression performs no path normalization and orders matches by stored old path.</remarks>
public sealed class AliasesByOldPathContainsQuery : ICompiledQuery<AliasDocument, IList<AliasDocument>>
{
    /// <summary>The substring passed to <see cref="string.Contains(string)"/>.</summary>
public required string OldPath { get; set; }

    /// <inheritdoc />
public Expression<Func<ISurrealDbQueryable<AliasDocument>, IList<AliasDocument>>> QueryIs()
    {
        return q => q
            .Where(x => x.OldPath.Contains(OldPath))
            .OrderBy(x => x.OldPath)
            .ToList();
    }
}

/// <summary>Selects the first alias whose stored old path exactly matches a supplied value.</summary>
/// <remarks>This query is not site-scoped and performs no path normalization. It returns <see langword="null"/> when no alias matches.</remarks>
public sealed class AliasByOldPathQuery : ICompiledQuery<AliasDocument, AliasDocument?>
{
    /// <summary>The old-path value used by the equality predicate.</summary>
public required string OldPath { get; set; }

    /// <inheritdoc />
public Expression<Func<ISurrealDbQueryable<AliasDocument>, AliasDocument?>> QueryIs()
    {
        return q => q
            .FirstOrDefault(x => x.OldPath == OldPath);
    }
}


/// <summary>Selects the first alias matching both a site and a stored old path.</summary>
/// <remarks>The expression performs no path normalization and returns <see langword="null"/> when no alias matches.</remarks>
public sealed class AliasByOldPathAndSiteIdQuery : ICompiledQuery<AliasDocument, AliasDocument?>
{
    /// <summary>The site identifier that must match <see cref="AliasDocument.SiteId"/>.</summary>
public required long SiteId { get; set; }
    /// <summary>The old-path value used by the equality predicate.</summary>
public required string OldPath { get; set; }

    /// <inheritdoc />
public Expression<Func<ISurrealDbQueryable<AliasDocument>, AliasDocument?>> QueryIs()
    {
        return q => q
            .FirstOrDefault(x => x.SiteId == SiteId && x.OldPath == OldPath);
    }
}

/// <summary>Selects aliases whose stored destination path exactly matches a supplied value.</summary>
/// <remarks>This query is not site-scoped, performs no path normalization, and orders matches by stored old path.</remarks>
public sealed class AliasesByNewPathQuery : ICompiledQuery<AliasDocument, IList<AliasDocument>>
{
    /// <summary>The destination-path value used by the equality predicate.</summary>
public required string NewPath { get; set; }

    /// <inheritdoc />
public Expression<Func<ISurrealDbQueryable<AliasDocument>, IList<AliasDocument>>> QueryIs()
    {
        return q => q
            .Where(x => x.NewPath == NewPath)
            .OrderBy(x => x.OldPath)
            .ToList();
    }
}

/// <summary>Selects aliases matching both a site and a stored destination path.</summary>
/// <remarks>The expression performs no path normalization and orders matches by stored old path.</remarks>
public sealed class AliasesBySiteIdAndNewPathQuery : ICompiledQuery<AliasDocument, IList<AliasDocument>>
{
    /// <summary>The site identifier that must match <see cref="AliasDocument.SiteId"/>.</summary>
public required long SiteId { get; set; }
    /// <summary>The destination-path value used by the equality predicate.</summary>
public required string NewPath { get; set; }

    /// <inheritdoc />
public Expression<Func<ISurrealDbQueryable<AliasDocument>, IList<AliasDocument>>> QueryIs()
    {
        return q => q
            .Where(x => x.SiteId == SiteId && x.NewPath == NewPath)
            .OrderBy(x => x.OldPath)
            .ToList();
    }
}

/// <summary>Selects aliases whose notes exactly match a supplied value.</summary>
/// <remarks>The expression performs no text normalization and orders matches by stored old path.</remarks>
public sealed class AliasesByNotesQuery : ICompiledQuery<AliasDocument, IList<AliasDocument>>
{
    /// <summary>The notes value used by the equality predicate.</summary>
public required string Notes { get; set; }

    /// <inheritdoc />
public Expression<Func<ISurrealDbQueryable<AliasDocument>, IList<AliasDocument>>> QueryIs()
    {
        return q => q
            .Where(x => x.Notes == Notes)
            .OrderBy(x => x.OldPath)
            .ToList();
    }
}

/// <inheritdoc cref="EntitiesCreatedInRangeQuery{T}"/>
public sealed class AliasesCreatedInRangeQuery : EntitiesCreatedInRangeQuery<AliasDocument>;

/// <inheritdoc cref="EntitiesModifiedInRangeQuery{T}"/>
public sealed class AliasesModifiedInRangeQuery : EntitiesModifiedInRangeQuery<AliasDocument>;
