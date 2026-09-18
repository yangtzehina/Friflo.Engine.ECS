// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using Friflo.Engine.ECS.Relations;
using Friflo.Engine.ECS.Utils;

// ReSharper disable once CheckNamespace
namespace Friflo.Engine.ECS;

public partial class EntityStore
{
    /// <summary>
    /// Create an in-memory snapshot of the store used to restore its state with <see cref="RestoreSnapshot"/>.<br/>
    /// E.g. to roll back a simulation or to return to a previous state in a test or an editor.
    /// </summary>
    /// <param name="snapshot">
    /// An optional snapshot previously created from this store. Its buffers are reused to avoid allocations.
    /// </param>
    /// <remarks>
    /// See <see cref="StoreSnapshot"/> for the state stored in a snapshot.<br/>
    /// Entity <see cref="Script"/>'s are not supported as their state cannot be copied.
    /// Event and signal handlers are no part of a snapshot.
    /// </remarks>
    /// <exception cref="NotSupportedException"> if the store contains entity <see cref="Script"/>'s </exception>
    public StoreSnapshot CreateSnapshot(StoreSnapshot snapshot = null)
    {
        if (snapshot == null) {
            snapshot = new StoreSnapshot(this);
        } else if (snapshot.store != this) {
            throw new ArgumentException("snapshot was created from a different EntityStore", nameof(snapshot));
        }
        AssertNoScripts();
        
        // --- nodes
        var nodesLength = nodes.Length;
        if (snapshot.nodes == null || snapshot.nodes.Length < nodesLength) {
            snapshot.nodes = new EntityNode[nodesLength];
        }
        Array.Copy(nodes, snapshot.nodes, nodesLength);
        snapshot.nodesLength    = nodesLength;
        snapshot.entityCount    = entityCount;
        
        // --- entity ids
        snapshot.sequenceId     = intern.sequenceId;
        snapshot.recycleIdCount = intern.recycleIds.CopyTo(ref snapshot.recycleIds);
        
        // --- archetypes
        var archetypes      = Archetypes;
        var archetypeCount  = archetypes.Length;
        if (snapshot.archetypes == null || snapshot.archetypes.Length < archetypeCount) {
            ArrayUtils.Resize(ref snapshot.archetypes, archetypeCount);
        }
        for (int n = 0; n < archetypeCount; n++) {
            archetypes[n].SaveTo(ref snapshot.archetypes[n]);
        }
        snapshot.archetypeCount = archetypeCount;
        
        // --- hierarchy
        var parentMap = extension.parentMap;
        if (snapshot.parentMap == null || snapshot.parentMap.Length < parentMap.Length) {
            snapshot.parentMap = new int[parentMap.Length];
        }
        Array.Copy(parentMap, snapshot.parentMap, parentMap.Length);
        snapshot.parentMapLength    = parentMap.Length;
        extension.childHeap.SaveTo(ref snapshot.childHeap);
        snapshot.storeRootId        = storeRoot.IsNull ? 0 : storeRoot.Id;
        
        // --- pids
        var id2Pid = extension.id2Pid;
        if (id2Pid != null) {
            var pidCount = id2Pid.Count;
            if (snapshot.pids == null || snapshot.pids.Length < pidCount) {
                snapshot.pids = new KeyValuePair<int, long>[pidCount];
            }
            int index = 0;
            foreach (var pair in id2Pid) {
                snapshot.pids[index++] = pair;
            }
            snapshot.pidCount = pidCount;
        }
        // --- relations
        var relationsMap = extension.relationsMap;
        if (relationsMap != null) {
            snapshot.relations ??= new RelationsSnapshot[relationsMap.Length];
            for (int n = 0; n < relationsMap.Length; n++) {
                var relations = relationsMap[n];
                if (relations == null) {
                    snapshot.relations[n].used = false;
                    continue;
                }
                relations.SaveTo(ref snapshot.relations[n]);
            }
        }
        return snapshot;
    }
    
