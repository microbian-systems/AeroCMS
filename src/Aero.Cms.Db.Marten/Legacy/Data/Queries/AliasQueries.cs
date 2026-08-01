using Aero.Cms.Core.Entities;
using Aero.Cms.Data.Queries.Base;
using Aero.Marten.Query;
using Marten.Linq;
using System.Linq.Expressions;

namespace Aero.Cms.Data.Queries;


/// <summary>
/// Represents a class for AliasByIdQuery.
/// </summary>
public sealed class AliasByIdQuery : EntityByIdQuery<AliasDocument>;

/// <summary>
/// Represents a class for AliasesByIdsQuery.
/// </summary>
public sealed class AliasesByIdsQuery : EntitiesByIdsQuery<AliasDocument>;

/// <summary>
/// Represents a class for AliasesBySiteIdQuery.
/// </summary>
public sealed class AliasesBySiteIdQuery : AeroCompiledQueryList<AliasDocument>
{
        /// <summary>
    /// Gets or sets the Site Id.
    /// </summary>
public required long SiteId { get; set; }

        /// <summary>
    /// QueryIs method.
    /// </summary>
public override Expression<Func<IMartenQueryable<AliasDocument>, IList<AliasDocument>>> QueryIs()
    {
        return q => q
            .Where(x => x.SiteId == SiteId)
            .OrderBy(x => x.OldPath)
            .ToList();
    }
}

/// <summary>
/// Represents a class for AliasesByOldPathContainsQuery.
/// </summary>
public sealed class AliasesByOldPathContainsQuery : AeroCompiledQuery<AliasDocument, IList<AliasDocument>>
{
        /// <summary>
    /// Gets or sets the Old Path.
    /// </summary>
public required string OldPath { get; set; }

        /// <summary>
    /// QueryIs method.
    /// </summary>
public override Expression<Func<IMartenQueryable<AliasDocument>, IList<AliasDocument>>> QueryIs()
    {
        return q => q
            .Where(x => x.OldPath.Contains(OldPath))
            .OrderBy(x => x.OldPath)
            .ToList();
    }
}

/// <summary>
/// Represents a class for AliasByOldPathQuery.
/// </summary>
public sealed class AliasByOldPathQuery : AeroCompiledQuery<AliasDocument, AliasDocument?>
{
        /// <summary>
    /// Gets or sets the Old Path.
    /// </summary>
public required string OldPath { get; set; }

        /// <summary>
    /// QueryIs method.
    /// </summary>
public override Expression<Func<IMartenQueryable<AliasDocument>, AliasDocument?>> QueryIs()
    {
        return q => q
            .FirstOrDefault(x => x.OldPath == OldPath);
    }
}


/// <summary>
/// Represents a class for AliasByOldPathAndSiteIdQuery.
/// </summary>
public sealed class AliasByOldPathAndSiteIdQuery : AeroCompiledQuery<AliasDocument, AliasDocument?>
{
        /// <summary>
    /// Gets or sets the Site Id.
    /// </summary>
public required long SiteId { get; set; }
        /// <summary>
    /// Gets or sets the Old Path.
    /// </summary>
public required string OldPath { get; set; }

        /// <summary>
    /// QueryIs method.
    /// </summary>
public override Expression<Func<IMartenQueryable<AliasDocument>, AliasDocument?>> QueryIs()
    {
        return q => q
            .FirstOrDefault(x => x.SiteId == SiteId && x.OldPath == OldPath);
    }
}

/// <summary>
/// Represents a class for AliasesByNewPathQuery.
/// </summary>
public sealed class AliasesByNewPathQuery : AeroCompiledQuery<AliasDocument, IList<AliasDocument>>
{
        /// <summary>
    /// Gets or sets the New Path.
    /// </summary>
public required string NewPath { get; set; }

        /// <summary>
    /// QueryIs method.
    /// </summary>
public override Expression<Func<IMartenQueryable<AliasDocument>, IList<AliasDocument>>> QueryIs()
    {
        return q => q
            .Where(x => x.NewPath == NewPath)
            .OrderBy(x => x.OldPath)
            .ToList();
    }
}

/// <summary>
/// Represents a class for AliasesBySiteIdAndNewPathQuery.
/// </summary>
public sealed class AliasesBySiteIdAndNewPathQuery : AeroCompiledQuery<AliasDocument, IList<AliasDocument>>
{
        /// <summary>
    /// Gets or sets the Site Id.
    /// </summary>
public required long SiteId { get; set; }
        /// <summary>
    /// Gets or sets the New Path.
    /// </summary>
public required string NewPath { get; set; }

        /// <summary>
    /// QueryIs method.
    /// </summary>
public override Expression<Func<IMartenQueryable<AliasDocument>, IList<AliasDocument>>> QueryIs()
    {
        return q => q
            .Where(x => x.SiteId == SiteId && x.NewPath == NewPath)
            .OrderBy(x => x.OldPath)
            .ToList();
    }
}

/// <summary>
/// Represents a class for AliasesByNotesQuery.
/// </summary>
public sealed class AliasesByNotesQuery : AeroCompiledQuery<AliasDocument, IList<AliasDocument>>
{
        /// <summary>
    /// Gets or sets the Notes.
    /// </summary>
public required string Notes { get; set; }

        /// <summary>
    /// QueryIs method.
    /// </summary>
public override Expression<Func<IMartenQueryable<AliasDocument>, IList<AliasDocument>>> QueryIs()
    {
        return q => q
            .Where(x => x.Notes == Notes)
            .OrderBy(x => x.OldPath)
            .ToList();
    }
}

/// <summary>
/// Represents a class for AliasesCreatedInRangeQuery.
/// </summary>
public sealed class AliasesCreatedInRangeQuery : EntitiesCreatedInRangeQuery<AliasDocument>;

/// <summary>
/// Represents a class for AliasesModifiedInRangeQuery.
/// </summary>
public sealed class AliasesModifiedInRangeQuery : EntitiesModifiedInRangeQuery<AliasDocument>;