using Microsoft.Xna.Framework;
using Terraria;

namespace TestMod.Content.Dusts
{
    public sealed class MomentumGearDust : TestModDustBase
    {
        public override string Texture => "TestMod/Assets/Textures/Dusts/MomentumGearDust";

        public override void OnSpawn(Dust dust)
        {
            dust.noGravity = true;
            dust.noLight = true;
            dust.rotation = Main.rand.NextFloat(MathHelper.TwoPi);
        }

        public override bool Update(Dust dust)
        {
            int age = AdvanceAge(dust);
            dust.position += dust.velocity;
            dust.velocity *= 0.94f;
            dust.rotation += (dust.dustIndex % 2 == 0 ? 1f : -1f) * 0.14f;
            dust.alpha += 8;
            dust.scale *= 0.98f;
            if (age >= 30 || dust.alpha >= 255)
                dust.active = false;
            return false;
        }

        public override bool PreDraw(Dust dust)
        {
            DrawTexture(dust, VisibleColor(dust, Color.White) * FadeFromAlpha(dust), dust.rotation, dust.scale);
            return false;
        }
    }
}
