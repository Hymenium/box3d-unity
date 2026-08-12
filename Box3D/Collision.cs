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
            in Capsule capsule,
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

                fixed (Capsule* capsulePtr = &capsule)
                {
                    return Ffi.b3OverlapCapsule(capsulePtr, transform, &proxy);
                }
            }
        }
    }
}
