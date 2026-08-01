
using System;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;

namespace Box3D
{
    public class DebugCompoundChild
    {
        public IDebugShape Shape { get; }
        public B3Transform Transform { get; }
        public DebugCompoundChild(IDebugShape shape, B3Transform transform)
        {
            Shape = shape;
            Transform = transform;
        }
    }

    public sealed unsafe class DebugCompoundShape : IDebugShape
    {
        public readonly Sys.b3CompoundData* Data;
        public readonly DebugCompoundChild[] Children;
        public DebugCompoundShape(CompoundView view, DebugCompoundChild[] children)
        {
            Data = view.NativeData;
            Children = children;
        }
        public CompoundView View => new(Data);
    }

    public static class CompoundDebugDraw
    {
        public static void AddSphereChildren(IDebugShapeFactory factory, CompoundView compound,
            List<DebugCompoundChild> children, in Shape owner)
        {
            foreach (var compound_sphere in compound.Spheres)
            {
                IDebugShape debug_shape = factory.CreateSphere(compound_sphere.sphere, in owner);

                if (debug_shape == null) continue;

                children.Add(new DebugCompoundChild(debug_shape, B3Transform.Identity));
            }
        }

        public static void AddCapsuleChildren(IDebugShapeFactory factory, CompoundView compound, List<DebugCompoundChild> children, in Shape owner)
        {
            foreach (var compound_capsule in compound.Capsules)
            {
                var debug_shape = factory.CreateCapsule(compound_capsule.capsule, in owner);
                if (debug_shape == null) continue;

                children.Add(new DebugCompoundChild(debug_shape, B3Transform.Identity));
            }
        }

        public static void AddHullChildren(IDebugShapeFactory factory, CompoundView compound, List<DebugCompoundChild> children, in Shape owner)
        {
            var hull_count = compound.HullCount;
            for (int i = 0; i < hull_count; i++)
            {
                var hull = compound.GetHull(i);
                var debug_shape = factory.CreateHull(hull.Hull, in owner);
                if (debug_shape == null) continue;

                children.Add(new DebugCompoundChild(debug_shape, hull.Transform));
            }
        }

        public static void AddMeshChildren(IDebugShapeFactory factory, CompoundView compound, List<DebugCompoundChild> children, in Shape owner)
        {
            var mesh_count = compound.MeshCount;
            for (int i = 0; i < mesh_count; i++)
            {
                var mesh = compound.GetMesh(i);
                var debug_shape = factory.CreateMesh(mesh.Mesh, in owner);
                if (debug_shape == null) continue;

                children.Add(new DebugCompoundChild(debug_shape, mesh.Transform));
            }
        }

        public static DebugCompoundShape CreateCompound(IDebugShapeFactory factory, CompoundView compound, in Shape source)
        {
            var children = new List<DebugCompoundChild>(
                compound.SphereCount +
                compound.CapsuleCount +
                compound.HullCount +
                compound.MeshCount);

            AddCapsuleChildren(factory, compound, children, source);
            AddHullChildren(factory, compound, children, source);
            AddMeshChildren(factory, compound, children, source);
            AddSphereChildren(factory, compound, children, source);

            return children.Count == 0
                    ? null
                    : new DebugCompoundShape(compound, children.ToArray());
        }

        public static void DestroyCompound(IDebugShapeFactory factory, DebugCompoundShape compound)
        {
            if (compound is null) return;

            for (int i = 0; i < compound.Children.Length; i++)
            {
                var child = compound.Children[i];
                if (child is null) continue;
                factory.DestroyShape(child.Shape);
            }
        }

        public static bool DrawCompound(IDebugDrawTarget target, DebugCompoundShape compound, in B3WorldTransform compound_transform, uint color, B3Aabb treeBounds)
        {
            int capacity = compound.Children.Length;
            Span<int> childIndices = capacity <= 128
                ? stackalloc int[capacity]
                : new int[capacity];

            CompoundQueryResult result = compound.View.QueryChildren(treeBounds, childIndices);

            for (int i = 0; i < result.Count; i++)
            {
                int childIndex = childIndices[i];
                ref readonly DebugCompoundChild child = ref compound.Children[childIndex];
                B3WorldTransform child_transform = compound_transform * child.Transform;
                if (!target.DrawShape(child.Shape, in child_transform, color))
                    return false;
            }
            return true;
        }
    }
}