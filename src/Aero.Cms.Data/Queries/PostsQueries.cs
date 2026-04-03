using Aero.Cms.Core.Entities;
using Aero.Cms.Data.Queries.Base;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Aero.Cms.Data.Queries;


public sealed class PostByIdQuery : BaseQuries<BlogPostDocument>;

public sealed class PostsByIdsQuery : EntitiesByIdsQuery<BlogPostDocument>;

public sealed class PostsByCreatedByQuery : EntitiesByCreatedByQuery<BlogPostDocument>;

public sealed class PostsCreatedInRangeQuery : EntitiesCreatedOnRangeQuery<BlogPostDocument>;

public sealed class PostsModifiedInRangeQuery : EntitiesModifiedOnRangeQuery<BlogPostDocument>;

//public sealed class PostsByCreatedByInCreatedRangeQuery
//    : EntitiesByCreatedByOnCreatedRangeQuery<BlogPostDocument>;