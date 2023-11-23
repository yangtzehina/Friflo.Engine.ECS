using Friflo.Fliox.Engine.ECS;
using Friflo.Fliox.Engine.ECS.Sync;
using NUnit.Framework;
using static NUnit.Framework.Assert;

// ReSharper disable InconsistentNaming
namespace Tests.ECS.Sync;

public static class Test_ComponentWriter
{
    [Test]
    public static void Test_ComponentWriter_write_components()
    {
        var store       = new EntityStore(PidType.UsePidAsId);
        var converter   = EntityConverter.Default;
        
        var entity  = store.CreateEntity(10);
        var child   = store.CreateEntity(11);
        entity.AddChild(child);
        entity.AddComponent(new Position { x = 1, y = 2, z = 3 });
        entity.AddScript(new TestScript1 { val1 = 10 });
        
        var node = converter.EntityToDataEntity(entity);
        
        AreEqual(10,    node.pid);
        AreEqual(1,     node.children.Count);
        AreEqual(11,    node.children[0]);
        AreEqual("{\"pos\":{\"x\":1,\"y\":2,\"z\":3},\"testRef1\":{\"val1\":10}}", node.components.AsString());
    }
    
    [Test]
    public static void Test_ComponentWriter_write_EntityName()
    {
        var store       = new EntityStore(PidType.UsePidAsId);
        var converter   = EntityConverter.Default;
        
        var entity      = store.CreateEntity();
        entity.AddComponent(new EntityName("test"));
        var dataEntity = converter.EntityToDataEntity(entity);
        
        AreEqual("{\"name\":{\"value\":\"test\"}}", dataEntity.components.AsString());
    }
    
    [Test]
    public static void Test_ComponentWriter_write_empty_components()
    {
        var store       = new EntityStore(PidType.UsePidAsId);
        var converter   = EntityConverter.Default;
        
        var entity  = store.CreateEntity(10);
        var node    = converter.EntityToDataEntity(entity);
        
        AreEqual(10,    node.pid);
        IsNull  (node.children);
    }
    
    [Test]
    public static void Test_ComponentWriter_write_tags()
    {
        var store       = new EntityStore(PidType.UsePidAsId);
        var converter   = EntityConverter.Default;
        
        var entity  = store.CreateEntity(10);
        entity.AddTag<TestTag>();
        var node    = converter.EntityToDataEntity(entity);
        
        AreEqual(10,                node.pid);
        AreEqual(1,                 node.tags.Count);
        Contains(nameof(TestTag),   node.tags);
    }
    
    [Test]
    public static void Test_ComponentWriter_write_components_Perf()
    {
        var store       = new EntityStore(PidType.UsePidAsId);
        var converter   = EntityConverter.Default;
        
        var entity  = store.CreateEntity(10);
        entity.AddComponent(new Position { x = 1, y = 2, z = 3 });
        entity.AddScript(new TestScript1 { val1 = 10 });

        int count = 10; // 2_000_000 ~ 1.935 ms
        DataEntity node = null;
        for (int n = 0; n < count; n++) {
            node = converter.EntityToDataEntity(entity);
        }
        AreEqual("{\"pos\":{\"x\":1,\"y\":2,\"z\":3},\"testRef1\":{\"val1\":10}}", node!.components.AsString());
    }
    
    [Test]
    public static void Test_ComponentWriter_DataEntity()
    {
        var dataEntity = new DataEntity { pid = 1234 };
        AreEqual("pid: 1234", dataEntity.ToString());
    }
    
    [Test]
    public static void Test_Entity_DebugJSON()
    {
        var store       = new EntityStore(PidType.UsePidAsId);
        var entity      = store.CreateEntity(10);
        var child       = store.CreateEntity(11);
        var unresolved  = new Unresolved { tags = new [] { "xyz " } };
        entity.AddChild(child);
        entity.AddComponent(new Position { x = 1, y = 2, z = 3 });
        entity.AddComponent(unresolved);
        entity.AddTag<TestTag>();
        entity.AddScript(new TestScript1 { val1 = 10 });

#pragma warning disable CS0618 // Type or member is obsolete
        var json = entity.DebugJSON;
#pragma warning restore CS0618 // Type or member is obsolete
        
        var expect =
"""
{
    "id": 10,
    "children": [
        11
    ],
    "components": {
        "pos": {"x":1,"y":2,"z":3},
        "testRef1": {"val1":10}
    },
    "tags": [
        "TestTag",
        "xyz "
    ]
}
""";
        AreEqual(expect, json);
    }
}

