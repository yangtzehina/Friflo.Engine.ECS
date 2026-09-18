using System;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Index;
using NUnit.Framework;
using Tests.ECS.Index;
using static NUnit.Framework.Assert;

// ReSharper disable InconsistentNaming
namespace Tests.ECS.Arch {

/// <summary>
/// Tests for the fast path of <see cref="QueryEntities.ApplyBatch"/> moving all entities of an archetype at once.
/// </summary>
public static class Test_Batch_Bulk
{
    /// > ArchetypeUtils.MinCapacity (512) per archetype to cover archetype growth of the target archetype
    private const int Count = 4000;
    
    /// <summary> Create entities in four archetypes: [], [Position], [Position, Rotation], [Position, #TestTag] </summary>
    private static EntityStore CreateStore(out int[] ids)
    {
        var store   = new EntityStore();
        ids         = new int[Count];
        for (int n = 0; n < Count; n++) {
            Entity entity;
            switch (n % 4) {
                case 0:     entity = store.CreateEntity();                                                          break;
                case 1:     entity = store.CreateEntity(new Position(n, 0, 0));                                     break;
                case 2:     entity = store.CreateEntity(new Position(n, 0, 0), new Rotation(n, 0, 0, 0));           break;
                default:    entity = store.CreateEntity(new Position(n, 0, 0), Tags.Get<TestTag>());                break;
            }
            ids[n] = entity.Id;
        }
        return store;
    }
    
    private static EntityBatch CreateBatch() {
        var batch = new EntityBatch();
        batch.Add(new Scale3(1, 2, 3));
        batch.Remove<Rotation>();
        batch.AddTag<TestTag2>();
        batch.RemoveTag<TestTag>();
        return batch;
    }
    
    /// Three source archetypes are merged into one target archetype. Result must be equal to applying the batch per entity.
    [Test]
    public static void Test_Batch_Bulk_equals_per_entity()
    {
        var bulkStore   = CreateStore(out var ids);
        var listStore   = CreateStore(out _);
        var batch       = CreateBatch();
        
        bulkStore.Query<Position>().Entities.ApplyBatch(batch);                 // fast path - entire archetypes
        listStore.Query<Position>().Entities.ToEntityList().ApplyBatch(batch);  // per entity
        
        AreEqual(Count, bulkStore.Count);
        for (int n = 0; n < Count; n++)
        {
            var bulk = bulkStore.GetEntityById(ids[n]);
            var list = listStore.GetEntityById(ids[n]);
            AreEqual(list.Archetype.ToString(), bulk.Archetype.ToString());
            if (n % 4 == 0) {
                AreEqual("[]",  bulk.Archetype.Name);
                continue;
            }
            AreEqual(new Position(n, 0, 0), bulk.GetComponent<Position>()); // moved component values are preserved
            AreEqual(new Scale3(1, 2, 3),   bulk.GetComponent<Scale3>());   // added component value is assigned
            IsFalse (bulk.HasComponent<Rotation>());
            IsTrue  (bulk.Tags.Has<TestTag2>());
            IsFalse (bulk.Tags.Has<TestTag>());
            // entity is stored in the archetype its node refers to
            IsTrue  (bulk.Archetype.EntityIds.IndexOf(bulk.Id) >= 0);
        }
        var target = bulkStore.GetArchetype(ComponentTypes.Get<Position, Scale3>(), Tags.Get<TestTag2>());
        AreEqual(Count / 4 * 3, target.Count);
        AreEqual(0,             bulkStore.GetArchetype(ComponentTypes.Get<Position>()).Count);
        AreEqual(0,             bulkStore.GetArchetype(ComponentTypes.Get<Position, Rotation>()).Count);
        AreEqual(0,             bulkStore.GetArchetype(ComponentTypes.Get<Position>(), Tags.Get<TestTag>()).Count);
        
        // all archetypes have same entity count as applying the batch per entity
        foreach (var archetype in listStore.Archetypes) {
            AreEqual(archetype.Count, bulkStore.GetArchetype(archetype.ComponentTypes, archetype.Tags).Count);
        }
        // moved entities are still valid targets for subsequent structural changes 
        var entity = bulkStore.GetEntityById(ids[1]);
        entity.AddComponent(new Rotation(1, 1, 1, 1));
        AreEqual(new Position(1, 0, 0), entity.GetComponent<Position>());
        entity.DeleteEntity();
        AreEqual(Count - 1, bulkStore.Count);
    }
    
    /// An archetype already containing the added component keeps its entities. Its component values are overwritten.
    [Test]
    public static void Test_Batch_Bulk_target_in_query()
    {
        var store = new EntityStore();
        for (int n = 0; n < 1000; n++) {
            store.CreateEntity(new Position(n, 0, 0));
            store.CreateEntity(new Position(n, 0, 0), new Scale3(7, 7, 7));
        }
        var batch = new EntityBatch();
        batch.Add(new Scale3(1, 2, 3));
        
        var query = store.Query<Position>();
        query.Entities.ApplyBatch(batch);
        
        AreEqual(0,    store.GetArchetype(ComponentTypes.Get<Position>()).Count);
        AreEqual(2000, store.GetArchetype(ComponentTypes.Get<Position, Scale3>()).Count);
        AreEqual(2000, query.Count);
        foreach (var entity in query.Entities) {
            AreEqual(new Scale3(1, 2, 3), entity.GetComponent<Scale3>());
        }
        // applying the batch again changes nothing
        query.Entities.ApplyBatch(batch);
        AreEqual(2000, store.GetArchetype(ComponentTypes.Get<Position, Scale3>()).Count);
    }
    
