using System;
using System.Collections.Generic;
using DCFApixels;
using Unity.Mathematics;
using UnityEngine;

namespace Box3d.Unity
{
    public class DebugXDrawShape : IDebugShape { }

    public sealed class DebugXSphere : DebugXDrawShape
    {
        public Sphere sphere;

        public DebugXSphere(Sphere sphere)
        {
            this.sphere = sphere;
        }
    }

    public sealed class DebugXCapsule : DebugXDrawShape
    {
        public Capsule capsule;

        public DebugXCapsule(Capsule capsule)
        {
            this.capsule = capsule;
        }
    }

    public sealed class DebugXHull : DebugXDrawShape
    {
        public readonly Mesh mesh;

        public DebugXHull(Mesh mesh)
        {
            this.mesh = mesh;
        }
    }

    public class DebugXMesh : DebugXDrawShape
    {
        public readonly Mesh mesh;
        public readonly float3 scale;

        public DebugXMesh(Mesh mesh, float3 scale)
        {
            this.mesh = mesh;
            this.scale = scale;
        }
    }

    public class DebugXHeightField : DebugXDrawShape
    {
        public readonly Mesh mesh;

        public DebugXHeightField(Mesh mesh)
        {
            this.mesh = mesh;
        }
    }

    public class DebugXShapeFactory : IDebugShapeFactory
    {
        // Buffered geometry
        public IDebugShape CreateSphere(in Sphere sphere, in Shape source)
        {
            return new DebugXSphere(sphere);
        }

        public IDebugShape CreateCapsule(in Capsule capsule, in Shape source)
        {
            return new DebugXCapsule(capsule);
        }

        // View are valid only during the call
        public IDebugShape CreateHull(HullView hull, in Shape source)
        {
            Vector3[] vertices = new Vector3[hull.PointCount];

            ReadOnlySpan<float3> points = hull.Points;

            for (int i = 0; i < vertices.Length; ++i)
            {
                float3 point = points[i];
                vertices[i] = new Vector3(point.x, point.y, point.z);
            }

            ReadOnlySpan<HullHalfEdge> edges = hull.HalfEdges;
            // Two indices per unique edge.
            int uniqueEdgeCount = edges.Length / 2;
            int[] indices = new int[uniqueEdgeCount * 2];

            int outputIndex = 0;

            for (int i = 0; i < edges.Length; ++i)
            {
                HullHalfEdge edge = edges[i];

                // Each physical edge appears twice in a half-edge mesh.
                // Keep only one of the pair.
                if (i > edge.Twin)
                    continue;

                indices[outputIndex++] = edge.Origin;
                indices[outputIndex++] = edges[edge.Twin].Origin;
            }

            Mesh mesh = new() { name = "Box3D Debug Hull" };

            mesh.SetVertices(vertices);
            mesh.SetIndices(indices, MeshTopology.Lines, 0);
            mesh.RecalculateBounds();

            return new DebugXHull(mesh);
        }

        public IDebugShape CreateMesh(MeshView mesh, in Shape source)
        {
            Mesh unity_mesh = new() { name = "Box3D Debug Mesh" };

            ReadOnlySpan<float3> vertices = mesh.Vertices;
            ReadOnlySpan<MeshTriangle> triangles = mesh.Triangles;

            if (vertices.Length > ushort.MaxValue)
            {
                Debug.LogWarning("Mesh has more vertices than ushort.MaxValue, using UInt32 index format");
                unity_mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            }

            Vector3[] unity_vertices = new Vector3[vertices.Length];
            int[] unity_triangles = new int[triangles.Length * 3];

            for (int i = 0; i < vertices.Length; ++i)
            {
                float3 point = vertices[i];
                unity_vertices[i] = new Vector3(point.x, point.y, point.z);
            }

            for (int i = 0; i < triangles.Length; ++i)
            {
                MeshTriangle triangle = triangles[i];
                unity_triangles[i * 3] = triangle.Index1;
                unity_triangles[i * 3 + 1] = triangle.Index2;
                unity_triangles[i * 3 + 2] = triangle.Index3;
            }

            unity_mesh.SetVertices(unity_vertices);
            unity_mesh.SetTriangles(unity_triangles, 0);
            unity_mesh.RecalculateBounds();

            return new DebugXMesh(unity_mesh, mesh.Scale);
        }

