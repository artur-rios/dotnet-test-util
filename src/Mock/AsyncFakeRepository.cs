using ArturRios.Data.Relational.Core.Entities;
using ArturRios.Data.Relational.Core.Interfaces;
using ArturRios.Output;

namespace ArturRios.Util.Test.Mock;

/// <summary>
/// In-memory implementation of <see cref="IAsyncRepository{T}"/> for use in tests.
/// Entities are stored in a backing list and identifiers are assigned sequentially starting at <c>1</c>.
/// Lookups that find no matching entity return a failed <see cref="DataOutput{T}"/> carrying an error rather than throwing.
/// Operations complete synchronously; each returns a already-completed <see cref="Task"/> and honors the supplied
/// <see cref="CancellationToken"/>.
/// </summary>
/// <typeparam name="T">The entity type handled by the repository.</typeparam>
public class AsyncFakeRepository<T> : IAsyncRepository<T> where T : Entity
{
    private readonly List<T> _items = [];
    private long _nextId = 1;

    /// <summary>Exposes the stored entities as a queryable sequence.</summary>
    /// <returns>
    /// A queryable over every stored entity backed by an async query provider, so EF Core's async operators
    /// (<c>ToListAsync</c>, <c>FirstOrDefaultAsync</c>, <c>CountAsync</c>, …) can be composed on top of it.
    /// </returns>
    public IQueryable<T> Query() => new TestAsyncEnumerable<T>(_items);

    /// <summary>Returns all stored entities.</summary>
    /// <param name="ct">A token to observe for cancellation.</param>
    /// <returns>A successful output whose data contains every stored entity.</returns>
    public Task<DataOutput<IEnumerable<T>>> GetAllAsync(CancellationToken ct = new())
    {
        ct.ThrowIfCancellationRequested();

        return Task.FromResult(DataOutput<IEnumerable<T>>.New.WithData(_items));
    }

    /// <summary>Returns the entity with the given identifier.</summary>
    /// <param name="id">The identifier to look up.</param>
    /// <param name="ct">A token to observe for cancellation.</param>
    /// <returns>
    /// A successful output carrying the matching entity, or a failed output when no stored entity has that identifier.
    /// </returns>
    public Task<DataOutput<T?>> GetByIdAsync(long id, CancellationToken ct = new())
    {
        ct.ThrowIfCancellationRequested();

        var item = _items.FirstOrDefault(x => x.Id == id);

        var output = item is null
            ? DataOutput<T?>.New.WithError($"Entity with Id {id} not found")
            : DataOutput<T?>.New.WithData(item);

        return Task.FromResult(output);
    }

    /// <summary>Adds <paramref name="entity"/> to the store, assigning it a fresh identifier.</summary>
    /// <param name="entity">The entity to store.</param>
    /// <param name="ct">A token to observe for cancellation.</param>
    /// <returns>A successful output carrying the identifier assigned to the entity.</returns>
    public Task<DataOutput<long>> CreateAsync(T entity, CancellationToken ct = new())
    {
        ct.ThrowIfCancellationRequested();

        entity.Id = _nextId++;

        _items.Add(entity);

        return Task.FromResult(DataOutput<long>.New.WithData(entity.Id));
    }

    /// <summary>Adds every entity in <paramref name="entities"/> to the store, assigning each a fresh identifier.</summary>
    /// <param name="entities">The entities to store.</param>
    /// <param name="ct">A token to observe for cancellation.</param>
    /// <returns>A successful output carrying the identifiers assigned, in insertion order.</returns>
    public Task<DataOutput<IEnumerable<long>>> CreateRangeAsync(IEnumerable<T> entities, CancellationToken ct = new())
    {
        ct.ThrowIfCancellationRequested();

        var ids = new List<long>();

        foreach (var entity in entities)
        {
            entity.Id = _nextId++;

            _items.Add(entity);
            ids.Add(entity.Id);
        }

        return Task.FromResult(DataOutput<IEnumerable<long>>.New.WithData(ids));
    }

