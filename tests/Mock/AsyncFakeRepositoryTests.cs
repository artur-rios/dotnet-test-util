using ArturRios.Data.Relational.Core.Repositories;
using ArturRios.Util.Test.Mock;
using Microsoft.EntityFrameworkCore;

namespace ArturRios.Util.Test.Tests.Mock;

[Trait("Category", "Unit")]
public class AsyncFakeRepositoryTests
{
    private static AsyncFakeRepository<Person, long> NewRepository() => new();

    [Fact]
    public async Task GivenAnEmptyFake_WhenCreatingEntitiesAsynchronously_ThenIdsAreAssignedSequentiallyFromOne()
    {
        var repository = NewRepository();

        var firstResult = await repository.CreateAsync(new Person { Name = "Ann" }, TestContext.Current.CancellationToken);
        var secondResult = await repository.CreateAsync(new Person { Name = "Bob" }, TestContext.Current.CancellationToken);

        Assert.True(firstResult.Success);
        Assert.Equal(1, firstResult.Data);
        Assert.Equal(2, secondResult.Data);
    }

    [Fact]
    public async Task GivenAStoredEntity_WhenFetchingItByIdAsynchronously_ThenItComesBack()
    {
        var repository = NewRepository();
        var id = (await repository.CreateAsync(new Person { Name = "Ann", Age = 30 }, TestContext.Current.CancellationToken)).Data;

        var found = await repository.GetByIdAsync(id, TestContext.Current.CancellationToken);

        Assert.True(found.Success);
        Assert.NotNull(found.Data);
        Assert.Equal("Ann", found.Data.Name);
    }

    [Fact]
    public async Task GivenAnUnknownId_WhenFetchingByIdAsynchronously_ThenASuccessfulOutputWithNoDataComesBack()
    {
        var repository = NewRepository();

        var result = await repository.GetByIdAsync(999, TestContext.Current.CancellationToken);

        Assert.True(result.Success);
        Assert.Null(result.Data);
    }

    [Fact]
    public async Task GivenStoredEntities_WhenFetchingAllAsynchronously_ThenEveryOneComesBack()
    {
        var repository = NewRepository();
        await repository.CreateAsync(new Person { Name = "Ann" }, TestContext.Current.CancellationToken);
        await repository.CreateAsync(new Person { Name = "Bob" }, TestContext.Current.CancellationToken);

        var all = await repository.GetAllAsync(TestContext.Current.CancellationToken);

        Assert.True(all.Success);
        Assert.Equal(2, all.Data!.Count());
    }

    [Fact]
    public async Task GivenStoredEntities_WhenQueryingAsynchronously_ThenEveryOneComesBack()
    {
        var repository = NewRepository();
        await repository.CreateAsync(new Person { Name = "Ann" }, TestContext.Current.CancellationToken);
        await repository.CreateAsync(new Person { Name = "Bob" }, TestContext.Current.CancellationToken);

        var all = repository.Query().ToList();

        Assert.Equal(2, all.Count);
    }

    [Fact]
    public async Task GivenTheFakeQuery_WhenMaterialisedWithToListAsync_ThenItIsSupported()
    {
        var repository = NewRepository();
        await repository.CreateAsync(new Person { Name = "Ann", Age = 30 }, TestContext.Current.CancellationToken);
        await repository.CreateAsync(new Person { Name = "Bob", Age = 25 }, TestContext.Current.CancellationToken);

        var adults = await repository.Query().Where(p => p.Age >= 18).ToListAsync(TestContext.Current.CancellationToken);

        Assert.Equal(2, adults.Count);
    }

    [Fact]
    public async Task GivenTheFakeQuery_WhenMaterialisedWithFirstOrDefaultAsync_ThenItIsSupported()
    {
        var repository = NewRepository();
        await repository.CreateAsync(new Person { Name = "Ann" }, TestContext.Current.CancellationToken);
        await repository.CreateAsync(new Person { Name = "Bob" }, TestContext.Current.CancellationToken);

        var bob = await repository.Query().FirstOrDefaultAsync(p => p.Name == "Bob", TestContext.Current.CancellationToken);

        Assert.NotNull(bob);
        Assert.Equal("Bob", bob.Name);
    }

