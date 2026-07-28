using ArturRios.Util.Test.Mock;
using Microsoft.EntityFrameworkCore;

namespace ArturRios.Util.Test.Tests.Mock;

public class AsyncFakeRepositoryTests
{
    private static AsyncFakeRepository<Person> NewRepository() => new();

    [Fact]
    public async Task CreateAsync_AssignsSequentialIdsStartingAtOne()
    {
        var repository = NewRepository();

        var firstResult = await repository.CreateAsync(new Person { Name = "Ann" });
        var secondResult = await repository.CreateAsync(new Person { Name = "Bob" });

        Assert.True(firstResult.Success);
        Assert.Equal(1, firstResult.Data);
        Assert.Equal(2, secondResult.Data);
    }

    [Fact]
    public async Task GetByIdAsync_ReturnsMatchingEntity()
    {
        var repository = NewRepository();
        var id = (await repository.CreateAsync(new Person { Name = "Ann", Age = 30 })).Data;

        var found = await repository.GetByIdAsync(id);

        Assert.True(found.Success);
        Assert.NotNull(found.Data);
        Assert.Equal("Ann", found.Data.Name);
    }

    [Fact]
    public async Task GetByIdAsync_WithUnknownId_ReturnsFailedOutput()
    {
        var repository = NewRepository();

        var result = await repository.GetByIdAsync(999);

        Assert.False(result.Success);
        Assert.Null(result.Data);
    }

    [Fact]
    public async Task GetAllAsync_ReturnsAllStoredEntities()
    {
        var repository = NewRepository();
        await repository.CreateAsync(new Person { Name = "Ann" });
        await repository.CreateAsync(new Person { Name = "Bob" });

        var all = await repository.GetAllAsync();

        Assert.True(all.Success);
        Assert.Equal(2, all.Data!.Count());
    }

    [Fact]
    public async Task Query_ReturnsAllStoredEntities()
    {
        var repository = NewRepository();
        await repository.CreateAsync(new Person { Name = "Ann" });
        await repository.CreateAsync(new Person { Name = "Bob" });

        var all = repository.Query().ToList();

        Assert.Equal(2, all.Count);
    }

    [Fact]
    public async Task Query_SupportsToListAsync()
    {
        var repository = NewRepository();
        await repository.CreateAsync(new Person { Name = "Ann", Age = 30 });
        await repository.CreateAsync(new Person { Name = "Bob", Age = 25 });

        var adults = await repository.Query().Where(p => p.Age >= 18).ToListAsync();

        Assert.Equal(2, adults.Count);
    }

    [Fact]
    public async Task Query_SupportsFirstOrDefaultAsync()
    {
        var repository = NewRepository();
        await repository.CreateAsync(new Person { Name = "Ann" });
        await repository.CreateAsync(new Person { Name = "Bob" });

        var bob = await repository.Query().FirstOrDefaultAsync(p => p.Name == "Bob");

        Assert.NotNull(bob);
        Assert.Equal("Bob", bob.Name);
    }

    [Fact]
    public async Task Query_SupportsCountAsync()
    {
        var repository = NewRepository();
        await repository.CreateAsync(new Person { Name = "Ann", Age = 30 });
        await repository.CreateAsync(new Person { Name = "Bob", Age = 15 });

        var adultCount = await repository.Query().CountAsync(p => p.Age >= 18);

        Assert.Equal(1, adultCount);
    }

    [Fact]
    public async Task UpdateAsync_CopiesWritablePropertiesButKeepsId()
    {
        var repository = NewRepository();
        var id = (await repository.CreateAsync(new Person { Name = "Ann", Age = 30 })).Data;

        var result = await repository.UpdateAsync(new Person { Id = id, Name = "Ann Updated", Age = 31 });

        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        Assert.Equal(id, result.Data.Id);
        Assert.Equal("Ann Updated", result.Data.Name);
        Assert.Equal(31, result.Data.Age);
        Assert.Equal("Ann Updated", (await repository.GetByIdAsync(id)).Data!.Name);
    }

