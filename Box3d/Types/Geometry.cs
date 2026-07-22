using System;
using System.Runtime.InteropServices;
using Unity.Mathematics;
using Assert = UnityEngine.Assertions.Assert;

namespace Box3d
{
    using Sys;
    /// <summary>Mirrors native b3Sphere (16 bytes): a sphere with a local-space offset.</summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct Sphere
    {
        public float3 Center;
        public float Radius;
    }

    /// <summary>Mirrors native b3Capsule (28 bytes): two hemisphere centers and a radius.</summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct Capsule
    {
        public float3 Center1;
        public float3 Center2;
        public float Radius;
    }

    /// <summary>Mirrors native b3AABB (24 bytes).</summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct B3Aabb
    {
        public float3 LowerBound;
        public float3 UpperBound;
    }

    /// <summary>Mirrors native b3Matrix3 (36 bytes, column-major).</summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct B3Matrix3
    {
        public float3 Cx;
        public float3 Cy;
        public float3 Cz;
    }

    /// <summary>Mirrors native b3BoxHull (440 bytes): a self-contained box hull returned by value
    /// from b3MakeBoxHull. Safe to keep on the stack — hull shape creation clones the data.
    /// The trailing blob holds the vertex/point/edge/face/plane arrays the header offsets index into.</summary>
    [StructLayout(LayoutKind.Sequential)]
    public unsafe struct BoxHull
    {
        internal b3HullData Base;
        internal fixed byte Data[304];

        /// <summary>A box hull with the given half-extents, centered at the local origin.</summary>
        public static BoxHull Create(float halfX, float halfY, float halfZ)
        {
            return Ffi.b3MakeBoxHull(halfX, halfY, halfZ);
        }

        /// <summary>A cube hull with the given half-width, centered at the local origin.</summary>
        public static BoxHull CreateCube(float halfWidth)
        {
            return Ffi.b3MakeCubeHull(halfWidth);
        }

        /// <summary>A box hull with the given half-extents, centered at <paramref name="offset"/>.</summary>
        public static BoxHull CreateOffset(float halfX, float halfY, float halfZ, float3 offset)
        {
            return Ffi.b3MakeOffsetBoxHull(halfX, halfY, halfZ, offset);
        }

        /// <summary>A box hull with the given half-extents, positioned and rotated by
        /// <paramref name="transform"/>.</summary>
        public static BoxHull CreateTransformed(float halfX, float halfY, float halfZ, B3Transform transform)
        {
            return Ffi.b3MakeTransformedBoxHull(halfX, halfY, halfZ, transform);
        }
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public readonly struct HullHalfEdge
    {
        public readonly byte Next;
        public readonly byte Twin;
        public readonly byte Origin;
        public readonly byte Face;
    }

    // ReadOnlySpan<float3> points = hull.Points;
    // ReadOnlySpan<HullHalfEdge> edges = hull.HalfEdges;

    // for (int i = 0; i < edges.Length; ++i)
    // {
    //     HullHalfEdge edge = edges[i];

    //     if (i >= edge.Twin)
    //         continue;

    //     HullHalfEdge twin = edges[edge.Twin];

