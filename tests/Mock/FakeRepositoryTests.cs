using ArturRios.Data.Relational.Core.Repositories;
using ArturRios.Util.Test.Mock;

namespace ArturRios.Util.Test.Tests.Mock;

[Trait("Category", "Unit")]
public class FakeRepositoryTests
{
    private static FakeRepository<Person, long> NewRepository() => new();

    [Fact]
    public void GivenAnEmptyFake_WhenCreatingEntities_ThenIdsAreAssignedSequentiallyFromOne()
    {
        var repository = NewRepository();

        var firstResult = repository.Create(new Person { Name = "Ann" });
        var secondResult = repository.Create(new Person { Name = "Bob" });

        Assert.True(firstResult.Success);
        Assert.Equal(1, firstResult.Data);
        Assert.Equal(2, secondResult.Data);
    }

    [Fact]
    public void GivenAStoredEntity_WhenFetchingItById_ThenItComesBack()
    {
        var repository = NewRepository();
        var id = repository.Create(new Person { Name = "Ann", Age = 30 }).Data;

        var found = repository.GetById(id);

        Assert.True(found.Success);
        Assert.NotNull(found.Data);
        Assert.Equal("Ann", found.Data.Name);
    }

    [Fact]
    public void GivenAnUnknownId_WhenFetchingById_ThenASuccessfulOutputWithNoDataComesBack()
    {
        var repository = NewRepository();

        var result = repository.GetById(999);

        Assert.True(result.Success);
        Assert.Null(result.Data);
    }

    [Fact]
    public void GivenStoredEntities_WhenFetchingAll_ThenEveryOneComesBack()
    {
        var repository = NewRepository();
        repository.Create(new Person { Name = "Ann" });
        repository.Create(new Person { Name = "Bob" });

        var all = repository.GetAll();

        Assert.True(all.Success);
        Assert.Equal(2, all.Data!.Count());
    }

    [Fact]
    public void GivenStoredEntities_WhenQuerying_ThenEveryOneComesBack()
    {
        var repository = NewRepository();
        repository.Create(new Person { Name = "Ann" });
        repository.Create(new Person { Name = "Bob" });

        var all = repository.Query().ToList();

        Assert.Equal(2, all.Count);
    }

    [Fact]
    public void GivenAStoredEntity_WhenUpdated_ThenWritablePropertiesAreCopiedAndTheIdIsKept()
    {
        var repository = NewRepository();
        var id = repository.Create(new Person { Name = "Ann", Age = 30 }).Data;

        var result = repository.Update(new Person { Id = id, Name = "Ann Updated", Age = 31 });

        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        Assert.Equal(id, result.Data.Id);
        Assert.Equal("Ann Updated", result.Data.Name);
        Assert.Equal(31, result.Data.Age);
        Assert.Equal("Ann Updated", repository.GetById(id).Data!.Name);
    }

    [Fact]
    public void GivenAnUnknownId_WhenUpdating_ThenAFailedOutputComesBack()
    {
        var repository = NewRepository();

        var result = repository.Update(new Person { Id = 42, Name = "Ghost" });

        Assert.False(result.Success);
        Assert.Equal([RelationalErrors.ConcurrencyMessage], result.Errors);
        Assert.Null(result.Data);
    }

    [Fact]
    public void GivenAStoredEntity_WhenDeleted_ThenItIsRemovedAndItsIdComesBack()
    {
        var repository = NewRepository();
        var id = repository.Create(new Person { Name = "Ann" }).Data;

        var result = repository.Delete(new Person { Id = id });

        Assert.True(result.Success);
        Assert.Equal(id, result.Data);
        Assert.Null(repository.GetById(id).Data);
    }

    [Fact]
    public void GivenAnUnknownId_WhenDeleting_ThenAFailedOutputComesBack()
    {
        var repository = NewRepository();

        var result = repository.Delete(new Person { Id = 42 });

        Assert.False(result.Success);
        Assert.Equal([RelationalErrors.ConcurrencyMessage], result.Errors);
    }

