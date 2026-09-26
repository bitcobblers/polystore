using PolyStore.Storage;
using PolyStore.Storage.Impl;

namespace PolyStore.Tests.Storage.Impl;

public class InMemoryCanonicalTupleStore
{
    private record TestTuple
    {
        public long Id { get; init; }
        public string? Name { get; init; }
    }

    [Fact]
    public void Insert_ReturnsUniqueRid()
    {
        var store = new InMemoryCanonicalTupleStore<TestTuple>();
        var rid1 = store.Insert(new TestTuple { Id = 1 });
        var rid2 = store.Insert(new TestTuple { Id = 2 });

        Assert.NotEqual(rid1, rid2);
    }

    [Fact]
    public void TryGet_ReturnsInsertedTuple()
    {
        var store = new InMemoryCanonicalTupleStore<TestTuple>();
        var tuple = new TestTuple { Id = 42, Name = "Alice" };
        var rid = store.Insert(tuple);

        Assert.True(store.TryGet(rid, out var retrieved));
        Assert.Equal(42, retrieved.Id);
        Assert.Equal("Alice", retrieved.Name);
    }

    [Fact]
    public void TryGet_ReturnsFalseForUnknownRid()
    {
        var store = new InMemoryCanonicalTupleStore<TestTuple>();
        store.Insert(new TestTuple { Id = 1 });

        Assert.False(store.TryGet(new Rid(), out _));
    }

    [Fact]
    public void Delete_RemovesTuple()
    {
        var store = new InMemoryCanonicalTupleStore<TestTuple>();
        var rid = store.Insert(new TestTuple { Id = 1 });

        store.Delete(rid);

        Assert.False(store.TryGet(rid, out _));
        Assert.Equal(0, store.Count);
    }

    [Fact]
    public void Count_ReflectsStoredTuples()
    {
        var store = new InMemoryCanonicalTupleStore<TestTuple>();
        Assert.Equal(0, store.Count);

        store.Insert(new TestTuple { Id = 1 });
        Assert.Equal(1, store.Count);

        store.Insert(new TestTuple { Id = 2 });
        Assert.Equal(2, store.Count);
    }
}
