using System;
using Unity.Mathematics;
using UnityEngine;
using LineList = System.Collections.Generic.List<Unity.Mathematics.float3>;

namespace Box3d.Unity
{
    public class GizmoDebugDrawShape : IDebugShape
    {
        public float3[] lines;
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

        // private sealed class CompoundDebugShape : IDebugShape
        // {
        //     public CompoundDebugShape(CompoundChild[] children)
        //     {
        //         Children = children;
        //     }

        //     public CompoundChild[] Children { get; }
        // }

        // private readonly struct CompoundChild
        // {
        //     public readonly IDebugShape Shape;
        //     public readonly B3Transform Transform;
        //     // TODO: Material IDs

        //     public CompoundChild(IDebugShape shape, B3Transform transform)
        //     {
        //         Shape = shape;
        //         Transform = transform;
        //     }
        // }

        // private static B3Transform Multiply(in B3Transform a, in B3Transform b)
        // {
        //     return new B3Transform
        //     {
        //         Position = a.Position + math.rotate(a.Rotation, b.Position),
        //         Rotation = math.mul(a.Rotation, b.Rotation),
        //     };
        // }

        // private static unsafe void AddSpheres(
        //     IDebugShapeFactory factory, b3CompoundData* compound,
        //     List<CompoundChild> children, in Shape owner)
        // {
        //     byte* base_ptr = (byte*)compound;

        //     b3CompoundSphere* spheres =
        //         (b3CompoundSphere*)(base_ptr + compound->sphereOffset);
        //     // TODO: Material ID in CompoundSphere not used.

        //     for (int i = 0; i < compound->sphereCount; ++i)
        //     {
        //         DebugShapeSource source = new(owner, i);
        //         b3CompoundSphere* instance = spheres + i;
        //         IDebugShape debug_shape = factory.CreateSphere(in instance->sphere, source);

        //         if (debug_shape == null) continue;

        //         children.Add(new CompoundChild(debug_shape, B3Transform.Identity));
        //     }
        // }

        // private static unsafe void AddCapsules(
        //     IDebugShapeFactory factory, b3CompoundData* compound,
        //     List<CompoundChild> children, in Shape owner)
        // {
        //     byte* base_ptr = (byte*)compound;

        //     b3CompoundCapsule* capsules =
        //         (b3CompoundCapsule*)(base_ptr + compound->capsuleOffset);
        //     // TODO: Material ID in CompoundCapsule not used.

        //     for (int i = 0; i < compound->capsuleCount; ++i)
        //     {
        //         DebugShapeSource source = new(owner, i);
        //         b3CompoundCapsule* instance = capsules + i;
        //         IDebugShape debug_shape = factory.CreateCapsule(in instance->capsule, source);

        //         if (debug_shape == null) continue;

        //         children.Add(new CompoundChild(debug_shape, B3Transform.Identity));
        //     }
        // }

        // private static unsafe void AddHulls(
        //     IDebugShapeFactory factory, b3CompoundData* compound,
        //     List<CompoundChild> children, in Shape owner)
        // {
        //     byte* base_ptr = (byte*)compound;

        //     b3CompoundHull* hulls =
        //         (b3CompoundHull*)(base_ptr + compound->hullOffset);
        //     // TODO: Material ID in CompoundHull not used

        //     for (int i = 0; i < compound->hullCount; ++i)
        //     {
        //         DebugShapeSource source = new(owner, i);
        //         b3CompoundHull* instance = hulls + i;
        //         IDebugShape debug_shape = factory.CreateHull(new HullView(instance->hull), source);

        //         if (debug_shape == null) continue;

        //         children.Add(new CompoundChild(debug_shape, B3Transform.Identity));
        //     }
        // }

        // private static unsafe void AddMeshes(
        //     IDebugShapeFactory factory, b3CompoundData* compound,
        //     List<CompoundChild> children, in Shape owner)
        // {
        //     byte* base_ptr = (byte*)compound;

        //     b3CompoundMesh* meshes = (b3CompoundMesh*)(base_ptr + compound->meshOffset);
        //     // TODO: Material IDs in CompoundMesh not used
        //     // Mesh triangle materials index into meshes.MaterialIndices array
        //     // B3_MAX_COMPOUND_MESH_MATERIALS = 4 => indices 0..3

        //     for (int i = 0; i < compound->meshCount; ++i)
        //     {
        //         DebugShapeSource source = new(owner, i);
        //         b3CompoundMesh* instance = meshes + i;

        //         IDebugShape debug_shape =
        //             factory.CreateMesh(new MeshView(instance->meshData, instance->scale), source);

        //         if (debug_shape == null) continue;

        //         children.Add(new CompoundChild(
        //             debug_shape,
        //             instance->transform));
        //     }
        // }

        // private static void DestroyCompoundChildren(IDebugShapeFactory factory, List<CompoundChild> children)
        // {
        //     foreach (var child in children)
        //     {
        //         try
        //         {
        //             factory.DestroyShape(child.Shape);
        //         }
        //         catch (Exception destroyException)
        //         {
        //             Debug.LogException(destroyException);
        //         }
        //     }
        // }

        // private static IDebugShape CreateCompound(IDebugShapeFactory factory, b3CompoundData* compound, in Shape owner)
        // {
        //     var children = new List<CompoundChild>(
        //         compound->sphereCount +
        //         compound->capsuleCount +
        //         compound->hullCount +
        //         compound->meshCount);

        //     try
        //     {
        //         AddSpheres(factory, compound, children, owner);
        //         AddCapsules(factory, compound, children, owner);
        //         AddHulls(factory, compound, children, owner);
        //         AddMeshes(factory, compound, children, owner);

        //         return children.Count == 0
        //             ? null
        //             : new CompoundDebugShape(children.ToArray());
        //     }
        //     catch
        //     {
        //         DestroyCompoundChildren(factory, children);
        //         throw;
        //     }
        // }

        // private static void DestroyCompound(IDebugShapeFactory factory, CompoundDebugShape compound)
        // {
        //     foreach (var child in compound.Children)
        //     {
        //         try
        //         {
        //             factory.DestroyShape(child.Shape);
        //         }
        //         catch (Exception destroyException)
        //         {
        //             Debug.LogException(destroyException);
        //         }
        //     }
        // }

        // private static bool DrawCompound(IDebugDrawTarget target, CompoundDebugShape compound,
        //     B3Transform compound_transform, uint color)
        // {
        //     foreach (var child in compound.Children)
        //     {
        //         B3Transform child_transform = Multiply(compound_transform, child.Transform);

        //         if (!target.DrawShape(child.Shape, in child_transform, color))
        //         {
        //             return false;
        //         }
        //     }
        //     return true;
        // }

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

        public IDebugShape CreateCompound(CompoundView compoundView, in Shape source) => null;

        public void DestroyShape(IDebugShape shape)
        {
            if (shape is GizmoDebugDrawShape gizmoShape)
            {
                gizmoShape.lines = null;
            }
        }
    }

    public class GizmoDebugDrawTarget : IDebugDrawTarget
    {
        public int drawCallCount;
        private static Color ToColor(uint hex)
        {
            return new Color(((hex >> 16) & 0xFF) / 255f, ((hex >> 8) & 0xFF) / 255f, (hex & 0xFF) / 255f);
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

        // Return true if drawing should continue
        public bool DrawShape(IDebugShape shape, in B3Transform transform, uint color)
        {
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
