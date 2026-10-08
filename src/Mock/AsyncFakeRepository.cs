using ArturRios.Data.Relational.Core.Entities;
using ArturRios.Data.Relational.Core.Interfaces;
using ArturRios.Data.Relational.Core.Repositories;
using ArturRios.Output;

namespace ArturRios.Util.Test.Mock;

/// <summary>
/// In-memory implementation of <see cref="IAsyncRepository{T, TKey}"/> for use in tests.
/// Entities are stored in a backing list. Outcomes match <c>EfRepository&lt;T, TKey&gt;</c>, so code tested against the
/// fake meets the same results in production: a lookup that finds nothing succeeds with no data; an update or delete
/// that would affect no row — an unknown id, or an <see cref="IVersionedEntity"/> whose
/// <see cref="IVersionedEntity.ConcurrencyStamp"/> is stale — fails with <see cref="RelationalErrors.ConcurrencyMessage"/>
/// and changes nothing; and an update issues a new stamp, as the real context does.
/// Operations complete synchronously; each returns an already-completed <see cref="Task"/> and honors the supplied
/// <see cref="CancellationToken"/>.
/// </summary>
/// <remarks>
/// <para>How <see cref="CreateAsync"/> and <see cref="CreateRangeAsync"/> assign identifiers depends on the key type:</para>
/// <list type="bullet">
/// <item><description><see cref="long"/> and <see cref="int"/>: sequentially, starting at <c>1</c>.</description></item>
/// <item><description><see cref="Guid"/>: <see cref="Guid.NewGuid"/>.</description></item>
/// <item><description>
/// Any other key type (e.g. <see cref="string"/>): the caller assigns the identifier before creating the entity;
/// creating one whose identifier is unset, or already stored, returns a failed output.
/// </description></item>
/// </list>
/// <para>
/// When a generator applies, it overwrites any identifier the caller set. Pass an id generator to the constructor
/// to replace the default for any key type.
/// </para>
/// </remarks>
/// <typeparam name="T">The entity type handled by the repository.</typeparam>
/// <typeparam name="TKey">The entity's primary key type.</typeparam>
public class AsyncFakeRepository<T, TKey> : IAsyncRepository<T, TKey> where T : Entity<TKey> where TKey : IEquatable<TKey>
{
    private readonly FakeEntityStore<T, TKey> _store;

    /// <summary>Creates an empty repository that assigns identifiers with the default for <typeparamref name="TKey"/>.</summary>
    public AsyncFakeRepository() : this(null) { }

    /// <summary>Creates an empty repository that assigns identifiers with <paramref name="idGenerator"/>.</summary>
    /// <param name="idGenerator">
    /// Called once per created entity to produce its identifier, or <c>null</c> to use the default for
    /// <typeparamref name="TKey"/>.
    /// </param>
    public AsyncFakeRepository(Func<TKey>? idGenerator) => _store = new FakeEntityStore<T, TKey>(idGenerator);

    /// <summary>Exposes the stored entities as a queryable sequence.</summary>
    /// <returns>
    /// A queryable over every stored entity backed by an async query provider, so EF Core's async operators
    /// (<c>ToListAsync</c>, <c>FirstOrDefaultAsync</c>, <c>CountAsync</c>, …) can be composed on top of it.
    /// </returns>
    public IQueryable<T> Query() => new TestAsyncEnumerable<T>(_store.Items);

    /// <summary>Returns all stored entities.</summary>
    /// <param name="ct">A token to observe for cancellation.</param>
    /// <returns>A successful output whose data is a snapshot of every stored entity, unaffected by later writes.</returns>
    public Task<DataOutput<IEnumerable<T>>> GetAllAsync(CancellationToken ct = default) =>
        Run(_store.GetAll, ct);

    /// <summary>Returns the entity with the given identifier.</summary>
    /// <param name="id">The identifier to look up.</param>
    /// <param name="ct">A token to observe for cancellation.</param>
    /// <returns>
    /// A successful output carrying the matching entity, or a successful output with no data when no stored entity
    /// has that identifier.
    /// </returns>
    public Task<DataOutput<T?>> GetByIdAsync(TKey id, CancellationToken ct = default) =>
        Run(() => _store.GetById(id), ct);

