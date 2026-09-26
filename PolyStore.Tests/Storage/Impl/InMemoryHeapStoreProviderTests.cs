using PolyStore.Storage;
using PolyStore.Storage.Impl;

namespace PolyStore.Tests.Storage.Impl;

public class InMemoryHeapStoreProviderTests
{
    private record TestTuple
    {
        public long Id { get; init; }
        public string? Name { get; init; }
    }

    [Fact]
    public void Add_Then_EnumerateRids_ReturnsRids()
    {
        var heap = new InMemoryHeapStoreProvider<TestTuple>();
        var rid1 = new Rid();
        var rid2 = new Rid();
        var rid3 = new Rid();

        heap.Add(rid1);
        heap.Add(rid2);
        heap.Add(rid3);

        Assert.Equal([rid1, rid2, rid3], heap.EnumerateRids().ToArray());
    }

    [Fact]
    public void Count_ReflectsNumberOfRids()
    {
        var heap = new InMemoryHeapStoreProvider<TestTuple>();
        Assert.Equal(0, heap.Count);

        heap.Add(new Rid());
        Assert.Equal(1, heap.Count);

        heap.Add(new Rid());
        Assert.Equal(2, heap.Count);
    }

    [Fact]
    public void Contains_ReflectsMembership()
    {
        var heap = new InMemoryHeapStoreProvider<TestTuple>();
        var rid = new Rid();

        Assert.False(heap.Contains(rid));

        heap.Add(rid);

        Assert.True(heap.Contains(rid));
    }

    [Fact]
    public void Remove_RemovesRid()
    {
        var heap = new InMemoryHeapStoreProvider<TestTuple>();
        var rid = new Rid();
        heap.Add(rid);

        Assert.True(heap.Remove(rid));
        Assert.False(heap.Contains(rid));
        Assert.Equal(0, heap.Count);
    }

    [Fact]
    public void Remove_ReturnsFalseForUnknownRid()
    {
        var heap = new InMemoryHeapStoreProvider<TestTuple>();

        Assert.False(heap.Remove(new Rid()));
    }

    [Fact]
    public void Add_DuplicateRid_IsTrackedOnlyOnce()
    {
        var heap = new InMemoryHeapStoreProvider<TestTuple>();
        var rid = new Rid();

        heap.Add(rid);
        heap.Add(rid);

        Assert.Equal(1, heap.Count);
        Assert.Equal([rid], heap.EnumerateRids().ToArray());
    }

    [Fact]
    public void EnumerateTuples_ResolvesRidsThroughCanonicalStore()
    {
        var store = new InMemoryCanonicalTupleStore<TestTuple>();
        var heap = new InMemoryHeapStoreProvider<TestTuple>();

        var alice = new TestTuple { Id = 1, Name = "Alice" };
        var bob = new TestTuple { Id = 2, Name = "Bob" };
        heap.Add(store.Insert(alice));
        heap.Add(store.Insert(bob));

        Assert.Equal([alice, bob], heap.EnumerateTuples(store).ToArray());
    }

    [Fact]
    public void EnumerateTuples_SkipsRidsAbsentFromCanonicalStore()
    {
        var store = new InMemoryCanonicalTupleStore<TestTuple>();
        var heap = new InMemoryHeapStoreProvider<TestTuple>();

        var alice = new TestTuple { Id = 1, Name = "Alice" };
        heap.Add(store.Insert(alice));
        heap.Add(new Rid()); // A RID with no canonical tuple.

        Assert.Equal([alice], heap.EnumerateTuples(store).ToArray());
    }

    [Fact]
    public void EnumerateRids_ReflectsStateAtTimeOfEnumeration()
    {
        var heap = new InMemoryHeapStoreProvider<TestTuple>();
        var rid1 = new Rid();
        heap.Add(rid1);

        var rids = heap.EnumerateRids();
        heap.Add(new Rid());

        Assert.Equal([rid1], rids.ToArray());
    }
}
