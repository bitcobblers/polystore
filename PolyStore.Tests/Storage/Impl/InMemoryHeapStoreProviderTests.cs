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
}
