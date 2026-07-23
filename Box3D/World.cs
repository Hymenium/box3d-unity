using System;
using System.Runtime.InteropServices;
using UnityEngine;

namespace Box3D
{
    using Sys;
    /// <summary>A Box3d simulation world. Thin value wrapper over a generation-validated world id —
    /// safe to copy; a stale handle fails <see cref="IsValid"/> rather than crashing.</summary>
    public partial struct World : IEquatable<World>
    {
        public WorldId Id;
        private GCHandle _debug_shape_factory_handle;

        public static unsafe World Create(in WorldDef def, IDebugShapeFactory shape_factory = null)
        {
            WorldDef local = def;
            GCHandle handle = default;

            if (shape_factory != null)
            {
                local.CreateDebugShape = NativeDebugDrawBridge.CREATE_SHAPE_PTR;
                local.DestroyDebugShape = NativeDebugDrawBridge.DESTROY_SHAPE_PTR;
                handle = GCHandle.Alloc(shape_factory);
                local.UserDebugShapeContext = GCHandle.ToIntPtr(handle);
            }

#if UNITY_WEBGL && !UNITY_EDITOR
            local.WorkerCount = 1; // WebGL players are single-threaded
#endif
            var world = new World
            {
                Id = Ffi.b3CreateWorld(&local),
                _debug_shape_factory_handle = handle,
            };
            return world;
        }

        /// <summary>Destroys the world and everything in it. All body/shape/joint ids become stale.</summary>
        public void Destroy()
        {
            if (Id.IsNull) return; // double-destroy would pass a null id into unvalidated native paths
            ClearCallbackSlots();
            Ffi.b3DestroyWorld(Id);
            if (_debug_shape_factory_handle.IsAllocated) _debug_shape_factory_handle.Free();
            Id = default;
        }

        public readonly bool IsValid => Ffi.b3World_IsValid(Id);

        /// <summary>Advances the simulation. Use a fixed timeStep (e.g. Time.fixedDeltaTime);
        /// 4 sub-steps is the recommended default.</summary>
        public void Step(float timeStep, int subStepCount = 4)
        {
            Ffi.b3World_Step(Id, timeStep, subStepCount);
        }

        public unsafe Body CreateBody(in BodyDef def)
        {
            BodyDef local = def;
            return Body.WrapUnchecked(Ffi.b3CreateBody(Id, &local));
        }

        /// <summary>Move events for bodies that moved during the last step.
        /// The span points into transient engine memory — valid only until the next Step or
        /// world mutation. Consume immediately; do not store.</summary>
        public unsafe ReadOnlySpan<BodyMoveEvent> GetBodyMoveEvents()
        {
            BodyEventsRaw raw = Ffi.b3World_GetBodyEvents(Id);
            return new ReadOnlySpan<BodyMoveEvent>((void*)raw.MoveEvents, raw.MoveCount);
        }

        /// <summary>Contact begin/end/hit events from the last step. Transient — valid only until
        /// the next Step or world mutation. Shapes opt in via ShapeDef.EnableContactEvents /
        /// EnableHitEvents (both false by default).</summary>
        public unsafe ContactEvents GetContactEvents()
        {
            ContactEventsRaw raw = Ffi.b3World_GetContactEvents(Id);
            return new ContactEvents(
                new ReadOnlySpan<ContactBeginTouchEvent>((void*)raw.BeginEvents, raw.BeginCount),
                new ReadOnlySpan<ContactEndTouchEvent>((void*)raw.EndEvents, raw.EndCount),
                new ReadOnlySpan<ContactHitEvent>((void*)raw.HitEvents, raw.HitCount));
        }

        /// <summary>Sensor begin/end events from the last step. Transient — valid only until the
        /// next Step or world mutation. Both the sensor and visitor shapes must opt in via
        /// ShapeDef.EnableSensorEvents (false by default).</summary>
        public unsafe SensorEvents GetSensorEvents()
        {
            SensorEventsRaw raw = Ffi.b3World_GetSensorEvents(Id);
            return new SensorEvents(
                new ReadOnlySpan<SensorBeginTouchEvent>((void*)raw.BeginEvents, raw.BeginCount),
                new ReadOnlySpan<SensorEndTouchEvent>((void*)raw.EndEvents, raw.EndCount));
        }

        /// <summary>Joint events (force/torque threshold exceeded) from the last step. Transient —
        /// valid only until the next Step or world mutation.</summary>
        public unsafe ReadOnlySpan<JointEvent> GetJointEvents()
        {
            JointEventsRaw raw = Ffi.b3World_GetJointEvents(Id);
            return new ReadOnlySpan<JointEvent>((void*)raw.JointEvents, raw.Count);
        }

        /// <summary>Applies a radial impulse to shapes within the explosion radius.
        /// Create the def via <see cref="ExplosionDef.Default"/>.</summary>
        public unsafe void Explode(in ExplosionDef def)
        {
            ExplosionDef local = def;
            Ffi.b3World_Explode(Id, &local);
        }

        /// <summary>Application-specific data attached to the world.</summary>
        public unsafe IntPtr UserData
        {
            get => (IntPtr)Ffi.b3World_GetUserData(Id);
            set => Ffi.b3World_SetUserData(Id, (void*)value);
        }

        public bool Equals(World other)
        {
            return Id.Equals(other.Id);
        }

        public override bool Equals(object obj)
        {
            return obj is World other && Equals(other);
        }

        public override int GetHashCode()
        {
            return Id.GetHashCode();
        }
        public static bool operator ==(World left, World right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(World left, World right)
        {
            return !left.Equals(right);
        }

    }
}