    /// <summary>Adds <paramref name="entity"/> to the store, assigning its identifier as described on the class.</summary>
    /// <param name="entity">The entity to store.</param>
    /// <param name="ct">A token to observe for cancellation.</param>
    /// <returns>
    /// A successful output carrying the entity's identifier, or a failed output when the identifier is unset (for
    /// key types without a generator) or already stored (<see cref="RelationalErrors.UniqueViolationMessage"/>).
    /// </returns>
    public Task<DataOutput<TKey>> CreateAsync(T entity, CancellationToken ct = default) =>
        Run(() => _store.Create(entity), ct);

    /// <summary>Adds every entity in <paramref name="entities"/> to the store, assigning identifiers as described on the class.</summary>
    /// <param name="entities">The entities to store.</param>
    /// <param name="ct">A token to observe for cancellation.</param>
    /// <returns>
    /// A successful output carrying the identifiers, in insertion order, or a failed output when any identifier is unset
    /// or duplicated — in which case nothing is stored.
    /// </returns>
    public Task<DataOutput<IEnumerable<TKey>>> CreateRangeAsync(IEnumerable<T> entities, CancellationToken ct = default) =>
        Run(() => _store.CreateRange(entities), ct);

    /// <summary>Copies the writable properties of <paramref name="entity"/> onto the stored entity with the same identifier.</summary>
    /// <param name="entity">The entity carrying the new values.</param>
    /// <param name="ct">A token to observe for cancellation.</param>
    /// <returns>
    /// A successful output carrying the updated stored entity, or a failed output carrying
    /// <see cref="RelationalErrors.ConcurrencyMessage"/> when no stored entity has a matching identifier (and, for an
    /// <see cref="IVersionedEntity"/>, concurrency stamp).
    /// </returns>
    public Task<DataOutput<T>> UpdateAsync(T entity, CancellationToken ct = default) =>
        Run(() => _store.Update(entity), ct);

    /// <summary>Updates every entity in <paramref name="entities"/>, all or none: when any of them would update no stored entity
    /// (see the single-entity update), nothing is updated.</summary>
    /// <param name="entities">The entities carrying the new values.</param>
    /// <param name="ct">A token to observe for cancellation.</param>
    /// <returns>
    /// A successful output carrying the updated stored entities, or a failed output carrying
    /// <see cref="RelationalErrors.ConcurrencyMessage"/>.
    /// </returns>
    public Task<DataOutput<IEnumerable<T>>> UpdateRangeAsync(IEnumerable<T> entities, CancellationToken ct = default) =>
        Run(() => _store.UpdateRange(entities), ct);

    /// <summary>Removes the stored entity with the same identifier as <paramref name="entity"/>.</summary>
    /// <param name="entity">The entity to remove.</param>
    /// <param name="ct">A token to observe for cancellation.</param>
    /// <returns>
    /// A successful output carrying the identifier of the removed entity, or a failed output carrying
    /// <see cref="RelationalErrors.ConcurrencyMessage"/> when no stored entity has a matching identifier (and, for an
    /// <see cref="IVersionedEntity"/>, concurrency stamp).
    /// </returns>
    public Task<DataOutput<TKey>> DeleteAsync(T entity, CancellationToken ct = default) =>
        Run(() => _store.Delete(entity), ct);

    /// <summary>Removes every stored entity whose identifier is in <paramref name="ids"/>.</summary>
    /// <param name="ids">The identifiers to remove.</param>
    /// <param name="ct">A token to observe for cancellation.</param>
    /// <returns>A successful output carrying the identifiers that were actually removed.</returns>
    public Task<DataOutput<IEnumerable<TKey>>> DeleteRangeAsync(IEnumerable<TKey> ids, CancellationToken ct = default) =>
        Run(() => _store.DeleteRange(ids), ct);

    private static Task<TResult> Run<TResult>(Func<TResult> operation, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        return Task.FromResult(operation());
    }
}
