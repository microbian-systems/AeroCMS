using Aero.Cms.Core.Entities;
using Aero.Cms.Data.Queries.Base;
using Aero.Marten.Query;
using Marten.Linq;
using System.Linq.Expressions;

namespace Aero.Cms.Data.Queries;


public sealed class AliasByIdQuery : EntityByIdQuery<AliasDocument>;

public sealed class AliasesByIdsQuery : EntitiesByIdsQuery<AliasDocument>;

public sealed class AliasesBySiteIdQuery : AeroCompiledQueryList<AliasDocument>
{
    public required long SiteId { get; set; }

    public override Expression<Func<IMartenQueryable<AliasDocument>, IList<AliasDocument>>> QueryIs()
    {
        return q => q
            .Where(x => x.SiteId == SiteId)
            .OrderBy(x => x.OldPath)
            .ToList();
    }
}

public sealed class AliasesByOldPathContainsQuery : AeroCompiledQuery<AliasDocument, IList<AliasDocument>>
{
    public required string OldPath { get; set; }

    public override Expression<Func<IMartenQueryable<AliasDocument>, IList<AliasDocument>>> QueryIs()
    {
        return q => q
            .Where(x => x.OldPath.Contains(OldPath))
            .OrderBy(x => x.OldPath)
            .ToList();
    }
}

public sealed class AliasByOldPathQuery : AeroCompiledQuery<AliasDocument, AliasDocument?>
{
    public required string OldPath { get; set; }

    public override Expression<Func<IMartenQueryable<AliasDocument>, AliasDocument?>> QueryIs()
    {
        return q => q
            .FirstOrDefault(x => x.OldPath == OldPath);
    }
}


public sealed class AliasByOldPathAndSiteIdQuery : AeroCompiledQuery<AliasDocument, AliasDocument?>
{
    public required long SiteId { get; set; }
    public required string OldPath { get; set; }

    public override Expression<Func<IMartenQueryable<AliasDocument>, AliasDocument?>> QueryIs()
    {
        return q => q
            .FirstOrDefault(x => x.SiteId == SiteId && x.OldPath == OldPath);
    }
}

public sealed class AliasesByNewPathQuery : AeroCompiledQuery<AliasDocument, IList<AliasDocument>>
{
    public required string NewPath { get; set; }

    public override Expression<Func<IMartenQueryable<AliasDocument>, IList<AliasDocument>>> QueryIs()
    {
        return q => q
            .Where(x => x.NewPath == NewPath)
            .OrderBy(x => x.OldPath)
            .ToList();
    }
}

public sealed class AliasesBySiteIdAndNewPathQuery : AeroCompiledQuery<AliasDocument, IList<AliasDocument>>
{
    public required long SiteId { get; set; }
    public required string NewPath { get; set; }

    public override Expression<Func<IMartenQueryable<AliasDocument>, IList<AliasDocument>>> QueryIs()
    {
        return q => q
            .Where(x => x.SiteId == SiteId && x.NewPath == NewPath)
            .OrderBy(x => x.OldPath)
            .ToList();
    }
}

public sealed class AliasesByNotesQuery : AeroCompiledQuery<AliasDocument, IList<AliasDocument>>
{
    public required string Notes { get; set; }

    public override Expression<Func<IMartenQueryable<AliasDocument>, IList<AliasDocument>>> QueryIs()
    {
        return q => q
            .Where(x => x.Notes == Notes)
            .OrderBy(x => x.OldPath)
            .ToList();
    }
}

public sealed class AliasesCreatedInRangeQuery : EntitiesCreatedInRangeQuery<AliasDocument>;

public sealed class AliasesModifiedInRangeQuery : EntitiesModifiedInRangeQuery<AliasDocument>;