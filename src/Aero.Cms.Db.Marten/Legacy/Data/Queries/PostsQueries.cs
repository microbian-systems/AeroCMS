using Aero.Cms.Core.Entities;
using Aero.Cms.Data.Queries.Base;

namespace Aero.Cms.Data.Queries;


/// <summary>
/// Represents a class for PostByIdQuery.
/// </summary>
public sealed class PostByIdQuery : EntityByIdQuery<PostDocument>;

/// <summary>
/// Represents a class for PostsByIdsQuery.
/// </summary>
public sealed class PostsByIdsQuery : EntitiesByIdsQuery<PostDocument>;

/// <summary>
/// Represents a class for PostsByCreatedByQuery.
/// </summary>
public sealed class PostsByCreatedByQuery : EntitiesByCreatedByQuery<PostDocument>;

/// <summary>
/// Represents a class for PostsCreatedInRangeQuery.
/// </summary>
public sealed class PostsCreatedInRangeQuery : EntitiesCreatedInRangeQuery<PostDocument>;

/// <summary>
/// Represents a class for PostsModifiedInRangeQuery.
/// </summary>
public sealed class PostsModifiedInRangeQuery : EntitiesModifiedInRangeQuery<PostDocument>;

//public sealed class PostsByCreatedByInCreatedRangeQuery
//    : EntitiesByCreatedByInCreatedRangeQuery<BlogPostDocument>;