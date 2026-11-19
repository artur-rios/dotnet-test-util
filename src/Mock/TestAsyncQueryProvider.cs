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
        // synchronous query and hand its outcome back as a Task<T>.
        var expectedResultType = typeof(TResult).GetGenericArguments()[0];

        return (TResult)ExecuteAsTaskMethod
            .MakeGenericMethod(expectedResultType)
            .Invoke(this, [expression, cancellationToken])!;
    }

    private static readonly MethodInfo ExecuteAsTaskMethod =
        typeof(TestAsyncQueryProvider<TEntity>).GetMethod(nameof(ExecuteAsTask), BindingFlags.NonPublic | BindingFlags.Instance)!;

    /// <summary>
    /// Runs the query as EF Core's own provider does for a scalar operator: a cancelled token gives a cancelled task,
    /// and an operator that throws (<c>SingleAsync</c> with no match) gives a faulted task carrying the operator's
    /// own exception and stack trace. It never throws itself, so the reflective call above cannot wrap an exception
    /// in a <see cref="TargetInvocationException"/>, which no handler written against EF Core would expect.
    /// </summary>
    private Task<TValue> ExecuteAsTask<TValue>(Expression expression, CancellationToken cancellationToken)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return Task.FromCanceled<TValue>(cancellationToken);
        }

        try
        {
            return Task.FromResult(inner.Execute<TValue>(expression));
        }
        catch (Exception exception)
        {
            return Task.FromException<TValue>(exception);
        }
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
        new TestAsyncEnumerator<T>(this.AsEnumerable().GetEnumerator(), cancellationToken);

    IQueryProvider IQueryable.Provider => new TestAsyncQueryProvider<T>(this);
}

/// <summary>
/// An <see cref="IAsyncEnumerator{T}"/> that pulls from a synchronous enumerator and, as EF Core's does, stops with a
/// cancelled step once <paramref name="cancellationToken"/> is cancelled.
/// </summary>
/// <typeparam name="T">The element type.</typeparam>
internal sealed class TestAsyncEnumerator<T>(IEnumerator<T> inner, CancellationToken cancellationToken) : IAsyncEnumerator<T>
{
    public T Current => inner.Current;

    public ValueTask<bool> MoveNextAsync() =>
        cancellationToken.IsCancellationRequested
            ? ValueTask.FromCanceled<bool>(cancellationToken)
            : ValueTask.FromResult(inner.MoveNext());

    public ValueTask DisposeAsync()
    {
        inner.Dispose();

        return ValueTask.CompletedTask;
    }
}
