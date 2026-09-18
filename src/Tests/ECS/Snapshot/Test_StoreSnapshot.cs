using System;
using System.Collections.Generic;
using System.Text;
using Friflo.Engine.ECS;
using NUnit.Framework;
using Tests.ECS.Index;
using Tests.ECS.Relations;
using Tests.Utils;
using static NUnit.Framework.Assert;

// ReSharper disable AccessToModifiedClosure
// ReSharper disable InconsistentNaming
namespace Tests.ECS.Snapshot {

public static class Test_StoreSnapshot
{
    /// Signature of the entire store state: entity order in archetypes and all component values.
    private class SignatureVisitor : IComponentColumnVisitor
    {
        internal readonly StringBuilder sb = new StringBuilder();
        
        public void VisitColumn<T>(Archetype archetype, ComponentType componentType, Span<T> components) where T : struct {
            sb.Append(componentType.Name);
            sb.Append('=');
            foreach (var component in components) {
                sb.Append(component);
                sb.Append(',');
            }
            sb.Append(' ');
        }
    }
    
    private static string GetSignature(EntityStore store)
    {
        var visitor = new SignatureVisitor();
        var sb      = visitor.sb;
        foreach (var archetype in store.Archetypes) {
            if (archetype.Count == 0) {
                continue; // archetypes are never removed from a store
            }
            sb.Append(archetype.Name);
            sb.Append(" ids:");
            foreach (var id in archetype.EntityIds) {
                sb.Append(id);
                sb.Append(',');
            }
            sb.Append(' ');
            archetype.VisitComponentColumns(visitor);
            sb.Append('\n');
        }
        sb.Append($"count: {store.Count}");
        return sb.ToString();
    }
    
    [Test]
    public static void Test_StoreSnapshot_components_tags()
    {
        var store   = new EntityStore();
        var entity1 = store.CreateEntity(new Position(1, 0, 0));
        var entity2 = store.CreateEntity(new Position(2, 0, 0), new Rotation(2, 0, 0, 0));
        var entity3 = store.CreateEntity(new Position(3, 0, 0), new EntityName("name-3"), Tags.Get<TestTag>());
        var entity4 = store.CreateEntity();
        var expect  = GetSignature(store);
        
        var snapshot = store.CreateSnapshot();
        AreEqual(4,                 snapshot.EntityCount);
        AreEqual("entities: 4",     snapshot.ToString());
        AreEqual(store.Archetypes.Length, snapshot.ArchetypeCount);
        
        // --- change store
        entity1.GetComponent<Position>().x = 11;
        entity1.AddComponent(new Scale3(1, 1, 1));              // new archetype
        entity2.RemoveComponent<Rotation>();
        entity3.RemoveTag<TestTag>();
        entity3.AddComponent(new EntityName("changed"));
        entity4.DeleteEntity();
        var entity5 = store.CreateEntity(new MyComponent1 { a = 5 }); // reuses id 4
        var entity6 = store.CreateEntity(new Position(6, 0, 0));
        AreEqual(4, entity5.Id);
        AreNotEqual(expect, GetSignature(store));
        
        store.RestoreSnapshot(snapshot);
        
        AreEqual(expect, GetSignature(store));
        AreEqual(4,                                 store.Count);
        AreEqual(new Position(1, 0, 0),             entity1.GetComponent<Position>());
        AreEqual("id: 1  [Position]",               entity1.ToString());
        AreEqual("id: 2  [Position, Rotation]",     entity2.ToString());
        AreEqual("id: 3  \"name-3\"  [EntityName, Position, #TestTag]", entity3.ToString());
        IsFalse (entity4.IsNull);                   // deleted entity exists again - same id and revision
        AreEqual("id: 4  []",                       entity4.ToString());
        IsTrue  (entity5.IsNull);                   // entities created after the snapshot are removed
        IsTrue  (entity6.IsNull);
        AreEqual(0, store.GetArchetype(ComponentTypes.Get<Position, Scale3>()).Count);
        AreEqual(0, store.GetArchetype(ComponentTypes.Get<MyComponent1>()).Count);
        
        // --- store can be changed after restore
        entity1.AddComponent(new Scale3(2, 2, 2));
        entity4.DeleteEntity();
        AreEqual(3, store.Count);
        
        // --- a snapshot can be restored multiple times
        store.RestoreSnapshot(snapshot);
        AreEqual(expect, GetSignature(store));
    }
    
