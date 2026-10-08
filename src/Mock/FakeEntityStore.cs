using ArturRios.Data.Relational.Core.Entities;
using ArturRios.Data.Relational.Core.Repositories;
using ArturRios.Output;

namespace ArturRios.Util.Test.Mock;

/// <summary>
/// The in-memory storage and semantics shared by <see cref="FakeRepository{T, TKey}"/> and
/// <see cref="AsyncFakeRepository{T, TKey}"/>. Outcomes follow <see cref="EfRepository{T, TKey}"/>: an unknown id is a
/// successful lookup with no data, writes that would affect no row (or a row whose <see cref="IVersionedEntity"/>
/// stamp has moved on) fail with <see cref="RelationalErrors.ConcurrencyMessage"/> and change nothing, and an id
/// that is already stored fails with <see cref="RelationalErrors.UniqueViolationMessage"/>.
/// </summary>
/// <typeparam name="T">The entity type.</typeparam>
/// <typeparam name="TKey">The entity's primary key type.</typeparam>
internal sealed class FakeEntityStore<T, TKey> where T : Entity<TKey> where TKey : IEquatable<TKey>
{
    private static readonly EqualityComparer<TKey> KeyComparer = EqualityComparer<TKey>.Default;

    private readonly Func<TKey>? _idGenerator;

    /// <summary>Creates a store that assigns identifiers with <paramref name="idGenerator"/>.</summary>
    /// <param name="idGenerator">
    /// The identifier generator, or <c>null</c> to use the default for <typeparamref name="TKey"/>
    /// (see <see cref="DefaultIdGenerator"/>).
    /// </param>
    public FakeEntityStore(Func<TKey>? idGenerator) => _idGenerator = idGenerator ?? DefaultIdGenerator();

    /// <summary>The stored entities, in insertion order.</summary>
    public List<T> Items { get; } = [];

    // A copy, as the real repository materializes a new list: handing out Items itself let a later write change
    // a result the caller already holds, and made deleting while enumerating it throw.
    public DataOutput<IEnumerable<T>> GetAll() => DataOutput<IEnumerable<T>>.New.WithData(Items.ToList());

    // No match is a successful lookup with no data, as the repository contract and EfRepository define it.
    public DataOutput<T?> GetById(TKey id) => DataOutput<T?>.New.WithData(Find(id));

    public DataOutput<TKey> Create(T entity)
    {
        var error = AssignId(entity, []);

        if (error is not null)
        {
            return DataOutput<TKey>.New.WithError(error);
        }

        Items.Add(entity);

        return DataOutput<TKey>.New.WithData(entity.Id);
    }

    public DataOutput<IEnumerable<TKey>> CreateRange(IEnumerable<T> entities)
    {
        // Assign and validate every id before storing anything, so a rejected range leaves the store untouched.
        var pending = new List<T>();
        var pendingIds = new HashSet<TKey>(KeyComparer);

        foreach (var entity in entities)
        {
            var error = AssignId(entity, pendingIds);

            if (error is not null)
            {
                return DataOutput<IEnumerable<TKey>>.New.WithError(error);
            }

            pending.Add(entity);
            pendingIds.Add(entity.Id);
        }

        Items.AddRange(pending);

        return DataOutput<IEnumerable<TKey>>.New.WithData(pending.Select(e => e.Id).ToList());
    }

    public DataOutput<T> Update(T entity)
    {
        var existingItem = FindWritable(entity);

        if (existingItem is null)
        {
            return DataOutput<T>.New.WithError(RelationalErrors.ConcurrencyMessage);
        }

        Apply(entity, existingItem);

        return DataOutput<T>.New.WithData(existingItem);
    }

    public DataOutput<IEnumerable<T>> UpdateRange(IEnumerable<T> entities)
    {
        // Check every entity before changing any, so a range with an unknown or stale entity fails as a whole and
        // leaves the store untouched, as the real repository's single SaveChanges does.
        var pairs = new List<(T Entity, T Existing)>();

        foreach (var entity in entities)
        {
            var existingItem = FindWritable(entity);

            if (existingItem is null)
            {
                return DataOutput<IEnumerable<T>>.New.WithError(RelationalErrors.ConcurrencyMessage);
            }

            pairs.Add((entity, existingItem));
        }

        foreach (var (entity, existingItem) in pairs)
        {
            Apply(entity, existingItem);
        }

        return DataOutput<IEnumerable<T>>.New.WithData(pairs.Select(pair => pair.Existing).ToList());
    }

