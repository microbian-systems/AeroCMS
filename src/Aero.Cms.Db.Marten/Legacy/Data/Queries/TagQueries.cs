using Aero.Cms.Core.Entities;
using Aero.Cms.Data.Queries.Base;
using Aero.Marten.Query;
using Marten.Linq;
using System.Linq.Expressions;

namespace Aero.Cms.Data.Queries;


public sealed class TagByIdQuery : EntityByIdQuery<TagModel>;

public sealed class TagsByIdsQuery : EntitiesByIdsQuery<TagModel>;

public sealed class TagsByNameQuery : AeroCompiledQuery<TagModel, IList<TagModel>>
{
    public required string Name { get; set; }

    public override Expression<Func<IMartenQueryable<TagModel>, IList<TagModel>>> QueryIs()
    {
        return q => q
            .Where(x => x.Name == Name)
            .OrderBy(x => x.Name)
            .ToList();
    }
}

public sealed class TagsByNameContainsQuery : AeroCompiledQuery<TagModel, IList<TagModel>>
{
    public required string Name { get; set; }

    public override Expression<Func<IMartenQueryable<TagModel>, IList<TagModel>>> QueryIs()
    {
        return q => q
            .Where(x => x.Name != null && x.Name.Contains(Name))
            .OrderBy(x => x.Name)
            .ToList();
    }
}

public sealed class TagsByDescriptionQuery : AeroCompiledQuery<TagModel, IList<TagModel>>
{
    public required string Description { get; set; }

    public override Expression<Func<IMartenQueryable<TagModel>, IList<TagModel>>> QueryIs()
    {
        return q => q
            .Where(x => x.Description == Description)
            .OrderBy(x => x.Name)
            .ToList();
    }
}

public sealed class TagsCreatedInRangeQuery : EntitiesCreatedInRangeQuery<TagModel>;

public sealed class TagsModifiedInRangeQuery : EntitiesModifiedInRangeQuery<TagModel>;