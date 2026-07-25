using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using DCFApixels;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;
using Assert = UnityEngine.Assertions.Assert;

namespace Box3D.Unity
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

        public IDebugShape CreateHull(HullView hull, in Shape source)
        {
            ReadOnlySpan<float3> points = hull.Points;
            ReadOnlySpan<HullHalfEdge> edges = hull.HalfEdges;

            // Two indices per unique edge.
            int uniqueEdgeCount = edges.Length / 2;
            int indexCount = uniqueEdgeCount * 2;
            Assert.IsTrue(points.Length <= ushort.MaxValue, "Hull points count exceeds ushort.MaxValue");

            Span<ushort> indices = indexCount <= 512
                ? stackalloc ushort[indexCount]
                : new ushort[indexCount];

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
            Assert.AreEqual(indexCount, outputIndex, "Hull index count mismatch");

            var dataArray = Mesh.AllocateWritableMeshData(1);
            var meshData = dataArray[0];

            VertexAttributeDescriptor vAttrDesc = new(
                VertexAttribute.Position,
                VertexAttributeFormat.Float32,
                3
            );
            meshData.SetVertexBufferParams(points.Length, vAttrDesc);
            meshData.SetIndexBufferParams(indexCount, IndexFormat.UInt16);

            // 3. Directly copy your ReadOnlySpan<float3> points into Unity's vertex memory
            var destVerts = meshData.GetVertexData<float3>();
            points.CopyTo(destVerts);

            // 4. Copy your wireframe line indices into Unity's index memory
            var destIndices = meshData.GetIndexData<ushort>();
            indices.CopyTo(destIndices);

            // 5. Configure SubMesh as MeshTopology.Lines
            meshData.subMeshCount = 1;
            meshData.SetSubMesh(0, new SubMeshDescriptor(0, indexCount, MeshTopology.Lines));

            // 6. Finalize into a Unity Mesh
            Mesh mesh = new() { name = "Box3D Debug Hull" };
            Mesh.ApplyAndDisposeWritableMeshData(dataArray, mesh);
            mesh.RecalculateBounds();

            return new DebugXHull(mesh);
        }

        public IDebugShape CreateMesh(MeshView mesh, in Shape source)
        {
            ReadOnlySpan<float3> vertices = mesh.Vertices;
            ReadOnlySpan<MeshTriangle> triangles = mesh.Triangles;

            int totalIndices = triangles.Length * 3;

            // 1. Allocate writable mesh data
            var dataArray = Mesh.AllocateWritableMeshData(1);
            var meshData = dataArray[0];

            // 2. Set index format based on vertex count
            IndexFormat indexFormat = vertices.Length > ushort.MaxValue
                ? IndexFormat.UInt32
                : IndexFormat.UInt16;

            // 3. Define vertex and index buffer parameters
            VertexAttributeDescriptor vAttrDesc = new(
                VertexAttribute.Position,
                VertexAttributeFormat.Float32,
                3
            );
            meshData.SetVertexBufferParams(vertices.Length, vAttrDesc);
            meshData.SetIndexBufferParams(totalIndices, indexFormat);

            // 4. Copy vertex positions directly from ReadOnlySpan<float3> (0-alloc)
            var destVerts = meshData.GetVertexData<float3>();
            vertices.CopyTo(destVerts);

            // 5. Direct 0-copy cast of MeshTriangle -> int span
            // Assumes MeshTriangle is layed out as 3 sequential 32-bit ints (Index1, Index2, Index3)
            Assert.AreEqual(3 * sizeof(int), Unsafe.SizeOf<MeshTriangle>());
            ReadOnlySpan<int> indexSpan = MemoryMarshal.Cast<MeshTriangle, int>(triangles);

            if (indexFormat == IndexFormat.UInt32)
            {
                var destIndices = meshData.GetIndexData<int>();
                indexSpan.CopyTo(destIndices);
            }
            else
            {
                // If ushort indices are required by UInt16 format, downcast into destination
                var destIndices = meshData.GetIndexData<ushort>();
                for (int i = 0; i < indexSpan.Length; ++i)
                {
                    destIndices[i] = (ushort)indexSpan[i];
                }
            }

            // 6. Set up triangle submesh
            meshData.subMeshCount = 1;
            meshData.SetSubMesh(0, new SubMeshDescriptor(0, totalIndices, MeshTopology.Triangles));

            // 7. Apply to final Unity Mesh
            Mesh unity_mesh = new() { name = "Box3D Debug Mesh" };
            Mesh.ApplyAndDisposeWritableMeshData(dataArray, unity_mesh);
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
            int max_index_count = height_field.TriangleCount * 3;

            // 1. Allocate writable mesh data
            var dataArray = Mesh.AllocateWritableMeshData(1);
            var meshData = dataArray[0];

            IndexFormat indexFormat = vertex_count > ushort.MaxValue
                ? IndexFormat.UInt32
                : IndexFormat.UInt16;

            meshData.SetVertexBufferParams(
                vertex_count,
                new VertexAttributeDescriptor(VertexAttribute.Position, VertexAttributeFormat.Float32, 3)
            );
            meshData.SetIndexBufferParams(max_index_count, indexFormat);

            // 2. Populate Vertices directly into Unity's destination memory
            var destVerts = meshData.GetVertexData<float3>();
            for (int row = 0; row < row_count; ++row)
            {
                for (int column = 0; column < column_count; ++column)
                {
                    int index = row * column_count + column;
                    destVerts[index] = height_field.GetPoint(column, row);
                }
            }

            // 3. Populate Indices directly into Unity's index memory
            int indexCount = 0;
            bool isCw = height_field.Clockwise;

            if (indexFormat == IndexFormat.UInt32)
            {
                var destIndices = meshData.GetIndexData<int>();

                for (int row = 0; row < row_count - 1; ++row)
                {
                    for (int column = 0; column < column_count - 1; ++column)
                    {
                        if (height_field.GetMaterial(column, row) == Consts.B3_HEIGHT_FIELD_HOLE)
                            continue;

                        int i00 = row * column_count + column;
                        int i10 = i00 + 1;
                        int i01 = i00 + column_count;
                        int i11 = i01 + 1;

                        if (isCw)
                        {
                            destIndices[indexCount++] = i00;
                            destIndices[indexCount++] = i10;
                            destIndices[indexCount++] = i01;

                            destIndices[indexCount++] = i11;
                            destIndices[indexCount++] = i01;
                            destIndices[indexCount++] = i10;
                        }
                        else
                        {
                            destIndices[indexCount++] = i00;
                            destIndices[indexCount++] = i01;
                            destIndices[indexCount++] = i10;

                            destIndices[indexCount++] = i11;
                            destIndices[indexCount++] = i10;
                            destIndices[indexCount++] = i01;
                        }
                    }
                }
            }
            else // UInt16 branch
            {
                var destIndices = meshData.GetIndexData<ushort>();

                for (int row = 0; row < row_count - 1; ++row)
                {
                    for (int column = 0; column < column_count - 1; ++column)
                    {
                        if (height_field.GetMaterial(column, row) == Consts.B3_HEIGHT_FIELD_HOLE)
                            continue;

                        ushort i00 = (ushort)(row * column_count + column);
                        ushort i10 = (ushort)(i00 + 1);
                        ushort i01 = (ushort)(i00 + column_count);
                        ushort i11 = (ushort)(i01 + 1);

                        if (isCw)
                        {
                            destIndices[indexCount++] = i00;
                            destIndices[indexCount++] = i10;
                            destIndices[indexCount++] = i01;

                            destIndices[indexCount++] = i11;
                            destIndices[indexCount++] = i01;
                            destIndices[indexCount++] = i10;
                        }
                        else
                        {
                            destIndices[indexCount++] = i00;
                            destIndices[indexCount++] = i01;
                            destIndices[indexCount++] = i10;

                            destIndices[indexCount++] = i11;
                            destIndices[indexCount++] = i10;
                            destIndices[indexCount++] = i01;
                        }
                    }
                }
            }

            if (indexCount == 0)
            {
                dataArray.Dispose();
                return null;
            }

            // 4. Finalize SubMesh with actual written index count
            meshData.subMeshCount = 1;
            meshData.SetSubMesh(0, new SubMeshDescriptor(0, indexCount, MeshTopology.Triangles));

            Mesh mesh = new() { name = "Box3D Debug HeightField" };
            Mesh.ApplyAndDisposeWritableMeshData(dataArray, mesh);
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

        private void DrawSphere(in B3Transform transform, float3 center, float radius, uint color)
        {
            var c = ToColor(color);
            var p = transform.Position + math.rotate(transform.Rotation, center);
            Color fill_c = new(c.r, c.g, c.b, 0.18f);
            DebugX.Draw(fill_c).Sphere(p, radius);

            float3x3 rotMatrix = new(transform.Rotation);
            float3 right = rotMatrix.c0;
            float3 up = rotMatrix.c1;
            float3 forward = rotMatrix.c2;

            DebugX.Draw(c).Circle(p, right, radius);
            DebugX.Draw(c).Circle(p, up, radius);
            DebugX.Draw(c).Circle(p, forward, radius);
            DebugX.Draw(Color.white).Line(p, p + forward * radius);
        }

        private void DrawCapsule(in B3Transform transform, in float3 c1, in float3 c2, float radius, uint color)
        {
            var c = ToColor(color);
            Color fill_c = new(c.r, c.g, c.b, 0.18f);
            var p1 = transform.Position + math.rotate(transform.Rotation, c1);
            var p2 = transform.Position + math.rotate(transform.Rotation, c2);

            DebugX.Draw(fill_c).Capsule(p1, p2, radius);
            DebugX.Draw(c).WireCapsule(p1, p2, radius);

            float3 forward = math.rotate(transform.Rotation, new float3(0, 0, 1));
            DebugX.Draw(Color.white).Line(transform.Position, transform.Position + forward * radius);
        }

        private void DrawMesh(in B3Transform transform, in Mesh mesh, uint color, in float3 scale)
        {
            var c = ToColor(color);
            Color fill_c = new(c.r, c.g, c.b, 0.18f);
            DebugX.Draw(fill_c).UnlitMesh(mesh, transform.Position, transform.Rotation, scale);
            DebugX.Draw(c).WireMesh(mesh, transform.Position, transform.Rotation, scale);
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
                    DrawSphere(transform, sphere.sphere.Center, sphere.sphere.Radius, color);
                    break;

                case DebugXCapsule capsule:
                    DrawCapsule(transform, capsule.capsule.Center1,
                        capsule.capsule.Center2,
                        capsule.capsule.Radius,
                        color);
                    break;

                case DebugXHull hull:
                    DrawMesh(transform, hull.mesh, color, new float3(1, 1, 1));
                    break;

                case DebugXMesh mesh:
                    DrawMesh(transform, mesh.mesh, color, mesh.scale);
                    break;

                case DebugXHeightField heightField:
                    DrawMesh(transform, heightField.mesh, color, new float3(1, 1, 1));
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
                transform.Position + math.mul(transform.Rotation, math.right()));
            DebugX.Draw(Color.green).Line(
                transform.Position,
                transform.Position + math.mul(transform.Rotation, math.up()));
            DebugX.Draw(Color.blue).Line(
                transform.Position,
                transform.Position + math.mul(transform.Rotation, math.forward()));
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
            DebugX.Draw(ToColor(color)).Cube(transform.Position, transform.Rotation, extents);
        }

        public void DrawString(float3 p, string str, uint color)
        {
            var s = DebugXTextSettings.ScreenSpace;
            s.BackgroundColor = ToColor(color);
            DebugX.Draw(Color.clear).Text(p, str, s);
        }
    }
}
