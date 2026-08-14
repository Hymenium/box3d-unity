#if DEBUGX_AVAILABLE

using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using DCFApixels;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;
using Assert = UnityEngine.Assertions.Assert;

#if BOX3D_DOUBLE
#warning "Box3D Debug Draw: Precision will be lost when casting double precision to Unity's float-based rendering."
#endif

namespace Box3D.Draw
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
        public readonly Mesh solidMesh;
        public readonly Mesh wireMesh;

        public DebugXHull(Mesh solidMesh, Mesh wireMesh)
        {
            this.solidMesh = solidMesh;
            this.wireMesh = wireMesh;
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
        private static readonly System.Collections.Generic.List<Mesh> s_Trash = new();
        private static bool s_Subscribed;

        public DebugXShapeFactory()
        {
            if (!s_Subscribed)
            {
                UnityEngine.Rendering.RenderPipelineManager.endContextRendering += (ctx, cams) => FlushTrash();
                UnityEngine.Camera.onPostRender += (cam) => FlushTrash();
                s_Subscribed = true;
            }
        }

        private static void FlushTrash()
        {
            if (s_Trash.Count > 0)
            {
                foreach (var m in s_Trash)
                {
                    if (m != null) UnityEngine.Object.DestroyImmediate(m);
                }
                s_Trash.Clear();
            }
        }

        private void SafeDestroyMesh(Mesh mesh)
        {
            if (mesh != null)
            {
                s_Trash.Add(mesh);
            }
        }

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

            // 1. Create Wire Mesh (Lines)
            int uniqueEdgeCount = edges.Length / 2;
            int lineIndexCount = uniqueEdgeCount * 2;
            Span<ushort> lineIndices = lineIndexCount <= 512
                ? stackalloc ushort[lineIndexCount]
                : new ushort[lineIndexCount];

            int lineOutIdx = 0;
            for (int i = 0; i < edges.Length; ++i)
            {
                HullHalfEdge edge = edges[i];
                if (i > edge.Twin) continue;
                lineIndices[lineOutIdx++] = edge.Origin;
                lineIndices[lineOutIdx++] = edges[edge.Twin].Origin;
            }

            Mesh wireMesh = new() { name = "Box3D Debug Hull Wire" };
            var dataArray1 = Mesh.AllocateWritableMeshData(1);
            var meshData1 = dataArray1[0];
            meshData1.SetVertexBufferParams(points.Length, new VertexAttributeDescriptor(VertexAttribute.Position, VertexAttributeFormat.Float32, 3));
            meshData1.SetIndexBufferParams(lineIndexCount, IndexFormat.UInt16);
            points.CopyTo(meshData1.GetVertexData<float3>());
            lineIndices.CopyTo(meshData1.GetIndexData<ushort>());
            meshData1.subMeshCount = 1;
            meshData1.SetSubMesh(0, new SubMeshDescriptor(0, lineIndexCount, MeshTopology.Lines));
            Mesh.ApplyAndDisposeWritableMeshData(dataArray1, wireMesh);
            wireMesh.RecalculateBounds();

            // 2. Create Solid Mesh (Triangles)
            int triangleCount = edges.Length - 2 * hull.FaceCount;
            int triIndexCount = triangleCount * 3;
            Span<ushort> triIndices = triIndexCount <= 1024
                ? stackalloc ushort[triIndexCount]
                : new ushort[triIndexCount];

            Span<int> faceStart = stackalloc int[hull.FaceCount];
            faceStart.Fill(-1);
            for (int i = 0; i < edges.Length; i++)
            {
                if (faceStart[edges[i].Face] == -1) faceStart[edges[i].Face] = i;
            }

            int triOutIdx = 0;
            for (int f = 0; f < hull.FaceCount; f++)
            {
                int startEdge = faceStart[f];
                if (startEdge == -1) continue;

                int v0 = edges[startEdge].Origin;
                int currEdge = edges[startEdge].Next;

                while (currEdge != startEdge)
                {
                    int v1 = edges[currEdge].Origin;
                    int nextEdge = edges[currEdge].Next;

                    if (nextEdge == startEdge) break;

                    int v2 = edges[nextEdge].Origin;

                    triIndices[triOutIdx++] = (ushort)v0;
                    triIndices[triOutIdx++] = (ushort)v1;
                    triIndices[triOutIdx++] = (ushort)v2;

                    currEdge = nextEdge;
                }
            }

            Mesh solidMesh = new() { name = "Box3D Debug Hull Solid" };
            var dataArray2 = Mesh.AllocateWritableMeshData(1);
            var meshData2 = dataArray2[0];
            meshData2.SetVertexBufferParams(points.Length, new VertexAttributeDescriptor(VertexAttribute.Position, VertexAttributeFormat.Float32, 3));
            meshData2.SetIndexBufferParams(triIndexCount, IndexFormat.UInt16);
            points.CopyTo(meshData2.GetVertexData<float3>());
            triIndices.CopyTo(meshData2.GetIndexData<ushort>());
            meshData2.subMeshCount = 1;
            meshData2.SetSubMesh(0, new SubMeshDescriptor(0, triOutIdx, MeshTopology.Triangles));
            Mesh.ApplyAndDisposeWritableMeshData(dataArray2, solidMesh);

            solidMesh.RecalculateNormals();
            solidMesh.RecalculateBounds();

            return new DebugXHull(solidMesh, wireMesh);
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

            // memory copy
            var destVerts = meshData.GetVertexData<float3>();
            vertices.CopyTo(destVerts);

            if (indexFormat == IndexFormat.UInt32)
            {
                // memory copy
                var destIndices = meshData.GetIndexData<MeshTriangle>();
                triangles.CopyTo(destIndices);
            }
            else
            {
                var destIndices = meshData.GetIndexData<ushort>();
                for (int i = 0; i < triangles.Length; ++i)
                {
                    destIndices[i * 3] = (ushort)triangles[i].Index1;
                    destIndices[i * 3 + 1] = (ushort)triangles[i].Index2;
                    destIndices[i * 3 + 2] = (ushort)triangles[i].Index3;
                }
            }

            // 6. Set up triangle submesh
            meshData.subMeshCount = 1;
            meshData.SetSubMesh(0, new SubMeshDescriptor(0, totalIndices, MeshTopology.Triangles));

            // 7. Apply to final Unity Mesh
            Mesh unity_mesh = new() { name = "Box3D Debug Mesh" };
            Mesh.ApplyAndDisposeWritableMeshData(dataArray, unity_mesh);
            unity_mesh.RecalculateNormals();
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

        public IDebugShape CreateCompound(CompoundView compoundView, in Shape source)
        {
            return CompoundDebugDraw.CreateCompound(this, compoundView, source);
        }


        public void DestroyShape(IDebugShape shape)
        {
            switch (shape)
            {
                case DebugCompoundShape compoundShape:
                    CompoundDebugDraw.DestroyCompound(this, compoundShape);
                    break;
                case DebugXHull hull:
                    SafeDestroyMesh(hull.solidMesh);
                    SafeDestroyMesh(hull.wireMesh);
                    break;
                case DebugXMesh mesh:
                    SafeDestroyMesh(mesh.mesh);
                    break;
                case DebugXHeightField heightField:
                    SafeDestroyMesh(heightField.mesh);
                    break;
                case DebugXCapsule:
                case DebugXSphere:
                    break;
            }
        }
    }

    public class DebugXDrawTarget : IDebugDrawTarget
    {
        public B3Aabb screenBounds = new()
        {
            LowerBound = new float3(-50f, -50f, -50f),
            UpperBound = new float3(50f, 50f, 50f)
        };

        private static Color ToColor(uint hex)
        {
            return new Color(((hex >> 16) & 0xFF) / 255f, ((hex >> 8) & 0xFF) / 255f, (hex & 0xFF) / 255f);
        }

        private void DrawSphere(in B3WorldTransform transform, float3 center, float radius, uint color)
        {
            var c = ToColor(color);
            var p = transform.Position + math.rotate(transform.Rotation, center);
            Color fill_c = new(c.r, c.g, c.b, 0.18f);

            DebugX.Draw(fill_c).Sphere(p, radius);
            DebugX.Draw(c).WireSphere(p, radius);

            float3 forward = math.rotate(transform.Rotation, new float3(0, 0, 1));
            DebugX.Draw(Color.white).Line(p, p + forward * radius);
        }

        private void DrawCapsule(in B3WorldTransform transform, in float3 c1, in float3 c2, float radius, uint color)
        {
            var c = ToColor(color);
            Color fill_c = new(c.r, c.g, c.b, 0.18f);
            var p1 = transform.Position + math.rotate(transform.Rotation, c1);
            var p2 = transform.Position + math.rotate(transform.Rotation, c2);

            var center = (p1 + p2) * 0.5f;
            var distance = math.distance(p1, p2);
            var height = distance + radius * 2f;
            var dir = distance > 0 ? (float3)((p2 - p1) / distance) : new float3(0, 1, 0);
            var rotation = UnityEngine.Quaternion.LookRotation(dir, UnityEngine.Vector3.up) * UnityEngine.Quaternion.Euler(90, 0, 0);

            DebugX.Draw(fill_c).Capsule(center, rotation, radius, height);
            DebugX.Draw(c).WireCapsule(center, rotation, radius, height);

            float3 forward = math.rotate(transform.Rotation, new float3(0, 0, 1));
            DebugX.Draw(Color.white).Line(transform.Position, transform.Position + forward * radius);
        }

        private void DrawMesh(in B3WorldTransform transform, in Mesh mesh, uint color, in float3 scale)
        {
            if (mesh == null) return;
            var c = ToColor(color);
            Color fill_c = new(c.r, c.g, c.b, 0.18f);
            DebugX.Draw(fill_c).Mesh(mesh, transform.Position, transform.Rotation, scale);
            DebugX.Draw(c).WireMesh(mesh, transform.Position, transform.Rotation, scale);
        }

        public bool DrawShape(IDebugShape shape, in B3WorldTransform transform, uint color)
        {
            if (shape is DebugCompoundShape compoundShape)
            {
                return CompoundDebugDraw.DrawCompound(this, compoundShape, in transform, color, screenBounds);
            }

            if (shape is not DebugXDrawShape)
            {
                Debug.LogError("Invalid shape type");
                return true;
            }

            switch (shape)
            {
                case DebugXSphere sphere:
                    DrawSphere(transform, sphere.sphere.Center, sphere.sphere.Radius, color);
                    break;

                case DebugXCapsule capsule:
                    DrawCapsule(
                        transform,
                        capsule.capsule.Center1,
                        capsule.capsule.Center2,
                        capsule.capsule.Radius,
                        color);
                    break;

                case DebugXHull hull:
                    var cHull = ToColor(color);
                    Color fill_cHull = new(cHull.r, cHull.g, cHull.b, 0.18f);
                    if (hull.solidMesh != null) DebugX.Draw(fill_cHull).Mesh(hull.solidMesh, transform.Position, transform.Rotation, new float3(1, 1, 1));
                    if (hull.wireMesh != null) DebugX.Draw(cHull).Mesh<DCFApixels.DebugXCore.GeometryUnlitMat>(hull.wireMesh, transform.Position, transform.Rotation, new float3(1, 1, 1));
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
        public void DrawSegment(B3Pos start, B3Pos end, uint color)
        {
            DebugX.Draw(ToColor(color)).Line(start, end);
        }

        public void DrawTransform(in B3WorldTransform transform)
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

        public void DrawPoint(B3Pos p, float size, uint color)
        {
            DebugX.Draw(ToColor(color)).Sphere(p, size * 0.5f);
        }

        public void DrawSphere(B3Pos p, float radius, uint color, float alpha)
        {
            DebugX.Draw(ToColor(color)).Sphere(p, radius);
        }

        public void DrawCapsule(B3Pos p1, B3Pos p2, float radius, uint color, float alpha)
        {
            var c = ToColor(color);
            Color fill_c = new(c.r, c.g, c.b, alpha);
            var center = (p1 + p2) * 0.5f;
            var distance = math.distance(p1, p2);
            var height = distance + radius * 2f;
            var dir = distance > 0 ? (float3)((p2 - p1) / distance) : new float3(0, 1, 0);
            var rotation = UnityEngine.Quaternion.LookRotation(dir, UnityEngine.Vector3.up) * UnityEngine.Quaternion.Euler(90, 0, 0);

            DebugX.Draw(fill_c).Capsule(center, rotation, radius, height);
            if (alpha > 0f) DebugX.Draw(c).WireCapsule(center, rotation, radius, height);
        }

        public void DrawBounds(in B3Aabb bounds, uint color)
        {
            float3 center = (bounds.LowerBound + bounds.UpperBound) * 0.5f;
            float3 size = (bounds.UpperBound - bounds.LowerBound); // Full size
            DebugX.Draw(ToColor(color)).WireCube(center, Quaternion.identity, size);
        }

        public void DrawBox(float3 extents, in B3WorldTransform transform, uint color)
        {
            DebugX.Draw(ToColor(color)).Cube(transform.Position, transform.Rotation, extents * 2f);
        }

        public void DrawString(B3Pos p, in string str, uint color)
        {
            var s = DebugXTextSettings.ScreenSpace;
            s.BackgroundColor = ToColor(color);
            DebugX.Draw(Color.clear).Text(p, str, s);
        }
    }
}

#endif // DEBUGX_AVAILABLE