    /// <summary>Copies the writable properties of <paramref name="entity"/> onto the stored entity with the same identifier.</summary>
    /// <param name="entity">The entity carrying the new values.</param>
    /// <param name="ct">A token to observe for cancellation.</param>
    /// <returns>
    /// A successful output carrying the updated stored entity, or a failed output when no stored entity has a matching identifier.
    /// </returns>
    public Task<DataOutput<T>> UpdateAsync(T entity, CancellationToken ct = new())
    {
        ct.ThrowIfCancellationRequested();

        var existingItem = _items.FirstOrDefault(item => item.Id == entity.Id);

        if (existingItem is null)
        {
            return Task.FromResult(DataOutput<T>.New.WithError($"Entity with Id {entity.Id} not found"));
        }

        CopyWritableProperties(entity, existingItem);

        return Task.FromResult(DataOutput<T>.New.WithData(existingItem));
    }

    /// <summary>Updates every entity that exists in the store, silently skipping identifiers that are not found.</summary>
    /// <param name="entities">The entities carrying the new values.</param>
    /// <param name="ct">A token to observe for cancellation.</param>
    /// <returns>A successful output carrying the stored entities that were updated.</returns>
    public Task<DataOutput<IEnumerable<T>>> UpdateRangeAsync(IEnumerable<T> entities, CancellationToken ct = new())
    {
        ct.ThrowIfCancellationRequested();

        var updated = new List<T>();

        foreach (var entity in entities)
        {
            var existingItem = _items.FirstOrDefault(item => item.Id == entity.Id);

            if (existingItem is null)
            {
                // Entities that do not exist are silently skipped.
                continue;
            }

            CopyWritableProperties(entity, existingItem);
            updated.Add(existingItem);
        }

        return Task.FromResult(DataOutput<IEnumerable<T>>.New.WithData(updated));
    }

    /// <summary>Removes the stored entity with the same identifier as <paramref name="entity"/>.</summary>
    /// <param name="entity">The entity to remove.</param>
    /// <param name="ct">A token to observe for cancellation.</param>
    /// <returns>
    /// A successful output carrying the identifier of the removed entity, or a failed output when no stored entity has a matching identifier.
    /// </returns>
    public Task<DataOutput<long>> DeleteAsync(T entity, CancellationToken ct = new())
    {
        ct.ThrowIfCancellationRequested();

        var existingItem = _items.FirstOrDefault(item => item.Id == entity.Id);

        if (existingItem is null)
        {
            return Task.FromResult(DataOutput<long>.New.WithError($"Entity with Id {entity.Id} not found"));
        }

        _items.Remove(existingItem);

        return Task.FromResult(DataOutput<long>.New.WithData(existingItem.Id));
    }

    /// <summary>Removes every stored entity whose identifier is in <paramref name="ids"/>.</summary>
    /// <param name="ids">The identifiers to remove.</param>
    /// <param name="ct">A token to observe for cancellation.</param>
    /// <returns>A successful output carrying the identifiers that were actually removed.</returns>
    public Task<DataOutput<IEnumerable<long>>> DeleteRangeAsync(IEnumerable<long> ids, CancellationToken ct = new())
    {
        ct.ThrowIfCancellationRequested();

        var idSet = ids.ToHashSet();
        var entities = _items.Where(e => idSet.Contains(e.Id)).ToList();

        foreach (var entity in entities)
        {
            _items.Remove(entity);
        }

        return Task.FromResult(DataOutput<IEnumerable<long>>.New.WithData([.. entities.Select(e => e.Id)]));
    }

    private static void CopyWritableProperties(T source, T target)
    {
        foreach (var prop in typeof(T).GetProperties())
        {
            if (!prop.CanWrite || prop.Name == nameof(Entity.Id))
            {
                continue;
            }

            prop.SetValue(target, prop.GetValue(source));
        }
    }
}
