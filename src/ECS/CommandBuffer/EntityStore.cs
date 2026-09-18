// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;

// ReSharper disable once CheckNamespace
namespace Friflo.Engine.ECS;


public partial class EntityStore
{
    /// <summary>
    /// Returns a <see cref="CommandBuffer"/> used to record and <see cref="CommandBuffer.Playback"/> entity changes. 
    /// </summary>
    public CommandBuffer GetCommandBuffer()
    {
        var pool = intern.commandBufferPool ??= new Stack<CommandBuffer>();
        lock (pool)
        {
            if (pool.TryPop(out var buffer)) {
                buffer.Reuse();
                return buffer;
            }
        }
        return new CommandBuffer(this);
    }
    
    /// <summary>
    /// Returns a set of <see cref="CommandBuffer"/>'s - one for each task of a <see cref="QueryJob.RunParallel"/> execution.<br/>
    /// See <see cref="TaskCommandBuffers"/>
    /// </summary>
    /// <param name="taskCount"> The number of tasks. Typically <see cref="ParallelJobRunner.ThreadCount"/> of the runner executing the job.</param>
    public TaskCommandBuffers GetTaskCommandBuffers(int taskCount)
    {
        if (taskCount < 1) {
            throw new ArgumentException($"expect taskCount > 0. was: {taskCount}", nameof(taskCount));
        }
        var buffers = new CommandBuffer[taskCount];
        for (int n = 0; n < taskCount; n++) {
            var buffer = new CommandBuffer(this);
            buffer.InitTaskBuffer();
            buffers[n] = buffer;
        }
        return new TaskCommandBuffers(this, buffers);
    }
    
    /// <summary>
    /// Returns a set of <see cref="CommandBuffer"/>'s - one for each thread of the given <paramref name="jobRunner"/>.<br/>
    /// See <see cref="TaskCommandBuffers"/>
    /// </summary>
    public TaskCommandBuffers GetTaskCommandBuffers(ParallelJobRunner jobRunner)
    {
        if (jobRunner == null) {
            throw new ArgumentNullException(nameof(jobRunner));
        }
        return GetTaskCommandBuffers(jobRunner.ThreadCount);
    }
    
    internal void ReturnCommandBuffer(CommandBuffer commandBuffer)
    {
        var pool = intern.commandBufferPool;
        lock (pool) {
            pool.Push(commandBuffer);
        }
    }
    
    internal Playback GetPlayback()
    {
        if (intern.playback == null) {
            intern.playback = new Playback(this);
        }
        return intern.playback;
    }
}