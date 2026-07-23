using UnityEngine;

namespace Box3D.Hybrid
{
    /// <summary>A prismatic (slider) joint: the body slides along <see cref="Axis"/> with no
    /// relative rotation. Optional translation limits and a motor.</summary>
    [Icon("Packages/com.suvitruf.box3d/Box3D.Hybrid.Editor/Icons/Box3DJoint.png")]
    [AddComponentMenu("Box3D/Joints/Slider Joint")]
    public class Box3DSliderJoint : Box3DJoint
    {
        [SerializeField, Tooltip("Local axis the body slides along.")]
        private Vector3 Axis = Vector3.right;

        [SerializeField, Tooltip("Limit the travel.")]
        private bool UseLimits;

        [SerializeField, Tooltip("Lower travel limit in meters.")]
        private float MinTranslation = -0.5f;

        [SerializeField, Tooltip("Upper travel limit in meters.")]
        private float MaxTranslation = 0.5f;

        [SerializeField, Tooltip("Drive the slider with a motor.")]
        private bool UseMotor;

        [SerializeField, Tooltip("Motor target speed in m/s.")]
        private float MotorSpeed = 1f;

        [SerializeField, Min(0f), Tooltip("Maximum motor force in newtons.")]
        private float MaxMotorForce = 100f;

        protected override Joint CreateJoint(BodyId bodyA, BodyId bodyB)
        {
            Vector3 worldAxis = transform.TransformDirection(Axis);

            PrismaticJointDef def = PrismaticJointDef.Default;
            SetupBase(ref def.Base, bodyA, bodyB, LocalSlideFrame(worldAxis));

            if (UseLimits)
            {
                def.EnableLimit = true;
                def.LowerTranslation = Mathf.Min(MinTranslation, MaxTranslation);
                def.UpperTranslation = Mathf.Max(MinTranslation, MaxTranslation);
            }
            if (UseMotor)
            {
                def.EnableMotor = true;
                def.MotorSpeed = MotorSpeed;
                def.MaxMotorForce = MaxMotorForce;
            }

            return World.World.CreatePrismaticJoint(def);
        }
    }
}
