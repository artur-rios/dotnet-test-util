using ArturRios.Util.Test.Mock;
using Microsoft.EntityFrameworkCore;

namespace ArturRios.Util.Test.Tests.Mock;

[Trait("Category", "Unit")]
public class AsyncFakeRepositoryKeyTypeTests
{
    [Fact]
    public async Task GivenALongKeyedEntityWithAnIdSet_WhenCreatingAsynchronously_ThenTheGeneratedIdReplacesIt()
    {
        var repository = new AsyncFakeRepository<Person, long>();

        var result = await repository.CreateAsync(new Person { Id = 42, Name = "Ann" });

        Assert.True(result.Success);
        Assert.Equal(1L, result.Data);
        Assert.Null((await repository.GetByIdAsync(42)).Data);
    }

    [Fact]
    public async Task GivenAnIntKeyedFake_WhenCreatingEntitiesAsynchronously_ThenIdsAreAssignedSequentiallyFromOne()
    {
        var repository = new AsyncFakeRepository<Counter, int>();

        var first = await repository.CreateAsync(new Counter { Label = "a" });
        var range = await repository.CreateRangeAsync([new Counter { Label = "b" }, new Counter { Label = "c" }]);

        Assert.Equal(1, first.Data);
        Assert.Equal([2, 3], range.Data);
    }

    [Fact]
    public async Task GivenAGuidKeyedFake_WhenCreatingAsynchronously_ThenTheEntityIsFoundByItsGeneratedGuid()
    {
        var repository = new AsyncFakeRepository<Device, Guid>();

        var id = (await repository.CreateAsync(new Device { Model = "X1" })).Data;

        Assert.NotEqual(Guid.Empty, id);
        Assert.Equal("X1", (await repository.GetByIdAsync(id)).Data!.Model);
        Assert.NotNull(await repository.Query().FirstOrDefaultAsync(d => d.Id == id));
    }

    [Fact]
    public async Task GivenAStringKeyedEntityWithAnId_WhenCreatingAsynchronously_ThenTheCallerAssignedIdIsKept()
    {
        var repository = new AsyncFakeRepository<Country, string>();

        var result = await repository.CreateAsync(new Country { Id = "BR", Name = "Brazil" });

        Assert.True(result.Success);
        Assert.Equal("BR", result.Data);
        Assert.Equal("Brazil", (await repository.GetByIdAsync("BR")).Data!.Name);
    }

    [Fact]
    public async Task GivenAStringKeyedEntityWithoutAnId_WhenCreatingAsynchronously_ThenAFailedOutputComesBack()
    {
        var repository = new AsyncFakeRepository<Country, string>();

        var result = await repository.CreateAsync(new Country { Name = "Nowhere" });

        Assert.False(result.Success);
        Assert.Empty(repository.Query());
    }

    [Fact]
    public async Task GivenStringKeyedStoredEntities_WhenUpdatingAndDeletingAsynchronously_ThenTheStringIdIdentifiesThem()
    {
        var repository = new AsyncFakeRepository<Country, string>();
        await repository.CreateRangeAsync([
            new Country { Id = "BR", Name = "Brazil" },
            new Country { Id = "PT", Name = "Portugal" }
        ]);

        var updated = await repository.UpdateAsync(new Country { Id = "BR", Name = "Brasil" });
        var deleted = await repository.DeleteRangeAsync(["PT"]);

        Assert.Equal("Brasil", updated.Data!.Name);
        Assert.Equal(["PT"], deleted.Data);
        Assert.Equal("BR", Assert.Single(await repository.Query().ToListAsync()).Id);
    }

    [Fact]
    public async Task GivenAnIdGenerator_WhenCreatingAsynchronously_ThenItsIdsAreUsed()
    {
        var next = 0;
        var repository = new AsyncFakeRepository<Country, string>(() => $"C{++next}");

        var result = await repository.CreateRangeAsync([new Country { Name = "Brazil" }, new Country { Name = "Portugal" }]);

        Assert.Equal(["C1", "C2"], result.Data);
    }

    [Fact]
    public async Task GivenACancelledToken_WhenCreatingAStringKeyedEntityAsynchronously_ThenCancellationIsThrownAndNothingIsStored()
    {
        var repository = new AsyncFakeRepository<Country, string>();
        var cancelled = new CancellationToken(canceled: true);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => repository.CreateAsync(new Country { Id = "BR", Name = "Brazil" }, cancelled));
        Assert.Empty(repository.Query());
    }
}
