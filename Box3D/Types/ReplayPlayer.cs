using System;
using System.Runtime.InteropServices;

namespace Box3D
{
    using Sys;

    /// <summary>Metadata about a replay. Mirrors native b3RecPlayerInfo.</summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct ReplayInfo
    {
        public int FrameCount;
        public int WorkerCount;
        public float TimeStep;
        public int SubStepCount;
        public float LengthScale;
        public B3Aabb Bounds;
    }

    /// <summary>A scrubbable player over a recorded simulation (the bytes from a <see cref="Recording"/>
    /// or a loaded file). Step or seek to any frame and inspect the replayed <see cref="World"/> — draw
    /// it, query contacts, read body transforms — and detect where a replay diverges from the original
    /// (non-determinism), including the first divergent frame. Replay at a different worker count than
    /// it was recorded to test cross-thread determinism. Owns a native world — call <see cref="Destroy"/>.
    /// <para>Value type: copies share one native pointer — call <see cref="Destroy"/> exactly once,
    /// through one copy; other copies still report <see cref="IsCreated"/> but dangle.</para></summary>
    public struct ReplayPlayer
    {
        private IntPtr _handle; // b3RecPlayer*

        private GCHandle _shapeFactoryHandle;

        public bool IsCreated => _handle != IntPtr.Zero;

        /// <summary>Creates a player from recording bytes (e.g. <c>recording.GetData()</c> or file bytes).</summary>
        public static unsafe ReplayPlayer Create(ReadOnlySpan<byte> data, int workerCount = 1)
        {
            fixed (byte* p = data)
            {
                return new ReplayPlayer { _handle = (IntPtr)Ffi.b3RecPlayer_Create(p, data.Length, workerCount) };
            }
        }

        public unsafe void Destroy()
        {
            if (_handle == IntPtr.Zero) return;
            Ffi.b3RecPlayer_Destroy((b3RecPlayer*)_handle);
            _handle = IntPtr.Zero;
            if (_shapeFactoryHandle.IsAllocated) _shapeFactoryHandle.Free();
        }

        /// <summary>Wires box3d's debug-shape callbacks so the replayed world draws shape <em>interiors</em>
        /// (not just contacts/bounds) via <c>World.DrawDebug</c>. Call once after <see cref="Create"/>, on
        /// the main thread. Not needed for headless validation.</summary>
        public unsafe void EnableShapeDrawing(IDebugShapeFactory shapeFactory)
        {
            if (_handle == IntPtr.Zero)
            {
                throw new ObjectDisposedException(nameof(ReplayPlayer));
            }

            if (_shapeFactoryHandle.IsAllocated)
            {
                throw new InvalidOperationException("A debug-shape factory is already registered.");
            }

            _shapeFactoryHandle = GCHandle.Alloc(shapeFactory);

            Ffi.b3RecPlayer_SetDebugShapeCallbacks((b3RecPlayer*)_handle,
                NativeDebugDrawBridge.CreateShapePtr,
                NativeDebugDrawBridge.DestroyShapePtr,
                (void*)GCHandle.ToIntPtr(_shapeFactoryHandle));
        }

        /// <summary>The replayed world at the current frame — pass to debug draw / <c>Body.GetContacts</c>
        /// / transform reads to inspect the replay. (Do not destroy it; the player owns it.)</summary>
        public unsafe World World => new World { Id = Ffi.b3RecPlayer_GetWorldId((b3RecPlayer*)_handle) };

        /// <summary>Advances one frame. Returns false once at the end.</summary>
        public unsafe bool StepFrame() => Ffi.b3RecPlayer_StepFrame((b3RecPlayer*)_handle);

        /// <summary>Jumps to <paramref name="frame"/> (re-simulated from the nearest keyframe).</summary>
        public unsafe void SeekFrame(int frame) => Ffi.b3RecPlayer_SeekFrame((b3RecPlayer*)_handle, frame);

        /// <summary>Rewinds to the first frame.</summary>
        public unsafe void Restart() => Ffi.b3RecPlayer_Restart((b3RecPlayer*)_handle);

        public unsafe int Frame => Ffi.b3RecPlayer_GetFrame((b3RecPlayer*)_handle);
        public unsafe int FrameCount => Ffi.b3RecPlayer_GetFrameCount((b3RecPlayer*)_handle);
        public unsafe bool IsAtEnd => Ffi.b3RecPlayer_IsAtEnd((b3RecPlayer*)_handle);

        /// <summary>Whether the replay has diverged from the recorded state (non-determinism detected).</summary>
        public unsafe bool HasDiverged => Ffi.b3RecPlayer_HasDiverged((b3RecPlayer*)_handle);

        /// <summary>The first frame that diverged, or -1 if it hasn't.</summary>
        public unsafe int DivergeFrame => Ffi.b3RecPlayer_GetDivergeFrame((b3RecPlayer*)_handle);

        /// <summary>Replay metadata (frame count, timestep, sub-steps, worker count, bounds).</summary>
        public unsafe ReplayInfo GetInfo()
        {
            // b3RecPlayerInfo is layout-identical to ReplayInfo — reinterpret the copy.
            b3RecPlayerInfo i = Ffi.b3RecPlayer_GetInfo((b3RecPlayer*)_handle);
            return *(ReplayInfo*)&i;
        }

        /// <summary>Re-runs the replay at a different worker count — a cross-thread determinism test.</summary>
        public unsafe void SetWorkerCount(int count) => Ffi.b3RecPlayer_SetWorkerCount((b3RecPlayer*)_handle, count);

        /// <summary>Number of tracked bodies in the replay.</summary>
        public unsafe int BodyCount => Ffi.b3RecPlayer_GetBodyCount((b3RecPlayer*)_handle);

        /// <summary>A tracked body at the current frame (index in [0, <see cref="BodyCount"/>)).</summary>
        public unsafe Body GetBody(int index) => new(Ffi.b3RecPlayer_GetBodyId((b3RecPlayer*)_handle, index));
    }
}
