using System;
using System.Runtime.InteropServices;
using AOT;
using Debug = UnityEngine.Debug;

namespace Box3d
{
    public readonly struct CompoundQueryResult
    {
        public readonly int Count;
        public readonly bool Truncated;

        public CompoundQueryResult(int count, bool truncated)
        {
            Count = count;
            Truncated = truncated;
        }
    }

    public static unsafe class CompoundQueries
    {
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private unsafe delegate NativeBool QueryCompoundDelegate(
            Sys.b3CompoundData* compound,
            int childIndex,
            void* userContext);

        private static readonly QueryCompoundDelegate QUERY_COMPOUND_INSTANCE = QueryCompound;
        private static readonly IntPtr QUERY_COMPOUND_PTR = Marshal.GetFunctionPointerForDelegate(QUERY_COMPOUND_INSTANCE);

        private unsafe struct ChildIndexCollectorContext
        {
            public int* Buffer;
            public int Capacity;
            public int Count;
            public bool Truncated;
            public ChildIndexCollectorContext(int* buffer, int capacity)
            {
                Buffer = buffer;
                Capacity = capacity;
                Count = 0;
                Truncated = false;
            }
        }

        [MonoPInvokeCallback(typeof(QueryCompoundDelegate))]
        private static NativeBool QueryCompound(Sys.b3CompoundData* compound, int childIndex, void* userContext)
        {
            try
            {
                var ctx = (ChildIndexCollectorContext*)userContext;
                if (ctx->Count == ctx->Capacity)
                {
                    ctx->Truncated = true;
                    return false;
                }

                ctx->Buffer[ctx->Count] = childIndex;
                ctx->Count++;
                return true;
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                return true;
            }
        }

        public static CompoundQueryResult Query(Sys.b3CompoundData* compound, in B3Aabb bounds,
            Span<int> indices)
        {
            fixed (int* p = indices)
            {
                ChildIndexCollectorContext context = new()
                {
                    Buffer = p,
                    Capacity = indices.Length
                };

                Ffi.b3QueryCompound(
                    compound,
                    bounds,
                    QUERY_COMPOUND_PTR,
                    &context);

                return new CompoundQueryResult(
                    context.Count,
                    context.Truncated);
            }
        }
    }
}