    /// Entities created after RestoreSnapshot() get the same ids as entities created after CreateSnapshot()
    [Test]
    public static void Test_StoreSnapshot_entity_ids()
    {
        var store = new EntityStore();
        for (int n = 0; n < 10; n++) {
            store.CreateEntity(new Position(n, 0, 0));
        }
        store.GetEntityById(3).DeleteEntity();
        store.GetEntityById(7).DeleteEntity();
        var snapshot = store.CreateSnapshot();
        
        var ids1 = new List<int>();
        for (int n = 0; n < 5; n++) {
            ids1.Add(store.CreateEntity().Id);
        }
        store.GetEntityById(1).DeleteEntity();
        
        store.RestoreSnapshot(snapshot);
        var ids2 = new List<int>();
        for (int n = 0; n < 5; n++) {
            ids2.Add(store.CreateEntity().Id);
        }
        AreEqual(new [] { 7, 3, 11, 12, 13 }, ids1);
        AreEqual(ids1, ids2);
    }
    
    [Test]
    public static void Test_StoreSnapshot_hierarchy()
    {
        var store   = new EntityStore();
        var root    = store.CreateEntity(1);
        var child2  = store.CreateEntity(2);
        var child3  = store.CreateEntity(3);
        var child4  = store.CreateEntity(4);
        root.AddChild(child2);
        root.AddChild(child3);
        child3.AddChild(child4);
        store.SetStoreRoot(root);
        
        var snapshot = store.CreateSnapshot();
        
        root.RemoveChild(child2);
        child4.DeleteEntity();
        var child5 = store.CreateEntity(5);
        root.InsertChild(0, child5);
        AreEqual(new [] { 5, 3 }, root.ChildIds.ToArray());
        
        store.RestoreSnapshot(snapshot);
        
        AreEqual(new [] { 2, 3 },   root.ChildIds.ToArray());
        AreEqual(new [] { 4 },      child3.ChildIds.ToArray());
        AreEqual(1,                 child2.Parent.Id);
        AreEqual(3,                 child4.Parent.Id);
        AreEqual(1,                 store.StoreRoot.Id);
        IsTrue  (child5.IsNull);
        // hierarchy can be changed after restore
        root.AddChild(child4);
        AreEqual(new [] { 2, 3, 4 }, root.ChildIds.ToArray());
        AreEqual(0,                 child3.ChildCount);
    }
    
    private static string IntRelations(Entity entity)
    {
        var sb = new StringBuilder();
        foreach (var relation in entity.GetRelations<IntRelation>()) {
            sb.Append(relation.value);
            sb.Append(' ');
        }
        return sb.ToString();
    }
    
    [Test]
    public static void Test_StoreSnapshot_relations()
    {
        var store   = new EntityStore();
        var entity1 = store.CreateEntity(1);
        var entity2 = store.CreateEntity(2);
        var entity3 = store.CreateEntity(3);
        entity1.AddRelation(new IntRelation { value = 10 });
        entity1.AddRelation(new IntRelation { value = 11 });
        entity2.AddRelation(new IntRelation { value = 20 });
        entity1.AddRelation(new AttackRelation { target = entity3, speed = 1 });
        entity2.AddRelation(new AttackRelation { target = entity3, speed = 2 });
        
        var snapshot = store.CreateSnapshot();
        
        entity1.RemoveRelation<IntRelation, int>(10);
        entity2.AddRelation(new IntRelation { value = 21 });
        entity3.DeleteEntity();     // removes both link relations
        entity1.AddRelation(new InventoryItem { type = InventoryItemType.Axe }); // relation type created after the snapshot
        AreEqual(0, entity1.GetRelations<AttackRelation>().Length);
        
        store.RestoreSnapshot(snapshot);
        
        AreEqual("10 11 ",      IntRelations(entity1));
        AreEqual("20 ",         IntRelations(entity2));
        AreEqual(1,             entity1.GetRelation<AttackRelation, Entity>(entity3).speed);
        AreEqual(2,             entity2.GetRelation<AttackRelation, Entity>(entity3).speed);
        AreEqual(2,             entity3.GetIncomingLinks<AttackRelation>().Count);
        AreEqual(0,             entity1.GetRelations<InventoryItem>().Length);
        
        // --- relations can be changed after restore. Deleting the link target removes its incoming links.
        entity1.AddRelation(new IntRelation { value = 12 });
        AreEqual("10 11 12 ",   IntRelations(entity1));
        entity3.DeleteEntity();
        AreEqual(0,             entity1.GetRelations<AttackRelation>().Length);
        AreEqual(0,             entity2.GetRelations<AttackRelation>().Length);
    }
    
