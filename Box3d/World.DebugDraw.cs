using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using AOT;
using Unity.Mathematics;
using Debug = UnityEngine.Debug;

namespace Box3d
{
    using Sys;

    public interface IDebugShape { }

    public readonly struct DebugShapeSource
    {
        public readonly Shape Owner;
        public readonly int ChildIndex;

        public DebugShapeSource(Shape owner, int childIndex = -1)
        {
            Owner = owner;
            ChildIndex = childIndex;
        }

        public bool IsCompoundChild => ChildIndex >= 0;
    }

    // Backends that return a non-null shape from any Create method
    // must also implement DrawShape and DestroyShape.
    public interface IDebugDrawBackend
    {
        // Buffered geometry
        IDebugShape CreateSphere(in Sphere sphere, in DebugShapeSource source);
        IDebugShape CreateCapsule(in Capsule shape, in DebugShapeSource source);

        // View are valid only during the call (data must be copied)
        IDebugShape CreateHull(HullView hull, in DebugShapeSource source);
        IDebugShape CreateMesh(MeshView mesh, in DebugShapeSource source);
        IDebugShape CreateHeightField(HeightFieldView heightField, in DebugShapeSource source);

        void DestroyShape(IDebugShape shape);

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

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public readonly struct HullHalfEdge
    {
        public readonly byte Next;
        public readonly byte Twin;
        public readonly byte Origin;
        public readonly byte Face;
    }

    // ReadOnlySpan<float3> points = hull.Points;
    // ReadOnlySpan<HullHalfEdge> edges = hull.HalfEdges;

    // for (int i = 0; i < edges.Length; ++i)
    // {
    //     HullHalfEdge edge = edges[i];

    //     if (i >= edge.Twin)
    //         continue;

    //     HullHalfEdge twin = edges[edge.Twin];

