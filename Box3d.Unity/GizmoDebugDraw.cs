using System;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;
using LineList = System.Collections.Generic.List<Unity.Mathematics.float3>;

namespace Box3D.Unity
{
    public class GizmoDebugDrawShape : IDebugShape
    {
        public float3[] lines;
    }

    public class CompoundChild
    {
        public GizmoDebugDrawShape Shape { get; }
        public B3Transform Transform { get; }
        public CompoundChild(GizmoDebugDrawShape shape, B3Transform transform)
        {
            Shape = shape;
            Transform = transform;
        }
    }

    internal sealed unsafe class CompoundDebugShape : IDebugShape
    {
        public readonly Sys.b3CompoundData* Data;
        public readonly CompoundChild[] Children;
        public CompoundDebugShape(CompoundView view, CompoundChild[] children)
        {
            Data = view.NativeData;
            Children = children;
        }
        public CompoundView View => new(Data);
    }

    public class GizmoDebugShapeFactory : IDebugShapeFactory
    {
        private static void AddLine(LineList lines, float3 a, float3 b)
        {
            lines.Add(a);
            lines.Add(b);
        }

        private static void AddCircle(LineList lines, float3 center, float radius, float3 axisA, float3 axisB)
        {
            const int segments = 16;
            float3 previous = center + axisA * radius;
            for (int i = 1; i <= segments; i++)
            {
                float angle = i * (2f * math.PI / segments);
                float3 next = center + (axisA * math.cos(angle) + axisB * math.sin(angle)) * radius;
                AddLine(lines, previous, next);
                previous = next;
            }
        }

        private void AddSphereChildren(CompoundView compound,
            List<CompoundChild> children, in Shape owner)
        {
            foreach (var compound_sphere in compound.Spheres)
            {
                IDebugShape debug_shape = CreateSphere(compound_sphere.sphere, in owner);

                if (debug_shape == null) continue;

                children.Add(new CompoundChild((GizmoDebugDrawShape)debug_shape, B3Transform.Identity));
            }
        }

        private void AddCapsuleChildren(CompoundView compound, List<CompoundChild> children, in Shape owner)
        {
            foreach (var compound_capsule in compound.Capsules)
            {
                var debug_shape = (GizmoDebugDrawShape)CreateCapsule(compound_capsule.capsule, in owner);
                if (debug_shape == null) continue;

                children.Add(new CompoundChild(debug_shape, B3Transform.Identity));
            }
        }

        private void AddHullChildren(CompoundView compound, List<CompoundChild> children, in Shape owner)
        {
            var hull_count = compound.HullCount;
            for (int i = 0; i < hull_count; i++)
            {
                var hull = compound.GetHull(i);
                var debug_shape = (GizmoDebugDrawShape)CreateHull(hull.Hull, in owner);
                if (debug_shape == null) continue;

                children.Add(new CompoundChild(debug_shape, hull.Transform));
            }
        }

        private void AddMeshChildren(CompoundView compound, List<CompoundChild> children, in Shape owner)
        {
            var mesh_count = compound.MeshCount;
            for (int i = 0; i < mesh_count; i++)
            {
                var mesh = compound.GetMesh(i);
                var debug_shape = (GizmoDebugDrawShape)CreateMesh(mesh.Mesh, owner);
                if (debug_shape == null) continue;

                children.Add(new CompoundChild(debug_shape, mesh.Transform));
            }
        }

        private void DestroyCompoundChildren(CompoundChild[] children)
        {
            foreach (var child in children)
            {
                try
                {
                    DestroyShape(child.Shape);
                }
                catch (Exception destroyException)
                {
                    Debug.LogException(destroyException);
                }
            }
        }

        // Buffered geometry
        public IDebugShape CreateSphere(in Sphere sphere, in Shape source)
        {
            var lines = new LineList(128);
            AddCircle(lines, sphere.Center, sphere.Radius, new float3(1f, 0f, 0f), new float3(0f, 1f, 0f));
            AddCircle(lines, sphere.Center, sphere.Radius, new float3(0f, 1f, 0f), new float3(0f, 0f, 1f));
            AddCircle(lines, sphere.Center, sphere.Radius, new float3(0f, 0f, 1f), new float3(1f, 0f, 0f));
            return new GizmoDebugDrawShape { lines = lines.ToArray() };
        }

