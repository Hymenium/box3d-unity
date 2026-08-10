using System;
using System.Runtime.InteropServices;
using AOT;
using Unity.Mathematics;
using Debug = UnityEngine.Debug;

namespace Box3D
{
    using Sys;

    /// <summary>What <see cref="World.DrawDebug"/> visualizes.</summary>
    [Flags]
    public enum DebugDrawFlags
    {
        None = 0,
        Shapes = 1 << 0,
        Joints = 1 << 1,
        JointExtras = 1 << 2,
        Bounds = 1 << 3,
        Mass = 1 << 4,
        Contacts = 1 << 5,
        ContactNormals = 1 << 6,
        ContactForces = 1 << 7,
        FrictionForces = 1 << 8,
        Islands = 1 << 9,
        GraphColors = 1 << 10,
        Default = Shapes | Joints,
    }

    public interface IDebugShape { }

    // Backends that return a non-null shape from any Create method
    // must also implement DrawShape and DestroyShape.
    public interface IDebugShapeFactory
    {
        // Buffered geometry
        IDebugShape CreateSphere(in Sphere sphere, in Shape source);
        IDebugShape CreateCapsule(in Capsule shape, in Shape source);

        // View are valid only during the call (data must be copied)
        IDebugShape CreateHull(HullView hull, in Shape source);
        IDebugShape CreateMesh(MeshView mesh, in Shape source);
        IDebugShape CreateHeightField(HeightFieldView heightField, in Shape source);

        IDebugShape CreateCompound(CompoundView compound, in Shape source);

        void DestroyShape(IDebugShape shape);
    }

    public interface IDebugDrawTarget
    {
        // Return true if drawing should continue
        bool DrawShape(IDebugShape shape, in B3WorldTransform transform, uint color);

        // Immediate geometry
        void DrawSegment(B3Pos start, B3Pos end, uint color);
        void DrawTransform(in B3WorldTransform transform);
        void DrawPoint(B3Pos p, float size, uint color);
        void DrawSphere(B3Pos p, float radius, uint color, float alpha);
        void DrawCapsule(B3Pos p1, B3Pos p2, float radius, uint color, float alpha);
        void DrawBounds(in B3Aabb bounds, uint color);
        void DrawBox(float3 extents, in B3WorldTransform transform, uint color);
        void DrawString(B3Pos p, in string str, uint color);
    }

    public unsafe partial struct World
    {
        // TODO: Add documentation
        public readonly void DrawDebug(IDebugDrawTarget target, DebugDrawFlags flags = DebugDrawFlags.Default, float drawRadius = 100f)
        {
            var bounds = new B3Aabb
            {
                LowerBound = new(-drawRadius, -drawRadius, -drawRadius),
                UpperBound = new(drawRadius, drawRadius, drawRadius),
            };
            DrawDebug(target, bounds, flags);
        }

        public readonly void DrawDebug(IDebugDrawTarget target, in B3Aabb drawBounds, DebugDrawFlags flags = DebugDrawFlags.Default)
        {
            if (target == null)
            {
                Debug.LogError("No IDebugDrawTarget provided.");
                return;
            }

            b3DebugDraw draw = NativeDebugDrawBridge.CreateNativeDraw();

            var handle = GCHandle.Alloc(target);
            draw.context = (void*)GCHandle.ToIntPtr(handle);

            draw.drawingBounds = drawBounds;

            draw.drawShapes = (flags & DebugDrawFlags.Shapes) != 0;
            draw.drawJoints = (flags & DebugDrawFlags.Joints) != 0;
            draw.drawJointExtras = (flags & DebugDrawFlags.JointExtras) != 0;
            draw.drawBounds = (flags & DebugDrawFlags.Bounds) != 0;
            draw.drawMass = (flags & DebugDrawFlags.Mass) != 0;
            draw.drawContacts = (flags & DebugDrawFlags.Contacts) != 0;
            draw.drawContactNormals = (flags & DebugDrawFlags.ContactNormals) != 0;
            draw.drawContactForces = (flags & DebugDrawFlags.ContactForces) != 0;
            draw.drawFrictionForces = (flags & DebugDrawFlags.FrictionForces) != 0;
            draw.drawIslands = (flags & DebugDrawFlags.Islands) != 0;
            draw.drawGraphColors = (flags & DebugDrawFlags.GraphColors) != 0;

            // DebugDrawBridge.DrawCallCount = 0;
            Ffi.b3World_Draw(Id, &draw, ulong.MaxValue);

            handle.Free();
        }
    }

