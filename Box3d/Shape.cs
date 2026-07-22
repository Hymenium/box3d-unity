using System;

namespace Box3d
{
    using Sys;

    /// <summary>A collision shape attached to a body. Thin value wrapper over a shape id.</summary>
    public partial struct Shape : IEquatable<Shape>
    {
        public ShapeId Id;

        public bool IsValid => Ffi.b3Shape_IsValid(Id);

        public static Shape Wrap(ShapeId id)
        {
            if (id.IsNull)
            {
                throw new ArgumentException("Cannot wrap a null shape ID.", nameof(id));
            }

            Shape shape = new() { Id = id };

            if (!shape.IsValid)
            {
                throw new ArgumentException(
                    "Cannot wrap an invalid or stale shape ID.",
                    nameof(id));
            }

            return shape;
        }

        public static Shape WrapUnchecked(ShapeId id) => new() { Id = id };

        public Body GetBodyWrapper() => Body.WrapUnchecked(GetBody());

        public void Destroy(bool updateBodyMass = true)
        {
            if (Id.IsNull) return; // double-destroy would pass a null id into unvalidated native paths
            Ffi.b3DestroyShape(Id, updateBodyMass);
            Id = default;
        }

        /// <summary>Application-specific data attached to the shape.</summary>
        public unsafe IntPtr UserData
        {
            get => (IntPtr)Ffi.b3Shape_GetUserData(Id);
            set => Ffi.b3Shape_SetUserData(Id, (void*)value);
        }

        /// <summary>Replaces the sphere geometry of a sphere shape.</summary>
        public unsafe void SetSphere(in Sphere sphere)
        {
            Sphere local = sphere;
            Ffi.b3Shape_SetSphere(Id, &local);
        }

        /// <summary>Replaces the capsule geometry of a capsule shape.</summary>
        public unsafe void SetCapsule(in Capsule capsule)
        {
            Capsule local = capsule;
            Ffi.b3Shape_SetCapsule(Id, &local);
        }

        /// <summary>Snapshots every contact currently on this shape — the touching shapes and their
        /// manifold(s) (points, normal, separation, impulses), as of the last <c>World.Step</c>.
        ///
        /// <para>Each native b3ContactData reaches its manifolds through internal engine memory that
        /// may become invalid — that pointer must never be stored. This copies the manifold data into
        /// managed memory here, so every returned <see cref="ContactData"/> is a safe snapshot.
        /// Querying contacts needs no opt-in (unlike the contact <i>event</i> stream, which requires
        /// <c>ShapeDef.EnableContactEvents</c>).</para>
        ///
        /// <para>Because box3d uses speculative collision, some points may be separated (positive
        /// <see cref="ManifoldPoint.Separation"/>) — near but not yet touching. Filter on
        /// <c>Separation &lt;= 0</c> if you only want points in contact. (<see cref="Body.GetContacts"/>
        /// returns only touching contacts.)</para></summary>
        public unsafe ContactData[] GetContacts()
        {
            int capacity = Ffi.b3Shape_GetContactCapacity(Id);
            if (capacity == 0) return Array.Empty<ContactData>();

            Span<b3ContactData> buffer = capacity <= 32
                ? stackalloc b3ContactData[capacity]
                : new b3ContactData[capacity];
            int count;
            fixed (b3ContactData* p = buffer)
            {
                count = Ffi.b3Shape_GetContactData(Id, p, capacity);
            }

            var result = new ContactData[count];
            for (int i = 0; i < count; i++)
            {
                result[i] = ContactData.FromNative(buffer[i]);
            }
            return result;
        }

        public bool Equals(Shape other)
        {
            return Id.Equals(other.Id);
        }

        public override bool Equals(object obj)
        {
            return obj is Shape other && Equals(other);
        }

        public override int GetHashCode()
        {
            return Id.GetHashCode();
        }
        public static bool operator ==(Shape left, Shape right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(Shape left, Shape right)
        {
            return !left.Equals(right);
        }

    }
}