    [Fact]
    public void GivenSeveralEntities_WhenCreatingARange_ThenTheyAreStoredAndTheirIdsComeBack()
    {
        var repository = NewRepository();

        var result = repository.CreateRange([
            new Person { Name = "Ann" },
            new Person { Name = "Bob" }
        ]);

        Assert.True(result.Success);
        Assert.Equal([1L, 2L], result.Data);
        Assert.Equal(2, repository.GetAll().Data!.Count());
    }

    [Fact]
    public void GivenStoredEntities_WhenUpdatingARange_ThenTheyAreUpdatedAndComeBack()
    {
        var repository = NewRepository();
        var firstId = repository.Create(new Person { Name = "Ann" }).Data;
        var secondId = repository.Create(new Person { Name = "Bob" }).Data;

        var result = repository.UpdateRange([
            new Person { Id = firstId, Name = "Ann Updated" },
            new Person { Id = secondId, Name = "Bob Updated" }
        ]);

        Assert.True(result.Success);
        Assert.Equal(2, result.Data!.Count());
        Assert.Equal("Ann Updated", repository.GetById(firstId).Data!.Name);
        Assert.Equal("Bob Updated", repository.GetById(secondId).Data!.Name);
    }

    [Fact]
    public void GivenARangeContainingUnknownEntities_WhenUpdating_ThenItFailsAndNothingIsUpdated()
    {
        var repository = NewRepository();
        var id = repository.Create(new Person { Name = "Ann" }).Data;

        var result = repository.UpdateRange([
            new Person { Id = id, Name = "Ann Updated" },
            new Person { Id = 999, Name = "Ghost" }
        ]);

        Assert.False(result.Success);
        Assert.Equal([RelationalErrors.ConcurrencyMessage], result.Errors);
        Assert.Equal("Ann", repository.GetById(id).Data!.Name);
    }

    [Fact]
    public void GivenStoredEntities_WhenDeletingARange_ThenTheMatchingOnesAreRemovedAndTheirIdsComeBack()
    {
        var repository = NewRepository();
        var firstId = repository.Create(new Person { Name = "Ann" }).Data;
        var secondId = repository.Create(new Person { Name = "Bob" }).Data;
        var thirdId = repository.Create(new Person { Name = "Cal" }).Data;

        var result = repository.DeleteRange([firstId, thirdId, 999]);

        Assert.True(result.Success);
        Assert.Equal([firstId, thirdId], result.Data);
        Assert.NotNull(repository.GetById(secondId).Data);
        Assert.Null(repository.GetById(firstId).Data);
        Assert.Null(repository.GetById(thirdId).Data);
    }

    [Fact]
    public void GivenAnEmptyFake_WhenFetchingAll_ThenASuccessfulEmptyListComesBack()
    {
        var all = NewRepository().GetAll();

        Assert.True(all.Success);
        Assert.Empty(all.Data!);
    }

    [Fact]
    public void GivenEmptyRanges_WhenCreatingUpdatingAndDeleting_ThenEachSucceedsWithNoDataAndTheStoreIsUnchanged()
    {
        var repository = NewRepository();
        repository.Create(new Person { Name = "Ann" });

        var created = repository.CreateRange([]);
        var updated = repository.UpdateRange([]);
        var deleted = repository.DeleteRange([]);

        Assert.True(created.Success);
        Assert.Empty(created.Data!);
        Assert.True(updated.Success);
        Assert.Empty(updated.Data!);
        Assert.True(deleted.Success);
        Assert.Empty(deleted.Data!);
        Assert.Equal("Ann", Assert.Single(repository.Query()).Name);
    }

