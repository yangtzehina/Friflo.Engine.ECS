﻿// Copyright (c) Ullrich Praetz. All rights reserved.
// See LICENSE file in the project root for full license information.

// ReSharper disable InconsistentNaming
// ReSharper disable once CheckNamespace
// ReSharper disable ConvertToPrimaryConstructor
namespace Friflo.Engine.ECS;


/// <summary>
/// <see cref="UniqueEntity"/> is used to assign a unique <c>string</c> to an entity within an <see cref="EntityStore"/>.<br/>
/// <br/>
/// To find a <see cref="UniqueEntity"/> within an <see cref="EntityStore"/> use <see cref="EntityStore.GetUniqueEntity"/>.<br/>
/// It basically acts as a singleton within an <see cref="EntityStore"/>. 
/// </summary>
[ComponentKey("unique")]
public struct UniqueEntity : IComponent
{
    public          string  uid;  //  8
    
    public override string  ToString() => $"UniqueEntity: '{uid}'";

    public UniqueEntity (string uid) {
        this.uid = uid;
    }
}