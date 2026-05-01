using Aero.Cms.Core.Entities;
using Aero.Cms.Data.Queries.Base;

namespace Aero.Cms.Data.Queries;


public sealed class PageByIdQuery : EntityByIdQuery<PageDocument>;
public sealed class PagesByIdsQuery : EntitiesByIdsQuery<PageDocument>;
public sealed class PagesCreatedByQuery : EntitiesByCreatedByQuery<PageDocument>;
public sealed class PagesModifiedByQuery : EntitiesByModifiedByQuery<PageDocument>;
public sealed class PagesCreatedOnRangeQuery : EntitiesCreatedInRangeQuery<PageDocument>;
public sealed class PagesModifiedOnRangeQuery : EntitiesModifiedInRangeQuery<PageDocument>;
public sealed class PagesByCreatedByInDateRangeQuery : EntitiesByCreatedByInDateRangeQuery<PageDocument>;
public sealed class PagesByModifiedByInDateRangeQuery : EntitiesByModifiedByInDateRangeQuery<PageDocument>;
public sealed class LatestPageCreatedByQuery : LatestCreatedByQuery<PageDocument>;
public sealed class LatestPageModifiedByQuery : LatestModifiedByQuery<PageDocument>;