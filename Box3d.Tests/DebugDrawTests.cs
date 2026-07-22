using NUnit.Framework;
using Unity.Mathematics;

namespace Box3d.Tests
{
    public sealed class TestingDebugShapeFactory : IDebugShapeFactory
    {
        public bool WasCalled { get; private set; }

        public IDebugShape CreateSphere(in Sphere shape, in Shape source)
        {
            WasCalled = true;
            return null;
        }

        public IDebugShape CreateCapsule(in Capsule shape, in Shape source) => null;
        public IDebugShape CreateHull(HullView hull, in Shape source) => null;
        public IDebugShape CreateMesh(MeshView mesh, in Shape source) => null;
        public IDebugShape CreateHeightField(HeightFieldView heightField, in Shape source) => null;
        public IDebugShape CreateCompound(CompoundView compoundView, in Shape source) => null;
        public void DestroyShape(IDebugShape shape) { }
    }

    public sealed class TestingDebugDrawTarget : IDebugDrawTarget
    {
        public bool WasCalled { get; private set; }

        public bool DrawShape(IDebugShape shape, in B3Transform transform, uint color)
        {
            WasCalled = true;
            return true;
        }
        public void DrawSegment(float3 start, float3 end, uint color) { }
        public void DrawTransform(in B3Transform transform) { }
        public void DrawPoint(float3 position, float size, uint color) { }
        public void DrawSphere(float3 position, float radius, uint color, float alpha) { }
        public void DrawCapsule(float3 p1, float3 p2, float radius, uint color, float alpha) { }
        public void DrawBounds(in B3Aabb bounds, uint color) { }
        public void DrawBox(float3 extents, in B3Transform transform, uint color) { }
        public void DrawString(float3 p, string str, uint color) { }
    }

    /// <summary>Debug-draw bridge: native draw callbacks must reach the managed trampolines.</summary>
    public class DebugDrawTests
    {
        [Test]
        public void DrawDebug_InvokesBridgeCallbacks()
        {
            var shapeFactory = new TestingDebugShapeFactory();
            var drawTarget = new TestingDebugDrawTarget();
            World world = World.Create(WorldDef.Default, shapeFactory);

            BodyDef bodyDef = BodyDef.Default;
            bodyDef.Type = BodyType.Dynamic;
            bodyDef.Position = new float3(0f, 1f, 0f);
            Body body = world.CreateBody(bodyDef);
            body.CreateSphereShape(ShapeDef.Default, new Sphere { Radius = 0.5f });
            body.CreateCapsuleShape(ShapeDef.Default, new Capsule
            {
                Center1 = new float3(0f, 0.5f, 0f),
                Center2 = new float3(0f, 1f, 0f),
                Radius = 0.2f,
            });
            world.Step(1f / 60f);

            DebugDrawFlags flags = DebugDrawFlags.Shapes | DebugDrawFlags.Bounds;
            world.DrawDebug(drawTarget, flags);

            Assert.IsTrue(shapeFactory.WasCalled, "drawing a world with shapes should invoke the managed draw trampolines");

            world.Destroy();
        }
    }
}
