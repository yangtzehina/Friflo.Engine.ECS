using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Serialize;
using NUnit.Framework;


// ReSharper disable ConditionalTernaryEqualBranch
// ReSharper disable CompareOfFloatsByEqualityOperator
// ReSharper disable StringLiteralTypo
// ReSharper disable InconsistentNaming
namespace Tests.ECS.Arch {

public static class Test_StructuralChangeException
{
    [Test]
    public static void Test_StructuralChangeException_Message()
    {
        var store = new EntityStore();
        store.CreateEntity();
        foreach (var entity in store.Entities)
        {
            var e = Assert.Throws<StructuralChangeException>(() => {
                entity.AddTag<TestTag>();
            });
            Assert.AreEqual("within query loop. See: https://friflo.gitbook.io/friflo.engine.ecs/documentation/query#structuralchangeexception", e!.Message);
        }
    }
    
    /// Deleting an entity within a query loop moves the last entity of the archetype to the position of the deleted entity.
    /// Without the exception this entity would be skipped by the loop.
    [Test]
    public static void Test_StructuralChangeException_DeleteEntity()
    {
        var store = new EntityStore();
        for (int n = 0; n < 10; n++) {
            store.CreateEntity(new Position(n, 0, 0));
        }
        var query = store.Query<Position>();
        int count = 0;
        foreach (var entity in query.Entities) {
            count++;
            Assert.Throws<StructuralChangeException>(() => {
                entity.DeleteEntity();
            });
        }
        Assert.AreEqual(10, count);
        Assert.AreEqual(10, store.Count);
        
        query.ForEachEntity((ref Position _, Entity entity) => {
            Assert.Throws<StructuralChangeException>(() => {
                entity.DeleteEntity();
            });
        });
        foreach (var (_, entities) in query.Chunks) {
            foreach (var entity in entities) {
                Assert.Throws<StructuralChangeException>(() => {
                    entity.DeleteEntity();
                });
            }
        }
        Assert.AreEqual(10, store.Count);
        
        // --- delete entities within a query loop using a CommandBuffer
        var buffer = store.GetCommandBuffer();
        foreach (var entity in query.Entities) {
            if (entity.Id % 2 == 0) {
                buffer.DeleteEntity(entity.Id);
            }
        }
        buffer.Playback();
        Assert.AreEqual(5, store.Count);
        
        // --- exception is not thrown if disabled by the query
        query.ThrowOnStructuralChange = false;
        foreach (var entity in query.Entities) {
            entity.DeleteEntity();
            break;
        }
        Assert.AreEqual(4, store.Count);
        
        // --- deleting an entity outside a query loop
        foreach (var entity in query.Entities.ToEntityList()) {
            entity.DeleteEntity();
        }
        Assert.AreEqual(0, store.Count);
    }
    
    [Test]
    public static void Test_StructuralChangeException_Entities()
    {
        var store = new EntityStore();
        store.CreateEntity(new MyComponent1(), new MyComponent2(), new MyComponent3(), new MyComponent4(), new MyComponent4());
        foreach (var entity in store.Entities)
        {
            TestExceptions(store, entity);
        }
    }
    
