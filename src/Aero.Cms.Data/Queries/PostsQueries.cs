using Aero.Cms.Core.Entities;
using Aero.Cms.Data.Queries.Base;

namespace Aero.Cms.Data.Queries;


/// <inheritdoc cref="EntityByIdQuery{T}"/>
public sealed class PostByIdQuery : EntityByIdQuery<PostDocument>;

/// <inheritdoc cref="EntitiesByIdsQuery{T}"/>
public sealed class PostsByIdsQuery : EntitiesByIdsQuery<PostDocument>;

/// <inheritdoc cref="EntitiesByCreatedByQuery{T}"/>
public sealed class PostsByCreatedByQuery : EntitiesByCreatedByQuery<PostDocument>;

/// <inheritdoc cref="EntitiesCreatedInRangeQuery{T}"/>
public sealed class PostsCreatedInRangeQuery : EntitiesCreatedInRangeQuery<PostDocument>;

/// <inheritdoc cref="EntitiesModifiedInRangeQuery{T}"/>
public sealed class PostsModifiedInRangeQuery : EntitiesModifiedInRangeQuery<PostDocument>;

//public sealed class PostsByCreatedByInCreatedRangeQuery
//    : EntitiesByCreatedByInCreatedRangeQuery<BlogPostDocument>;
