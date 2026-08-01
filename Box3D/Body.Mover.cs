using System;
using System.Runtime.InteropServices;
using Unity.Mathematics;

namespace Box3D
{
    public partial struct Body
    {
        public unsafe int CollideMover(Span<BodyPlaneResult> planes, B3Pos origin, in Capsule mover, QueryFilter filter)
        {
            Capsule localMover = mover;
            fixed (BodyPlaneResult* buffer = planes)
            {
                return Ffi.b3Body_CollideMover(Id, buffer, planes.Length, origin, &localMover, filter, Transform);
            }
        }
    }
}