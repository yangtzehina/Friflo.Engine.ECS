﻿// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using static System.Diagnostics.DebuggerBrowsableState;
using Browse = System.Diagnostics.DebuggerBrowsableAttribute;

// ReSharper disable InconsistentNaming
// ReSharper disable once CheckNamespace
namespace Friflo.Engine.ECS;

public readonly struct EntityLink
{
    public              Entity          Target      => new Entity(Entity.store, target);
    public  override    string          ToString()  => $"Entity: {Entity.Id} -> Target: {target}  [{Component.GetType().Name}]";
    
    public  readonly    Entity          Entity;     // 16
    public  readonly    IComponent      Component;  //  8
    private readonly    int             target;     //  4

    internal EntityLink(in Entity entity, int target, IComponent component) {
        Entity      = entity;
        this.target = target;
        Component   = component;
    }
}

[DebuggerTypeProxy(typeof(EntityLinksDebugView))]
public readonly struct EntityLinks : IReadOnlyList<EntityLink>
{
#region properties
    public                  int         Count       => links.Length;
    public                  EntityStore Store       => target.store;
    public   override       string      ToString()  => $"EntityLinks[{Count}]";
    #endregion
    
#region fields
    [Browse(Never)] private  readonly   Entity          target;         // 16
    [Browse(Never)] internal readonly   EntityLink[]    links;  //  8
    #endregion
    
#region general
    internal EntityLinks(in Entity target, EntityLink[]  links) {
        this.target = target;
        this.links  = links;
    }
    
    public EntityLink this[int index] => links[index];
    
    public string Debug()
    {
        if (Count == 0) return "{ }";
        var sb = new StringBuilder();
        sb.Append("{ ");
        foreach (var link in links) {
            if (sb.Length > 2) sb.Append(", ");
            sb.Append(link.Entity.Id);
        }
        sb.Append(" }");
        return sb.ToString();
    }
    #endregion

    
#region IEnumerator
    public EntityLinkEnumerator                     GetEnumerator() => new EntityLinkEnumerator (this);
    
    // --- IEnumerable
    IEnumerator                         IEnumerable.GetEnumerator() => new EntityLinkEnumerator (this);

    // --- IEnumerable<>
    IEnumerator<EntityLink> IEnumerable<EntityLink>.GetEnumerator() => new EntityLinkEnumerator (this);
    #endregion
}


public struct EntityLinkEnumerator : IEnumerator<EntityLink>
{
    private  readonly   EntityLink[]    entityLinks;    //  8
    private             int             index;          //  4
    
    internal EntityLinkEnumerator(EntityLinks entityLinks) {
        this.entityLinks    = entityLinks.links;
        index               = -1;
    }
    
    // --- IEnumerator
    public          void         Reset()    => index = -1;

    readonly object IEnumerator.Current     => entityLinks[index];

    public   EntityLink         Current     => entityLinks[index];
    
    // --- IEnumerator
    public bool MoveNext()
    {
        if (index < entityLinks.Length - 1) {
            index++;
            return true;
        }
        return false;
    }
    
    public readonly void Dispose() { }
}

internal sealed class EntityLinksDebugView
{
    [Browse(RootHidden)]
    internal readonly    EntityLink[]    links;
    
    internal EntityLinksDebugView(EntityLinks entityLinks) {
        links = entityLinks.links;
    }
}

