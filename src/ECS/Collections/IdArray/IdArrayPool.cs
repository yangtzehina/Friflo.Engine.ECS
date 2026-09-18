// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using System;

// ReSharper disable UseCollectionExpression
// ReSharper disable once CheckNamespace
namespace Friflo.Engine.ECS.Collections;

internal sealed class IdArrayPool
{
    public              int             Count       => count;
    internal            int             FreeCount   => freeStarts.Count;
    
    private             int[]           ids;
    private             StackArray<int> freeStarts;
    private  readonly   int             arraySize;
    private             int             freeStart;
    private             int             maxStart;
    private             int             count;

    public override string ToString() => $"arraySize: {arraySize} count: {count}";

    internal IdArrayPool(int poolIndex)
    {
        arraySize   = 2 << (poolIndex - 1);
        ids         = Array.Empty<int>();
        freeStarts  = new StackArray<int>(Array.Empty<int>());
    }
    
    #region snapshot
    internal void SaveTo(ref IdArrayPoolSnapshot snapshot)
    {
        snapshot.used       = true;
        snapshot.freeStart  = freeStart;
        snapshot.maxStart   = maxStart;
        snapshot.count      = count;
        snapshot.freeCount  = freeStarts.CopyTo(ref snapshot.freeStarts);
        // only ids < freeStart are in use
        if (snapshot.ids == null || snapshot.ids.Length < freeStart) {
            snapshot.ids = new int[freeStart];
        }
        Array.Copy(ids, snapshot.ids, freeStart);
    }
    
    internal void RestoreFrom(in IdArrayPoolSnapshot snapshot)
    {
        if (!snapshot.used) {
            freeStart   = 0;
            count       = 0;
            freeStarts.Clear();
            return;
        }
        freeStart   = snapshot.freeStart;
        count       = snapshot.count;
        if (ids.Length < snapshot.freeStart) {
            maxStart = Math.Max(maxStart, snapshot.maxStart);
            ArrayUtils.Resize(ref ids, maxStart);
        }
        Array.Copy(snapshot.ids, ids, snapshot.freeStart);
        freeStarts.Set(snapshot.freeStarts, snapshot.freeCount);
    }
    #endregion
    
    internal static int[] GetIds(int count, IdArrayHeap heap)
    {
        return heap.pools[IdArrayHeap.PoolIndex(count)].ids;
    }
    
    internal static IdArrayPool GetPool(IdArrayHeap heap, int index, out int[] ids)
    {
        var pool = heap.pools[index];
        ids = pool.ids;
        return pool;
    }

    /// <summary>
    /// Return the start index within the returned newIds.
    /// </summary>
    internal int CreateArray(out int[] newIds)
    {
        count++;
        if (freeStarts.TryPop(out var start)) {
            newIds = ids;
            return start;
        }
        start       = freeStart;
        freeStart   = start + arraySize;
        if (start < maxStart) {
            newIds = ids;
            return start;
        }
        maxStart    = Math.Max(4 * arraySize, 2 * maxStart);
        ArrayUtils.Resize(ref ids, maxStart);
        newIds = ids;
        return start;
    }
    
    /// <summary>
    /// Delete the array with the passed start index.
    /// </summary>
    internal void DeleteArray(int start, out int[] ids)
    {
        count--;
        ids = this.ids;
        if (count > 0) {
            freeStarts.Push(start);
            return;
        }
        freeStart = 0;
        freeStarts.Clear();
    }
    
}

internal struct IdArrayPoolSnapshot
{
    internal    bool    used;
    internal    int[]   ids;
    internal    int[]   freeStarts;
    internal    int     freeCount;
    internal    int     freeStart;
    internal    int     maxStart;
    internal    int     count;
}