    internal unsafe static class NativeDebugDrawBridge
    {
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private unsafe delegate void* CreateShapeDelegate(
            b3DebugShape* debugShape,
            void* userContext);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private unsafe delegate void DestroyShapeDelegate(
            void* userShape,
            void* userContext);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private unsafe delegate NativeBool DrawShapeDelegate(
            void* userShape,
            B3WorldTransform transform,
            uint color,
            void* context);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private unsafe delegate void DrawSegmentDelegate(
            B3Pos start,
            B3Pos end,
            uint color,
            void* context);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private unsafe delegate void DrawTransformDelegate(
            B3WorldTransform transform,
            void* context);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private unsafe delegate void DrawPointDelegate(
            B3Pos p,
            float size,
            uint color,
            void* context);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private unsafe delegate void DrawSphereDelegate(
            B3Pos p,
            float radius,
            uint color,
            float alpha,
            void* context);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private unsafe delegate void DrawCapsuleDelegate(
            B3Pos p1,
            B3Pos p2,
            float radius,
            uint color,
            float alpha,
            void* context);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private unsafe delegate void DrawBoundsDelegate(
            B3Aabb aabb,
            uint color,
            void* context);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private unsafe delegate void DrawBoxDelegate(
            float3 extents,
            B3WorldTransform transform,
            uint color,
            void* context);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private unsafe delegate void DrawStringDelegate(
            B3Pos p,
            sbyte* s,
            uint color,
            void* context);

        private static readonly CreateShapeDelegate _createShapeInstance = CreateShape;
        private static readonly DestroyShapeDelegate _destroyShapeInstance = DestroyShape;
        private static readonly DrawShapeDelegate _drawShapeInstance = DrawShape;
        private static readonly DrawSegmentDelegate _drawSegmentInstance = DrawSegment;
        private static readonly DrawTransformDelegate _drawTransformInstance = DrawTransform;
        private static readonly DrawPointDelegate _drawPointInstance = DrawPoint;
        private static readonly DrawSphereDelegate _drawSphereInstance = DrawSphere;
        private static readonly DrawCapsuleDelegate _drawCapsuleInstance = DrawCapsule;
        private static readonly DrawBoundsDelegate _drawBoundsInstance = DrawBounds;
        private static readonly DrawBoxDelegate _drawBoxInstance = DrawBox;
        private static readonly DrawStringDelegate _drawStringInstance = DrawString;

        internal static readonly IntPtr CreateShapePtr = Marshal.GetFunctionPointerForDelegate(_createShapeInstance);
        internal static readonly IntPtr DestroyShapePtr = Marshal.GetFunctionPointerForDelegate(_destroyShapeInstance);
        private static readonly IntPtr _drawShapePtr = Marshal.GetFunctionPointerForDelegate(_drawShapeInstance);
        private static readonly IntPtr _drawSegmentPtr = Marshal.GetFunctionPointerForDelegate(_drawSegmentInstance);
        private static readonly IntPtr _drawTransformPtr = Marshal.GetFunctionPointerForDelegate(_drawTransformInstance);
        private static readonly IntPtr _drawPointPtr = Marshal.GetFunctionPointerForDelegate(_drawPointInstance);
        private static readonly IntPtr _drawSpherePtr = Marshal.GetFunctionPointerForDelegate(_drawSphereInstance);
        private static readonly IntPtr _drawCapsulePtr = Marshal.GetFunctionPointerForDelegate(_drawCapsuleInstance);
        private static readonly IntPtr _drawBoundsPtr = Marshal.GetFunctionPointerForDelegate(_drawBoundsInstance);
        private static readonly IntPtr _drawBoxPtr = Marshal.GetFunctionPointerForDelegate(_drawBoxInstance);
        private static readonly IntPtr _drawStringPtr = Marshal.GetFunctionPointerForDelegate(_drawStringInstance);

