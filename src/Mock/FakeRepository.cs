using ArturRios.Data;
using ArturRios.Data.Interfaces;

namespace ArturRios.Util.Test.Mock;

/// <summary>
/// In-memory implementation of <see cref="ICrudRepository{T}"/> and <see cref="IRangeRepository{T}"/> for use in tests.
/// Entities are stored in a backing list and identifiers are assigned sequentially starting at <c>1</c>.
/// </summary>
/// <typeparam name="T">The entity type handled by the repository.</typeparam>
public class FakeRepository<T> : ICrudRepository<T>, IRangeRepository<T> where T : Entity
{
    private readonly List<T> _items = [];
    private int _nextId = 1;

    /// <summary>Adds <paramref name="entity"/> to the store, assigning it a fresh identifier.</summary>
    /// <param name="entity">The entity to store.</param>
    /// <returns>The identifier assigned to the entity.</returns>
    public int Create(T entity)
    {
        entity.Id = _nextId++;

        _items.Add(entity);

        return entity.Id;
    }

    /// <summary>Returns all stored entities as a queryable sequence.</summary>
    public IQueryable<T> GetAll() => _items.AsQueryable();

    /// <summary>Returns the entity with the given identifier, or <c>null</c> when none matches.</summary>
    /// <param name="id">The identifier to look up.</param>
    public T? GetById(int id) => _items.FirstOrDefault(x => x.Id == id);

    /// <summary>Copies the writable properties of <paramref name="entity"/> onto the stored entity with the same identifier.</summary>
    /// <param name="entity">The entity carrying the new values.</param>
    /// <returns>The updated stored entity.</returns>
    /// <exception cref="KeyNotFoundException">Thrown when no stored entity has a matching identifier.</exception>
    public T Update(T entity)
    {
        var existingItem = _items.FirstOrDefault(item => item.Id == entity.Id);

        if (existingItem is null)
        {
            throw new KeyNotFoundException($"Entity with Id {entity.Id} not found");
        }

        var properties = typeof(T).GetProperties();

        foreach (var prop in properties)
        {
            if (!prop.CanWrite || prop.Name == nameof(Entity.Id))
            {
                continue;
            }

            var value = prop.GetValue(entity);
            prop.SetValue(existingItem, value);
        }

        return existingItem;
    }

    /// <summary>Removes the stored entity with the same identifier as <paramref name="entity"/>.</summary>
    /// <param name="entity">The entity to remove.</param>
    /// <returns>The identifier of the removed entity.</returns>
    /// <exception cref="KeyNotFoundException">Thrown when no stored entity has a matching identifier.</exception>
    public int Delete(T entity)
    {
        var existingItem = _items.FirstOrDefault(item => item.Id == entity.Id);

        if (existingItem is null)
        {
            throw new KeyNotFoundException($"Entity with Id {entity.Id} not found");
        }

        _items.Remove(existingItem);

        return existingItem.Id;
    }

    /// <summary>Updates every entity that exists in the store, skipping identifiers that are not found.</summary>
    /// <param name="entities">The entities carrying the new values.</param>
    /// <returns>The stored entities that were updated.</returns>
    public IEnumerable<T> UpdateRange(List<T> entities)
    {
        var updated = new List<T>();

        foreach (var entity in entities)
        {
            try
            {
                updated.Add(Update(entity));
            }
            catch (KeyNotFoundException)
            {
                // Entities that do not exist are silently skipped.
            }
        }

        return updated;
    }

    /// <summary>Removes every stored entity whose identifier is in <paramref name="ids"/>.</summary>
    /// <param name="ids">The identifiers to remove.</param>
    /// <returns>The identifiers that were actually removed.</returns>
    public IEnumerable<int> DeleteRange(List<int> ids)
    {
        var entities = _items.Where(e => ids.Contains(e.Id)).ToList();

        foreach (var entity in entities)
        {
            _items.Remove(entity);
        }

        return entities.Select(e => e.Id).ToList();
    }
}
