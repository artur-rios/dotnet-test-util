using ArturRios.Util.Test.Mock;

namespace ArturRios.Util.Test.Tests.Mock;

[Trait("Category", "Unit")]
public class FakeRepositoryTests
{
    private static FakeRepository<Person> NewRepository() => new();

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
    public void GivenAnUnknownId_WhenFetchingById_ThenAFailedOutputComesBack()
    {
        var repository = NewRepository();

        var result = repository.GetById(999);

        Assert.False(result.Success);
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
        Assert.False(repository.GetById(id).Success);
    }

    [Fact]
    public void GivenAnUnknownId_WhenDeleting_ThenAFailedOutputComesBack()
    {
        var repository = NewRepository();

        var result = repository.Delete(new Person { Id = 42 });

        Assert.False(result.Success);
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
    public void GivenARangeContainingUnknownEntities_WhenUpdating_ThenTheUnknownOnesAreSkipped()
    {
        var repository = NewRepository();
        var id = repository.Create(new Person { Name = "Ann" }).Data;

        var result = repository.UpdateRange([
            new Person { Id = id, Name = "Ann Updated" },
            new Person { Id = 999, Name = "Ghost" }
        ]);

        var updated = result.Data!.ToList();

        Assert.Single(updated);
        Assert.Equal("Ann Updated", updated[0].Name);
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
        Assert.True(repository.GetById(secondId).Success);
        Assert.False(repository.GetById(firstId).Success);
        Assert.False(repository.GetById(thirdId).Success);
    }
}
