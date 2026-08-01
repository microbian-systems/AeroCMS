using System.Linq.Expressions;
using AeroDB.Sable;
using NSubstitute;

namespace Aero.Cms.Core.Tests.Services;

/// <summary>
/// Helper for creating mock <see cref="ISurrealDbQueryable{T}"/> instances
/// backed by in-memory lists, suitable for NSubstitute test setups.
/// </summary>
internal static class SurrealQueryableTestHelper
{
    /// <summary>
    /// Wraps an <see cref="IEnumerable{T}"/> as a mock <see cref="ISurrealDbQueryable{T}"/>
    /// with full LINQ provider plumbing, so that FirstOrDefaultAsync, ToListAsync, etc.
    /// work against the provided data.
    /// </summary>
    public static ISableQueryable<T> AsSurrealQueryable<T>(this IEnumerable<T> source) where T : class
    {
        var list = source.ToList();
        var enumerableQuery = list.AsQueryable();

        var mock = Substitute.For<ISableQueryable<T>>();

        // IQueryable plumbing
        mock.Expression.Returns(enumerableQuery.Expression);
        mock.ElementType.Returns(enumerableQuery.ElementType);
        mock.Provider.Returns(enumerableQuery.Provider);
        mock.GetEnumerator().Returns(ci => list.GetEnumerator());

        // Async LINQ methods
        mock.ToListAsync(Arg.Any<CancellationToken>())
            .Returns(ci => Task.FromResult(list));
        mock.FirstOrDefaultAsync(Arg.Any<CancellationToken>())
            .Returns(ci => Task.FromResult(list.FirstOrDefault()));
        mock.FirstOrDefaultAsync(Arg.Any<Expression<Func<T, bool>>>(), Arg.Any<CancellationToken>())
            .Returns(ci =>
            {
                var predicate = ci.ArgAt<Expression<Func<T, bool>>>(0).Compile();
                return Task.FromResult(list.FirstOrDefault(predicate));
            });
        mock.FirstOrDefaultAsync(Arg.Any<Expression<Func<T, bool>>>())
            .Returns(ci =>
            {
                var predicate = ci.ArgAt<Expression<Func<T, bool>>>(0).Compile();
                return Task.FromResult(list.FirstOrDefault(predicate));
            });
        mock.AnyAsync(Arg.Any<CancellationToken>())
            .Returns(ci => Task.FromResult(list.Any()));
        mock.CountAsync(Arg.Any<CancellationToken>())
            .Returns(ci => Task.FromResult(list.Count));

        return mock;
    }
}
