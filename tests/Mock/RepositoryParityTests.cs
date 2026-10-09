using ArturRios.Data.Relational.Core.Configuration;
using ArturRios.Data.Relational.Core.Entities;
using ArturRios.Data.Relational.Core.Interfaces;
using ArturRios.Data.Relational.Core.Repositories;
using ArturRios.Output;
using ArturRios.Util.Test.Mock;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace ArturRios.Util.Test.Tests.Mock;

/// <summary>
/// Runs the same scenarios against the fakes and against the real <see cref="EfRepository{T, TKey}"/> (over an
/// in-memory SQLite database), so a test that passes against a fake passes for the reason it would in production.
/// Where the two disagree, a handler tested against the fake is green while the deployed one fails.
/// </summary>
[Trait("Category", "Unit")]
public sealed class RepositoryParityTests : IDisposable
{
    private readonly SqliteConnection _connection = new("DataSource=:memory:");

    public RepositoryParityTests()
    {
        _connection.Open();

        using var context = NewContext();
        context.Database.EnsureCreated();
    }

    public void Dispose() => _connection.Dispose();

    private sealed class ParityContext(DbContextOptions options) : BaseDbContext(options)
    {
        public DbSet<Person> People => Set<Person>();
        public DbSet<Account> Accounts => Set<Account>();
    }

    private ParityContext NewContext() =>
        new(new DbContextOptionsBuilder<ParityContext>().UseSqlite(_connection).Options);

    /// <summary>Both implementations, each starting empty.</summary>
    private IEnumerable<(string Name, IRepository<T, long> Repository)> Repositories<T>() where T : Entity<long>
    {
        yield return ("fake", new FakeRepository<T, long>());
        yield return ("ef", new PerCallEfRepository<T>(NewContext));
    }

    private IEnumerable<(string Name, IAsyncRepository<T, long> Repository)> AsyncRepositories<T>() where T : Entity<long>
    {
        yield return ("fake", new AsyncFakeRepository<T, long>());
        yield return ("ef", new PerCallEfRepository<T>(NewContext));
    }

    /// <summary>
    /// The real repository with a fresh context per call, as each call would get from its own request scope in a
    /// deployed service — so an entity created by one call is not still tracked when the next one runs.
    /// </summary>
    private sealed class PerCallEfRepository<T>(Func<ParityContext> newContext)
        : IRepository<T, long>, IAsyncRepository<T, long> where T : Entity<long>
    {
        private TResult Run<TResult>(Func<EfRepository<T, long>, TResult> call)
        {
            using var context = newContext();

            return call(new EfRepository<T, long>(context));
        }

        private async Task<TResult> RunAsync<TResult>(Func<EfRepository<T, long>, Task<TResult>> call)
        {
            await using var context = newContext();

            return await call(new EfRepository<T, long>(context));
        }

        // Left undisposed: the query is materialized by the caller after this returns.
        public IQueryable<T> Query() => new EfRepository<T, long>(newContext()).Query();
        public DataOutput<IEnumerable<T>> GetAll() => Run(r => r.GetAll());
        public DataOutput<T?> GetById(long id) => Run(r => r.GetById(id));
        public DataOutput<long> Create(T entity) => Run(r => r.Create(entity));
        public DataOutput<IEnumerable<long>> CreateRange(IEnumerable<T> entities) => Run(r => r.CreateRange(entities));
        public DataOutput<T> Update(T entity) => Run(r => r.Update(entity));
        public DataOutput<IEnumerable<T>> UpdateRange(IEnumerable<T> entities) => Run(r => r.UpdateRange(entities));
        public DataOutput<long> Delete(T entity) => Run(r => r.Delete(entity));
        public DataOutput<IEnumerable<long>> DeleteRange(IEnumerable<long> ids) => Run(r => r.DeleteRange(ids));

        public Task<DataOutput<IEnumerable<T>>> GetAllAsync(CancellationToken ct = default) =>
            RunAsync(r => r.GetAllAsync(ct));
        public Task<DataOutput<T?>> GetByIdAsync(long id, CancellationToken ct = default) =>
            RunAsync(r => r.GetByIdAsync(id, ct));
        public Task<DataOutput<long>> CreateAsync(T entity, CancellationToken ct = default) =>
            RunAsync(r => r.CreateAsync(entity, ct));
        public Task<DataOutput<IEnumerable<long>>> CreateRangeAsync(IEnumerable<T> entities, CancellationToken ct = default) =>
            RunAsync(r => r.CreateRangeAsync(entities, ct));
        public Task<DataOutput<T>> UpdateAsync(T entity, CancellationToken ct = default) =>
            RunAsync(r => r.UpdateAsync(entity, ct));
        public Task<DataOutput<IEnumerable<T>>> UpdateRangeAsync(IEnumerable<T> entities, CancellationToken ct = default) =>
            RunAsync(r => r.UpdateRangeAsync(entities, ct));
        public Task<DataOutput<long>> DeleteAsync(T entity, CancellationToken ct = default) =>
            RunAsync(r => r.DeleteAsync(entity, ct));
        public Task<DataOutput<IEnumerable<long>>> DeleteRangeAsync(IEnumerable<long> ids, CancellationToken ct = default) =>
            RunAsync(r => r.DeleteRangeAsync(ids, ct));
    }

