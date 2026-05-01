using Aero.Cms.Core.Entities;
using Aero.Cms.Data.Queries.Base;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Aero.Cms.Data.Queries;


public sealed class PostByIdQuery : EntityByIdQuery<BlogPostDocument>;

public sealed class PostsByIdsQuery : EntitiesByIdsQuery<BlogPostDocument>;

public sealed class PostsByCreatedByQuery : EntitiesByCreatedByQuery<BlogPostDocument>;

public sealed class PostsCreatedInRangeQuery : EntitiesCreatedInRangeQuery<BlogPostDocument>;

public sealed class PostsModifiedInRangeQuery : EntitiesModifiedInRangeQuery<BlogPostDocument>;

//public sealed class PostsByCreatedByInCreatedRangeQuery
//    : EntitiesByCreatedByInCreatedRangeQuery<BlogPostDocument>;