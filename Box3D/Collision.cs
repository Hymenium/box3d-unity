using System;
using Unity.Mathematics;

namespace Box3D
{
    using Sys;

    /// <summary>
    /// High-level static collision queries that wrap the FFI to hide b3ShapeProxy pointers.
    /// </summary>
    public static class Collision
    {
        /// <summary>
        /// Checks for overlap between a Capsule and a shape proxy defined by a set of points and a radius.
        /// </summary>
        public static unsafe NativeBool OverlapCapsule(
            Capsule capsule,
            B3Transform transform,
            ReadOnlySpan<float3> proxyPoints,
            float proxyRadius)
        {
            fixed (float3* pointsPtr = proxyPoints)
            {
                var proxy = new b3ShapeProxy
                {
                    points = pointsPtr,
                    count = proxyPoints.Length,
                    radius = proxyRadius
                };

                return Ffi.b3OverlapCapsule(&capsule, transform, &proxy);
            }
        }

        /// <summary>
        /// Collides two capsules and populates the given local manifold.
        /// </summary>
        public static unsafe void CollideCapsules(
            ref b3LocalManifold manifold,
            int capacity,
            Capsule capsuleA,
            Capsule capsuleB,
            B3Transform transformBtoA)
        {
            fixed (b3LocalManifold* manifoldPtr = &manifold)
            {
                Ffi.b3CollideCapsules(
                    manifoldPtr,
                    capacity,
                    &capsuleA,
                    &capsuleB,
                    transformBtoA);
            }
        }
    }
}