    [Test]
    public static void Test_StoreSnapshot_indexes()
    {
        var store   = new EntityStore();
        var target  = store.CreateEntity(100);
        for (int n = 1; n <= 10; n++) {
            store.CreateEntity(new IndexedInt { value = n % 2 }, new LinkComponent { entity = target });
        }
        var snapshot = store.CreateSnapshot();
        
        var entity1 = store.GetEntityById(1);
        var entity2 = store.GetEntityById(2);
        entity1.AddComponent(new IndexedInt { value = 5 });     // update indexed value
        entity2.RemoveComponent<IndexedInt>();
        store.GetEntityById(3).DeleteEntity();
        store.CreateEntity(new IndexedInt { value = 1 });
        target.DeleteEntity();                                  // removes all LinkComponent's
        AreEqual(0, store.Query<LinkComponent>().Count);
        
        store.RestoreSnapshot(snapshot);
        
        AreEqual(5,     store.GetEntitiesWithComponentValue<IndexedInt, int>(0).Count);
        AreEqual(5,     store.GetEntitiesWithComponentValue<IndexedInt, int>(1).Count);
        AreEqual(0,     store.GetEntitiesWithComponentValue<IndexedInt, int>(5).Count);
        AreEqual(5,     store.Query().HasValue<IndexedInt, int>(1).Count);
        AreEqual(10,    target.GetIncomingLinks<LinkComponent>().Count);
        
        // --- indexes are maintained after restore
        entity1.AddComponent(new IndexedInt { value = 0 });
        AreEqual(6,     store.GetEntitiesWithComponentValue<IndexedInt, int>(0).Count);
        AreEqual(4,     store.GetEntitiesWithComponentValue<IndexedInt, int>(1).Count);
        target.DeleteEntity();
        AreEqual(0,     store.Query<LinkComponent>().Count);
    }
    
    [Test]
    public static void Test_StoreSnapshot_pids()
    {
        var store   = new EntityStore(PidType.RandomPids);
        var entity1 = store.CreateEntity();
        var entity2 = store.CreateEntity();
        var pid1    = entity1.Pid;
        var pid2    = entity2.Pid;
        var snapshot = store.CreateSnapshot();
        
        entity2.DeleteEntity();
        var entity3 = store.CreateEntity();
        var pid3    = entity3.Pid;
        
        store.RestoreSnapshot(snapshot);
        
        AreEqual(pid1, entity1.Pid);
        AreEqual(pid2, entity2.Pid);
        AreEqual(2,    store.PidToId(pid2));
        IsTrue (store.TryGetEntityByPid(pid2, out var found));
        AreEqual(2,    found.Id);
        IsFalse(store.TryGetEntityByPid(pid3, out _) && pid3 != pid2);
    }
    
    /// Event handlers are no part of a snapshot. Handlers of entities not present in the snapshot are removed.
    [Test]
    public static void Test_StoreSnapshot_event_handlers()
    {
        var store   = new EntityStore();
        var entity1 = store.CreateEntity(1);
        int events1 = 0;
        int events2 = 0;
        var snapshot = store.CreateSnapshot();
        
        entity1.OnComponentChanged += _ => events1++;       // handler added after the snapshot to an entity of the snapshot
        var entity2 = store.CreateEntity(2);
        entity2.OnComponentChanged += _ => events2++;
        entity2.OnTagsChanged      += _ => events2++;
        
        store.RestoreSnapshot(snapshot);
        
        entity1.AddComponent(new Position());
        AreEqual(1, events1);
        var newEntity2 = store.CreateEntity(2);             // same id as the removed entity
        newEntity2.AddComponent(new Position());
        newEntity2.AddTag<TestTag>();
        AreEqual(0, events2);
    }
    