    [Fact]
    public async Task GivenTheFakeQuery_WhenMaterialisedWithCountAsync_ThenItIsSupported()
    {
        var repository = NewRepository();
        await repository.CreateAsync(new Person { Name = "Ann", Age = 30 }, TestContext.Current.CancellationToken);
        await repository.CreateAsync(new Person { Name = "Bob", Age = 15 }, TestContext.Current.CancellationToken);

        var adultCount = await repository.Query().CountAsync(p => p.Age >= 18, TestContext.Current.CancellationToken);

        Assert.Equal(1, adultCount);
    }

    [Fact]
    public async Task GivenAStoredEntity_WhenUpdatedAsynchronously_ThenWritablePropertiesAreCopiedAndTheIdIsKept()
    {
        var repository = NewRepository();
        var id = (await repository.CreateAsync(new Person { Name = "Ann", Age = 30 }, TestContext.Current.CancellationToken)).Data;

        var result = await repository.UpdateAsync(new Person { Id = id, Name = "Ann Updated", Age = 31 },
            TestContext.Current.CancellationToken);

        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        Assert.Equal(id, result.Data.Id);
        Assert.Equal("Ann Updated", result.Data.Name);
        Assert.Equal(31, result.Data.Age);
        Assert.Equal("Ann Updated", (await repository.GetByIdAsync(id, TestContext.Current.CancellationToken)).Data!.Name);
    }

    [Fact]
    public async Task GivenAnUnknownId_WhenUpdatingAsynchronously_ThenAFailedOutputComesBack()
    {
        var repository = NewRepository();

        var result = await repository.UpdateAsync(new Person { Id = 42, Name = "Ghost" }, TestContext.Current.CancellationToken);

        Assert.False(result.Success);
        Assert.Equal([RelationalErrors.ConcurrencyMessage], result.Errors);
        Assert.Null(result.Data);
    }

    [Fact]
    public async Task GivenAStoredEntity_WhenDeletedAsynchronously_ThenItIsRemovedAndItsIdComesBack()
    {
        var repository = NewRepository();
        var id = (await repository.CreateAsync(new Person { Name = "Ann" }, TestContext.Current.CancellationToken)).Data;

        var result = await repository.DeleteAsync(new Person { Id = id }, TestContext.Current.CancellationToken);

        Assert.True(result.Success);
        Assert.Equal(id, result.Data);
        Assert.Null((await repository.GetByIdAsync(id, TestContext.Current.CancellationToken)).Data);
    }

    [Fact]
    public async Task GivenAnUnknownId_WhenDeletingAsynchronously_ThenAFailedOutputComesBack()
    {
        var repository = NewRepository();

        var result = await repository.DeleteAsync(new Person { Id = 42 }, TestContext.Current.CancellationToken);

        Assert.False(result.Success);
        Assert.Equal([RelationalErrors.ConcurrencyMessage], result.Errors);
    }

    [Fact]
    public async Task GivenSeveralEntities_WhenCreatingARangeAsynchronously_ThenTheyAreStoredAndTheirIdsComeBack()
    {
        var repository = NewRepository();

        var result = await repository.CreateRangeAsync([
            new Person { Name = "Ann" },
            new Person { Name = "Bob" }
        ], TestContext.Current.CancellationToken);

        Assert.True(result.Success);
        Assert.Equal([1L, 2L], result.Data);
        Assert.Equal(2, (await repository.GetAllAsync(TestContext.Current.CancellationToken)).Data!.Count());
    }

    [Fact]
    public async Task GivenStoredEntities_WhenUpdatingARangeAsynchronously_ThenTheyAreUpdatedAndComeBack()
    {
        var repository = NewRepository();
        var firstId = (await repository.CreateAsync(new Person { Name = "Ann" }, TestContext.Current.CancellationToken)).Data;
        var secondId = (await repository.CreateAsync(new Person { Name = "Bob" }, TestContext.Current.CancellationToken)).Data;

        var result = await repository.UpdateRangeAsync([
            new Person { Id = firstId, Name = "Ann Updated" },
            new Person { Id = secondId, Name = "Bob Updated" }
        ], TestContext.Current.CancellationToken);

        Assert.True(result.Success);
        Assert.Equal(2, result.Data!.Count());
        Assert.Equal("Ann Updated", (await repository.GetByIdAsync(firstId, TestContext.Current.CancellationToken)).Data!.Name);
        Assert.Equal("Bob Updated", (await repository.GetByIdAsync(secondId, TestContext.Current.CancellationToken)).Data!.Name);
    }

