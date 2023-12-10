using System;
using Friflo.Fliox.Engine.ECS;
using Friflo.Fliox.Engine.ECS.Serialize;
using Friflo.Json.Fliox;
using NUnit.Framework;
using static NUnit.Framework.Assert;

// ReSharper disable InconsistentNaming
namespace Tests.ECS.Serialize;

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
        entity.AddTag<TestTag>();
        entity.AddTag<TestTag3>();
        entity.AddComponent(new Position { x = 1, y = 2, z = 3 });
        entity.AddScript(new TestScript1 { val1 = 10 });
        
        var dataEntity = converter.EntityToDataEntity(entity, null, false);
        
        AreEqual(10,                dataEntity.pid);
        
        AreEqual(2,                 dataEntity.tags.Count);
        AreEqual("test-tag",        dataEntity.tags[0]);
        AreEqual(nameof(TestTag3),  dataEntity.tags[1]);
        
        AreEqual(1,                 dataEntity.children.Count);
        AreEqual(11,                dataEntity.children[0]);
        AreEqual("{\"pos\":{\"x\":1,\"y\":2,\"z\":3},\"script1\":{\"val1\":10}}", dataEntity.components.AsString());
        
var expect =
"""
{
    "id": 10,
    "children": [
        11
    ],
    "components": {
        "pos": {"x":1,"y":2,"z":3},
        "script1": {"val1":10}
    },
    "tags": [
        "test-tag",
        "TestTag3"
    ]
}
""";
        var json = dataEntity.DebugJSON;
        AreEqual(expect, json);
        
        dataEntity.components = new JsonValue("xxx");
        json = dataEntity.DebugJSON;
        AreEqual("'components' error: unexpected character while reading value. Found: x path: '(root)' at position: 1", json);
    }
    
    [Test]
    public static void Test_ComponentWriter_write_EntityName()
    {
        var store       = new EntityStore(PidType.UsePidAsId);
        var converter   = EntityConverter.Default;
        
        var entity      = store.CreateEntity();
        entity.AddComponent(new EntityName("test"));
        var dataEntity = converter.EntityToDataEntity(entity, null, false);
        
        AreEqual("{\"name\":{\"value\":\"test\"}}", dataEntity.components.AsString());
    }
    
    [Test]
    public static void Test_ComponentWriter_write_empty_components()
    {
        var store       = new EntityStore(PidType.UsePidAsId);
        var converter   = EntityConverter.Default;
        
        var entity      = store.CreateEntity(10);
        var dataEntity  = converter.EntityToDataEntity(entity, null, false);
        
        AreEqual(10,    dataEntity.pid);
        IsNull  (dataEntity.children);
    }
    
    [Test]
    public static void Test_ComponentWriter_write_tags()
    {
        var store       = new EntityStore(PidType.UsePidAsId);
        var converter   = EntityConverter.Default;
        
        var entity      = store.CreateEntity(10);
        entity.AddTag<TestTag>();
        entity.AddTag<TestTag3>();
        var dataEntity  = converter.EntityToDataEntity(entity, null, false);
        
        AreEqual(10,                dataEntity.pid);
        AreEqual(2,                 dataEntity.tags.Count);
        Contains("test-tag",        dataEntity.tags);
        Contains(nameof(TestTag3),  dataEntity.tags);
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
        DataEntity dataEntity = null;
        for (int n = 0; n < count; n++) {
            dataEntity = converter.EntityToDataEntity(entity, null, false);
        }
        AreEqual("{\"pos\":{\"x\":1,\"y\":2,\"z\":3},\"script1\":{\"val1\":10}}", dataEntity!.components.AsString());
    }
    
    [Test]
    public static void Test_ComponentWriter_DataEntity()
    {
        var dataEntity = new DataEntity { pid = 1234 };
        AreEqual("pid: 1234", dataEntity.ToString());
    }
    
    [Test]
    public static void Test_Entity_DebugJSON_get()
    {
        var store       = new EntityStore(PidType.UsePidAsId);
        var entity      = store.CreateEntity(10);
        var child       = store.CreateEntity(11);
        var unresolved  = new Unresolved { tags = new [] { "xyz" } };
        entity.AddChild(child);
        entity.AddComponent(new Position { x = 1, y = 2, z = 3 });
        entity.AddComponent(unresolved);
        entity.AddTag<TestTag>();
        entity.AddTag<TestTag3>();
        entity.AddScript(new TestScript1 { val1 = 10 });

        AreEqual(SampleJson, entity.DebugJSON);
    }
    
    private const string SampleJson =
    """
    {
        "id": 10,
        "children": [
            11
        ],
        "components": {
            "pos": {"x":1,"y":2,"z":3},
            "script1": {"val1":10}
        },
        "tags": [
            "test-tag",
            "TestTag3",
            "xyz"
        ]
    }
    """;
    
    [Test]
    public static void Test_Entity_DebugJSON_set()
    {
        var store   = new EntityStore(PidType.UsePidAsId);
        var entity  = store.CreateEntity(1);
        
        entity.DebugJSON = SampleJson;
        
        AreEqual(1,                     entity.Id); // "id" in passed sample JSON is ignored
        AreEqual(1,                     entity.ChildCount);
        AreEqual(11,                    entity.ChildIds[0]);
        
        AreEqual(2,                     entity.Components.Count);
        AreEqual(new Position(1, 2, 3), entity.Position);
        
        var unresolved = entity.GetComponent<Unresolved>();
        AreEqual(1,                     unresolved.tags.Length);
        AreEqual("xyz",                 unresolved.tags[0]);
        
        AreEqual(1,                     entity.Scripts.Length);
        var script =                    entity.GetScript<TestScript1>();
        AreEqual(10,                    script.val1);
        
        AreEqual(2,                     entity.Tags.Count);
        IsTrue  (                       entity.Tags.Has<TestTag>());
        IsTrue  (                       entity.Tags.Has<TestTag3>());
    }
    
    [Test]
    public static void Test_Entity_DebugJSON_set_errors()
    {
        var store   = new EntityStore(PidType.UsePidAsId);
        var entity  = store.CreateEntity(1);
        {
            var e = Throws<ArgumentNullException>(() => {
                entity.DebugJSON = null;
            });
            AreEqual("Value cannot be null. (Parameter 'json')", e!.Message);
        } {
            var e = Throws<ArgumentException>(() => {
                entity.DebugJSON = "";
            });
            AreEqual("Error: unexpected EOF on root path: '(root)' at position: 0", e!.Message);
        } {
            var e = Throws<ArgumentException>(() => {
                entity.DebugJSON = "[]";
            });
            AreEqual("Error: expect object entity. was: ArrayStart at position: 1 path: '[]' at position: 1", e!.Message);
        } {
            var e = Throws<ArgumentException>(() => {
                entity.DebugJSON = "{}x";
            });
            AreEqual("Error: Expected EOF path: '(root)' at position: 3", e!.Message);
        } {
            var e = Throws<ArgumentException>(() => {
                entity.DebugJSON = "{";
            });
            AreEqual("Error: unexpected EOF > expect key path: '(root)' at position: 1", e!.Message);
        } {
            var e = Throws<ArgumentException>(() => {
                entity.DebugJSON = "{\"components\":[]}";
            });
            AreEqual("Error: expect 'components' == object. was: array. path: 'components[]' at position: 15", e!.Message);
        }
    }
}

