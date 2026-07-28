using System.Linq.Expressions;
using System.Reflection;
using Microsoft.EntityFrameworkCore.Query;

namespace ArturRios.Util.Test.Mock;

/// <summary>
/// An <see cref="IAsyncQueryProvider"/> that executes queries synchronously over an in-memory source but
/// exposes them through the async pipeline, so EF Core's async operators (<c>ToListAsync</c>,
/// <c>FirstOrDefaultAsync</c>, <c>CountAsync</c>, …) work against a fake repository. Adapted from the standard
/// Entity Framework Core testing pattern.
/// </summary>
/// <typeparam name="TEntity">The element type of the queries created by this provider.</typeparam>
internal sealed class TestAsyncQueryProvider<TEntity>(IQueryProvider inner) : IAsyncQueryProvider
{
    public IQueryable CreateQuery(Expression expression) => new TestAsyncEnumerable<TEntity>(expression);

    public IQueryable<TElement> CreateQuery<TElement>(Expression expression) => new TestAsyncEnumerable<TElement>(expression);

    public object? Execute(Expression expression) => inner.Execute(expression);

    public TResult Execute<TResult>(Expression expression) => inner.Execute<TResult>(expression);

    public TResult ExecuteAsync<TResult>(Expression expression, CancellationToken cancellationToken = default)
    {
        // EF Core's scalar async operators call ExecuteAsync with TResult = Task<T>; run the underlying
        // synchronous query and wrap the result in a completed Task<T>.
        var expectedResultType = typeof(TResult).GetGenericArguments()[0];

        var executionResult = typeof(IQueryProvider)
            .GetMethod(nameof(IQueryProvider.Execute), 1, [typeof(Expression)])!
            .MakeGenericMethod(expectedResultType)
            .Invoke(this, [expression]);

        return (TResult)typeof(Task)
            .GetMethod(nameof(Task.FromResult))!
            .MakeGenericMethod(expectedResultType)
            .Invoke(null, [executionResult])!;
    }
}

/// <summary>
/// An <see cref="IQueryable{T}"/> whose provider is a <see cref="TestAsyncQueryProvider{TEntity}"/> and which
/// is itself an <see cref="IAsyncEnumerable{T}"/>, enabling both scalar async operators and
/// <c>await foreach</c> / <c>ToListAsync</c> over an in-memory source.
/// </summary>
/// <typeparam name="T">The element type.</typeparam>
internal sealed class TestAsyncEnumerable<T> : EnumerableQuery<T>, IAsyncEnumerable<T>, IQueryable<T>
{
    public TestAsyncEnumerable(IEnumerable<T> enumerable) : base(enumerable) { }

    public TestAsyncEnumerable(Expression expression) : base(expression) { }

    public IAsyncEnumerator<T> GetAsyncEnumerator(CancellationToken cancellationToken = default) =>
        new TestAsyncEnumerator<T>(this.AsEnumerable().GetEnumerator());

    IQueryProvider IQueryable.Provider => new TestAsyncQueryProvider<T>(this);
}

/// <summary>An <see cref="IAsyncEnumerator{T}"/> that pulls from a synchronous enumerator.</summary>
/// <typeparam name="T">The element type.</typeparam>
internal sealed class TestAsyncEnumerator<T>(IEnumerator<T> inner) : IAsyncEnumerator<T>
{
    public T Current => inner.Current;

    public ValueTask<bool> MoveNextAsync() => ValueTask.FromResult(inner.MoveNext());

    public ValueTask DisposeAsync()
    {
        inner.Dispose();

        return ValueTask.CompletedTask;
    }
}