    [Fact]
    public async Task GivenARangeContainingUnknownEntities_WhenUpdatingAsynchronously_ThenItFailsAndNothingIsUpdated()
    {
        var repository = NewRepository();
        var id = (await repository.CreateAsync(new Person { Name = "Ann" }, TestContext.Current.CancellationToken)).Data;

        var result = await repository.UpdateRangeAsync([
            new Person { Id = id, Name = "Ann Updated" },
            new Person { Id = 999, Name = "Ghost" }
        ], TestContext.Current.CancellationToken);

        Assert.False(result.Success);
        Assert.Equal([RelationalErrors.ConcurrencyMessage], result.Errors);
        Assert.Equal("Ann", (await repository.GetByIdAsync(id, TestContext.Current.CancellationToken)).Data!.Name);
    }

    [Fact]
    public async Task GivenStoredEntities_WhenDeletingARangeAsynchronously_ThenTheMatchingOnesAreRemovedAndTheirIdsComeBack()
    {
        var repository = NewRepository();
        var firstId = (await repository.CreateAsync(new Person { Name = "Ann" }, TestContext.Current.CancellationToken)).Data;
        var secondId = (await repository.CreateAsync(new Person { Name = "Bob" }, TestContext.Current.CancellationToken)).Data;
        var thirdId = (await repository.CreateAsync(new Person { Name = "Cal" }, TestContext.Current.CancellationToken)).Data;

        var result = await repository.DeleteRangeAsync([firstId, thirdId, 999], TestContext.Current.CancellationToken);

        Assert.True(result.Success);
        Assert.Equal([firstId, thirdId], result.Data);
        Assert.NotNull((await repository.GetByIdAsync(secondId, TestContext.Current.CancellationToken)).Data);
        Assert.Null((await repository.GetByIdAsync(firstId, TestContext.Current.CancellationToken)).Data);
        Assert.Null((await repository.GetByIdAsync(thirdId, TestContext.Current.CancellationToken)).Data);
    }

    [Fact]
    public async Task GivenACancelledToken_WhenFetchingAllAsynchronously_ThenCancellationIsThrown()
    {
        var repository = NewRepository();
        var cancelled = new CancellationToken(canceled: true);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => repository.GetAllAsync(cancelled));
    }

    [Fact]
    public async Task GivenACancelledToken_WhenCreatingAsynchronously_ThenCancellationIsThrownAndNothingIsStored()
    {
        var repository = NewRepository();
        var cancelled = new CancellationToken(canceled: true);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => repository.CreateAsync(new Person { Name = "Ann" }, cancelled));
        Assert.Empty(repository.Query());
    }

    [Fact]
    public async Task GivenAVersionedEntityWithAStaleStamp_WhenUpdatingAsynchronously_ThenAConcurrencyConflictComesBack()
    {
        var repository = new AsyncFakeRepository<Account, long>();
        var id = (await repository.CreateAsync(new Account { Owner = "Ann" }, TestContext.Current.CancellationToken)).Data;
        var stamp = (await repository.GetByIdAsync(id, TestContext.Current.CancellationToken)).Data!.ConcurrencyStamp;

        var stale = await repository.UpdateAsync(new Account { Id = id, Owner = "Mallory" }, TestContext.Current.CancellationToken);
        var current = await repository.UpdateAsync(new Account { Id = id, Owner = "Bob", ConcurrencyStamp = stamp },
            TestContext.Current.CancellationToken);

        Assert.Equal([RelationalErrors.ConcurrencyMessage], stale.Errors);
        Assert.True(current.Success);
        Assert.Equal("Bob", (await repository.GetByIdAsync(id, TestContext.Current.CancellationToken)).Data!.Owner);
        Assert.NotEqual(stamp, (await repository.GetByIdAsync(id, TestContext.Current.CancellationToken)).Data!.ConcurrencyStamp);
    }

    [Fact]
    public async Task GivenAFetchedList_WhenAnEntityIsCreatedAfterwards_ThenTheListDoesNotChange()
    {
        var repository = NewRepository();
        await repository.CreateAsync(new Person { Name = "Ann" }, TestContext.Current.CancellationToken);

        var all = (await repository.GetAllAsync(TestContext.Current.CancellationToken)).Data!;
        await repository.CreateAsync(new Person { Name = "Bob" }, TestContext.Current.CancellationToken);

        Assert.Single(all);
    }
}