        public IDebugShape CreateCapsule(in Capsule capsule, in Shape source)
        {
            var lines = new LineList(128);
            float3 axis = math.normalizesafe(capsule.Center2 - capsule.Center1, new float3(0f, 1f, 0f));
            float3 side = math.normalizesafe(math.cross(axis, new float3(0.371f, 0.827f, 0.421f)), new float3(1f, 0f, 0f));
            float3 forward = math.cross(axis, side);
            float radius = capsule.Radius;
            AddCircle(lines, capsule.Center1, capsule.Radius, side, forward);
            AddCircle(lines, capsule.Center2, capsule.Radius, side, forward);
            AddLine(lines, capsule.Center1 + side * radius, capsule.Center2 + side * radius);
            AddLine(lines, capsule.Center1 - side * radius, capsule.Center2 - side * radius);
            AddLine(lines, capsule.Center1 + forward * radius, capsule.Center2 + forward * radius);
            AddLine(lines, capsule.Center1 - forward * radius, capsule.Center2 - forward * radius);
            return new GizmoDebugDrawShape { lines = lines.ToArray() };
        }

        // View are valid only during the call
        public IDebugShape CreateHull(HullView hull, in Shape source)
        {
            var lines = new LineList(128);
            ReadOnlySpan<float3> points = hull.Points;
            ReadOnlySpan<HullHalfEdge> edges = hull.HalfEdges;

            for (int i = 0; i < edges.Length; ++i)
            {
                HullHalfEdge edge = edges[i];

                if (i >= edge.Twin)
                    continue;

                HullHalfEdge twin = edges[edge.Twin];

                AddLine(
                    lines,
                    points[edge.Origin],
                    points[twin.Origin]);
            }
            return new GizmoDebugDrawShape { lines = lines.ToArray() };
        }

        // Unsupported (occlusion issues with lines)
        public IDebugShape CreateMesh(MeshView mesh, in Shape source) => null;

        // Unsupported (occlusion issues with lines)
        public IDebugShape CreateHeightField(HeightFieldView heightField, in Shape source) => null;

        public IDebugShape CreateCompound(CompoundView compound, in Shape source)
        {
            var children = new List<CompoundChild>(
                compound.SphereCount +
                compound.CapsuleCount +
                compound.HullCount +
                compound.MeshCount);

            try
            {
                AddCapsuleChildren(compound, children, source);
                AddHullChildren(compound, children, source);
                AddMeshChildren(compound, children, source);
                AddSphereChildren(compound, children, source);

                return children.Count == 0
                    ? null
                    : new CompoundDebugShape(compound, children.ToArray());
            }
            catch
            {
                // DestroyCompoundChildren(factory, children);
                throw;
            }
        }

        public void DestroyShape(IDebugShape shape)
        {
            switch (shape)
            {
                case GizmoDebugDrawShape gizmoShape:
                    gizmoShape.lines = null;
                    break;
                case CompoundDebugShape compoundShape:
                    DestroyCompoundChildren(compoundShape.Children);
                    break;
            }
        }
    }

    public class GizmoDebugDrawTarget : IDebugDrawTarget
    {
        public int drawCallCount;
        public B3Aabb screenBounds = new()
        {
            LowerBound = new float3(-50f, -50f, -50f),
            UpperBound = new float3(50f, 50f, 50f)
        };

        private static Color ToColor(uint hex)
        {
            return new Color(((hex >> 16) & 0xFF) / 255f, ((hex >> 8) & 0xFF) / 255f, (hex & 0xFF) / 255f);
        }

        private static B3Transform Multiply(in B3Transform a, in B3Transform b)
        {
            return new B3Transform
            {
                Position = a.Position + math.rotate(a.Rotation, b.Position),
                Rotation = math.mul(a.Rotation, b.Rotation),
            };
        }

        private static void Line(float3 a, float3 b, Color color)
        {
            Debug.DrawLine(a, b, color);
        }

        private static float3[] Corners(B3Aabb aabb)
        {
            var corners = new float3[8];
            for (int i = 0; i < 8; i++)
            {
                corners[i] = new float3(
                    (i & 1) == 0 ? aabb.LowerBound.x : aabb.UpperBound.x,
                    (i & 2) == 0 ? aabb.LowerBound.y : aabb.UpperBound.y,
                    (i & 4) == 0 ? aabb.LowerBound.z : aabb.UpperBound.z);
            }
            return corners;
        }

        private static void DrawEdges(float3[] corners, Color color)
        {
            // Connect corners differing in exactly one bit (12 box edges).
            for (int i = 0; i < 8; i++)
            {
                for (int bit = 1; bit <= 4; bit <<= 1)
                {
                    int j = i | bit;
                    if (j != i) Line(corners[i], corners[j], color);
                }
            }
        }

        private static void DrawCircleLines(float3 center, float radius, float3 axisA, float3 axisB, Color color)
        {
            const int segments = 16;
            float3 previous = center + axisA * radius;
            for (int i = 1; i <= segments; i++)
            {
                float angle = i * (2f * math.PI / segments);
                float3 next = center + (axisA * math.cos(angle) + axisB * math.sin(angle)) * radius;
                Line(previous, next, color);
                previous = next;
            }
        }