    [Fact]
    public async Task UpdateAsync_WithUnknownId_ReturnsFailedOutput()
    {
        var repository = NewRepository();

        var result = await repository.UpdateAsync(new Person { Id = 42, Name = "Ghost" });

        Assert.False(result.Success);
        Assert.Null(result.Data);
    }

    [Fact]
    public async Task DeleteAsync_RemovesEntityAndReturnsId()
    {
        var repository = NewRepository();
        var id = (await repository.CreateAsync(new Person { Name = "Ann" })).Data;

        var result = await repository.DeleteAsync(new Person { Id = id });

        Assert.True(result.Success);
        Assert.Equal(id, result.Data);
        Assert.False((await repository.GetByIdAsync(id)).Success);
    }

    [Fact]
    public async Task DeleteAsync_WithUnknownId_ReturnsFailedOutput()
    {
        var repository = NewRepository();

        var result = await repository.DeleteAsync(new Person { Id = 42 });

        Assert.False(result.Success);
    }

    [Fact]
    public async Task CreateRangeAsync_StoresEntitiesAndReturnsAssignedIds()
    {
        var repository = NewRepository();

        var result = await repository.CreateRangeAsync([
            new Person { Name = "Ann" },
            new Person { Name = "Bob" }
        ]);

        Assert.True(result.Success);
        Assert.Equal([1L, 2L], result.Data);
        Assert.Equal(2, (await repository.GetAllAsync()).Data!.Count());
    }

    [Fact]
    public async Task UpdateRangeAsync_UpdatesExistingEntitiesAndReturnsThem()
    {
        var repository = NewRepository();
        var firstId = (await repository.CreateAsync(new Person { Name = "Ann" })).Data;
        var secondId = (await repository.CreateAsync(new Person { Name = "Bob" })).Data;

        var result = await repository.UpdateRangeAsync([
            new Person { Id = firstId, Name = "Ann Updated" },
            new Person { Id = secondId, Name = "Bob Updated" }
        ]);

        Assert.True(result.Success);
        Assert.Equal(2, result.Data!.Count());
        Assert.Equal("Ann Updated", (await repository.GetByIdAsync(firstId)).Data!.Name);
        Assert.Equal("Bob Updated", (await repository.GetByIdAsync(secondId)).Data!.Name);
    }

    [Fact]
    public async Task UpdateRangeAsync_SkipsUnknownEntities()
    {
        var repository = NewRepository();
        var id = (await repository.CreateAsync(new Person { Name = "Ann" })).Data;

        var result = await repository.UpdateRangeAsync([
            new Person { Id = id, Name = "Ann Updated" },
            new Person { Id = 999, Name = "Ghost" }
        ]);

        var updated = result.Data!.ToList();

        Assert.Single(updated);
        Assert.Equal("Ann Updated", updated[0].Name);
    }

    [Fact]
    public async Task DeleteRangeAsync_RemovesMatchingEntitiesAndReturnsIds()
    {
        var repository = NewRepository();
        var firstId = (await repository.CreateAsync(new Person { Name = "Ann" })).Data;
        var secondId = (await repository.CreateAsync(new Person { Name = "Bob" })).Data;
        var thirdId = (await repository.CreateAsync(new Person { Name = "Cal" })).Data;

        var result = await repository.DeleteRangeAsync([firstId, thirdId, 999]);

        Assert.True(result.Success);
        Assert.Equal([firstId, thirdId], result.Data);
        Assert.True((await repository.GetByIdAsync(secondId)).Success);
        Assert.False((await repository.GetByIdAsync(firstId)).Success);
        Assert.False((await repository.GetByIdAsync(thirdId)).Success);
    }

    [Fact]
    public async Task GetAllAsync_WithCancelledToken_ThrowsOperationCanceled()
    {
        var repository = NewRepository();
        var cancelled = new CancellationToken(canceled: true);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => repository.GetAllAsync(cancelled));
    }

    [Fact]
    public async Task CreateAsync_WithCancelledToken_ThrowsAndDoesNotStore()
    {
        var repository = NewRepository();
        var cancelled = new CancellationToken(canceled: true);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => repository.CreateAsync(new Person { Name = "Ann" }, cancelled));
        Assert.Empty(repository.Query());
    }
}
