using ArturRios.Util.Test.Mock;

namespace ArturRios.Util.Test.Tests.Mock;

public class FakeRepositoryTests
{
    private static FakeRepository<Person> NewRepository() => new();

    [Fact]
    public void Create_AssignsSequentialIdsStartingAtOne()
    {
        var repository = NewRepository();

        var firstResult = repository.Create(new Person { Name = "Ann" });
        var secondResult = repository.Create(new Person { Name = "Bob" });

        Assert.True(firstResult.Success);
        Assert.Equal(1, firstResult.Data);
        Assert.Equal(2, secondResult.Data);
    }

    [Fact]
    public void GetById_ReturnsMatchingEntity()
    {
        var repository = NewRepository();
        var id = repository.Create(new Person { Name = "Ann", Age = 30 }).Data;

        var found = repository.GetById(id);

        Assert.True(found.Success);
        Assert.NotNull(found.Data);
        Assert.Equal("Ann", found.Data.Name);
    }

    [Fact]
    public void GetById_WithUnknownId_ReturnsFailedOutput()
    {
        var repository = NewRepository();

        var result = repository.GetById(999);

        Assert.False(result.Success);
        Assert.Null(result.Data);
    }

    [Fact]
    public void GetAll_ReturnsAllStoredEntities()
    {
        var repository = NewRepository();
        repository.Create(new Person { Name = "Ann" });
        repository.Create(new Person { Name = "Bob" });

        var all = repository.GetAll();

        Assert.True(all.Success);
        Assert.Equal(2, all.Data!.Count());
    }

    [Fact]
    public void Query_ReturnsAllStoredEntities()
    {
        var repository = NewRepository();
        repository.Create(new Person { Name = "Ann" });
        repository.Create(new Person { Name = "Bob" });

        var all = repository.Query().ToList();

        Assert.Equal(2, all.Count);
    }

    [Fact]
    public void Update_CopiesWritablePropertiesButKeepsId()
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
    public void Update_WithUnknownId_ReturnsFailedOutput()
    {
        var repository = NewRepository();

        var result = repository.Update(new Person { Id = 42, Name = "Ghost" });

        Assert.False(result.Success);
        Assert.Null(result.Data);
    }

    [Fact]
    public void Delete_RemovesEntityAndReturnsId()
    {
        var repository = NewRepository();
        var id = repository.Create(new Person { Name = "Ann" }).Data;

        var result = repository.Delete(new Person { Id = id });

        Assert.True(result.Success);
        Assert.Equal(id, result.Data);
        Assert.False(repository.GetById(id).Success);
    }

    [Fact]
    public void Delete_WithUnknownId_ReturnsFailedOutput()
    {
        var repository = NewRepository();

        var result = repository.Delete(new Person { Id = 42 });

        Assert.False(result.Success);
    }

    [Fact]
    public void CreateRange_StoresEntitiesAndReturnsAssignedIds()
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
    public void UpdateRange_UpdatesExistingEntitiesAndReturnsThem()
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
    public void UpdateRange_SkipsUnknownEntities()
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
    public void DeleteRange_RemovesMatchingEntitiesAndReturnsIds()
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
