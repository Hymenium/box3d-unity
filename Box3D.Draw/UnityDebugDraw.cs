using System;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;

#if BOX3D_DOUBLE
#warning "Box3D Debug Draw: Precision will be lost when casting double precision to Unity's float-based rendering."
#endif

namespace Box3D.Draw
{
    public class UnityDebugShape : IDebugShape { }

    public sealed class UnityDebugSphere : UnityDebugShape
    {
        public Sphere Sphere;

        public UnityDebugSphere(Sphere sphere)
        {
            Sphere = sphere;
        }
    }

    public sealed class UnityDebugCapsule : UnityDebugShape
    {
        public Capsule Capsule;

        public UnityDebugCapsule(Capsule capsule)
        {
            Capsule = capsule;
        }
    }

    public sealed class UnityDebugHull : UnityDebugShape
    {
        public readonly Mesh SolidMesh;
        public readonly Mesh WireMesh;

        public UnityDebugHull(Mesh solidMesh, Mesh wireMesh)
        {
            SolidMesh = solidMesh;
            WireMesh = wireMesh;
        }
    }

    public class UnityDebugMesh : UnityDebugShape
    {
        public readonly Mesh SolidMesh;
        public readonly Mesh WireMesh;
        public readonly float3 Scale;

        public UnityDebugMesh(Mesh solidMesh, Mesh wireMesh, float3 scale)
        {
            SolidMesh = solidMesh;
            WireMesh = wireMesh;
            Scale = scale;
        }
    }

    public class UnityDebugHeightField : UnityDebugShape
    {
        public readonly Mesh SolidMesh;
        public readonly Mesh WireMesh;

        public UnityDebugHeightField(Mesh solidMesh, Mesh wireMesh)
        {
            SolidMesh = solidMesh;
            WireMesh = wireMesh;
        }
    }

    public class UnityDebugShapeFactory : IDebugShapeFactory
    {
        public IDebugShape CreateSphere(in Sphere sphere, in Shape source)
        {
            return new UnityDebugSphere(sphere);
        }

        public IDebugShape CreateCapsule(in Capsule capsule, in Shape source)
        {
            return new UnityDebugCapsule(capsule);
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
            meshData1.SetVertexBufferParams(points.Length,
                new VertexAttributeDescriptor(VertexAttribute.Position, VertexAttributeFormat.Float32, 3, 0),
                new VertexAttributeDescriptor(VertexAttribute.Normal, VertexAttributeFormat.Float32, 3, 1));
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
            meshData2.SetVertexBufferParams(points.Length,
                new VertexAttributeDescriptor(VertexAttribute.Position, VertexAttributeFormat.Float32, 3, 0));
            meshData2.SetIndexBufferParams(triIndexCount, IndexFormat.UInt16);

            var vertexData = meshData2.GetVertexData<float3>();
            for (int i = 0; i < points.Length; i++)
            {
                vertexData[i] = points[i];
            }
            triIndices.CopyTo(meshData2.GetIndexData<ushort>());

            meshData2.subMeshCount = 1;
            meshData2.SetSubMesh(0, new SubMeshDescriptor(0, triOutIdx, MeshTopology.Triangles));
            Mesh.ApplyAndDisposeWritableMeshData(dataArray2, solidMesh);

            solidMesh.RecalculateNormals();
            solidMesh.RecalculateBounds();

            return new UnityDebugHull(solidMesh, wireMesh);
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
            meshData.SetVertexBufferParams(vertices.Length,
                new VertexAttributeDescriptor(VertexAttribute.Position, VertexAttributeFormat.Float32, 3, 0),
                new VertexAttributeDescriptor(VertexAttribute.Normal, VertexAttributeFormat.Float32, 3, 0),
                new VertexAttributeDescriptor(VertexAttribute.Tangent, VertexAttributeFormat.Float32, 4, 0),
                new VertexAttributeDescriptor(VertexAttribute.TexCoord0, VertexAttributeFormat.Float32, 2, 0));
            meshData.SetIndexBufferParams(totalIndices, indexFormat);

            // memory copy
            // var destVerts = meshData.GetVertexData<DebugVertex>();
            // for (int i = 0; i < vertices.Length; i++)
            // {
            //     destVerts[i] = new DebugVertex { pos = vertices[i] };
            // }

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

            return new UnityDebugMesh(unity_mesh, unity_mesh, mesh.Scale);
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
                new VertexAttributeDescriptor(VertexAttribute.Position, VertexAttributeFormat.Float32, 3, 0),
                new VertexAttributeDescriptor(VertexAttribute.Normal, VertexAttributeFormat.Float32, 3, 0),
                new VertexAttributeDescriptor(VertexAttribute.Tangent, VertexAttributeFormat.Float32, 4, 0),
                new VertexAttributeDescriptor(VertexAttribute.TexCoord0, VertexAttributeFormat.Float32, 2, 0)
            );
            meshData.SetIndexBufferParams(max_index_count, indexFormat);

