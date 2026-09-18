// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using System;

// ReSharper disable once CheckNamespace
namespace Friflo.Engine.ECS;

/// <summary>
/// A set of <see cref="CommandBuffer"/>'s - one for each task of a <see cref="QueryJob.RunParallel"/> execution.<br/>
/// Use <see cref="ChunkEntities.TaskIndex"/> to get the <see cref="CommandBuffer"/> of the executing task. 
/// </summary>
/// <remarks>
/// Compared to <see cref="CommandBufferSynced"/>:<br/>
/// - Recording commands requires no lock as each task records to its own <see cref="CommandBuffer"/>.<br/>
/// - <see cref="Playback"/> executes the buffers in the order of their task index.
///   The components of a chunk are assigned to tasks in ascending order.
///   So the result of <see cref="Playback"/> - the order of entities in archetypes and the ids of created entities -
///   is <b>deterministic</b> for a given <see cref="ParallelJobRunner.ThreadCount"/>.
///   It does not depend on thread scheduling like the command order recorded by a <see cref="CommandBufferSynced"/>.<br/>
/// <br/>
/// <see cref="CommandBuffer.CreateEntity"/> requires reserving entity ids with <see cref="ReserveEntityIds"/> on the main thread
/// before executing the job.
/// </remarks>
public sealed class TaskCommandBuffers
{
#region public properties
    /// <summary> Number of <see cref="CommandBuffer"/>'s. One for each task. </summary>
    public              int             Count       => buffers.Length;
    
    /// <summary> Return the <see cref="CommandBuffer"/> of the task with the given <paramref name="taskIndex"/>. </summary>
    public              CommandBuffer   this[int taskIndex] => buffers[taskIndex];
    
    public   override   string          ToString()  => $"tasks: {buffers.Length}";
    #endregion
    
#region private fields
    private readonly    EntityStore     store;
    private readonly    CommandBuffer[] buffers;
    #endregion
    
    internal TaskCommandBuffers(EntityStore store, CommandBuffer[] buffers) {
        this.store      = store;
        this.buffers    = buffers;
    }
    
#region methods
    /// <summary>
    /// Ensure every task can create <paramref name="countPerTask"/> entities with <see cref="CommandBuffer.CreateEntity"/>.<br/>
    /// Must be called on the <b>main</b> thread. Reserved ids not used by a task remain reserved for subsequent job executions.
    /// </summary>
    public void ReserveEntityIds(int countPerTask)
    {
        if (countPerTask < 0) {
            throw new ArgumentException($"expect countPerTask >= 0. was: {countPerTask}", nameof(countPerTask));
        }
        foreach (var buffer in buffers) {
            buffer.ReserveIds(countPerTask);
        }
    }
    
    /// <summary>
    /// Return reserved entity ids not used by <see cref="CommandBuffer.CreateEntity"/> to the <see cref="EntityStore"/>.<br/>
    /// Must be called on the <b>main</b> thread.
    /// </summary>
    public void ReleaseEntityIds()
    {
        // release in reverse order so ids are reused in the same order they were reserved
        for (int n = buffers.Length - 1; n >= 0; n--) {
            buffers[n].ReleaseIds();
        }
    }
    
    /// <summary> Number of reserved entity ids available for the task with the given <paramref name="taskIndex"/>. </summary>
    public int GetReservedEntityIds(int taskIndex) => buffers[taskIndex].ReservedIdCount;
    
    /// <summary>
    /// Execute the recorded entity changes of all tasks in the order of their task index.<br/>
    /// <see cref="Playback"/> must be called on the <b>main</b> thread.
    /// </summary>
    public void Playback()
    {
        if (store.internBase.activeQueryLoops > 0) {
            throw EntityStoreBase.StructuralChangeWithinQueryLoop();
        }
        foreach (var buffer in buffers) {
            buffer.Playback();
        }
    }
    
    /// <summary> Remove the recorded entity changes of all tasks. </summary>
    public void Clear()
    {
        foreach (var buffer in buffers) {
            buffer.Clear();
        }
    }
    #endregion
}