    //     AddLine(
    //         lines,
    //         points[edge.Origin],
    //         points[twin.Origin]);
    // }
    public readonly unsafe ref struct HullView
    {
        private readonly b3HullData* _data;

        public HullView(b3HullData* data)
        {
            _data = data;
        }

        public int PointCount => _data->VertexCount;
        public int HalfEdgeCount => _data->EdgeCount;
        public int FaceCount => _data->FaceCount;

        public ReadOnlySpan<float3> Points
        {
            get
            {
                byte* base_ptr = (byte*)_data;
                float3* points =
                    (float3*)(base_ptr + _data->PointOffset);

                return new ReadOnlySpan<float3>(
                    points,
                    _data->VertexCount);
            }
        }

        public ReadOnlySpan<HullHalfEdge> HalfEdges
        {
            get
            {
                byte* base_ptr = (byte*)_data;
                HullHalfEdge* edges =
                    (HullHalfEdge*)(base_ptr + _data->EdgeOffset);

                return new ReadOnlySpan<HullHalfEdge>(
                    edges,
                    _data->EdgeCount);
            }
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    public readonly struct MeshTriangle
    {
        public readonly int Index1;
        public readonly int Index2;
        public readonly int Index3;
    }

    // ReadOnlySpan<float3> vertices = mesh.Vertices;
    // ReadOnlySpan<MeshTriangle> triangles = mesh.Triangles;
    // float3 scale = mesh.Scale;

    // foreach (MeshTriangle triangle in triangles)
    // {
    //     float3 a = vertices[triangle.Index1] * scale;
    //     float3 b = vertices[triangle.Index2] * scale;
    //     float3 c = vertices[triangle.Index3] * scale;

    //     AddLine(a, b);
    //     AddLine(b, c);
    //     AddLine(c, a);
    // }
    public readonly unsafe ref struct MeshView
    {
        private readonly b3MeshData* _data;
        public readonly float3 Scale;

        public MeshView(b3MeshData* data, float3 scale)
        {
            _data = data;
            Scale = scale;
        }

        public int VertexCount => _data->vertexCount;
        public int TriangleCount => _data->triangleCount;
        public int MaterialCount => _data->materialCount;

        public ReadOnlySpan<float3> Vertices
        {
            get
            {
                byte* base_ptr = (byte*)_data;

                return new ReadOnlySpan<float3>(
                    (float3*)(base_ptr + _data->vertexOffset),
                    _data->vertexCount);
            }
        }

        public ReadOnlySpan<MeshTriangle> Triangles
        {
            get
            {
                byte* base_ptr = (byte*)_data;

                return new ReadOnlySpan<MeshTriangle>(
                    (MeshTriangle*)(base_ptr + _data->triangleOffset),
                    _data->triangleCount);
            }
        }

        public ReadOnlySpan<int> MaterialIndices
        {
            get
            {
                if (_data->materialCount == 0)
                    return ReadOnlySpan<int>.Empty;

                byte* base_ptr = (byte*)_data;

                return new ReadOnlySpan<int>(
                    (int*)(base_ptr + _data->materialOffset),
                    _data->triangleCount);
            }
        }
    }

    public readonly unsafe ref struct HeightFieldView
    {
        private readonly b3HeightFieldData* _data;

        public HeightFieldView(b3HeightFieldData* data)
        {
            _data = data;
        }

        public int ColumnCount => _data->columnCount;
        public int RowCount => _data->rowCount;

        public int CellCount =>
            (ColumnCount - 1) * (RowCount - 1);

        public int TriangleCount =>
            2 * CellCount;

        public float MinHeight => _data->minHeight;
        public float MaxHeight => _data->maxHeight;
        public float HeightScale => _data->heightScale;

        public float3 Scale => _data->scale;

        public bool Clockwise => _data->clockwise;

        public ReadOnlySpan<ushort> CompressedHeights
        {
            get
            {
                byte* base_ptr = (byte*)_data;

                return new ReadOnlySpan<ushort>(
                    (ushort*)(base_ptr + _data->heightsOffset),
                    ColumnCount * RowCount);
            }
        }

        public ReadOnlySpan<byte> Materials
        {
            get
            {
                byte* base_ptr = (byte*)_data;

                return new ReadOnlySpan<byte>(
                    base_ptr + _data->materialOffset,
                    CellCount);
            }
        }

        public ReadOnlySpan<byte> TriangleFlags
        {
            get
            {
                byte* base_ptr = (byte*)_data;

                return new ReadOnlySpan<byte>(
                    base_ptr + _data->flagsOffset,
                    TriangleCount);
            }
        }

        public float GetHeight(int column, int row)
        {
            CheckGridPoint(column, row);

            int index = row * ColumnCount + column;
            ushort compressed = CompressedHeights[index];

            return MinHeight + HeightScale * compressed;
        }

        public float3 GetPoint(int column, int row)
        {
            float height = GetHeight(column, row);

            return new float3(
                column * Scale.x,
                height * Scale.y,
                row * Scale.z);
        }

        public byte GetMaterial(int column, int row)
        {
            CheckCell(column, row);

            int index = row * (ColumnCount - 1) + column;
            return Materials[index];
        }

        private void CheckGridPoint(int column, int row)
        {
            if ((uint)column >= (uint)ColumnCount)
                throw new ArgumentOutOfRangeException(nameof(column));

            if ((uint)row >= (uint)RowCount)
                throw new ArgumentOutOfRangeException(nameof(row));
        }

        public byte GetTriangleFlag(int column, int row, int triangle)
        {
            CheckCell(column, row);

            if ((uint)triangle >= 2u)
                throw new ArgumentOutOfRangeException(nameof(triangle));

            int cell_index =
                row * (ColumnCount - 1) + column;

            int triangle_index =
                2 * cell_index + triangle;

            return TriangleFlags[triangle_index];
        }

        private void CheckCell(int column, int row)
        {
            if ((uint)column >= (uint)(ColumnCount - 1))
                throw new ArgumentOutOfRangeException(nameof(column));

            if ((uint)row >= (uint)(RowCount - 1))
                throw new ArgumentOutOfRangeException(nameof(row));
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    public readonly struct CompoundSphere
    {
        public readonly int materialId;
        public readonly Sphere sphere;
    }

    [StructLayout(LayoutKind.Sequential)]
    public readonly struct CompoundCapsule
    {
        public readonly int materialId;
        public readonly Capsule capsule;
    }

    [StructLayout(LayoutKind.Sequential)]
    public readonly unsafe ref struct CompoundHullView
    {
        private readonly b3CompoundHull* _child;
        public unsafe CompoundHullView(b3CompoundHull* child)
        {
            _child = child;
        }

        public readonly int MaterialIndex => _child->materialIndex;
        public readonly B3Transform Transform => _child->transform;
        public readonly HullView Hull => new(_child->hull);
    }

    [StructLayout(LayoutKind.Sequential)]
    public readonly unsafe ref struct CompoundMeshView
    {
        private readonly b3CompoundMesh* _child;
        public unsafe CompoundMeshView(b3CompoundMesh* child)
        {
            _child = child;
        }

        public ReadOnlySpan<int> MaterialIndices =>
            new(_child->materialIndices, Consts.B3_MAX_COMPOUND_MESH_MATERIALS);
        public readonly B3Transform Transform => _child->transform;
        public readonly MeshView Mesh => new(_child->meshData, _child->scale);

        public readonly int GetMaterialIndex(int triangle_index)
        {
            int local_index = Mesh.MaterialIndices[triangle_index];
            Assert.IsTrue((uint)local_index < math.max(1, (uint)Mesh.MaterialCount),
                $"Native mesh data invalid material index: {local_index}");
            return MaterialIndices[local_index];
        }
    }

    public readonly unsafe ref struct CompoundView
    {
        private readonly b3CompoundData* _data;

        public CompoundView(b3CompoundData* data)
        {
            _data = data;
        }

        public int SphereCount => _data->sphereCount;
        public int CapsuleCount => _data->capsuleCount;
        public int HullCount => _data->hullCount;
        public int MeshCount => _data->meshCount;

        public ReadOnlySpan<CompoundSphere> Spheres
        {
            get
            {
                byte* base_ptr = (byte*)_data;

                return new ReadOnlySpan<CompoundSphere>(
                    (CompoundSphere*)(base_ptr + _data->sphereOffset),
                    SphereCount);
            }
        }

        public ReadOnlySpan<CompoundCapsule> Capsules
        {
            get
            {
                byte* base_ptr = (byte*)_data;

                return new ReadOnlySpan<CompoundCapsule>(
                    (CompoundCapsule*)(base_ptr + _data->capsuleOffset),
                    CapsuleCount);
            }
        }

        public CompoundHullView GetHull(int index)
        {
            if ((uint)index >= (uint)HullCount)
                throw new ArgumentOutOfRangeException(nameof(index));

            byte* base_ptr = (byte*)_data;
            b3CompoundHull* hulls =
                (b3CompoundHull*)(base_ptr + _data->hullOffset);

            return new CompoundHullView(hulls + index);
        }

        public CompoundMeshView GetMesh(int index)
        {
            if ((uint)index >= (uint)MeshCount)
                throw new ArgumentOutOfRangeException(nameof(index));

            byte* base_ptr = (byte*)_data;
            b3CompoundMesh* meshes =
                (b3CompoundMesh*)(base_ptr + _data->meshOffset);

            return new CompoundMeshView(meshes + index);
        }
    }
}