    private static void AssertFailedWith<TData>(string name, string error, DataOutput<TData> output)
    {
        Assert.False(output.Success, name);
        Assert.Equal([error], output.Errors);
    }

    [Fact]
    public void GivenAnUnknownId_WhenFetchingById_ThenBothSucceedWithNoData()
    {
        foreach (var (name, repository) in Repositories<Person>())
        {
            var result = repository.GetById(999);

            Assert.True(result.Success, name);
            Assert.Null(result.Data);
        }
    }

    [Fact]
    public async Task GivenAnUnknownId_WhenFetchingByIdAsynchronously_ThenBothSucceedWithNoData()
    {
        foreach (var (name, repository) in AsyncRepositories<Person>())
        {
            var result = await repository.GetByIdAsync(999, TestContext.Current.CancellationToken);

            Assert.True(result.Success, name);
            Assert.Null(result.Data);
        }
    }

    [Fact]
    public void GivenAFetchedList_WhenEntitiesAreCreatedAfterwards_ThenTheListIsASnapshot()
    {
        foreach (var (name, repository) in Repositories<Person>())
        {
            repository.Create(new Person { Name = "Ann" });

            var all = repository.GetAll().Data!;

            repository.Create(new Person { Name = "Bob" });

            Assert.True(all.Count() == 1, name);
        }
    }

    [Fact]
    public void GivenAFetchedList_WhenEachEntityIsDeletedWhileEnumerating_ThenEveryDeleteSucceeds()
    {
        foreach (var (name, repository) in Repositories<Person>())
        {
            repository.CreateRange([new Person { Name = "Ann" }, new Person { Name = "Bob" }]);

            foreach (var person in repository.GetAll().Data!)
            {
                Assert.True(repository.Delete(person).Success, name);
            }

            Assert.True(!repository.Query().Any(), name);
        }
    }

    [Fact]
    public void GivenAnUnknownId_WhenUpdating_ThenBothFailWithAConcurrencyConflict()
    {
        foreach (var (name, repository) in Repositories<Person>())
        {
            AssertFailedWith(name, RelationalErrors.ConcurrencyMessage, repository.Update(new Person { Id = 999 }));
        }
    }

    [Fact]
    public void GivenAnUnknownId_WhenDeleting_ThenBothFailWithAConcurrencyConflict()
    {
        foreach (var (name, repository) in Repositories<Person>())
        {
            AssertFailedWith(name, RelationalErrors.ConcurrencyMessage, repository.Delete(new Person { Id = 999 }));
        }
    }

    [Fact]
    public async Task GivenARangeContainingAnUnknownEntity_WhenUpdating_ThenBothFailAndUpdateNothing()
    {
        foreach (var (name, repository) in AsyncRepositories<Person>())
        {
            var id = (await repository.CreateAsync(new Person { Name = "Ann" }, TestContext.Current.CancellationToken)).Data;

            var result = await repository.UpdateRangeAsync(
                [new Person { Id = id, Name = "Ann Smith" }, new Person { Id = 999, Name = "Ghost" }],
                    TestContext.Current.CancellationToken);

            AssertFailedWith(name, RelationalErrors.ConcurrencyMessage, result);
            Assert.Equal("Ann", repository.Query().AsNoTracking().Single(p => p.Id == id).Name);
        }
    }

    [Fact]
    public void GivenAStaleConcurrencyStamp_WhenUpdating_ThenBothFailWithAConcurrencyConflict()
    {
        foreach (var (name, repository) in Repositories<Account>())
        {
            var id = repository.Create(new Account { Owner = "Ann" }).Data;

            // A new instance carries a fresh stamp, as an entity rebuilt from a request without the stamp would.
            var result = repository.Update(new Account { Id = id, Owner = "Mallory" });

            AssertFailedWith(name, RelationalErrors.ConcurrencyMessage, result);
            Assert.Equal("Ann", repository.Query().AsNoTracking().Single(a => a.Id == id).Owner);
        }
    }

    [Fact]
    public void GivenTheCurrentConcurrencyStamp_WhenUpdating_ThenBothSucceedAndIssueANewStamp()
    {
        foreach (var (name, repository) in Repositories<Account>())
        {
            var created = new Account { Owner = "Ann" };
            var id = repository.Create(created).Data;
            var stamp = repository.Query().AsNoTracking().Single(a => a.Id == id).ConcurrencyStamp;

            var update = new Account { Id = id, Owner = "Ann Smith", ConcurrencyStamp = stamp };
            var result = repository.Update(update);

            Assert.True(result.Success, name);

            var stored = repository.Query().AsNoTracking().Single(a => a.Id == id);
            Assert.Equal("Ann Smith", stored.Owner);
            Assert.NotEqual(stamp, stored.ConcurrencyStamp);
            Assert.Equal(stored.ConcurrencyStamp, update.ConcurrencyStamp);
        }
    }

    [Fact]
    public void GivenAStaleConcurrencyStamp_WhenDeleting_ThenBothFailWithAConcurrencyConflict()
    {
        foreach (var (name, repository) in Repositories<Account>())
        {
            var id = repository.Create(new Account { Owner = "Ann" }).Data;

            var result = repository.Delete(new Account { Id = id });

            AssertFailedWith(name, RelationalErrors.ConcurrencyMessage, result);
            Assert.True(repository.Query().Any(a => a.Id == id), name);
        }
    }
}
