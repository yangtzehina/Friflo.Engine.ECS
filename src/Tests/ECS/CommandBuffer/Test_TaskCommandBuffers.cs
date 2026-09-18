using System;
using System.Text;
using Friflo.Engine.ECS;
using NUnit.Framework;
using static NUnit.Framework.Assert;

// ReSharper disable AccessToDisposedClosure
// ReSharper disable InconsistentNaming
namespace Tests.ECS.Buffer {

public static class Test_TaskCommandBuffers
{
    private const int EntityCount = 10_000;
    
    /// Record commands in a parallel job depending only on entity ids.
    private static string RunJob(int threadCount, bool parallel)
    {
        using var runner = new ParallelJobRunner(threadCount);
        var store = new EntityStore();
        for (int n = 1; n <= EntityCount; n++) {
            store.CreateEntity(new Position(n, 0, 0));
        }
        var buffers = store.GetTaskCommandBuffers(runner);
        AreEqual(threadCount, buffers.Count);
        buffers.ReserveEntityIds(EntityCount);
        
        var job = store.Query<Position>().ForEach((positions, entities) =>
        {
            var commands = buffers[entities.TaskIndex];
            for (int n = 0; n < entities.Length; n++) {
                var id = entities[n];
                if (id % 11 == 0) {
                    commands.DeleteEntity(id);  // a deleted entity must not be changed by other commands
                    continue;
                }
                if (id %  2 == 0)   commands.AddTag<TestTag>(id);
                if (id %  3 == 0)   commands.AddComponent(id, new Rotation(id, 0, 0, 0));
                if (id %  5 == 0)   commands.RemoveComponent<Position>(id);
                if (id %  7 == 0) {
                    var newId = commands.CreateEntity();
                    commands.AddComponent(newId, new Scale3(id, 0, 0));
                }
            }
        });
        job.JobRunner               = runner;
        job.MinParallelChunkLength  = 100;
        if (parallel) {
            job.RunParallel();
        } else {
            job.Run();
        }
        buffers.Playback();
        buffers.ReleaseEntityIds();
        
        var created = EntityCount / 7 - EntityCount / 77; // entities divisible by 7 create an entity - except deleted ones
        AreEqual(EntityCount - EntityCount / 11 + created, store.Count);
        return GetSignature(store);
    }
    
    /// The signature contains the order of entities in all archetypes and the component value of created entities.
    private static string GetSignature(EntityStore store)
    {
        var sb = new StringBuilder();
        foreach (var archetype in store.Archetypes) {
            sb.Append(archetype.Name);
            sb.Append(':');
            foreach (var id in archetype.EntityIds) {
                sb.Append(id);
                sb.Append(',');
            }
            sb.Append('\n');
        }
        foreach (var entity in store.Query<Scale3>().Entities) {
            sb.Append(entity.Id);
            sb.Append('=');
            sb.Append(entity.GetComponent<Scale3>().x);
            sb.Append(',');
        }
        return sb.ToString();
    }
    
    /// The result of Playback() - entity order in archetypes and ids of created entities - must not depend on thread scheduling.
    [Test]
    public static void Test_TaskCommandBuffers_deterministic()
    {
        foreach (var threadCount in new [] { 2, 3, 8 }) {
            var expect = RunJob(threadCount, true);
            for (int n = 0; n < 20; n++) {
                var signature = RunJob(threadCount, true);
                if (expect != signature) {
                    Fail($"result depends on thread scheduling. threadCount: {threadCount}, run: {n}");
                }
            }
        }
    }
    
    /// Sequential execution records all commands to the buffer of task 0. 
    [Test]
    public static void Test_TaskCommandBuffers_sequential()
    {
        var run1 = RunJob(4, false);
        var run2 = RunJob(4, false);
        AreEqual(run1, run2);
    }
    
    [Test]
    public static void Test_TaskCommandBuffers_reserve_ids()
    {
        var store   = new EntityStore();
        var buffers = store.GetTaskCommandBuffers(2);
        AreEqual("tasks: 2", buffers.ToString());
        
        var e = Throws<InvalidOperationException>(() => {
            buffers[0].CreateEntity();
        });
        AreEqual("no reserved entity id available. Use TaskCommandBuffers.ReserveEntityIds() before running the job.", e!.Message);
        
        buffers.ReserveEntityIds(2);
        AreEqual(2, buffers.GetReservedEntityIds(0));
        AreEqual(2, buffers.GetReservedEntityIds(1));
        
        // ids are reserved per task in ascending order
        AreEqual(1, buffers[0].CreateEntity());
        AreEqual(3, buffers[1].CreateEntity());
        AreEqual(2, buffers[0].CreateEntity());
        AreEqual(0, buffers.GetReservedEntityIds(0));
        AreEqual(1, buffers.GetReservedEntityIds(1));
        
        // top-up: task 1 keeps its reserved id 4 and gets one new id. Task 0 gets two new ids.
        buffers.ReserveEntityIds(2);
        AreEqual(4, buffers[1].CreateEntity());
        AreEqual(5, buffers[0].CreateEntity());
        AreEqual(7, buffers[1].CreateEntity());
        
        buffers.Playback();
        AreEqual(6, store.Count);
        for (int id = 1; id <= 7; id++) {
            AreEqual(id != 6, store.TryGetEntityById(id, out _));
        }
        // released ids are reused by the store
        buffers.ReleaseEntityIds();
        AreEqual(0, buffers.GetReservedEntityIds(0));
        AreEqual(6, store.CreateEntity().Id);
        AreEqual(8, store.CreateEntity().Id);
        
        // task buffers can be reused after Playback()
        buffers[1].AddTag<TestTag>(1);
        buffers.Clear();
        buffers[0].AddTag<TestTag2>(1);
        buffers.Playback();
        AreEqual("id: 1  [#TestTag2]", store.GetEntityById(1).ToString());
    }
    
    [Test]
    public static void Test_TaskCommandBuffers_exceptions()
    {
        var store = new EntityStore();
        store.CreateEntity(new Position());
        Throws<ArgumentException>(() => {
            store.GetTaskCommandBuffers(0);
        });
        Throws<ArgumentNullException>(() => {
            store.GetTaskCommandBuffers(null);
        });
        var buffers = store.GetTaskCommandBuffers(1);
        Throws<ArgumentException>(() => {
            buffers.ReserveEntityIds(-1);
        });
        foreach (var _ in store.Query<Position>().Entities) {
            Throws<StructuralChangeException>(() => {
                buffers.Playback();
            });
        }
    }
}

}