    /// Re-simulating from a snapshot results in the same store state including the order of entities in archetypes.
    [Test]
    public static void Test_StoreSnapshot_rollback_deterministic()
    {
        var store = new EntityStore();
        for (int n = 0; n < 1000; n++) {
            store.CreateEntity(new Position(n, 0, 0), new MyComponent1 { a = n });
        }
        var query = store.Query<Position, MyComponent1>();
        
        void Simulate(int step)
        {
            var buffer = store.GetCommandBuffer();
            foreach (var (positions, components, entities) in query.Chunks) {
                for (int n = 0; n < entities.Length; n++) {
                    positions[n].x += 1;
                    var value = components[n].a + step;
                    var id    = entities[n];
                    if (value % 97 == 0) {
                        buffer.DeleteEntity(id); // a deleted entity must not be changed by other commands
                        continue;
                    }
                    if (value %  7 == 0)    buffer.AddTag<TestTag>(id);
                    if (value % 11 == 0)    buffer.AddComponent(id, new Rotation(step, 0, 0, 0));
                    if (value % 13 == 0)    buffer.RemoveTag<TestTag>(id);
                    if (value % 53 == 0) {
                        var newId = buffer.CreateEntity();
                        buffer.AddComponent(newId, new Position(0, step, 0));
                        buffer.AddComponent(newId, new MyComponent1 { a = value });
                    }
                }
            }
            buffer.Playback();
        }
        StoreSnapshot snapshot = null;
        for (int step = 0; step < 20; step++) {
            if (step == 10) snapshot = store.CreateSnapshot();
            Simulate(step);
        }
        var expect = GetSignature(store);
        
        for (int run = 0; run < 3; run++) {
            store.RestoreSnapshot(snapshot);
            for (int step = 10; step < 20; step++) {
                Simulate(step);
            }
            AreEqual(expect, GetSignature(store));
        }
    }
    
    /// Reusing a snapshot requires no allocations if its buffers are large enough.
    [Test]
    public static void Test_StoreSnapshot_reuse()
    {
        var store = new EntityStore();
        var root  = store.CreateEntity();
        for (int n = 0; n < 1000; n++) {
            var entity = store.CreateEntity(new Position(n, 0, 0), new EntityName("name"));
            entity.AddRelation(new IntRelation { value = n });
            root.AddChild(entity);
        }
        var snapshot = store.CreateSnapshot();
        store.CreateSnapshot(snapshot);         // warm up
        store.RestoreSnapshot(snapshot);
        
        var start = Mem.GetAllocatedBytes();
        var same  = store.CreateSnapshot(snapshot);
        store.RestoreSnapshot(snapshot);
        Mem.AssertNoAlloc(start);
        AreSame(snapshot, same);
        AreEqual(1001, snapshot.EntityCount);
    }
    
    [Test]
    public static void Test_StoreSnapshot_exceptions()
    {
        var store1      = new EntityStore();
        var store2      = new EntityStore();
        var snapshot1   = store1.CreateSnapshot();
        
        Throws<ArgumentNullException>(() => { store1.RestoreSnapshot(null); });
        var e = Throws<ArgumentException>(() => { store2.RestoreSnapshot(snapshot1); });
        StringAssert.StartsWith("snapshot was created from a different EntityStore", e!.Message);
        Throws<ArgumentException>(() => { store2.CreateSnapshot(snapshot1); });
        
        store1.CreateEntity(new Position());
        foreach (var _ in store1.Query<Position>().Entities) {
            Throws<StructuralChangeException>(() => { store1.RestoreSnapshot(snapshot1); });
        }
        // --- scripts are not supported
        var entity = store2.CreateEntity();
        entity.AddScript(new TestScript1());
        var e2 = Throws<NotSupportedException>(() => { store2.CreateSnapshot(); });
        AreEqual("snapshots of an EntityStore containing entity Scripts are not supported", e2!.Message);
    }
    
    private class CountVisitor : IComponentColumnVisitor
    {
        internal readonly List<string> columns = new List<string>();
        
        public void VisitColumn<T>(Archetype archetype, ComponentType componentType, Span<T> components) where T : struct {
            columns.Add($"{componentType.Name}[{components.Length}] {typeof(T).Name}");
            if (typeof(T) == typeof(MyComponent1)) {
                for (int n = 0; n < components.Length; n++) {
                    components[n] = (T)(object)new MyComponent1 { a = 42 }; // components can be modified
                }
            }
        }
    }
    
    [Test]
    public static void Test_StoreSnapshot_VisitComponentColumns()
    {
        var store = new EntityStore();
        for (int n = 0; n < 3; n++) {
            store.CreateEntity(new Position(n, 0, 0), new MyComponent1 { a = n });
        }
        store.CreateEntity(Tags.Get<TestTag>());
        var visitor = new CountVisitor();
        foreach (var archetype in store.Archetypes) {
            archetype.VisitComponentColumns(visitor);
        }
        visitor.columns.Sort();
        AreEqual(new [] { "MyComponent1[3] MyComponent1", "Position[3] Position" }, visitor.columns.ToArray());
        foreach (var entity in store.Query<MyComponent1>().Entities) {
            AreEqual(42, entity.GetComponent<MyComponent1>().a);
        }
        Throws<ArgumentNullException>(() => {
            store.Archetypes[0].VisitComponentColumns(null);
        });
    }
}

}
