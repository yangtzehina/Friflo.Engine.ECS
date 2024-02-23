﻿using Friflo.Engine.ECS;
using NUnit.Framework;
using static NUnit.Framework.Assert;

// ReSharper disable InconsistentNaming
namespace Internal.ECS;

public static class Test_EntityList
{
    [Test]
    public static void Test_EntityList_DebugView()
    {
        var store   = new EntityStore(PidType.UsePidAsId);
        var list    = new EntityList(store);
        list.Add(store.CreateEntity(1).Id);
        list.Add(store.CreateEntity(2).Id);
        
        var debugView   = new EntityListDebugView(list);
        var entities    = debugView.Entities;
        
        AreEqual(2, entities.Length);
        AreEqual(1, entities[0].Id);
        AreEqual(2, entities[1].Id);
    }
}