        public IDebugShape CreateHeightField(HeightFieldView height_field, in Shape source)
        {
            int column_count = height_field.ColumnCount;
            int row_count = height_field.RowCount;

            if (column_count < 2 || row_count < 2)
                return null;

            int vertex_count = column_count * row_count;

            var vertices = new Vector3[vertex_count];

            for (int row = 0; row < row_count; ++row)
            {
                for (int column = 0; column < column_count; ++column)
                {
                    int index = row * column_count + column;
                    float3 point = height_field.GetPoint(column, row);

                    vertices[index] =
                        new Vector3(point.x, point.y, point.z);
                }
            }

            // Some cells may be holes, so the final index count may be
            // smaller than height_field.TriangleCount * 3.
            var indices = new List<int>(height_field.TriangleCount * 3);

            for (int row = 0; row < row_count - 1; ++row)
            {
                for (int column = 0; column < column_count - 1; ++column)
                {
                    byte material =
                        height_field.GetMaterial(column, row);

                    if (material == Consts.B3_HEIGHT_FIELD_HOLE)
                        continue;

                    int i00 = row * column_count + column;
                    int i10 = i00 + 1;
                    int i01 = i00 + column_count;
                    int i11 = i01 + 1;

                    if (height_field.Clockwise)
                    {
                        // tri0: p00, p10, p01
                        indices.Add(i00);
                        indices.Add(i10);
                        indices.Add(i01);

                        // tri1: p11, p01, p10
                        indices.Add(i11);
                        indices.Add(i01);
                        indices.Add(i10);
                    }
                    else
                    {
                        // tri0: p00, p01, p10
                        indices.Add(i00);
                        indices.Add(i01);
                        indices.Add(i10);

                        // tri1: p11, p10, p01
                        indices.Add(i11);
                        indices.Add(i10);
                        indices.Add(i01);
                    }
                }
            }

            if (indices.Count == 0)
                return null;

            var mesh = new UnityEngine.Mesh
            {
                name = "Box3D Debug HeightField"
            };

            if (vertex_count > ushort.MaxValue)
            {
                mesh.indexFormat =
                    UnityEngine.Rendering.IndexFormat.UInt32;
            }

            mesh.SetVertices(vertices);
            mesh.SetTriangles(indices, 0, calculateBounds: false);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();

            return new DebugXHeightField(mesh);
        }

        public IDebugShape CreateCompound(CompoundView compoundView, in Shape source) => null;

        public void DestroyShape(IDebugShape shape)
        {
            switch (shape)
            {
                case DebugXHull hull:
                    UnityEngine.Object.Destroy(hull.mesh);
                    break;
                case DebugXMesh mesh:
                    UnityEngine.Object.Destroy(mesh.mesh);
                    break;
                case DebugXHeightField heightField:
                    UnityEngine.Object.Destroy(heightField.mesh);
                    break;
                case DebugXCapsule:
                case DebugXSphere:
                    break;
            }
        }
    }

    internal class DebugDrawTarget : IDebugDrawTarget
    {
        private static Color ToColor(uint hex)
        {
            return new Color(((hex >> 16) & 0xFF) / 255f, ((hex >> 8) & 0xFF) / 255f, (hex & 0xFF) / 255f);
        }

        public bool DrawShape(IDebugShape shape, in B3Transform transform, uint color)
        {
            if (shape is not DebugXDrawShape)
            {
                Debug.LogError("Invalid shape type");
                return true;
            }

            Color unityColor = ToColor(color);
            switch (shape)
            {
                case DebugXSphere sphere:
                    DebugX.Draw(unityColor).Sphere(
                        transform.Position + sphere.sphere.Center,
                        sphere.sphere.Radius);
                    break;

                case DebugXCapsule capsule:
                    float3 a = transform.Position + math.mul(transform.Rotation, capsule.capsule.Center1);
                    float3 b = transform.Position + math.mul(transform.Rotation, capsule.capsule.Center2);
                    DebugX.Draw(unityColor).Capsule(a, b, capsule.capsule.Radius);
                    break;

                case DebugXHull hull:
                    DebugX.Draw(unityColor).UnlitMesh(
                        hull.mesh,
                        transform.Position,
                        transform.Rotation,
                        Vector3.one);
                    break;

                case DebugXMesh mesh:
                    DebugX.Draw(unityColor).UnlitMesh(
                        mesh.mesh,
                        transform.Position,
                        transform.Rotation,
                        mesh.scale);
                    break;
            }

            return true;
        }

        // Immediate geometry
        public void DrawSegment(float3 start, float3 end, uint color)
        {
            DebugX.Draw(ToColor(color)).Line(start, end);
        }

        public void DrawTransform(in B3Transform transform)
        {
            DebugX.Draw(Color.red).Line(
                transform.Position,
                transform.Position + math.mul(transform.Rotation,
                    new float3(1f, 0f, 0f)));
            DebugX.Draw(Color.green).Line(
                transform.Position,
                transform.Position + math.mul(transform.Rotation,
                    new float3(0f, 1f, 0f)));
            DebugX.Draw(Color.blue).Line(
                transform.Position,
                transform.Position + math.mul(transform.Rotation,
                    new float3(0f, 0f, 1f)));
        }

        public void DrawPoint(float3 position, float size, uint color)
        {
            DebugX.Draw(ToColor(color)).Sphere(position, size * 0.5f);
        }

        public void DrawSphere(float3 position, float radius, uint color, float alpha)
        {
            DebugX.Draw(ToColor(color)).Sphere(position, radius);
        }

        public void DrawCapsule(float3 p1, float3 p2, float radius, uint color, float alpha)
        {
            DebugX.Draw(ToColor(color)).Capsule(p1, p2, radius);
        }

        public void DrawBounds(in B3Aabb bounds, uint color)
        {
            float3 center = (bounds.LowerBound + bounds.UpperBound) * 0.5f;
            float3 size = (bounds.UpperBound - bounds.LowerBound) * 0.5f;
            DebugX.Draw(ToColor(color)).WireCube(center, Quaternion.identity, size);
        }

        public void DrawBox(float3 extents, in B3Transform transform, uint color)
        {
            DebugX.Draw(ToColor(color)).WireCube(transform.Position, transform.Rotation, extents);
        }

        public void DrawString(float3 p, string str, uint color)
        {
            var s = DebugXTextSettings.ScreenSpace;
            s.BackgroundColor = ToColor(color);
            DebugX.Draw(Color.clear).Text(p, str, s);
        }
    }
}