    [Fact]
    public void GivenARangeOfIdsThatRepeatsOne_WhenDeleting_ThenThatIdIsRemovedAndReportedOnce()
    {
        var repository = NewRepository();
        var id = repository.Create(new Person { Name = "Ann" }).Data;

        var result = repository.DeleteRange([id, id]);

        Assert.Equal([id], result.Data);
        Assert.Empty(repository.Query());
    }

    [Fact]
    public void GivenAStoredEntity_WhenUpdated_ThenTheStoredInstanceComesBackRatherThanTheOneThatWasPassed()
    {
        var repository = NewRepository();
        var stored = new Person { Name = "Ann" };
        var id = repository.Create(stored).Data;
        var update = new Person { Id = id, Name = "Ann Smith" };

        var result = repository.Update(update);

        Assert.Same(stored, result.Data);
        Assert.NotSame(update, result.Data);
    }

    [Fact]
    public void GivenTheCurrentConcurrencyStamp_WhenDeletingAVersionedEntity_ThenItIsRemoved()
    {
        var repository = new FakeRepository<Account, long>();
        var id = repository.Create(new Account { Owner = "Ann" }).Data;
        var stamp = repository.GetById(id).Data!.ConcurrencyStamp;

        var result = repository.Delete(new Account { Id = id, ConcurrencyStamp = stamp });

        Assert.True(result.Success);
        Assert.Empty(repository.Query());
    }

    [Fact]
    public void GivenVersionedEntitiesWithTheirCurrentStamps_WhenUpdatingARange_ThenEachGetsANewStampOnBothInstances()
    {
        var repository = new FakeRepository<Account, long>();
        repository.CreateRange([new Account { Owner = "Ann" }, new Account { Owner = "Bob" }]);
        var before = repository.Query().ToDictionary(a => a.Id, a => a.ConcurrencyStamp);
        var updates = before.Select(pair => new Account { Id = pair.Key, Owner = "Updated", ConcurrencyStamp = pair.Value })
            .ToList();

        var result = repository.UpdateRange(updates);

        Assert.True(result.Success);
        Assert.All(repository.Query(), stored =>
        {
            Assert.Equal("Updated", stored.Owner);
            Assert.NotEqual(before[stored.Id], stored.ConcurrencyStamp);
            Assert.Equal(stored.ConcurrencyStamp, updates.Single(u => u.Id == stored.Id).ConcurrencyStamp);
        });
    }

    [Fact]
    public void GivenARangeWithOneStaleStamp_WhenUpdating_ThenItFailsAndNoEntityOrStampChanges()
    {
        var repository = new FakeRepository<Account, long>();
        var firstId = repository.Create(new Account { Owner = "Ann" }).Data;
        var secondId = repository.Create(new Account { Owner = "Bob" }).Data;
        var firstStamp = repository.GetById(firstId).Data!.ConcurrencyStamp;
        var secondStamp = repository.GetById(secondId).Data!.ConcurrencyStamp;

        var result = repository.UpdateRange([
            new Account { Id = firstId, Owner = "Ann Smith", ConcurrencyStamp = firstStamp },
            new Account { Id = secondId, Owner = "Mallory" }
        ]);

        Assert.Equal([RelationalErrors.ConcurrencyMessage], result.Errors);
        Assert.Equal("Ann", repository.GetById(firstId).Data!.Owner);
        Assert.Equal(firstStamp, repository.GetById(firstId).Data!.ConcurrencyStamp);
        Assert.Equal(secondStamp, repository.GetById(secondId).Data!.ConcurrencyStamp);
    }

    [Fact]
    public void GivenAnEntityWithAReadOnlyProperty_WhenUpdated_ThenTheWritableOnesAreCopiedAndTheReadOnlyOneIsSkipped()
    {
        var repository = new FakeRepository<Product, long>();
        var id = repository.Create(new Product { Name = "Pen" }).Data;

        var result = repository.Update(new Product { Id = id, Name = "Pencil" });

        Assert.True(result.Success);
        Assert.Equal($"#{id} Pencil", repository.GetById(id).Data!.Label);
    }
}