    public DataOutput<TKey> Delete(T entity)
    {
        var existingItem = FindWritable(entity);

        if (existingItem is null)
        {
            return DataOutput<TKey>.New.WithError(RelationalErrors.ConcurrencyMessage);
        }

        Items.Remove(existingItem);

        return DataOutput<TKey>.New.WithData(existingItem.Id);
    }

    public DataOutput<IEnumerable<TKey>> DeleteRange(IEnumerable<TKey> ids)
    {
        var idSet = ids.ToHashSet(KeyComparer);
        var entities = Items.Where(e => idSet.Contains(e.Id)).ToList();

        foreach (var entity in entities)
        {
            Items.Remove(entity);
        }

        return DataOutput<IEnumerable<TKey>>.New.WithData(entities.Select(e => e.Id).ToList());
    }

    private T? Find(TKey id) => Items.FirstOrDefault(item => KeyComparer.Equals(item.Id, id));

    /// <summary>
    /// The stored entity <paramref name="entity"/> may update or delete: the one with its id, provided that, for an
    /// <see cref="IVersionedEntity"/>, it carries the stored <see cref="IVersionedEntity.ConcurrencyStamp"/>. The real
    /// repository's UPDATE/DELETE matches on the id and the stamp, and affecting no row is a concurrency conflict.
    /// </summary>
    /// <returns>The stored entity, or <c>null</c> when the write would affect no row.</returns>
    private T? FindWritable(T entity)
    {
        var existingItem = Find(entity.Id);

        if (existingItem is IVersionedEntity stored && entity is IVersionedEntity incoming &&
            stored.ConcurrencyStamp != incoming.ConcurrencyStamp)
        {
            return null;
        }

        return existingItem;
    }

    /// <summary>
    /// Copies <paramref name="source"/> onto the stored <paramref name="target"/> and, for an
    /// <see cref="IVersionedEntity"/>, issues a new stamp on both, as the real context does on every update.
    /// </summary>
    private static void Apply(T source, T target)
    {
        CopyWritableProperties(source, target);

        if (target is not IVersionedEntity versioned)
        {
            return;
        }

        versioned.ConcurrencyStamp = Guid.NewGuid();

        if (source is IVersionedEntity incoming)
        {
            incoming.ConcurrencyStamp = versioned.ConcurrencyStamp;
        }
    }

    /// <summary>
    /// Gives <paramref name="entity"/> its identifier: a generated one when the store has a generator (overwriting
    /// any value the caller set), otherwise the caller-assigned one, which must be set and not yet stored.
    /// </summary>
    /// <returns><c>null</c> on success, otherwise the error message.</returns>
    private string? AssignId(T entity, HashSet<TKey> pendingIds)
    {
        if (_idGenerator is not null)
        {
            entity.Id = _idGenerator();
        }
        else if (entity.Id is null || KeyComparer.Equals(entity.Id, default!))
        {
            return $"Entity Id must be assigned before it is created: there is no default id generator for {typeof(TKey).Name} keys";
        }

        return Find(entity.Id) is not null || pendingIds.Contains(entity.Id)
            ? RelationalErrors.UniqueViolationMessage
            : null;
    }

    /// <summary>
    /// The generator used when none is supplied: sequential from <c>1</c> for <see cref="long"/> and <see cref="int"/>
    /// keys, <see cref="Guid.NewGuid"/> for <see cref="Guid"/> keys, and none for any other key type.
    /// </summary>
    private static Func<TKey>? DefaultIdGenerator()
    {
        if (typeof(TKey) == typeof(long))
        {
            long next = 0;

            return () => (TKey)(object)++next;
        }

        if (typeof(TKey) == typeof(int))
        {
            var next = 0;

            return () => (TKey)(object)++next;
        }

        if (typeof(TKey) == typeof(Guid))
        {
            return () => (TKey)(object)Guid.NewGuid();
        }

        return null;
    }

    private static void CopyWritableProperties(T source, T target)
    {
        foreach (var prop in typeof(T).GetProperties())
        {
            if (!prop.CanWrite || prop.Name == nameof(Entity<TKey>.Id))
            {
                continue;
            }

            prop.SetValue(target, prop.GetValue(source));
        }
    }
}
