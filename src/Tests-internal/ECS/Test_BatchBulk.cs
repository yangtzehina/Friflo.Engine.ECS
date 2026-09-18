using Friflo.Engine.ECS;
using NUnit.Framework;
using Tests.ECS;
using static NUnit.Framework.Assert;

// ReSharper disable InconsistentNaming
namespace Internal.ECS {

public static class Test_BatchBulk
{
    /// Components containing references are cleared in the emptied source archetype so they do not keep objects alive.
    /// Unmanaged components are left untouched.
    [Test]
    public static void Test_BatchBulk_clear_references()
    {
        var store = new EntityStore();
        for (int n = 0; n < 10; n++) {
            store.CreateEntity(new EntityName("name-" + n), new MyComponent1 { a = n + 1 });
        }
        var source      = store.GetArchetype(ComponentTypes.Get<EntityName, MyComponent1>());
        var nameHeap    = (StructHeap<EntityName>)  source.heapMap[StructInfo<EntityName>.Index];
        var compHeap    = (StructHeap<MyComponent1>)source.heapMap[StructInfo<MyComponent1>.Index];
        AreEqual("name-9", nameHeap.components[9].value);
        
        var batch = new EntityBatch();
        batch.AddTag<TestTag>();
        store.Query<EntityName>().Entities.ApplyBatch(batch);
        
        AreEqual(0, source.Count);
        for (int n = 0; n < 10; n++) {
            IsNull  (nameHeap.components[n].value);
            AreEqual(n + 1, compHeap.components[n].a);
        }
        var target      = store.GetArchetype(ComponentTypes.Get<EntityName, MyComponent1>(), Tags.Get<TestTag>());
        var targetNames = (StructHeap<EntityName>)target.heapMap[StructInfo<EntityName>.Index];
        AreEqual(10,        target.Count);
        AreEqual("name-9",  targetNames.components[9].value);
        
        // entity nodes refer to the target archetype and their component index
        foreach (var entity in store.Query<EntityName>().Entities) {
            ref var node = ref store.nodes[entity.Id];
            AreSame (target,    node.archetype);
            AreEqual(entity.Id, target.entityIds[node.compIndex]);
        }
    }
}

}
