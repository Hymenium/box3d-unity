using System;
using System.Runtime.InteropServices;
using AOT;
using Unity.Mathematics;

namespace Box3D
{
    using Sys;

    public unsafe partial struct World
    {
        // public sealed class ShapeCollector : IOverlapCallback
        // {
        //     private readonly List<ShapeId> _results;

        //     public ShapeCollector(List<ShapeId> results)
        //     {
        //         _results = results;
        //     }

        //     public bool ReportShape(ShapeId shape_id)
        //     {
        //         _results.Add(shape_id);
        //         return true;
        //     }
        // }

        public interface IOverlapCallback
        {
            // return true to continue, false to stop
            bool ReportShape(ShapeId shape_id);
        }

        public interface ICastCallback
        {
            // return -1: ignore this shape and continue
            // return 0: terminate the ray cast
            // return fraction: clip the ray to this point
            // return 1: don't clip the ray and continue
            float OnHit(
                ShapeId shape_id,
                float3 point, float3 normal, float fraction,
                ulong user_material_id,
                int triangle_index,
                int child_index);
        }

        private static readonly b3OverlapResultFcn OverlapTrampolineDelegate = ManagedOverlapTrampoline;
        private static readonly b3CastResultFcn CastTrampolineDelegate = ManagedCastTrampoline;
        private static readonly IntPtr OVERLAP_TRAMPOLINE_PTR = Marshal.GetFunctionPointerForDelegate(OverlapTrampolineDelegate);
        private static readonly IntPtr CAST_TRAMPOLINE_PTR = Marshal.GetFunctionPointerForDelegate(CastTrampolineDelegate);

        [MonoPInvokeCallback(typeof(b3OverlapResultFcn))]
        private static unsafe NativeBool ManagedOverlapTrampoline(ShapeId shape_id, void* raw_context)
        {
            var callback = (IOverlapCallback)GCHandle.FromIntPtr((IntPtr)raw_context).Target!;
            return callback.ReportShape(shape_id);
        }

        [MonoPInvokeCallback(typeof(b3CastResultFcn))]
        private static unsafe float ManagedCastTrampoline(
            ShapeId shape_id,
            B3Pos point, float3 normal, float fraction,
            ulong user_material_id,
            int triangle_index,
            int child_index,
            void* raw_context)
        {
            var callback = (ICastCallback)GCHandle.FromIntPtr((IntPtr)raw_context).Target!;
            return callback.OnHit(
                shape_id, point, normal, fraction,
                user_material_id, triangle_index, child_index);
        }

        public unsafe TreeStats OverlapAABB(B3Aabb aabb, QueryFilter filter, IOverlapCallback callback)
        {
            GCHandle handle = GCHandle.Alloc(callback);
            try
            {
                void* context = (void*)GCHandle.ToIntPtr(handle);
                b3TreeStats s = Ffi.b3World_OverlapAABB(
                    Id,
                    aabb,
                    filter,
                    OVERLAP_TRAMPOLINE_PTR,
                    context);

                return new(s.nodeVisits, s.leafVisits);
            }
            finally
            {
                handle.Free();
            }
        }

        public unsafe TreeStats OverlapShape(B3Pos origin, ReadOnlySpan<float3> proxyPoints, float proxyRadius,
            QueryFilter filter, IOverlapCallback callback, void* context)
        {
            if (proxyPoints.IsEmpty) throw new ArgumentException("proxy needs at least one point", nameof(proxyPoints));
            GCHandle handle = GCHandle.Alloc(callback);
            try
            {
                fixed (float3* points = proxyPoints)
                {
                    var proxy = new b3ShapeProxy { points = points, count = proxyPoints.Length, radius = proxyRadius };
                    void* ctx = (void*)GCHandle.ToIntPtr(handle);
                    b3TreeStats s = Ffi.b3World_OverlapShape(
                        Id,
                        origin,
                        &proxy,
                        filter,
                        OVERLAP_TRAMPOLINE_PTR,
                        ctx);
                    return new(s.nodeVisits, s.leafVisits);
                }
            }
            finally
            {
                handle.Free();
            }
        }