            // 2. Populate Vertices directly into Unity's destination memory
            // var destVerts = meshData.GetVertexData<DebugVertex>();
            // for (int row = 0; row < row_count; ++row)
            // {
            //     for (int column = 0; column < column_count; ++column)
            //     {
            //         int index = row * column_count + column;
            //         destVerts[index] = new DebugVertex { pos = height_field.GetPoint(column, row) };
            //     }
            // }

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

            return new UnityDebugHeightField(mesh, mesh);
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
                case UnityDebugHull hull:
                    UnityEngine.Object.Destroy(hull.SolidMesh);
                    UnityEngine.Object.Destroy(hull.WireMesh);
                    break;
                case UnityDebugMesh mesh:
                    UnityEngine.Object.Destroy(mesh.SolidMesh);
                    UnityEngine.Object.Destroy(mesh.WireMesh);
                    break;
                case UnityDebugHeightField heightField:
                    UnityEngine.Object.Destroy(heightField.SolidMesh);
                    UnityEngine.Object.Destroy(heightField.WireMesh);
                    break;
            }
        }
    }

    public class UnityDebugDrawTarget : IDebugDrawTarget
    {
        public B3Aabb screenBounds = new()
        {
            LowerBound = new float3(-50f, -50f, -50f),
            UpperBound = new float3(50f, 50f, 50f)
        };

        private static Mesh _solidSphere;
        private static Mesh _solidCapsule;
        private static Mesh _solidCube;
        private static Mesh _solidCylinder;
        private static Material _mat;

        private System.Collections.Generic.List<UnityEngine.Vector3> _lineVertices = new(16384);
        private System.Collections.Generic.List<UnityEngine.Color32> _lineColors = new(16384);
        private System.Collections.Generic.List<int> _lineIndices = new(16384);
        private Mesh _lineMesh;
        private Material _lineMat;

        private static void InitMeshes()
        {
            if (_solidSphere != null) return;

            var sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            _solidSphere = sphere.GetComponent<MeshFilter>().sharedMesh;
            UnityEngine.Object.Destroy(sphere);

            var capsule = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            _solidCapsule = capsule.GetComponent<MeshFilter>().sharedMesh;
            UnityEngine.Object.Destroy(capsule);

            var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            _solidCube = cube.GetComponent<MeshFilter>().sharedMesh;
            UnityEngine.Object.Destroy(cube);

            var shader = Shader.Find("Hidden/Internal-Colored");
            _mat = new Material(shader);
            _mat.hideFlags = HideFlags.HideAndDontSave;
            _mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            _mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            _mat.SetInt("_Cull", (int)UnityEngine.Rendering.CullMode.Off);
            _mat.SetInt("_ZWrite", 0);
        }

        private static System.Collections.Generic.Dictionary<float, Mesh> _capsuleMeshes = new();

        private static Mesh GetCapsuleMesh(float distance, float radius)
        {
            if (radius <= 0.0001f) return _solidSphere;
            float ratio = distance / radius;
            ratio = math.round(ratio * 20f) / 20f;

            if (_capsuleMeshes.TryGetValue(ratio, out Mesh mesh)) return mesh;

            if (_capsuleMeshes.Count > 100) _capsuleMeshes.Clear();

            mesh = UnityEngine.Object.Instantiate(_solidCapsule);
            mesh.name = $"Capsule_{ratio}";

            Vector3[] vertices = mesh.vertices;
            float targetDistance = ratio * 0.5f;
            float delta = (targetDistance - 1.0f) * 0.5f;

            for (int i = 0; i < vertices.Length; i++)
            {
                if (vertices[i].y > 0.001f) vertices[i].y += delta;
                else if (vertices[i].y < -0.001f) vertices[i].y -= delta;
            }

            mesh.vertices = vertices;
            mesh.RecalculateBounds();
            _capsuleMeshes[ratio] = mesh;
            return mesh;
        }

        private static Color ToColor(uint hex)
        {
            return new Color(((hex >> 16) & 0xFF) / 255f, ((hex >> 8) & 0xFF) / 255f, (hex & 0xFF) / 255f);
        }

        private void DrawSolidMesh(
            Mesh mesh,
            in float3 position,
            in quaternion rotation,
            in float3 scale,
            Color color)
        {
            if (mesh == null) return;
            InitMeshes();
            MaterialPropertyBlock block = new MaterialPropertyBlock();
            block.SetColor("_Color", color);
            Graphics.DrawMesh(mesh, Matrix4x4.TRS(position, rotation, scale), _mat, 0, null, 0, block);
        }

        private void DrawWireMesh(
            Mesh mesh,
            in float3 position,
            in quaternion rotation,
            in float3 scale,
            Color color)
        {
            if (mesh == null) return;
            InitMeshes();
            MaterialPropertyBlock block = new MaterialPropertyBlock();
            block.SetColor("_Color", color);
            Graphics.DrawMesh(mesh, Matrix4x4.TRS(position, rotation, scale), _mat, 0, null, 0, block);
        }

        private static void DrawCircleLines(float3 center, float radius, float3 axisA, float3 axisB, Color color)
        {
            const int segments = 16;
            float3 previous = center + axisA * radius;
            for (int i = 1; i <= segments; i++)
            {
                float angle = i * (2f * math.PI / segments);
                float3 next = center + (axisA * math.cos(angle) + axisB * math.sin(angle)) * radius;
                Debug.DrawLine(previous, next, color);
                previous = next;
            }
        }

        private void DrawArcLines(float3 center, float radius, float3 axisA, float3 axisB, Color color)
        {
            const int segments = 8;
            float3 previous = center + axisA * radius;
            for (int i = 1; i <= segments; i++)
            {
                float angle = i * (math.PI / segments);
                float3 next = center + (axisA * math.cos(angle) + axisB * math.sin(angle)) * radius;
                DrawLine(previous, next, color);
                previous = next;
            }
        }

        private void DrawLine(
            in float3 start,
            in float3 end,
            Color color)
        {
            Color32 c32 = color;
            _lineVertices.Add(start);
            _lineVertices.Add(end);
            _lineColors.Add(c32);
            _lineColors.Add(c32);
        }

        public void FlushLines()
        {
            if (_lineVertices.Count == 0) return;

            if (_lineMesh == null)
            {
                _lineMesh = new Mesh();
                _lineMesh.name = "Box3D Line Buffer";
                _lineMesh.MarkDynamic();
                _lineMesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;

                var shader = Shader.Find("Hidden/Internal-Colored");
                _lineMat = new Material(shader);
                _lineMat.hideFlags = HideFlags.HideAndDontSave;
                _lineMat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                _lineMat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                _lineMat.SetInt("_Cull", (int)UnityEngine.Rendering.CullMode.Off);
                _lineMat.SetInt("_ZWrite", 0);
            }

            _lineMesh.Clear();
            _lineMesh.SetVertices(_lineVertices);
            _lineMesh.SetColors(_lineColors);

            if (_lineIndices.Capacity < _lineVertices.Count)
            {
                _lineIndices.Capacity = _lineVertices.Count;
            }

            _lineIndices.Clear();
            for (int i = 0; i < _lineVertices.Count; i++)
            {
                _lineIndices.Add(i);
            }

            _lineMesh.SetIndices(_lineIndices, MeshTopology.Lines, 0);

            Graphics.DrawMesh(_lineMesh, Matrix4x4.identity, _lineMat, 0);

            _lineVertices.Clear();
            _lineColors.Clear();
        }

        private void DrawSphere(
            in B3WorldTransform transform,
            float3 localCenter,
            float radius,
            uint color)
        {
            var c = ToColor(color);
            float3 position = transform.Position.ToFloat3() + math.rotate(transform.Rotation, localCenter);

            DrawSolidMesh(
                _solidSphere,
                position,
                quaternion.identity,
                new float3(radius * 2f),
                new Color(c.r, c.g, c.b, 0.18f));

            DrawCircleLines(position, radius, new float3(1f, 0f, 0f), new float3(0f, 1f, 0f), c);
            DrawCircleLines(position, radius, new float3(0f, 1f, 0f), new float3(0f, 0f, 1f), c);
            DrawCircleLines(position, radius, new float3(0f, 0f, 1f), new float3(1f, 0f, 0f), c);

            var forward = math.rotate(transform.Rotation, math.forward());
            DrawLine(position, position + forward * radius, Color.white);
        }

        private void DrawCapsule(in B3WorldTransform transform, in float3 c1, in float3 c2, float radius, uint color)
        {
            var c = ToColor(color);
            Color fill_c = new(c.r, c.g, c.b, 0.18f);
            float3 p1 = transform.Position.ToFloat3() + math.rotate(transform.Rotation, c1);
            float3 p2 = transform.Position.ToFloat3() + math.rotate(transform.Rotation, c2);

            var center = (p1 + p2) * 0.5f;
            var distance = math.distance(p1, p2);
            var dir = distance > 0 ? (float3)((p2 - p1) / distance) : new float3(0, 1, 0);
            var rotation = UnityEngine.Quaternion.LookRotation(dir, UnityEngine.Vector3.up) * UnityEngine.Quaternion.Euler(90, 0, 0);

            Mesh capsuleMesh = GetCapsuleMesh(distance, radius);
            DrawSolidMesh(capsuleMesh, center, rotation, new float3(radius * 2f), fill_c);

            float3 axis = math.normalizesafe(p2 - p1, new float3(0f, 1f, 0f));
            float3 side = math.normalizesafe(math.cross(axis, new float3(0.371f, 0.827f, 0.421f)), new float3(1f, 0f, 0f));
            float3 fwd = math.cross(axis, side);

            DrawCircleLines(p1, radius, side, fwd, c);
            DrawCircleLines(p2, radius, side, fwd, c);
            DrawLine(p1 + side * radius, p2 + side * radius, c);
            DrawLine(p1 - side * radius, p2 - side * radius, c);
            DrawLine(p1 + fwd * radius, p2 + fwd * radius, c);
            DrawLine(p1 - fwd * radius, p2 - fwd * radius, c);

            DrawArcLines(p2, radius, side, axis, c);
            DrawArcLines(p2, radius, fwd, axis, c);
            DrawArcLines(p1, radius, side, -axis, c);
            DrawArcLines(p1, radius, fwd, -axis, c);

            float3 forward = math.rotate(transform.Rotation, new float3(0, 0, 1));
            DrawLine(transform.Position.ToFloat3(), transform.Position.ToFloat3() + forward * radius, Color.white);
        }

        private void DrawMesh(in B3WorldTransform transform, in Mesh mesh, uint color, in float3 scale)
        {
            if (mesh == null) return;
            var c = ToColor(color);
            Color fill_c = new(c.r, c.g, c.b, 0.18f);
            DrawSolidMesh(mesh, transform.Position.ToFloat3(), transform.Rotation, scale, fill_c);
            DrawWireMesh(mesh, transform.Position.ToFloat3(), transform.Rotation, scale, c);
        }

        public bool DrawShape(IDebugShape shape, in B3WorldTransform transform, uint color)
        {
            if (shape is DebugCompoundShape compoundShape)
            {
                return CompoundDebugDraw.DrawCompound(this, compoundShape, in transform, color, screenBounds);
            }

            if (shape is not UnityDebugShape)
            {
                Debug.LogError("Invalid shape type");
                return true;
            }

            switch (shape)
            {
                case UnityDebugSphere sphere:
                    DrawSphere(transform, sphere.Sphere.Center, sphere.Sphere.Radius, color);
                    break;

                case UnityDebugCapsule capsule:
                    DrawCapsule(
                        transform,
                        capsule.Capsule.Center1,
                        capsule.Capsule.Center2,
                        capsule.Capsule.Radius,
                        color);
                    break;

                case UnityDebugHull hull:
                    var cHull = ToColor(color);
                    Color fill_cHull = new(cHull.r, cHull.g, cHull.b, 0.18f);
                    if (hull.SolidMesh != null)
                    {
                        DrawSolidMesh(
                            hull.SolidMesh,
                            transform.Position.ToFloat3(),
                            transform.Rotation,
                            new float3(1f),
                            fill_cHull);
                    }

                    if (hull.WireMesh != null)
                    {
                        DrawWireMesh(
                            hull.WireMesh,
                            transform.Position.ToFloat3(),
                            transform.Rotation,
                            new float3(1f),
                            cHull);
                    }
                    break;

                case UnityDebugMesh mesh:
                    DrawMesh(transform, mesh.SolidMesh, color, mesh.Scale);
                    break;

                case UnityDebugHeightField heightField:
                    DrawMesh(transform, heightField.SolidMesh, color, new float3(1, 1, 1));
                    break;
            }

            return true;
        }

        // Immediate geometry
        public void DrawSegment(B3Pos start, B3Pos end, uint color)
        {
            DrawLine(start.ToFloat3(), end.ToFloat3(), ToColor(color));
        }

        public void DrawTransform(in B3WorldTransform transform)
        {
            const float axisLength = 0.3f;
            float3 p = transform.Position;
            DrawLine(p, p + math.mul(transform.Rotation, new float3(axisLength, 0f, 0f)), Color.red);
            DrawLine(p, p + math.mul(transform.Rotation, new float3(0f, axisLength, 0f)), Color.green);
            DrawLine(p, p + math.mul(transform.Rotation, new float3(0f, 0f, axisLength)), Color.blue);
        }

        public void DrawPoint(B3Pos p, float size, uint color)
        {
            var c = ToColor(color);
            float3 position = p.ToFloat3();
            DrawSolidMesh(
                _solidSphere,
                position,
                quaternion.identity,
                new float3(size),
                c);
        }

        public void DrawSphere(B3Pos p, float radius, uint color, float alpha)
        {
            var c = ToColor(color);
            float3 position = p.ToFloat3();
            DrawSolidMesh(
                _solidSphere,
                position,
                quaternion.identity,
                new float3(radius * 2f),
                new Color(c.r, c.g, c.b, alpha));

            if (alpha > 0f)
            {
                DrawCircleLines(position, radius, new float3(1f, 0f, 0f), new float3(0f, 1f, 0f), c);
                DrawCircleLines(position, radius, new float3(0f, 1f, 0f), new float3(0f, 0f, 1f), c);
                DrawCircleLines(position, radius, new float3(0f, 0f, 1f), new float3(1f, 0f, 0f), c);
            }
        }

        public void DrawCapsule(B3Pos p1, B3Pos p2, float radius, uint color, float alpha)
        {
            var c = ToColor(color);
            Color fill_c = new(c.r, c.g, c.b, alpha);
            float3 p1_f = p1.ToFloat3();
            float3 p2_f = p2.ToFloat3();

            var center = (p1_f + p2_f) * 0.5f;
            var distance = math.distance(p1_f, p2_f);
            var dir = distance > 0 ? (float3)((p2_f - p1_f) / distance) : new float3(0, 1, 0);
            var rotation = UnityEngine.Quaternion.LookRotation(dir, UnityEngine.Vector3.up) * UnityEngine.Quaternion.Euler(90, 0, 0);

            if (alpha > 0f)
            {
                Mesh capsuleMesh = GetCapsuleMesh(distance, radius);
                DrawSolidMesh(capsuleMesh, center, rotation, new float3(radius * 2f), fill_c);
            }

            if (alpha > 0f)
            {
                float3 axis = math.normalizesafe(p2_f - p1_f, new float3(0f, 1f, 0f));
                float3 side = math.normalizesafe(math.cross(axis, new float3(0.371f, 0.827f, 0.421f)), new float3(1f, 0f, 0f));
                float3 fwd = math.cross(axis, side);

                DrawCircleLines(p1_f, radius, side, fwd, c);
                DrawCircleLines(p2_f, radius, side, fwd, c);
                DrawLine(p1_f + side * radius, p2_f + side * radius, c);
                DrawLine(p1_f - side * radius, p2_f - side * radius, c);
                DrawLine(p1_f + fwd * radius, p2_f + fwd * radius, c);
                DrawLine(p1_f - fwd * radius, p2_f - fwd * radius, c);
            }
        }

        public void DrawBounds(in B3Aabb bounds, uint color)
        {
            var c = ToColor(color);
            float3 center = (bounds.LowerBound + bounds.UpperBound) * 0.5f;
            float3 size = (bounds.UpperBound - bounds.LowerBound);
            DrawSolidMesh(_solidCube, center, quaternion.identity, size, new Color(c.r, c.g, c.b, 0.1f));

            Span<float3> corners = stackalloc float3[8];
            for (int i = 0; i < 8; i++)
            {
                corners[i] = new float3(
                    (i & 1) == 0 ? bounds.LowerBound.x : bounds.UpperBound.x,
                    (i & 2) == 0 ? bounds.LowerBound.y : bounds.UpperBound.y,
                    (i & 4) == 0 ? bounds.LowerBound.z : bounds.UpperBound.z);
            }
            for (int i = 0; i < 8; i++)
            {
                for (int bit = 1; bit <= 4; bit <<= 1)
                {
                    int j = i | bit;
                    if (j != i) DrawLine(corners[i], corners[j], c);
                }
            }
        }

        public void DrawBox(float3 extents, in B3WorldTransform transform, uint color)
        {
            var c = ToColor(color);
            DrawSolidMesh(_solidCube, transform.Position, transform.Rotation, extents * 2f, new Color(c.r, c.g, c.b, 0.18f));

            Span<float3> corners = stackalloc float3[8];
            for (int i = 0; i < 8; i++)
            {
                float3 local = new(
                    (i & 1) == 0 ? -extents.x : extents.x,
                    (i & 2) == 0 ? -extents.y : extents.y,
                    (i & 4) == 0 ? -extents.z : extents.z);
                corners[i] = transform.Position + math.mul(transform.Rotation, local);
            }
            for (int i = 0; i < 8; i++)
            {
                for (int bit = 1; bit <= 4; bit <<= 1)
                {
                    int j = i | bit;
                    if (j != i) DrawLine(corners[i], corners[j], c);
                }
            }
        }

        public void DrawString(B3Pos p, in string str, uint color)
        {
            // Empty for now as string rendering without an external package or GUI is complex
        }
    }
}