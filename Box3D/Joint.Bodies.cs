namespace Box3D
{
    public partial struct Joint
    {
        /// <summary>The first body this joint connects.</summary>
        public Body BodyA => new(Ffi.b3Joint_GetBodyA(Id));

        /// <summary>The second body this joint connects.</summary>
        public Body BodyB => new(Ffi.b3Joint_GetBodyB(Id));

        /// <summary>Whether the two connected bodies are allowed to collide with each other.</summary>
        public bool CollideConnected => Ffi.b3Joint_GetCollideConnected(Id);
    }
}
