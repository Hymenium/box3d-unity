using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using AOT;
using Unity.Mathematics;
using Debug = UnityEngine.Debug;

namespace Box3d
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
        bool DrawShape(IDebugShape shape, in B3Transform transform, uint color);

        // Immediate geometry
        void DrawSegment(float3 start, float3 end, uint color);
        void DrawTransform(in B3Transform transform);
        void DrawPoint(float3 position, float size, uint color);
        void DrawSphere(float3 position, float radius, uint color, float alpha);
        void DrawCapsule(float3 p1, float3 p2, float radius, uint color, float alpha);
        void DrawBounds(in B3Aabb bounds, uint color);
        void DrawBox(float3 extents, in B3Transform transform, uint color);
        void DrawString(float3 p, string str, uint color);
    }

    public unsafe partial struct World
    {
        // TODO: Add documentation
        public void DrawDebug(IDebugDrawTarget target, DebugDrawFlags flags = DebugDrawFlags.Default, float drawRadius = 100f)
        {
            if (target == null)
            {
                Debug.LogError("No IDebugDrawTarget provided.");
                return;
            }

            b3DebugDraw draw = NativeDebugDrawBridge.CreateNativeDraw();

            var handle = GCHandle.Alloc(target);
            draw.context = (void*)GCHandle.ToIntPtr(handle);

            draw.drawingBounds = new B3Aabb
            {
                LowerBound = new float3(-drawRadius, -drawRadius, -drawRadius),
                UpperBound = new float3(drawRadius, drawRadius, drawRadius),
            };
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
        private unsafe delegate void* CreateShapeDelegate(b3DebugShape* debugShape, void* userContext);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private unsafe delegate void DestroyShapeDelegate(void* userShape, void* userContext);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private unsafe delegate NativeBool DrawShapeDelegate(
            void* userShape,
            B3Transform transform,
            uint color,
            void* context);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private unsafe delegate void DrawSegmentDelegate(
            float3 start,
            float3 end,
            uint color,
            void* context);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private unsafe delegate void DrawTransformDelegate(
            B3Transform transform,
            void* context);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private unsafe delegate void DrawPointDelegate(
            float3 p,
            float size,
            uint color,
            void* context);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private unsafe delegate void DrawSphereDelegate(
            float3 p,
            float radius,
            uint color,
            float alpha,
            void* context);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private unsafe delegate void DrawCapsuleDelegate(
            float3 p1,
            float3 p2,
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
            B3Transform transform,
            uint color,
            void* context);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private unsafe delegate void DrawStringDelegate(
            float3 p,
            sbyte* s,
            uint color,
            void* context);

        private static readonly CreateShapeDelegate CREATE_SHAPE_INSTANCE = CreateShape;
        private static readonly DestroyShapeDelegate DESTROY_SHAPE_INSTANCE = DestroyShape;
        private static readonly DrawShapeDelegate DRAW_SHAPE_INSTANCE = DrawShape;
        private static readonly DrawSegmentDelegate DRAW_SEGMENT_INSTANCE = DrawSegment;
        private static readonly DrawTransformDelegate DRAW_TRANSFORM_INSTANCE = DrawTransform;
        private static readonly DrawPointDelegate DRAW_POINT_INSTANCE = DrawPoint;
        private static readonly DrawSphereDelegate DRAW_SPHERE_INSTANCE = DrawSphere;
        private static readonly DrawCapsuleDelegate DRAW_CAPSULE_INSTANCE = DrawCapsule;
        private static readonly DrawBoundsDelegate DRAW_BOUNDS_INSTANCE = DrawBounds;
        private static readonly DrawBoxDelegate DRAW_BOX_INSTANCE = DrawBox;
        private static readonly DrawStringDelegate DRAW_STRING_INSTANCE = DrawString;

        internal static readonly IntPtr CREATE_SHAPE_PTR = Marshal.GetFunctionPointerForDelegate(CREATE_SHAPE_INSTANCE);
        internal static readonly IntPtr DESTROY_SHAPE_PTR = Marshal.GetFunctionPointerForDelegate(DESTROY_SHAPE_INSTANCE);
        private static readonly IntPtr DRAW_SHAPE_PTR = Marshal.GetFunctionPointerForDelegate(DRAW_SHAPE_INSTANCE);
        private static readonly IntPtr DRAW_SEGMENT_PTR = Marshal.GetFunctionPointerForDelegate(DRAW_SEGMENT_INSTANCE);
        private static readonly IntPtr DRAW_TRANSFORM_PTR = Marshal.GetFunctionPointerForDelegate(DRAW_TRANSFORM_INSTANCE);
        private static readonly IntPtr DRAW_POINT_PTR = Marshal.GetFunctionPointerForDelegate(DRAW_POINT_INSTANCE);
        private static readonly IntPtr DRAW_SPHERE_PTR = Marshal.GetFunctionPointerForDelegate(DRAW_SPHERE_INSTANCE);
        private static readonly IntPtr DRAW_CAPSULE_PTR = Marshal.GetFunctionPointerForDelegate(DRAW_CAPSULE_INSTANCE);
        private static readonly IntPtr DRAW_BOUNDS_PTR = Marshal.GetFunctionPointerForDelegate(DRAW_BOUNDS_INSTANCE);
        private static readonly IntPtr DRAW_BOX_PTR = Marshal.GetFunctionPointerForDelegate(DRAW_BOX_INSTANCE);
        private static readonly IntPtr DRAW_STRING_PTR = Marshal.GetFunctionPointerForDelegate(DRAW_STRING_INSTANCE);

        internal static b3DebugDraw CreateNativeDraw()
        {
            var native_draw = Ffi.b3DefaultDebugDraw();

            native_draw.DrawShapeFcn = DRAW_SHAPE_PTR;
            native_draw.DrawSegmentFcn = DRAW_SEGMENT_PTR;
            native_draw.DrawTransformFcn = DRAW_TRANSFORM_PTR;
            native_draw.DrawPointFcn = DRAW_POINT_PTR;
            native_draw.DrawSphereFcn = DRAW_SPHERE_PTR;
            native_draw.DrawCapsuleFcn = DRAW_CAPSULE_PTR;
            native_draw.DrawBoundsFcn = DRAW_BOUNDS_PTR;
            native_draw.DrawBoxFcn = DRAW_BOX_PTR;
            native_draw.DrawStringFcn = DRAW_STRING_PTR;

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
                Shape owner = Shape.WrapUnchecked(debugShape->shapeId);
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
            B3Transform transform,
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
            float3 start,
            float3 end,
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
            B3Transform transform,
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
            float3 p,
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
        private static void DrawSphere(float3 p, float radius, uint color, float alpha, void* context)
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
        private static void DrawCapsule(float3 p1, float3 p2, float radius, uint color, float alpha, void* context)
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
        private static void DrawBox(float3 extents, B3Transform transform, uint color, void* context)
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
        private static void DrawString(float3 p, sbyte* s, uint color, void* context)
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