        private bool DrawCompound(CompoundDebugShape compound, in B3Transform compound_transform, uint color, B3Aabb treeBounds)
        {
            int capacity = compound.Children.Length;
            Span<int> childIndices = capacity <= 128
                ? stackalloc int[capacity]
                : new int[capacity];

            CompoundQueryResult result = compound.View.QueryChildren(treeBounds, childIndices);

            for (int i = 0; i < result.Count; i++)
            {
                int childIndex = childIndices[i];
                ref readonly CompoundChild child = ref compound.Children[childIndex];
                B3Transform child_transform = Multiply(compound_transform, child.Transform);
                DrawShape(child.Shape, in child_transform, color);
            }
            return true;
        }

        // Return true if drawing should continue
        public bool DrawShape(IDebugShape shape, in B3Transform transform, uint color)
        {
            if (shape is CompoundDebugShape compoundShape)
            {
                // Can use compoundShape.Data->tree
                return DrawCompound(compoundShape, in transform, color, screenBounds);
            }

            if (shape is not GizmoDebugDrawShape gizmoShape)
            {
                Debug.LogError("Invalid shape type");
                return true;
            }

            drawCallCount++;
            Color unityColor = ToColor(color);
            for (int i = 0; i < gizmoShape.lines.Length; i += 2)
            {
                Line(transform.Position + math.mul(transform.Rotation, gizmoShape.lines[i]),
                    transform.Position + math.mul(transform.Rotation, gizmoShape.lines[i + 1]), unityColor);
            }
            return true;
        }

        // Immediate geometry
        public void DrawSegment(float3 start, float3 end, uint color)
        {
            drawCallCount++;
            Line(start, end, ToColor(color));
        }

        public void DrawTransform(in B3Transform transform)
        {
            drawCallCount++;
            const float axisLength = 0.3f;
            float3 p = transform.Position;
            Line(p, p + math.mul(transform.Rotation, new float3(axisLength, 0f, 0f)), Color.red);
            Line(p, p + math.mul(transform.Rotation, new float3(0f, axisLength, 0f)), Color.green);
            Line(p, p + math.mul(transform.Rotation, new float3(0f, 0f, axisLength)), Color.blue);
        }

        public void DrawPoint(float3 position, float size, uint color)
        {
            drawCallCount++;
            Color unityColor = ToColor(color);
            float h = size * 0.02f;
            Line(
                position - new float3(h, 0f, 0f),
                position + new float3(h, 0f, 0f),
                unityColor);
            Line(
                position - new float3(0f, h, 0f),
                position + new float3(0f, h, 0f),
                unityColor);
            Line(
                position - new float3(0f, 0f, h),
                position + new float3(0f, 0f, h),
                unityColor);
        }

        public void DrawSphere(float3 position, float radius, uint color, float alpha)
        {
            drawCallCount++;
            Color unityColor = ToColor(color);
            DrawCircleLines(
                position,
                radius,
                new float3(1f, 0f, 0f),
                new float3(0f, 1f, 0f),
                unityColor);
            DrawCircleLines(
                position,
                radius,
                new float3(0f, 1f, 0f),
                new float3(0f, 0f, 1f),
                unityColor);
            DrawCircleLines(
                position,
                radius,
                new float3(0f, 0f, 1f),
                new float3(1f, 0f, 0f),
                unityColor);
        }

        public void DrawCapsule(float3 p1, float3 p2, float radius, uint color, float alpha)
        {
            drawCallCount++;
            Color unityColor = ToColor(color);
            float3 axis = math.normalizesafe(p2 - p1, new float3(0f, 1f, 0f));
            float3 side = math.normalizesafe(math.cross(axis, new float3(0.371f, 0.827f, 0.421f)), new float3(1f, 0f, 0f));
            float3 forward = math.cross(axis, side);

            DrawCircleLines(p1, radius, side, forward, unityColor);
            DrawCircleLines(p2, radius, side, forward, unityColor);
            Line(p1 + side * radius, p2 + side * radius, unityColor);
            Line(p1 - side * radius, p2 - side * radius, unityColor);
            Line(p1 + forward * radius, p2 + forward * radius, unityColor);
            Line(p1 - forward * radius, p2 - forward * radius, unityColor);
        }

        public void DrawBounds(in B3Aabb bounds, uint color)
        {
            drawCallCount++;
            DrawEdges(Corners(bounds), ToColor(color));
        }

        public void DrawBox(float3 extents, in B3Transform transform, uint color)
        {
            drawCallCount++;
            var corners = new float3[8];
            for (int i = 0; i < 8; i++)
            {
                float3 local = new(
                    (i & 1) == 0 ? -extents.x : extents.x,
                    (i & 2) == 0 ? -extents.y : extents.y,
                    (i & 4) == 0 ? -extents.z : extents.z);
                corners[i] = transform.Position + math.mul(transform.Rotation, local);
            }
            DrawEdges(corners, ToColor(color));
        }

        public void DrawString(float3 p, string str, uint color) { }
    }
}