    //     AddLine(
    //         lines,
    //         points[edge.Origin],
    //         points[twin.Origin]);
    // }
    public readonly unsafe ref struct HullView
    {
        private readonly b3HullData* _data;

        public HullView(b3HullData* data)
        {
            _data = data;
        }

        public int PointCount => _data->VertexCount;
        public int HalfEdgeCount => _data->EdgeCount;
        public int FaceCount => _data->FaceCount;

        public ReadOnlySpan<float3> Points
        {
            get
            {
                byte* base_ptr = (byte*)_data;
                float3* points =
                    (float3*)(base_ptr + _data->PointOffset);

                return new ReadOnlySpan<float3>(
                    points,
                    _data->VertexCount);
            }
        }

        public ReadOnlySpan<HullHalfEdge> HalfEdges
        {
            get
            {
                byte* base_ptr = (byte*)_data;
                HullHalfEdge* edges =
                    (HullHalfEdge*)(base_ptr + _data->EdgeOffset);

                return new ReadOnlySpan<HullHalfEdge>(
                    edges,
                    _data->EdgeCount);
            }
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    public readonly struct MeshTriangle
    {
        public readonly int Index1;
        public readonly int Index2;
        public readonly int Index3;
    }

    // ReadOnlySpan<float3> vertices = mesh.Vertices;
    // ReadOnlySpan<MeshTriangle> triangles = mesh.Triangles;
    // float3 scale = mesh.Scale;

    // foreach (MeshTriangle triangle in triangles)
    // {
    //     float3 a = vertices[triangle.Index1] * scale;
    //     float3 b = vertices[triangle.Index2] * scale;
    //     float3 c = vertices[triangle.Index3] * scale;

    //     AddLine(a, b);
    //     AddLine(b, c);
    //     AddLine(c, a);
    // }
    public readonly unsafe ref struct MeshView
    {
        private readonly b3MeshData* _data;
        public float3 Scale { get; }

        public MeshView(b3MeshData* data, float3 scale)
        {
            _data = data;
            Scale = scale;
        }

        public MeshView(b3Mesh* mesh) : this(mesh->data, mesh->scale) { }

        public ReadOnlySpan<float3> Vertices
        {
            get
            {
                b3MeshData* data = _data;
                byte* base_ptr = (byte*)data;

                return new ReadOnlySpan<float3>(
                    (float3*)(base_ptr + data->vertexOffset),
                    data->vertexCount);
            }
        }

        public ReadOnlySpan<MeshTriangle> Triangles
        {
            get
            {
                b3MeshData* data = _data;
                byte* base_ptr = (byte*)data;

                return new ReadOnlySpan<MeshTriangle>(
                    (MeshTriangle*)(base_ptr + data->triangleOffset),
                    data->triangleCount);
            }
        }
    }

    public readonly unsafe ref struct HeightFieldView
    {
        private readonly b3HeightFieldData* _data;

        public HeightFieldView(b3HeightFieldData* data)
        {
            _data = data;
        }

        public int ColumnCount => _data->columnCount;
        public int RowCount => _data->rowCount;

        public int CellCount =>
            (ColumnCount - 1) * (RowCount - 1);

        public int TriangleCount =>
            2 * CellCount;

        public float MinHeight => _data->minHeight;
        public float MaxHeight => _data->maxHeight;
        public float HeightScale => _data->heightScale;

        public float3 Scale => _data->scale;

        public bool Clockwise => _data->clockwise;

        public ReadOnlySpan<ushort> CompressedHeights
        {
            get
            {
                byte* base_ptr = (byte*)_data;

                return new ReadOnlySpan<ushort>(
                    (ushort*)(base_ptr + _data->heightsOffset),
                    ColumnCount * RowCount);
            }
        }

        public ReadOnlySpan<byte> Materials
        {
            get
            {
                byte* base_ptr = (byte*)_data;

                return new ReadOnlySpan<byte>(
                    base_ptr + _data->materialOffset,
                    CellCount);
            }
        }

        public ReadOnlySpan<byte> TriangleFlags
        {
            get
            {
                byte* base_ptr = (byte*)_data;

                return new ReadOnlySpan<byte>(
                    base_ptr + _data->flagsOffset,
                    TriangleCount);
            }
        }

        public float GetHeight(int column, int row)
        {
            CheckGridPoint(column, row);

            int index = row * ColumnCount + column;
            ushort compressed = CompressedHeights[index];

            return MinHeight + HeightScale * compressed;
        }

        public float3 GetPoint(int column, int row)
        {
            float height = GetHeight(column, row);

            return new float3(
                column * Scale.x,
                height * Scale.y,
                row * Scale.z);
        }

        public byte GetMaterial(int column, int row)
        {
            CheckCell(column, row);

            int index = row * (ColumnCount - 1) + column;
            return Materials[index];
        }

        private void CheckGridPoint(int column, int row)
        {
            if ((uint)column >= (uint)ColumnCount)
                throw new ArgumentOutOfRangeException(nameof(column));

            if ((uint)row >= (uint)RowCount)
                throw new ArgumentOutOfRangeException(nameof(row));
        }

        public byte GetTriangleFlag(int column, int row, int triangle)
        {
            CheckCell(column, row);

            if ((uint)triangle >= 2u)
                throw new ArgumentOutOfRangeException(nameof(triangle));

            int cell_index =
                row * (ColumnCount - 1) + column;

            int triangle_index =
                2 * cell_index + triangle;

            return TriangleFlags[triangle_index];
        }

        private void CheckCell(int column, int row)
        {
            if ((uint)column >= (uint)(ColumnCount - 1))
                throw new ArgumentOutOfRangeException(nameof(column));

            if ((uint)row >= (uint)(RowCount - 1))
                throw new ArgumentOutOfRangeException(nameof(row));
        }
    }

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

    public unsafe partial struct World
    {
        /// <summary>Draws the world's debug visualization if IDebugDrawBackend 
        /// was loaded using <see cref="DebugDraw.SetBackend"/> before calling 
        /// <see cref="World.Create"/>. </summary>
        public void DrawDebug(DebugDrawFlags flags = DebugDrawFlags.Default, float drawRadius = 100f)
        {
            if (!NativeDebugDrawBridge.IsBridgeOwned(Id)) return;

            b3DebugDraw draw = NativeDebugDrawBridge.NativeDraw;
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
        }
    }

    public static class DebugDraw
    {
        public static void SetBackend(IDebugDrawBackend backend)
        {
            NativeDebugDrawBridge.SetBackend(backend);
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

        private static readonly IntPtr CREATE_SHAPE_PTR = Marshal.GetFunctionPointerForDelegate(CREATE_SHAPE_INSTANCE);
        private static readonly IntPtr DESTROY_SHAPE_PTR = Marshal.GetFunctionPointerForDelegate(DESTROY_SHAPE_INSTANCE);
        private static readonly IntPtr DRAW_SHAPE_PTR = Marshal.GetFunctionPointerForDelegate(DRAW_SHAPE_INSTANCE);
        private static readonly IntPtr DRAW_SEGMENT_PTR = Marshal.GetFunctionPointerForDelegate(DRAW_SEGMENT_INSTANCE);
        private static readonly IntPtr DRAW_TRANSFORM_PTR = Marshal.GetFunctionPointerForDelegate(DRAW_TRANSFORM_INSTANCE);
        private static readonly IntPtr DRAW_POINT_PTR = Marshal.GetFunctionPointerForDelegate(DRAW_POINT_INSTANCE);
        private static readonly IntPtr DRAW_SPHERE_PTR = Marshal.GetFunctionPointerForDelegate(DRAW_SPHERE_INSTANCE);
        private static readonly IntPtr DRAW_CAPSULE_PTR = Marshal.GetFunctionPointerForDelegate(DRAW_CAPSULE_INSTANCE);
        private static readonly IntPtr DRAW_BOUNDS_PTR = Marshal.GetFunctionPointerForDelegate(DRAW_BOUNDS_INSTANCE);
        private static readonly IntPtr DRAW_BOX_PTR = Marshal.GetFunctionPointerForDelegate(DRAW_BOX_INSTANCE);
        private static readonly IntPtr DRAW_STRING_PTR = Marshal.GetFunctionPointerForDelegate(DRAW_STRING_INSTANCE);

        // managed object
        private static IDebugDrawBackend _backend;
        private static GCHandle _backendHandle;
        private static readonly bool[] BridgeOwned = new bool[Ffi.B3_MAX_WORLDS + 1];

        internal static b3DebugDraw NativeDraw;


        internal static bool IsConfigured => _backend is not null;

        internal static void SetBridgeOwned(WorldId id, bool owned)
        {
            BridgeOwned[id.Index1] = owned;
        }

        internal static bool IsBridgeOwned(WorldId id)
        {
            return BridgeOwned[id.Index1];
        }

        internal static void SetBackend(IDebugDrawBackend backend)
        {
            if (_backendHandle.IsAllocated)
            {
                throw new InvalidOperationException(
                    "The debug-draw backend has already been configured.");
            }

            _backend = backend ?? throw new ArgumentNullException(nameof(backend));
            _backendHandle = GCHandle.Alloc(_backend);

            NativeDraw = Ffi.b3DefaultDebugDraw();
            NativeDraw.context =
                (void*)GCHandle.ToIntPtr(_backendHandle);

            NativeDraw.DrawShapeFcn = DRAW_SHAPE_PTR;
            NativeDraw.DrawSegmentFcn = DRAW_SEGMENT_PTR;
            NativeDraw.DrawTransformFcn = DRAW_TRANSFORM_PTR;
            NativeDraw.DrawPointFcn = DRAW_POINT_PTR;
            NativeDraw.DrawSphereFcn = DRAW_SPHERE_PTR;
            NativeDraw.DrawCapsuleFcn = DRAW_CAPSULE_PTR;
            NativeDraw.DrawBoundsFcn = DRAW_BOUNDS_PTR;
            NativeDraw.DrawBoxFcn = DRAW_BOX_PTR;
            NativeDraw.DrawStringFcn = DRAW_STRING_PTR;
        }

        internal static void ConfigureWorldDef(ref WorldDef def)
        {
            def.CreateDebugShape = CREATE_SHAPE_PTR;
            def.DestroyDebugShape = DESTROY_SHAPE_PTR;
            def.UserDebugShapeContext = GCHandle.ToIntPtr(_backendHandle);
        }

        internal static void ConfigureRecPlayer(b3RecPlayer* recPlayer)
        {
            Ffi.b3RecPlayer_SetDebugShapeCallbacks(recPlayer,
                CREATE_SHAPE_PTR,
                DESTROY_SHAPE_PTR,
                (void*)GCHandle.ToIntPtr(_backendHandle));
        }

        private static IDebugDrawBackend GetBackend(void* context)
        {
            if (context == null)
            {
                throw new InvalidOperationException(
                    "The debug-draw context pointer is null.");
            }

            GCHandle handle =
                GCHandle.FromIntPtr((IntPtr)context);

            if (handle.Target is not IDebugDrawBackend backend)
            {
                throw new InvalidOperationException(
                    "The debug-draw context does not contain an IDebugDrawBackend.");
            }

            return backend;
        }

        private sealed class CompoundDebugShape : IDebugShape
        {
            public CompoundDebugShape(CompoundChild[] children)
            {
                Children = children;
            }

            public CompoundChild[] Children { get; }
        }

        private readonly struct CompoundChild
        {
            public readonly IDebugShape Shape;
            public readonly B3Transform Transform;

            public CompoundChild(IDebugShape shape, B3Transform transform)
            {
                Shape = shape;
                Transform = transform;
            }
        }

        private static B3Transform Multiply(in B3Transform a, in B3Transform b)
        {
            return new B3Transform
            {
                Position = a.Position + math.rotate(a.Rotation, b.Position),
                Rotation = math.mul(a.Rotation, b.Rotation),
            };
        }

        private static unsafe void AddSpheres(
            IDebugDrawBackend backend, b3CompoundData* compound,
            List<CompoundChild> children, in Shape owner)
        {
            byte* base_ptr = (byte*)compound;

            b3CompoundSphere* spheres =
                (b3CompoundSphere*)(base_ptr + compound->sphereOffset);

            for (int i = 0; i < compound->sphereCount; ++i)
            {
                DebugShapeSource source = new(owner, i);
                b3CompoundSphere* instance = spheres + i;
                IDebugShape debug_shape = backend.CreateSphere(in instance->sphere, source);

                if (debug_shape == null) continue;

                children.Add(new CompoundChild(debug_shape, B3Transform.Identity));
            }
        }

        private static unsafe void AddCapsules(
            IDebugDrawBackend backend, b3CompoundData* compound,
            List<CompoundChild> children, in Shape owner)
        {
            byte* base_ptr = (byte*)compound;

            b3CompoundCapsule* capsules =
                (b3CompoundCapsule*)(base_ptr + compound->capsuleOffset);

            for (int i = 0; i < compound->capsuleCount; ++i)
            {
                DebugShapeSource source = new(owner, i);
                b3CompoundCapsule* instance = capsules + i;
                IDebugShape debug_shape = backend.CreateCapsule(in instance->capsule, source);

                if (debug_shape == null) continue;

                children.Add(new CompoundChild(debug_shape, B3Transform.Identity));
            }
        }

        private static unsafe void AddHulls(
            IDebugDrawBackend backend, b3CompoundData* compound,
            List<CompoundChild> children, in Shape owner)
        {
            byte* base_ptr = (byte*)compound;

            b3CompoundHull* hulls =
                (b3CompoundHull*)(base_ptr + compound->hullOffset);

            for (int i = 0; i < compound->hullCount; ++i)
            {
                DebugShapeSource source = new(owner, i);
                b3CompoundHull* instance = hulls + i;
                IDebugShape debug_shape = backend.CreateHull(new HullView(instance->hull), source);

                if (debug_shape == null) continue;

                children.Add(new CompoundChild(debug_shape, B3Transform.Identity));
            }
        }

        private static unsafe void AddMeshes(
            IDebugDrawBackend backend, b3CompoundData* compound,
            List<CompoundChild> children, in Shape owner)
        {
            byte* base_ptr = (byte*)compound;

            b3CompoundMesh* meshes = (b3CompoundMesh*)(base_ptr + compound->meshOffset);

            for (int i = 0; i < compound->meshCount; ++i)
            {
                DebugShapeSource source = new(owner, i);
                b3CompoundMesh* instance = meshes + i;

                IDebugShape debug_shape =
                    backend.CreateMesh(new MeshView(instance->meshData, instance->scale), source);

                if (debug_shape == null) continue;

                children.Add(new CompoundChild(
                    debug_shape,
                    instance->transform));
            }
        }

        private static void DestroyCompoundChildren(IDebugDrawBackend backend, List<CompoundChild> children)
        {
            foreach (var child in children)
            {
                try
                {
                    backend.DestroyShape(child.Shape);
                }
                catch (Exception destroyException)
                {
                    Debug.LogException(destroyException);
                }
            }
        }

        private static IDebugShape CreateCompound(IDebugDrawBackend backend, b3CompoundData* compound, in Shape owner)
        {
            var children = new List<CompoundChild>(
                compound->sphereCount +
                compound->capsuleCount +
                compound->hullCount +
                compound->meshCount);

            try
            {
                AddSpheres(backend, compound, children, owner);
                AddCapsules(backend, compound, children, owner);
                AddHulls(backend, compound, children, owner);
                AddMeshes(backend, compound, children, owner);

                return children.Count == 0
                    ? null
                    : new CompoundDebugShape(children.ToArray());
            }
            catch
            {
                DestroyCompoundChildren(backend, children);
                throw;
            }
        }

        private static void DestroyCompound(IDebugDrawBackend backend, CompoundDebugShape compound)
        {
            foreach (var child in compound.Children)
            {
                try
                {
                    backend.DestroyShape(child.Shape);
                }
                catch (Exception destroyException)
                {
                    Debug.LogException(destroyException);
                }
            }
        }

        private static bool DrawCompound(IDebugDrawBackend backend, CompoundDebugShape compound,
            B3Transform compound_transform, uint color)
        {
            foreach (var child in compound.Children)
            {
                B3Transform child_transform = Multiply(compound_transform, child.Transform);

                if (!backend.DrawShape(child.Shape, in child_transform, color))
                {
                    return false;
                }
            }
            return true;
        }

        [MonoPInvokeCallback(typeof(CreateShapeDelegate))]
        private static void* CreateShape(b3DebugShape* debugShape, void* userContext)
        {
            try
            {
                if (debugShape == null) return null;

                IDebugDrawBackend backend = GetBackend(userContext);
                Shape owner = Shape.WrapUnchecked(debugShape->shapeId);
                DebugShapeSource source = new(owner);
                IDebugShape u_shape = debugShape->type switch
                {
                    ShapeType.Sphere => backend.CreateSphere(*debugShape->sphere, source),
                    ShapeType.Capsule => backend.CreateCapsule(*debugShape->capsule, source),
                    ShapeType.Hull => backend.CreateHull(new HullView(debugShape->hull), source),
                    ShapeType.Compound => CreateCompound(backend, debugShape->compound, owner),
                    ShapeType.Mesh => backend.CreateMesh(new MeshView(debugShape->mesh), source),
                    ShapeType.HeightField => backend.CreateHeightField(
                            new HeightFieldView(debugShape->heightField), source
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
                    IDebugDrawBackend backend = GetBackend(userContext);
                    IDebugShape shape = GetShape(userShape);

                    if (shape is CompoundDebugShape compound)
                    {
                        DestroyCompound(backend, compound);
                    }
                    else
                    {
                        backend.DestroyShape(shape);
                    }
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
            try
            {
                IDebugDrawBackend backend = GetBackend(context);
                IDebugShape shape = GetShape(userShape);

                if (shape is CompoundDebugShape compound)
                {
                    return DrawCompound(backend, compound, transform, color);
                }
                else
                {
                    return backend.DrawShape(shape, transform, color);
                }
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                return false;
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
                IDebugDrawBackend backend = GetBackend(context);

                backend.DrawSegment(start, end, color);
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
                IDebugDrawBackend backend = GetBackend(context);

                backend.DrawTransform(transform);
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
                IDebugDrawBackend backend = GetBackend(context);

                backend.DrawPoint(p, size, color);
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
                IDebugDrawBackend backend = GetBackend(context);
                backend.DrawSphere(p, radius, color, alpha);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
        }

        [MonoPInvokeCallback(typeof(DrawCapsuleDelegate))]
        private static void DrawCapsule(float3 p1, float3 p2, float radius, uint color, float alpha, void* context)
        {
            try
            {
                IDebugDrawBackend backend = GetBackend(context);
                backend.DrawCapsule(p1, p2, radius, color, alpha);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
        }

        [MonoPInvokeCallback(typeof(DrawBoundsDelegate))]
        private static void DrawBounds(B3Aabb aabb, uint color, void* context)
        {
            try
            {
                IDebugDrawBackend backend = GetBackend(context);
                backend.DrawBounds(aabb, color);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
        }

        [MonoPInvokeCallback(typeof(DrawBoxDelegate))]
        private static void DrawBox(float3 extents, B3Transform transform, uint color, void* context)
        {
            try
            {
                IDebugDrawBackend backend = GetBackend(context);
                backend.DrawBox(extents, in transform, color);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
        }

        [MonoPInvokeCallback(typeof(DrawStringDelegate))]
        private static void DrawString(float3 p, sbyte* s, uint color, void* context)
        {
            try
            {
                string str = Marshal.PtrToStringUTF8((IntPtr)s) ?? string.Empty;
                IDebugDrawBackend backend = GetBackend(context);
                backend.DrawString(p, str, color);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
        }

    }

}