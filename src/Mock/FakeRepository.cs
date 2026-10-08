using ArturRios.Data.Relational.Core.Entities;
using ArturRios.Data.Relational.Core.Interfaces;
using ArturRios.Data.Relational.Core.Repositories;
using ArturRios.Output;

namespace ArturRios.Util.Test.Mock;

/// <summary>
/// In-memory implementation of <see cref="IRepository{T, TKey}"/> for use in tests.
/// Entities are stored in a backing list. Outcomes match <c>EfRepository&lt;T, TKey&gt;</c>, so code tested against the
/// fake meets the same results in production: a lookup that finds nothing succeeds with no data; an update or delete
/// that would affect no row — an unknown id, or an <see cref="IVersionedEntity"/> whose
/// <see cref="IVersionedEntity.ConcurrencyStamp"/> is stale — fails with <see cref="RelationalErrors.ConcurrencyMessage"/>
/// and changes nothing; and an update issues a new stamp, as the real context does.
/// </summary>
/// <remarks>
/// <para>How <see cref="Create"/> and <see cref="CreateRange"/> assign identifiers depends on the key type:</para>
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
public class FakeRepository<T, TKey> : IRepository<T, TKey> where T : Entity<TKey> where TKey : IEquatable<TKey>
{
    private readonly FakeEntityStore<T, TKey> _store;

    /// <summary>Creates an empty repository that assigns identifiers with the default for <typeparamref name="TKey"/>.</summary>
    public FakeRepository() : this(null) { }

    /// <summary>Creates an empty repository that assigns identifiers with <paramref name="idGenerator"/>.</summary>
    /// <param name="idGenerator">
    /// Called once per created entity to produce its identifier, or <c>null</c> to use the default for
    /// <typeparamref name="TKey"/>.
    /// </param>
    public FakeRepository(Func<TKey>? idGenerator) => _store = new FakeEntityStore<T, TKey>(idGenerator);

    /// <summary>Exposes the stored entities as a queryable sequence.</summary>
    /// <returns>A queryable over every stored entity.</returns>
    public IQueryable<T> Query() => _store.Items.AsQueryable();

    /// <summary>Returns all stored entities.</summary>
    /// <returns>A successful output whose data is a snapshot of every stored entity, unaffected by later writes.</returns>
    public DataOutput<IEnumerable<T>> GetAll() => _store.GetAll();

    /// <summary>Returns the entity with the given identifier.</summary>
    /// <param name="id">The identifier to look up.</param>
    /// <returns>
    /// A successful output carrying the matching entity, or a successful output with no data when no stored entity
    /// has that identifier.
    /// </returns>
    public DataOutput<T?> GetById(TKey id) => _store.GetById(id);

    /// <summary>Adds <paramref name="entity"/> to the store, assigning its identifier as described on the class.</summary>
    /// <param name="entity">The entity to store.</param>
    /// <returns>
    /// A successful output carrying the entity's identifier, or a failed output when the identifier is unset (for
    /// key types without a generator) or already stored (<see cref="RelationalErrors.UniqueViolationMessage"/>).
    /// </returns>
    public DataOutput<TKey> Create(T entity) => _store.Create(entity);

    /// <summary>Adds every entity in <paramref name="entities"/> to the store, assigning identifiers as described on the class.</summary>
    /// <param name="entities">The entities to store.</param>
    /// <returns>
    /// A successful output carrying the identifiers, in insertion order, or a failed output when any identifier is unset
    /// or duplicated — in which case nothing is stored.
    /// </returns>
    public DataOutput<IEnumerable<TKey>> CreateRange(IEnumerable<T> entities) => _store.CreateRange(entities);

    /// <summary>Copies the writable properties of <paramref name="entity"/> onto the stored entity with the same identifier.</summary>
    /// <param name="entity">The entity carrying the new values.</param>
    /// <returns>
    /// A successful output carrying the updated stored entity, or a failed output carrying
    /// <see cref="RelationalErrors.ConcurrencyMessage"/> when no stored entity has a matching identifier (and, for an
    /// <see cref="IVersionedEntity"/>, concurrency stamp).
    /// </returns>
    public DataOutput<T> Update(T entity) => _store.Update(entity);

    /// <summary>Updates every entity in <paramref name="entities"/>, all or none: when any of them would update no stored entity
    /// (see the single-entity update), nothing is updated.</summary>
    /// <param name="entities">The entities carrying the new values.</param>
    /// <returns>
    /// A successful output carrying the updated stored entities, or a failed output carrying
    /// <see cref="RelationalErrors.ConcurrencyMessage"/>.
    /// </returns>
    public DataOutput<IEnumerable<T>> UpdateRange(IEnumerable<T> entities) => _store.UpdateRange(entities);

    /// <summary>Removes the stored entity with the same identifier as <paramref name="entity"/>.</summary>
    /// <param name="entity">The entity to remove.</param>
    /// <returns>
    /// A successful output carrying the identifier of the removed entity, or a failed output carrying
    /// <see cref="RelationalErrors.ConcurrencyMessage"/> when no stored entity has a matching identifier (and, for an
    /// <see cref="IVersionedEntity"/>, concurrency stamp).
    /// </returns>
    public DataOutput<TKey> Delete(T entity) => _store.Delete(entity);

    /// <summary>Removes every stored entity whose identifier is in <paramref name="ids"/>.</summary>
    /// <param name="ids">The identifiers to remove.</param>
    /// <returns>A successful output carrying the identifiers that were actually removed.</returns>
    public DataOutput<IEnumerable<TKey>> DeleteRange(IEnumerable<TKey> ids) => _store.DeleteRange(ids);
}
