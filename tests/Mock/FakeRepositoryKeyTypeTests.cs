using ArturRios.Util.Test.Mock;

namespace ArturRios.Util.Test.Tests.Mock;

[Trait("Category", "Unit")]
public class FakeRepositoryKeyTypeTests
{
    [Fact]
    public void GivenALongKeyedEntityWithAnIdSet_WhenCreating_ThenTheGeneratedIdReplacesIt()
    {
        var repository = new FakeRepository<Person, long>();

        var result = repository.Create(new Person { Id = 42, Name = "Ann" });

        Assert.True(result.Success);
        Assert.Equal(1L, result.Data);
        Assert.False(repository.GetById(42).Success);
    }

    [Fact]
    public void GivenAnIntKeyedFake_WhenCreatingEntities_ThenIdsAreAssignedSequentiallyFromOne()
    {
        var repository = new FakeRepository<Counter, int>();

        var first = repository.Create(new Counter { Label = "a" });
        var range = repository.CreateRange([new Counter { Label = "b" }, new Counter { Label = "c" }]);

        Assert.Equal(1, first.Data);
        Assert.Equal([2, 3], range.Data);
        Assert.Equal("c", repository.GetById(3).Data!.Label);
    }

    [Fact]
    public void GivenAGuidKeyedFake_WhenCreatingEntities_ThenEachGetsAFreshNonEmptyGuid()
    {
        var repository = new FakeRepository<Device, Guid>();
        var callerAssigned = Guid.NewGuid();

        var first = repository.Create(new Device { Id = callerAssigned, Model = "X1" });
        var second = repository.Create(new Device { Model = "X2" });

        Assert.True(first.Success);
        Assert.NotEqual(Guid.Empty, first.Data);
        Assert.NotEqual(callerAssigned, first.Data);
        Assert.NotEqual(first.Data, second.Data);
        Assert.Equal("X1", repository.GetById(first.Data).Data!.Model);
    }

    [Fact]
    public void GivenAGuidKeyedStoredEntity_WhenUpdatedAndDeleted_ThenTheGuidIdentifiesIt()
    {
        var repository = new FakeRepository<Device, Guid>();
        var id = repository.Create(new Device { Model = "X1" }).Data;

        var updated = repository.Update(new Device { Id = id, Model = "X1 Pro" });
        var deleted = repository.Delete(new Device { Id = id });

        Assert.Equal("X1 Pro", updated.Data!.Model);
        Assert.Equal(id, deleted.Data);
        Assert.Empty(repository.Query());
    }

    [Fact]
    public void GivenAStringKeyedEntityWithAnId_WhenCreating_ThenTheCallerAssignedIdIsKept()
    {
        var repository = new FakeRepository<Country, string>();

        var result = repository.Create(new Country { Id = "BR", Name = "Brazil" });

        Assert.True(result.Success);
        Assert.Equal("BR", result.Data);
        Assert.Equal("Brazil", repository.GetById("BR").Data!.Name);
    }

    [Fact]
    public void GivenAStringKeyedEntityWithoutAnId_WhenCreating_ThenAFailedOutputComesBackAndNothingIsStored()
    {
        var repository = new FakeRepository<Country, string>();

        var result = repository.Create(new Country { Name = "Nowhere" });

        Assert.False(result.Success);
        Assert.Empty(repository.Query());
    }

    [Fact]
    public void GivenAStoredStringKey_WhenCreatingAnotherEntityWithTheSameId_ThenAFailedOutputComesBack()
    {
        var repository = new FakeRepository<Country, string>();
        repository.Create(new Country { Id = "BR", Name = "Brazil" });

        var result = repository.Create(new Country { Id = "BR", Name = "Duplicate" });

        Assert.False(result.Success);
        Assert.Single(repository.Query());
        Assert.Equal("Brazil", repository.GetById("BR").Data!.Name);
    }

    [Fact]
    public void GivenARangeWithARepeatedStringKey_WhenCreatingIt_ThenAFailedOutputComesBackAndNothingIsStored()
    {
        var repository = new FakeRepository<Country, string>();

        var result = repository.CreateRange([
            new Country { Id = "BR", Name = "Brazil" },
            new Country { Id = "PT", Name = "Portugal" },
            new Country { Id = "BR", Name = "Duplicate" }
        ]);

        Assert.False(result.Success);
        Assert.Empty(repository.Query());
    }

    [Fact]
    public void GivenStringKeyedStoredEntities_WhenDeletingARange_ThenTheMatchingOnesAreRemoved()
    {
        var repository = new FakeRepository<Country, string>();
        repository.CreateRange([
            new Country { Id = "BR", Name = "Brazil" },
            new Country { Id = "PT", Name = "Portugal" },
            new Country { Id = "AR", Name = "Argentina" }
        ]);

        var result = repository.DeleteRange(["BR", "AR", "XX"]);

        Assert.True(result.Success);
        Assert.Equal(["BR", "AR"], result.Data);
        Assert.Equal("PT", Assert.Single(repository.Query()).Id);
    }

    [Fact]
    public void GivenAnIdGenerator_WhenCreatingStringKeyedEntities_ThenItsIdsAreUsed()
    {
        var next = 0;
        var repository = new FakeRepository<Country, string>(() => $"C{++next}");

        var first = repository.Create(new Country { Name = "Brazil" });
        var range = repository.CreateRange([new Country { Name = "Portugal" }]);

        Assert.Equal("C1", first.Data);
        Assert.Equal(["C2"], range.Data);
    }

    [Fact]
    public void GivenAnIdGenerator_WhenCreatingLongKeyedEntities_ThenItReplacesTheSequentialDefault()
    {
        var next = 100L;
        var repository = new FakeRepository<Person, long>(() => next += 10);

        var result = repository.CreateRange([new Person { Name = "Ann" }, new Person { Name = "Bob" }]);

        Assert.Equal([110L, 120L], result.Data);
    }

    [Fact]
    public void GivenAnIdGeneratorThatRepeatsAnId_WhenCreating_ThenAFailedOutputComesBack()
    {
        var repository = new FakeRepository<Device, Guid>(() => Guid.Empty);
        repository.Create(new Device { Model = "X1" });

        var result = repository.Create(new Device { Model = "X2" });

        Assert.False(result.Success);
        Assert.Single(repository.Query());
    }
}