        public unsafe TreeStats CastRay(float3 origin, float3 translation, QueryFilter filter, ICastCallback callback)
        {
            GCHandle handle = GCHandle.Alloc(callback);
            try
            {
                void* ctx = (void*)GCHandle.ToIntPtr(handle);
                b3TreeStats s = Ffi.b3World_CastRay(
                    Id,
                    origin,
                    translation,
                    filter,
                    CAST_TRAMPOLINE_PTR,
                    ctx);
                return new(s.nodeVisits, s.leafVisits);
            }
            finally
            {
                handle.Free();
            }
        }

        public unsafe TreeStats CastShape(B3Pos origin, ReadOnlySpan<float3> proxyPoints, float proxyRadius,
            float3 translation, QueryFilter filter, ICastCallback callback, void* context)
        {
            if (proxyPoints.IsEmpty) throw new ArgumentException("proxy needs at least one point", nameof(proxyPoints));
            GCHandle handle = GCHandle.Alloc(callback);
            try
            {
                fixed (float3* points = proxyPoints)
                {
                    var proxy = new b3ShapeProxy { points = points, count = proxyPoints.Length, radius = proxyRadius };
                    void* ctx = (void*)GCHandle.ToIntPtr(handle);
                    b3TreeStats s = Ffi.b3World_CastShape(
                        Id,
                        origin,
                        &proxy,
                        translation,
                        filter,
                        CAST_TRAMPOLINE_PTR,
                        ctx);
                    return new(s.nodeVisits, s.leafVisits);
                }
            }
            finally
            {
                handle.Free();
            }
        }

        // Collector trampolines: static, rooted for the process lifetime, IL2CPP-safe via
        // MonoPInvokeCallback. Per-call state travels through the native context pointer as a
        // stack-allocated context struct — no allocation per query.

        private static readonly b3OverlapResultFcn OverlapCollectorDelegate = OverlapCollector;
        private static readonly IntPtr OverlapCollectorPtr = Marshal.GetFunctionPointerForDelegate(OverlapCollectorDelegate);
        private static readonly b3CastResultFcn CastCollectorDelegate = CastCollector;
        private static readonly IntPtr CastCollectorPtr = Marshal.GetFunctionPointerForDelegate(CastCollectorDelegate);

        private unsafe struct ShapeCollectorContext
        {
            public ShapeId* Buffer;
            public int Capacity;
            public int Count;
        }

        private unsafe struct RayCollectorContext
        {
            public RayHit* Buffer;
            public int Capacity;
            public int Count;
        }

        [MonoPInvokeCallback(typeof(b3OverlapResultFcn))]
        private static unsafe NativeBool OverlapCollector(ShapeId shapeId, void* context)
        {
            var ctx = (ShapeCollectorContext*)context;
            if (ctx->Count == ctx->Capacity) return false; // buffer full — stop the query
            ctx->Buffer[ctx->Count] = shapeId;
            ctx->Count++;
            return true;
        }

        [MonoPInvokeCallback(typeof(b3CastResultFcn))]
        private static unsafe float CastCollector(ShapeId shapeId, B3Pos point, float3 normal,
            float fraction, ulong userMaterialId, int triangleIndex, int childIndex, void* context)
        {
            var ctx = (RayCollectorContext*)context;
            if (ctx->Count == ctx->Capacity) return 0f; // buffer full — terminate
            ctx->Buffer[ctx->Count] = new RayHit
            {
                ShapeId = shapeId,
                Point = point,
                Normal = normal,
                Fraction = fraction,
                UserMaterialId = userMaterialId,
                TriangleIndex = triangleIndex,
                ChildIndex = childIndex,
            };
            ctx->Count++;
            return 1f; // keep the full ray — collect every hit
        }

        /// <summary>Finds shapes whose bounds overlap the AABB. Fills the buffer, returns the
        /// count (query stops early if the buffer fills up).</summary>
        public int OverlapAABB(B3Aabb aabb, QueryFilter filter, Span<ShapeId> results)
            => OverlapAABB(aabb, filter, results, out _);

