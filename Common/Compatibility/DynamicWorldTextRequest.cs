using Microsoft.Xna.Framework;

namespace TestMod.Common.Compatibility
{
    public readonly struct DynamicWorldTextRequest
    {
        public readonly string Text;
        public readonly Vector2 WorldPosition;
        public readonly string StyleKey;
        public readonly Color? OverrideColor;
        public readonly bool Crit;
        public readonly int? Lifetime;
        public readonly Vector2? Velocity;
        public readonly float Scale;
        public readonly int Seed;

        public DynamicWorldTextRequest(
            string text,
            Vector2 worldPosition,
            string styleKey,
            Color? overrideColor = null,
            bool crit = false,
            int? lifetime = null,
            Vector2? velocity = null,
            float scale = 1f,
            int seed = 0)
        {
            Text = text;
            WorldPosition = worldPosition;
            StyleKey = styleKey;
            OverrideColor = overrideColor;
            Crit = crit;
            Lifetime = lifetime;
            Velocity = velocity;
            Scale = scale;
            Seed = seed;
        }
    }
}
