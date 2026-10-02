using PolyStore.Storage;
using PolyStore.Storage.Impl;

namespace PolyStore.IntegrationTests.Storage;

public class HeapCanonicalStoreBridgeTests
{
    private record TestTuple
    {
        public long Id { get; init; }
        public string? Name { get; init; }
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
    public void DeleteFromCanonicalStore_HidesTupleFromHeapEnumeration()
    {
        var store = new InMemoryCanonicalTupleStore<TestTuple>();
        var heap = new InMemoryHeapStoreProvider<TestTuple>();

        var alice = new TestTuple { Id = 1, Name = "Alice" };
        var rid = store.Insert(alice);
        heap.Add(rid);

        store.Delete(rid);

        Assert.Empty(heap.EnumerateTuples(store).ToArray());
        Assert.True(heap.Contains(rid)); // The heap still tracks the RID; it is not authoritative.
    }

    [Fact]
    public void RemoveFromHeap_HidesTupleFromEnumeration_ButKeepsCanonicalTuple()
    {
        var store = new InMemoryCanonicalTupleStore<TestTuple>();
        var heap = new InMemoryHeapStoreProvider<TestTuple>();

        var alice = new TestTuple { Id = 1, Name = "Alice" };
        var rid = store.Insert(alice);
        heap.Add(rid);

        heap.Remove(rid);

        Assert.Empty(heap.EnumerateTuples(store).ToArray());
        Assert.True(store.TryGet(rid, out var tuple));
        Assert.Equal(alice, tuple); // The canonical store still owns the tuple.
    }

    [Fact]
    public void Add_DuplicateRid_EnumeratesTupleOnce()
    {
        var store = new InMemoryCanonicalTupleStore<TestTuple>();
        var heap = new InMemoryHeapStoreProvider<TestTuple>();

        var alice = new TestTuple { Id = 1, Name = "Alice" };
        var rid = store.Insert(alice);
        heap.Add(rid);
        heap.Add(rid);

        Assert.Equal([alice], heap.EnumerateTuples(store).ToArray());
    }

    [Fact]
    public void EnumerateTuples_FollowsHeapOrder_NotCanonicalInsertionOrder()
    {
        var store = new InMemoryCanonicalTupleStore<TestTuple>();
        var heap = new InMemoryHeapStoreProvider<TestTuple>();

        var alice = new TestTuple { Id = 1, Name = "Alice" };
        var bob = new TestTuple { Id = 2, Name = "Bob" };
        var carol = new TestTuple { Id = 3, Name = "Carol" };
        var aliceRid = store.Insert(alice);
        var bobRid = store.Insert(bob);
        var carolRid = store.Insert(carol);

        heap.Add(carolRid);
        heap.Add(aliceRid);
        heap.Add(bobRid);

        Assert.Equal([carol, alice, bob], heap.EnumerateTuples(store).ToArray());
    }
}
