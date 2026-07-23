using ArturRios.Data.Relational.Core.Entities;
using ArturRios.Data.Relational.Core.Interfaces;
using ArturRios.Output;

namespace ArturRios.Util.Test.Mock;

/// <summary>
/// In-memory implementation of <see cref="IRepository{T}"/> for use in tests.
/// Entities are stored in a backing list and identifiers are assigned sequentially starting at <c>1</c>.
/// Lookups that find no matching entity return a failed <see cref="DataOutput{T}"/> carrying an error rather than throwing.
/// </summary>
/// <typeparam name="T">The entity type handled by the repository.</typeparam>
public class FakeRepository<T> : IRepository<T> where T : Entity
{
    private readonly List<T> _items = [];
    private long _nextId = 1;

    /// <summary>Exposes the stored entities as a queryable sequence.</summary>
    /// <returns>A queryable over every stored entity.</returns>
    public IQueryable<T> Query() => _items.AsQueryable();

    /// <summary>Returns all stored entities.</summary>
    /// <returns>A successful output whose data contains every stored entity.</returns>
    public DataOutput<IEnumerable<T>> GetAll() => DataOutput<IEnumerable<T>>.New.WithData(_items);

    /// <summary>Returns the entity with the given identifier.</summary>
    /// <param name="id">The identifier to look up.</param>
    /// <returns>
    /// A successful output carrying the matching entity, or a failed output when no stored entity has that identifier.
    /// </returns>
    public DataOutput<T?> GetById(long id)
    {
        var item = _items.FirstOrDefault(x => x.Id == id);

        return item is null
            ? DataOutput<T?>.New.WithError($"Entity with Id {id} not found")
            : DataOutput<T?>.New.WithData(item);
    }

    /// <summary>Adds <paramref name="entity"/> to the store, assigning it a fresh identifier.</summary>
    /// <param name="entity">The entity to store.</param>
    /// <returns>A successful output carrying the identifier assigned to the entity.</returns>
    public DataOutput<long> Create(T entity)
    {
        entity.Id = _nextId++;

        _items.Add(entity);

        return DataOutput<long>.New.WithData(entity.Id);
    }

    /// <summary>Adds every entity in <paramref name="entities"/> to the store, assigning each a fresh identifier.</summary>
    /// <param name="entities">The entities to store.</param>
    /// <returns>A successful output carrying the identifiers assigned, in insertion order.</returns>
    public DataOutput<IEnumerable<long>> CreateRange(IEnumerable<T> entities)
    {
        var ids = new List<long>();

        foreach (var entity in entities)
        {
            entity.Id = _nextId++;

            _items.Add(entity);
            ids.Add(entity.Id);
        }

        return DataOutput<IEnumerable<long>>.New.WithData(ids);
    }

    /// <summary>Copies the writable properties of <paramref name="entity"/> onto the stored entity with the same identifier.</summary>
    /// <param name="entity">The entity carrying the new values.</param>
    /// <returns>
    /// A successful output carrying the updated stored entity, or a failed output when no stored entity has a matching identifier.
    /// </returns>
    public DataOutput<T> Update(T entity)
    {
        var existingItem = _items.FirstOrDefault(item => item.Id == entity.Id);

        if (existingItem is null)
        {
            return DataOutput<T>.New.WithError($"Entity with Id {entity.Id} not found");
        }

        CopyWritableProperties(entity, existingItem);

        return DataOutput<T>.New.WithData(existingItem);
    }

    /// <summary>Updates every entity that exists in the store, silently skipping identifiers that are not found.</summary>
    /// <param name="entities">The entities carrying the new values.</param>
    /// <returns>A successful output carrying the stored entities that were updated.</returns>
    public DataOutput<IEnumerable<T>> UpdateRange(IEnumerable<T> entities)
    {
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

        return DataOutput<IEnumerable<T>>.New.WithData(updated);
    }

    /// <summary>Removes the stored entity with the same identifier as <paramref name="entity"/>.</summary>
    /// <param name="entity">The entity to remove.</param>
    /// <returns>
    /// A successful output carrying the identifier of the removed entity, or a failed output when no stored entity has a matching identifier.
    /// </returns>
    public DataOutput<long> Delete(T entity)
    {
        var existingItem = _items.FirstOrDefault(item => item.Id == entity.Id);

        if (existingItem is null)
        {
            return DataOutput<long>.New.WithError($"Entity with Id {entity.Id} not found");
        }

        _items.Remove(existingItem);

        return DataOutput<long>.New.WithData(existingItem.Id);
    }

    /// <summary>Removes every stored entity whose identifier is in <paramref name="ids"/>.</summary>
    /// <param name="ids">The identifiers to remove.</param>
    /// <returns>A successful output carrying the identifiers that were actually removed.</returns>
    public DataOutput<IEnumerable<long>> DeleteRange(IEnumerable<long> ids)
    {
        var idSet = ids.ToHashSet();
        var entities = _items.Where(e => idSet.Contains(e.Id)).ToList();

        foreach (var entity in entities)
        {
            _items.Remove(entity);
        }

        return DataOutput<IEnumerable<long>>.New.WithData(entities.Select(e => e.Id).ToList());
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
