// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using System;

// ReSharper disable once CheckNamespace
namespace Friflo.Engine.ECS;

/// <summary>
/// Used to access the component columns of an <see cref="Archetype"/> with <see cref="Archetype.VisitComponentColumns"/>
/// without knowing their component types at compile time.
/// </summary>
public interface IComponentColumnVisitor
{
    /// <summary>
    /// Called for every component type of an <see cref="Archetype"/>.
    /// </summary>
    /// <param name="archetype"> the archetype storing the components </param>
    /// <param name="componentType"> the type of the components. Its <see cref="SchemaType.Name"/> / <see cref="SchemaType.ComponentKey"/> is a stable identifier.</param>
    /// <param name="components"> the components of all entities in the archetype. Same order as <see cref="Archetype.EntityIds"/>.</param>
    void VisitColumn<T>(Archetype archetype, ComponentType componentType, Span<T> components) where T : struct;
}
