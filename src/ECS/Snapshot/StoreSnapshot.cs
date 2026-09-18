// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using Friflo.Engine.ECS.Collections;
using Friflo.Engine.ECS.Relations;

// ReSharper disable once CheckNamespace
namespace Friflo.Engine.ECS;

/// <summary>
/// An in-memory snapshot of an <see cref="EntityStore"/> created with <see cref="EntityStore.CreateSnapshot"/>.<br/>
/// It is used to restore the state of the store with <see cref="EntityStore.RestoreSnapshot"/>.
/// </summary>
/// <remarks>
/// A snapshot contains: entities with their ids and revisions, components, tags, the entity hierarchy, relations, pids
/// and the state required to create subsequent entity ids in the same order.<br/>
/// Components are copied by value. Reference type fields of a component refer to the same object in the store and the snapshot.<br/>
/// A snapshot can only be restored to the store it was created from. It is not a serialization format.
/// </remarks>
public sealed class StoreSnapshot
{
#region public properties
    /// <summary> Number of entities stored in the snapshot. </summary>
    public              int                     EntityCount     => entityCount;
    /// <summary> Number of archetypes of the store when the snapshot was created. </summary>
    public              int                     ArchetypeCount  => archetypeCount;
    public   override   string                  ToString()      => $"entities: {entityCount}";
    #endregion
    
#region internal fields
    internal readonly   EntityStore             store;
    // --- nodes
    internal            EntityNode[]            nodes;
    internal            int                     nodesLength;
    internal            int                     entityCount;
    // --- entity ids
    internal            int                     sequenceId;
    internal            int[]                   recycleIds;
    internal            int                     recycleIdCount;
    // --- archetypes
    internal            ArchetypeSnapshot[]     archetypes;
    internal            int                     archetypeCount;
    // --- hierarchy
    internal            int[]                   parentMap;
    internal            int                     parentMapLength;
    internal            IdArrayPoolSnapshot[]   childHeap;
    internal            int                     storeRootId;
    // --- pids
    internal            KeyValuePair<int,long>[] pids;
    internal            int                     pidCount;
    // --- relations
    internal            RelationsSnapshot[]     relations;
    #endregion
    
    internal StoreSnapshot(EntityStore store) {
        this.store = store;
    }
}

internal struct ArchetypeSnapshot
{
    internal    int     count;
    internal    int[]   ids;
    /// <summary> contains a T[] for each <see cref="Archetype.structHeaps"/> </summary>
    internal    Array[] columns;
}