    /// Event handlers require sending events per entity => fallback to apply the batch per entity.
    [Test]
    public static void Test_Batch_Bulk_events()
    {
        var store       = CreateStore(out _);
        int added       = 0;
        int removed     = 0;
        int tagsChanged = 0;
        store.OnComponentAdded   += _ => added++;
        store.OnComponentRemoved += _ => removed++;
        store.OnTagsChanged      += _ => tagsChanged++;
        
        store.Query<Position>().Entities.ApplyBatch(CreateBatch());
        
        AreEqual(Count / 4 * 3, added);         // Scale3 added to all entities with Position
        AreEqual(Count / 4,     removed);       // Rotation removed
        AreEqual(Count / 4 * 3, tagsChanged);
        AreEqual(Count / 4 * 3, store.GetArchetype(ComponentTypes.Get<Position, Scale3>(), Tags.Get<TestTag2>()).Count);
    }
    
    /// Indexed components require updating their index per entity => fallback to apply the batch per entity.
    [Test]
    public static void Test_Batch_Bulk_indexed_component()
    {
        var store = CreateStore(out _);
        var batch = new EntityBatch();
        batch.Add(new IndexedInt { value = 42 });
        
        store.Query<Position>().Entities.ApplyBatch(batch);
        
        AreEqual(Count / 4 * 3, store.GetEntitiesWithComponentValue<IndexedInt, int>(42).Count);
        
        batch.Clear();
        batch.Remove<IndexedInt>();
        store.Query<Position>().Entities.ApplyBatch(batch);
        AreEqual(0,             store.GetEntitiesWithComponentValue<IndexedInt, int>(42).Count);
    }
    
    /// A query with a value condition returns single entities of an archetype => only these entities are changed. 
    [Test]
    public static void Test_Batch_Bulk_value_condition()
    {
        var store = new EntityStore();
        for (int n = 0; n < 100; n++) {
            store.CreateEntity(new Position(n, 0, 0), new IndexedInt { value = n % 2 });
        }
        var batch = new EntityBatch();
        batch.AddTag<TestTag2>();
        
        store.Query<Position>().HasValue<IndexedInt, int>(1).Entities.ApplyBatch(batch);
        
        AreEqual(50, store.GetArchetype(ComponentTypes.Get<Position, IndexedInt>(), Tags.Get<TestTag2>()).Count);
        AreEqual(50, store.GetArchetype(ComponentTypes.Get<Position, IndexedInt>()).Count);
    }
    
    [Test]
    public static void Test_Batch_Bulk_within_query_loop()
    {
        var store = CreateStore(out _);
        var query = store.Query<Position>();
        var batch = CreateBatch();
        foreach (var _ in query.Entities) {
            Throws<StructuralChangeException>(() => {
                query.Entities.ApplyBatch(batch);
            });
            break;
        }
        // an empty query does not throw
        var empty = store.Query<MyComponent1>();
        foreach (var _ in query.Entities) {
            empty.Entities.ApplyBatch(batch);
            break;
        }
        AreEqual(Count / 4, store.GetArchetype(ComponentTypes.Get<Position>()).Count);
    }
    
    /// The capacity of emptied archetypes shrinks like removing their entities one by one.
    [Test]
    public static void Test_Batch_Bulk_shrink()
    {
        var store = new EntityStore { ShrinkRatioThreshold = 0 };
        for (int n = 0; n < 10_000; n++) {
            store.CreateEntity(new Position(n, 0, 0));
        }
        var source = store.GetArchetype(ComponentTypes.Get<Position>());
        AreEqual(16384, source.Capacity);
        
        var batch = new EntityBatch();
        batch.AddTag<TestTag>();
        store.Query<Position>().Entities.ApplyBatch(batch);
        
        AreEqual(0,     source.Count);
        AreEqual(1024,  source.Capacity);
        var target = store.GetArchetype(ComponentTypes.Get<Position>(), Tags.Get<TestTag>());
        AreEqual(10_000, target.Count);
        AreEqual(16384,  target.Capacity);
        
        // emptied archetype can be reused
        batch.Clear();
        batch.RemoveTag<TestTag>();
        store.Query<Position>().Entities.ApplyBatch(batch);
        AreEqual(10_000, source.Count);
        AreEqual(0,      target.Count);
        int count = 0;
        foreach (var entity in store.Query<Position>().Entities) {
            count++;
            AreEqual(entity.Id - 1, (int)entity.GetComponent<Position>().x);
        }
        AreEqual(10_000, count);
    }
}

}