        internal static b3DebugDraw CreateNativeDraw()
        {
            var native_draw = Ffi.b3DefaultDebugDraw();

            native_draw.DrawShapeFcn = _drawShapePtr;
            native_draw.DrawSegmentFcn = _drawSegmentPtr;
            native_draw.DrawTransformFcn = _drawTransformPtr;
            native_draw.DrawPointFcn = _drawPointPtr;
            native_draw.DrawSphereFcn = _drawSpherePtr;
            native_draw.DrawCapsuleFcn = _drawCapsulePtr;
            native_draw.DrawBoundsFcn = _drawBoundsPtr;
            native_draw.DrawBoxFcn = _drawBoxPtr;
            native_draw.DrawStringFcn = _drawStringPtr;

            return native_draw;
        }

        private static IDebugShape GetShape(void* userShape)
        {
            if (userShape == null)
            {
                throw new InvalidOperationException(
                    "The buffered debug shape pointer is null.");
            }

            GCHandle handle =
                GCHandle.FromIntPtr((IntPtr)userShape);

            if (handle.Target is not IDebugShape shape)
            {
                throw new InvalidOperationException(
                    "The buffered debug shape has already been released.");
            }

            return shape;
        }

        private static IDebugShapeFactory GetFactory(void* context)
        {
            if (context == null)
            {
                throw new InvalidOperationException(
                    "The debug-draw context pointer is null.");
            }

            GCHandle handle =
                GCHandle.FromIntPtr((IntPtr)context);

            if (handle.Target is not IDebugShapeFactory factory)
            {
                throw new InvalidOperationException(
                    "The debug-draw context does not contain an IDebugShapeFactory.");
            }

            return factory;
        }

        private static IDebugDrawTarget GetDrawTarget(void* context)
        {
            if (context == null)
            {
                throw new InvalidOperationException(
                    "The debug-draw context pointer is null.");
            }

            GCHandle handle =
                GCHandle.FromIntPtr((IntPtr)context);

            if (handle.Target is not IDebugDrawTarget target)
            {
                throw new InvalidOperationException(
                    "The debug-draw context does not contain an IDebugDrawTarget.");
            }

            return target;
        }

        [MonoPInvokeCallback(typeof(CreateShapeDelegate))]
        private static void* CreateShape(b3DebugShape* debugShape, void* userContext)
        {
            try
            {
                if (debugShape == null) return null;

                IDebugShapeFactory factory = GetFactory(userContext);
                Shape owner = new(debugShape->shapeId);
                IDebugShape u_shape = debugShape->type switch
                {
                    ShapeType.Sphere => factory.CreateSphere(*debugShape->sphere, owner),
                    ShapeType.Capsule => factory.CreateCapsule(*debugShape->capsule, owner),
                    ShapeType.Hull => factory.CreateHull(new HullView(debugShape->hull), owner),
                    ShapeType.Compound => factory.CreateCompound(new CompoundView(debugShape->compound), owner),
                    ShapeType.Mesh => factory.CreateMesh(new MeshView(debugShape->mesh->data, debugShape->mesh->scale), owner),
                    ShapeType.HeightField => factory.CreateHeightField(
                            new HeightFieldView(debugShape->heightField), owner
                        ),
                    _ => throw new InvalidOperationException($"Unknown debug shape type: {debugShape->type}"),
                };

                // If the shape is null, return null
                if (u_shape == null) return null;

                GCHandle shapeHandle = GCHandle.Alloc(u_shape);
                return (void*)GCHandle.ToIntPtr(shapeHandle);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                return null;
            }
        }