    private static void TestExceptions(EntityStore store, Entity entity)
    {
        Assert.Throws<StructuralChangeException>(() => {
            entity.AddTag<TestTag>();
        });
        Assert.Throws<StructuralChangeException>(() => {
            entity.RemoveTag<TestTag>();
        });
        Assert.Throws<StructuralChangeException>(() => {
            entity.AddComponent<Position>();
        });
        Assert.Throws<StructuralChangeException>(() => {
            entity.RemoveComponent<Position>();
        });
        
        var target = store.CreateEntity();
        Assert.Throws<StructuralChangeException>(() => {
            entity.CopyEntity(target);
        });
        Assert.Throws<StructuralChangeException>(() => {
            target.DeleteEntity();
        });
        Assert.IsFalse(target.IsNull); // entity is not deleted
        
        var buffer = store.GetCommandBuffer();
        Assert.Throws<StructuralChangeException>(() => {
            buffer.Playback();
        });
        
        var entityBatch = new EntityBatch();
        Assert.Throws<StructuralChangeException>(() => {
            entityBatch.ApplyTo(entity);
        });
        
        TestMultiAddRemoveExceptions(entity);
        
        var converter = EntityConverter.Default;
        var dataEntity = new DataEntity { pid = 1  };
        Assert.Throws<StructuralChangeException>(() => {
            converter.DataEntityToEntity(dataEntity, store, out _);
        });
    }
    
        
    private static void TestMultiAddRemoveExceptions(Entity entity)
    {
        // --- add multiple components
        Assert.Throws<StructuralChangeException>(() => {
            entity.Add(new Position());
        });
        Assert.Throws<StructuralChangeException>(() => {
            entity.Add(new Position(), new Scale3());
        });
        Assert.Throws<StructuralChangeException>(() => {
            entity.Add(new Position(), new Scale3(), new Rotation());
        });
        Assert.Throws<StructuralChangeException>(() => {
            entity.Add(new Position(), new Scale3(), new Rotation(), new MyComponent1());
        });
        Assert.Throws<StructuralChangeException>(() => {
            entity.Add(new Position(), new Scale3(), new Rotation(), new MyComponent1(), new MyComponent2());
        });
        Assert.Throws<StructuralChangeException>(() => {
            entity.Add(new Position(), new Scale3(), new Rotation(), new MyComponent1(), new MyComponent2(), new MyComponent3());
        });
        Assert.Throws<StructuralChangeException>(() => {
            entity.Add(new Position(), new Scale3(), new Rotation(), new MyComponent1(), new MyComponent2(), new MyComponent3(), new MyComponent4());
        });
        Assert.Throws<StructuralChangeException>(() => {
            entity.Add(new Position(), new Scale3(), new Rotation(), new MyComponent1(), new MyComponent2(), new MyComponent3(), new MyComponent4(), new MyComponent5());
        });
        Assert.Throws<StructuralChangeException>(() => {
            entity.Add(new Position(), new Scale3(), new Rotation(), new MyComponent1(), new MyComponent2(), new MyComponent3(), new MyComponent4(), new MyComponent5(), new MyComponent6());
        });
        Assert.Throws<StructuralChangeException>(() => {
            entity.Add(new Position(), new Scale3(), new Rotation(), new MyComponent1(), new MyComponent2(), new MyComponent3(), new MyComponent4(), new MyComponent5(), new MyComponent6(), new MyComponent7());
        });
        
        // --- remove multiple components
        Assert.Throws<StructuralChangeException>(() => {
            entity.Remove<Position>();
        });
        Assert.Throws<StructuralChangeException>(() => {
            entity.Remove<Position, Scale3>();
        });
        Assert.Throws<StructuralChangeException>(() => {
            entity.Remove<Position, Scale3, Rotation>();
        });
        Assert.Throws<StructuralChangeException>(() => {
            entity.Remove<Position, Scale3, Rotation, MyComponent1>();
        });
        Assert.Throws<StructuralChangeException>(() => {
            entity.Remove<Position, Scale3, Rotation, MyComponent1, MyComponent2>();
        });
        Assert.Throws<StructuralChangeException>(() => {
            entity.Remove<Position, Scale3, Rotation, MyComponent1, MyComponent2, MyComponent3>();
        });
        Assert.Throws<StructuralChangeException>(() => {
            entity.Remove<Position, Scale3, Rotation, MyComponent1, MyComponent2, MyComponent3, MyComponent4>();
        });
        Assert.Throws<StructuralChangeException>(() => {
            entity.Remove<Position, Scale3, Rotation, MyComponent1, MyComponent2, MyComponent3, MyComponent4, MyComponent5>();
        });
        Assert.Throws<StructuralChangeException>(() => {
            entity.Remove<Position, Scale3, Rotation, MyComponent1, MyComponent2, MyComponent3, MyComponent4, MyComponent5, MyComponent6>();
        });
        Assert.Throws<StructuralChangeException>(() => {
            entity.Remove<Position, Scale3, Rotation, MyComponent1, MyComponent2, MyComponent3, MyComponent4, MyComponent5, MyComponent6, MyComponent7>();
        });
    }
    
}

}

