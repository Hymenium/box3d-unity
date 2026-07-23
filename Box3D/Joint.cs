using System;

namespace Box3D
{
    /// <summary>A joint of any type. Thin value wrapper over a generation-validated joint id.
    /// Typed joints (<see cref="RevoluteJoint"/>, …) convert implicitly to this.</summary>
    public partial struct Joint : IEquatable<Joint>
    {
        public JointId Id;

        /// <summary>Wraps a joint id — the way back into the wrapper API from ids delivered by
        /// events (e.g. <see cref="JointEvent.JointId"/>) or stored by user code. Cheap and
        /// unchecked; test <see cref="IsValid"/>, or use <see cref="World.TryGetJoint"/> for the
        /// validated form.</summary>
        public Joint(JointId id) => Id = id;

        public bool IsValid => Ffi.b3Joint_IsValid(Id);

        /// <summary>Application-specific data attached to the joint. Delivered back in
        /// <see cref="JointEvent.UserData"/>.</summary>
        public unsafe IntPtr UserData
        {
            get => (IntPtr)Ffi.b3Joint_GetUserData(Id);
            set => Ffi.b3Joint_SetUserData(Id, (void*)value);
        }

        public void Destroy(bool wakeAttached = true)
        {
            if (Id.IsNull) return; // double-destroy would pass a null id into unvalidated native paths
            Ffi.b3DestroyJoint(Id, wakeAttached);
            Id = default;
        }

        public bool Equals(Joint other)
        {
            return Id.Equals(other.Id);
        }

        public override bool Equals(object obj)
        {
            return obj is Joint other && Equals(other);
        }

        public override int GetHashCode()
        {
            return Id.GetHashCode();
        }
        public static bool operator ==(Joint left, Joint right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(Joint left, Joint right)
        {
            return !left.Equals(right);
        }

    }
}