    /// <summary>
    /// Restore the state of the store to the given <paramref name="snapshot"/> created with <see cref="CreateSnapshot"/>.
    /// </summary>
    /// <remarks>
    /// - Entities created after the snapshot are removed. Entities deleted after the snapshot exist again with their former id and revision.<br/>
    /// - <see cref="Entity"/> instances obtained after creating the snapshot must not be used anymore.<br/>
    /// - Subsequent calls of <see cref="CreateEntity()"/> return the same ids as they did after creating the snapshot.<br/>
    /// - Component indexes are rebuilt. No events are sent.<br/>
    /// - Event handlers of entities not present in the snapshot are removed. 
    /// </remarks>
    /// <exception cref="StructuralChangeException"> if called within a query loop </exception>
    /// <exception cref="NotSupportedException"> if the store contains entity <see cref="Script"/>'s </exception>
    public void RestoreSnapshot(StoreSnapshot snapshot)
    {
        if (snapshot == null) {
            throw new ArgumentNullException(nameof(snapshot));
        }
        if (snapshot.store != this) {
            throw new ArgumentException("snapshot was created from a different EntityStore", nameof(snapshot));
        }
        if (internBase.activeQueryLoops > 0) {
            throw StructuralChangeWithinQueryLoop();
        }
        AssertNoScripts();
        
        // --- remove all entities from component indexes. Requires the current components. 
        UpdateComponentIndexes(false);
        
        // --- archetypes
        var archetypes      = Archetypes;
        var archetypeCount  = snapshot.archetypeCount;
        for (int n = 0; n < archetypeCount; n++) {
            archetypes[n].RestoreFrom(snapshot.archetypes[n]);
        }
        for (int n = archetypeCount; n < archetypes.Length; n++) {
            archetypes[n].RemoveAllEntities(); // archetypes created after the snapshot
        }
        // --- nodes
        RestoreNodes(snapshot);
        entityCount         = snapshot.entityCount;
        
        // --- entity ids
        intern.sequenceId   = snapshot.sequenceId;
        intern.recycleIds.Set(snapshot.recycleIds, snapshot.recycleIdCount);
        
        // --- hierarchy
        var parentMap = extension.parentMap;
        if (parentMap.Length < snapshot.parentMapLength) {
            extension.parentMap = parentMap = new int[snapshot.parentMapLength];
        }
        Array.Copy (snapshot.parentMap, parentMap, snapshot.parentMapLength);
        Array.Clear(parentMap, snapshot.parentMapLength, parentMap.Length - snapshot.parentMapLength);
        extension.childHeap.RestoreFrom(snapshot.childHeap);
        storeRoot = snapshot.storeRootId == 0 ? default : new Entity(this, snapshot.storeRootId);
        
        // --- pids
        var id2Pid = extension.id2Pid;
        if (id2Pid != null) {
            var pid2Id = extension.pid2Id;
            id2Pid.Clear();
            pid2Id.Clear();
            for (int n = 0; n < snapshot.pidCount; n++) {
                var pair = snapshot.pids[n];
                id2Pid.Add(pair.Key,   pair.Value);
                pid2Id.Add(pair.Value, pair.Key);
            }
        }
        // --- relations
        var relationsMap = extension.relationsMap;
        if (relationsMap != null) {
            for (int n = 0; n < relationsMap.Length; n++) {
                var relations = relationsMap[n];
                if (relations == null) {
                    continue;
                }
                if (snapshot.relations == null) {
                    relations.RestoreFrom(default);
                } else {
                    relations.RestoreFrom(snapshot.relations[n]);
                }
            }
        }
        // --- add all entities to component indexes. Requires the restored components and nodes.
        UpdateComponentIndexes(true);
    }
    
    private void RestoreNodes(StoreSnapshot snapshot)
    {
        var localNodes      = nodes;        // nodes never shrink => localNodes.Length >= snapshot.nodesLength
        var snapshotNodes   = snapshot.nodes;
        var snapshotLength  = snapshot.nodesLength;
        for (int id = 0; id < localNodes.Length; id++)
        {
            ref var node        = ref localNodes[id];
            var snapshotNode    = id < snapshotLength ? snapshotNodes[id] : default;
            var hasEvent        = node.hasEvent;
            var signalTypeCount = node.signalTypeCount;
            if (hasEvent != 0 || signalTypeCount != 0) {
                // Event handlers are no part of a snapshot. Handlers of an entity are kept only if the same entity is present in the snapshot.
                var sameEntity = node.archetype != null && snapshotNode.archetype != null && node.revision == snapshotNode.revision;
                if (!sameEntity) {
                    RemoveAllEntityEventHandlers(this, node, id);
                    hasEvent        = 0;
                    signalTypeCount = 0;
                }
            }
            node                    = snapshotNode;
            node.hasEvent           = hasEvent;
            node.signalTypeCount    = signalTypeCount;
        }
    }
    
    /// <summary> Add / remove all entities having indexed components to / from their component index. </summary>
    private void UpdateComponentIndexes(bool add)
    {
        var indexTypes = Static.EntitySchema.indexTypes;
        if (indexTypes.Count == 0) {
            return;
        }
        foreach (var archetype in Archetypes)
        {
            var count = archetype.entityCount;
            if (count == 0) {
                continue;
            }
            var types = archetype.componentTypes;
            types.bitSet = BitSet.Intersect(types.bitSet, indexTypes.bitSet);
            if (types.Count == 0) {
                continue;
            }
            var ids     = archetype.entityIds;
            var heapMap = archetype.heapMap;
            foreach (var componentType in types)
            {
                var heap = heapMap[componentType.StructIndex];
                for (int index = 0; index < count; index++)
                {
                    var entity = new Entity(this, ids[index]);
                    if (add) {
                        heap.AddIndex(entity);
                    } else {
                        heap.StashComponent(index);
                        heap.RemoveIndex(entity);
                    }
                }
            }
        }
    }
    
    private void AssertNoScripts()
    {
        if (extension.entityScriptCount > 1) {
            throw new NotSupportedException("snapshots of an EntityStore containing entity Scripts are not supported");
        }
    }
}
