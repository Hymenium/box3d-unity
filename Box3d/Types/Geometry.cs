using System.Runtime.InteropServices;
using Unity.Mathematics;

namespace Box3d
{
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
        internal Sys.b3HullData Base;
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
}