        [MonoPInvokeCallback(typeof(DestroyShapeDelegate))]
        private static void DestroyShape(void* userShape, void* userContext)
        {
            try
            {
                if (userShape == null)
                {
                    return;
                }

                try
                {
                    IDebugShapeFactory factory = GetFactory(userContext);
                    IDebugShape shape = GetShape(userShape);

                    factory.DestroyShape(shape);
                }
                finally
                {
                    GCHandle.FromIntPtr((IntPtr)userShape).Free();
                }
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }
        }

        [MonoPInvokeCallback(typeof(DrawShapeDelegate))]
        private static NativeBool DrawShape(
            void* userShape,
            B3WorldTransform transform,
            uint color,
            void* context)
        {
            if (userShape == null) return true;

            try
            {
                IDebugDrawTarget target = GetDrawTarget(context);
                IDebugShape shape = GetShape(userShape);

                target.DrawShape(shape, transform, color);
                return true;
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                return true;
            }
        }

        [MonoPInvokeCallback(typeof(DrawSegmentDelegate))]
        private static void DrawSegment(
            B3Pos start,
            B3Pos end,
            uint color,
            void* context)
        {
            try
            {
                IDebugDrawTarget target = GetDrawTarget(context);

                target.DrawSegment(start, end, color);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }
        }

        [MonoPInvokeCallback(typeof(DrawTransformDelegate))]
        private static void DrawTransform(
            B3WorldTransform transform,
            void* context)
        {
            try
            {
                IDebugDrawTarget target = GetDrawTarget(context);

                target.DrawTransform(transform);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }
        }

        [MonoPInvokeCallback(typeof(DrawPointDelegate))]
        private static void DrawPoint(
            B3Pos p,
            float size,
            uint color,
            void* context)
        {
            try
            {
                IDebugDrawTarget target = GetDrawTarget(context);

                target.DrawPoint(p, size, color);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }
        }

        [MonoPInvokeCallback(typeof(DrawSphereDelegate))]
        private static void DrawSphere(B3Pos p, float radius, uint color, float alpha, void* context)
        {
            try
            {
                IDebugDrawTarget target = GetDrawTarget(context);
                target.DrawSphere(p, radius, color, alpha);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }
        }

        [MonoPInvokeCallback(typeof(DrawCapsuleDelegate))]
        private static void DrawCapsule(B3Pos p1, B3Pos p2, float radius, uint color, float alpha, void* context)
        {
            try
            {
                IDebugDrawTarget target = GetDrawTarget(context);
                target.DrawCapsule(p1, p2, radius, color, alpha);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }
        }

        [MonoPInvokeCallback(typeof(DrawBoundsDelegate))]
        private static void DrawBounds(B3Aabb aabb, uint color, void* context)
        {
            try
            {
                IDebugDrawTarget target = GetDrawTarget(context);
                target.DrawBounds(aabb, color);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }
        }

        [MonoPInvokeCallback(typeof(DrawBoxDelegate))]
        private static void DrawBox(float3 extents, B3WorldTransform transform, uint color, void* context)
        {
            try
            {
                IDebugDrawTarget target = GetDrawTarget(context);
                target.DrawBox(extents, transform, color);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }
        }

        [MonoPInvokeCallback(typeof(DrawStringDelegate))]
        private static void DrawString(B3Pos p, sbyte* s, uint color, void* context)
        {
            try
            {
                string str = Marshal.PtrToStringUTF8((IntPtr)s) ?? string.Empty;
                IDebugDrawTarget target = GetDrawTarget(context);
                target.DrawString(p, str, color);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }
        }
    }
}