        /// <summary>As <see cref="OverlapAABB(B3Aabb, QueryFilter, Span{ShapeId})"/>, also reporting the
        /// broadphase-tree work the query did (<see cref="TreeStats"/>) — useful for profiling queries.</summary>
        public unsafe int OverlapAABB(B3Aabb aabb, QueryFilter filter, Span<ShapeId> results, out TreeStats stats)
        {
            fixed (ShapeId* buffer = results)
            {
                var ctx = new ShapeCollectorContext { Buffer = buffer, Capacity = results.Length };
                b3TreeStats s = Ffi.b3World_OverlapAABB(Id, aabb, filter, OverlapCollectorPtr, &ctx);
                stats = new(s.nodeVisits, s.leafVisits);
                return ctx.Count;
            }
        }

        /// <summary>Finds shapes overlapping a convex point-cloud proxy (a sphere when one point,
        /// a capsule when two, a hull otherwise) placed at origin. Fills the buffer, returns the count.</summary>
        public int OverlapShape(float3 origin, ReadOnlySpan<float3> proxyPoints, float proxyRadius,
            QueryFilter filter, Span<ShapeId> results)
            => OverlapShape(origin, proxyPoints, proxyRadius, filter, results, out _);

        /// <summary>As the other <c>OverlapShape</c>, also reporting broadphase-tree work.</summary>
        public unsafe int OverlapShape(B3Pos origin, ReadOnlySpan<float3> proxyPoints, float proxyRadius,
            QueryFilter filter, Span<ShapeId> results, out TreeStats stats)
        {
            if (proxyPoints.IsEmpty) throw new ArgumentException("proxy needs at least one point", nameof(proxyPoints));
            fixed (float3* points = proxyPoints)
            fixed (ShapeId* buffer = results)
            {
                var proxy = new b3ShapeProxy { points = points, count = proxyPoints.Length, radius = proxyRadius };
                var ctx = new ShapeCollectorContext { Buffer = buffer, Capacity = results.Length };
                b3TreeStats s = Ffi.b3World_OverlapShape(Id, origin, &proxy, filter, OverlapCollectorPtr, &ctx);
                stats = new(s.nodeVisits, s.leafVisits);
                return ctx.Count;
            }
        }

        /// <summary>Collects every hit along the ray (unordered). Fills the buffer, returns the
        /// count. For just the nearest hit use <see cref="CastRayClosest"/>.</summary>
        public int CastRay(float3 origin, float3 translation, QueryFilter filter, Span<RayHit> hits)
            => CastRay(origin, translation, filter, hits, out _);

        /// <summary>As the other <c>CastRay</c>, also reporting broadphase-tree work.</summary>
        public unsafe int CastRay(float3 origin, float3 translation, QueryFilter filter, Span<RayHit> hits, out TreeStats stats)
        {
            fixed (RayHit* buffer = hits)
            {
                var ctx = new RayCollectorContext { Buffer = buffer, Capacity = hits.Length };
                b3TreeStats s = Ffi.b3World_CastRay(Id, origin, translation, filter, CastCollectorPtr, &ctx);
                stats = new(s.nodeVisits, s.leafVisits);
                return ctx.Count;
            }
        }

        /// <summary>Sweeps a convex point-cloud proxy from origin along translation, collecting
        /// every hit (unordered). Fills the buffer, returns the count.</summary>
        public int CastShape(float3 origin, ReadOnlySpan<float3> proxyPoints, float proxyRadius,
            float3 translation, QueryFilter filter, Span<RayHit> hits)
            => CastShape(origin, proxyPoints, proxyRadius, translation, filter, hits, out _);

        /// <summary>As the other <c>CastShape</c>, also reporting broadphase-tree work.</summary>
        public unsafe int CastShape(float3 origin, ReadOnlySpan<float3> proxyPoints, float proxyRadius,
            float3 translation, QueryFilter filter, Span<RayHit> hits, out TreeStats stats)
        {
            if (proxyPoints.IsEmpty) throw new ArgumentException("proxy needs at least one point", nameof(proxyPoints));
            fixed (float3* points = proxyPoints)
            fixed (RayHit* buffer = hits)
            {
                var proxy = new b3ShapeProxy { points = points, count = proxyPoints.Length, radius = proxyRadius };
                var ctx = new RayCollectorContext { Buffer = buffer, Capacity = hits.Length };
                b3TreeStats s = Ffi.b3World_CastShape(Id, origin, &proxy, translation, filter, CastCollectorPtr, &ctx);
                stats = new(s.nodeVisits, s.leafVisits);
                return ctx.Count;
            }
        }
    }
}
