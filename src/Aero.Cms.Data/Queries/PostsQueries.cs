using Aero.Cms.Core.Entities;
using Aero.Cms.Data.Queries.Base;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Aero.Cms.Data.Queries;


public sealed class PostByIdQuery : EntityByIdQuery<PostDocument>;

public sealed class PostsByIdsQuery : EntitiesByIdsQuery<PostDocument>;

public sealed class PostsByCreatedByQuery : EntitiesByCreatedByQuery<PostDocument>;

public sealed class PostsCreatedInRangeQuery : EntitiesCreatedInRangeQuery<PostDocument>;

public sealed class PostsModifiedInRangeQuery : EntitiesModifiedInRangeQuery<PostDocument>;

//public sealed class PostsByCreatedByInCreatedRangeQuery
//    : EntitiesByCreatedByInCreatedRangeQuery<BlogPostDocument>;