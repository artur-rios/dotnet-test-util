using ArturRios.Util.Test.Mock;

namespace ArturRios.Util.Test.Tests.Mock;

public class FakeRepositoryTests
{
    private static FakeRepository<Person> NewRepository() => new();

    [Fact]
    public void Create_AssignsSequentialIdsStartingAtOne()
    {
        var repository = NewRepository();

        var firstId = repository.Create(new Person { Name = "Ann" });
        var secondId = repository.Create(new Person { Name = "Bob" });

        Assert.Equal(1, firstId);
        Assert.Equal(2, secondId);
    }

    [Fact]
    public void GetById_ReturnsMatchingEntity()
    {
        var repository = NewRepository();
        var id = repository.Create(new Person { Name = "Ann", Age = 30 });

        var found = repository.GetById(id);

        Assert.NotNull(found);
        Assert.Equal("Ann", found!.Name);
    }

    [Fact]
    public void GetById_WithUnknownId_ReturnsNull()
    {
        var repository = NewRepository();

        Assert.Null(repository.GetById(999));
    }

    [Fact]
    public void GetAll_ReturnsAllStoredEntities()
    {
        var repository = NewRepository();
        repository.Create(new Person { Name = "Ann" });
        repository.Create(new Person { Name = "Bob" });

        var all = repository.GetAll().ToList();

        Assert.Equal(2, all.Count);
    }

    [Fact]
    public void Update_CopiesWritablePropertiesButKeepsId()
    {
        var repository = NewRepository();
        var id = repository.Create(new Person { Name = "Ann", Age = 30 });

        var result = repository.Update(new Person { Id = id, Name = "Ann Updated", Age = 31 });

        Assert.Equal(id, result.Id);
        Assert.Equal("Ann Updated", result.Name);
        Assert.Equal(31, result.Age);
        Assert.Equal("Ann Updated", repository.GetById(id)!.Name);
    }

    [Fact]
    public void Update_WithUnknownId_Throws()
    {
        var repository = NewRepository();

        Assert.Throws<KeyNotFoundException>(() => repository.Update(new Person { Id = 42, Name = "Ghost" }));
    }

    [Fact]
    public void Delete_RemovesEntityAndReturnsId()
    {
        var repository = NewRepository();
        var id = repository.Create(new Person { Name = "Ann" });

        var deletedId = repository.Delete(new Person { Id = id });

        Assert.Equal(id, deletedId);
        Assert.Null(repository.GetById(id));
    }

    [Fact]
    public void Delete_WithUnknownId_Throws()
    {
        var repository = NewRepository();

        Assert.Throws<KeyNotFoundException>(() => repository.Delete(new Person { Id = 42 }));
    }

    [Fact]
    public void UpdateRange_UpdatesExistingEntitiesAndReturnsThem()
    {
        var repository = NewRepository();
        var firstId = repository.Create(new Person { Name = "Ann" });
        var secondId = repository.Create(new Person { Name = "Bob" });

        var updated = repository.UpdateRange([
            new Person { Id = firstId, Name = "Ann Updated" },
            new Person { Id = secondId, Name = "Bob Updated" }
        ]).ToList();

        Assert.Equal(2, updated.Count);
        Assert.Equal("Ann Updated", repository.GetById(firstId)!.Name);
        Assert.Equal("Bob Updated", repository.GetById(secondId)!.Name);
    }

    [Fact]
    public void UpdateRange_SkipsUnknownEntities()
    {
        var repository = NewRepository();
        var id = repository.Create(new Person { Name = "Ann" });

        var updated = repository.UpdateRange([
            new Person { Id = id, Name = "Ann Updated" },
            new Person { Id = 999, Name = "Ghost" }
        ]).ToList();

        Assert.Single(updated);
        Assert.Equal("Ann Updated", updated[0].Name);
    }

    [Fact]
    public void DeleteRange_RemovesMatchingEntitiesAndReturnsIds()
    {
        var repository = NewRepository();
        var firstId = repository.Create(new Person { Name = "Ann" });
        var secondId = repository.Create(new Person { Name = "Bob" });
        var thirdId = repository.Create(new Person { Name = "Cal" });

        var deleted = repository.DeleteRange([firstId, thirdId, 999]).ToList();

        Assert.Equal([firstId, thirdId], deleted);
        Assert.NotNull(repository.GetById(secondId));
        Assert.Null(repository.GetById(firstId));
        Assert.Null(repository.GetById(thirdId));
    }
}
