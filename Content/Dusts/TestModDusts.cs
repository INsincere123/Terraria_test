using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.ModLoader;

namespace TestMod.Content.Dusts
{
    public abstract class TestModDustBase : ModDust
    {
        public override string Texture => null;

        protected static int AdvanceAge(Dust dust)
        {
            int age = dust.customData is int value ? value : 0;
            dust.customData = age + 1;
            return age;
        }

        protected static float FadeFromAlpha(Dust dust)
            => MathHelper.Clamp(1f - dust.alpha / 255f, 0f, 1f);

        protected static void DrawPixel(Vector2 position, Color color, float rotation, Vector2 scale)
        {
            Main.spriteBatch.Draw(
                TextureAssets.MagicPixel.Value,
                position - Main.screenPosition,
                new Rectangle(0, 0, 1, 1),
                color,
                rotation,
                new Vector2(0.5f, 0.5f),
                scale,
                SpriteEffects.None,
                0f);
        }

        protected void DrawTexture(Dust dust, Color color, float rotation, float scale)
        {
            Texture2D texture = Texture2D.Value;
            Main.spriteBatch.Draw(
                texture,
                dust.position - Main.screenPosition,
                null,
                color,
                rotation,
                new Vector2(texture.Width * 0.5f, texture.Height * 0.5f),
                scale,
                SpriteEffects.None,
                0f);
        }

        protected static Color VisibleColor(Dust dust, Color fallback)
        {
            if (dust.color == default)
                return fallback;

            return dust.color;
        }
    }

    public class CosmicSparkDust : TestModDustBase
    {
        public override string Texture => "TestMod/Assets/Textures/Dusts/CosmicSparkDust";

        public override void OnSpawn(Dust dust)
        {
            dust.noGravity = true;
            dust.noLight = false;
            dust.alpha = Math.Min(dust.alpha, 80);
            dust.rotation = Main.rand.NextFloat(MathHelper.TwoPi);
            dust.color = VisibleColor(dust, new Color(255, 122, 48));
        }

        public override bool Update(Dust dust)
        {
            int age = AdvanceAge(dust);

            dust.position += dust.velocity;
            dust.velocity *= 0.94f;
            dust.rotation += (dust.dustIndex % 2 == 0 ? -1f : 1f) * 0.08f;
            dust.alpha += age < 8 ? 2 : 8;
            dust.scale *= 0.982f;

            Lighting.AddLight(dust.position, dust.color.ToVector3() * dust.scale * 0.18f);

            if (dust.alpha >= 255 || dust.scale <= 0.08f)
                dust.active = false;

            return false;
        }

        public override bool PreDraw(Dust dust)
        {
            float fade = FadeFromAlpha(dust);
            if (fade <= 0f)
                return false;

            Color color = VisibleColor(dust, new Color(255, 122, 48));
            float scale = dust.scale * MathHelper.Lerp(0.12f, 0.36f, fade);
            DrawTexture(dust, color * (0.86f * fade), dust.rotation, scale);
            DrawTexture(dust, Color.White * (0.36f * fade), dust.rotation, scale * 0.55f);
            return false;
        }
    }

    public class GraviticVoidDust : TestModDustBase
    {
        public override string Texture => "TestMod/Assets/Textures/Dusts/GraviticVoidDust";

        public override void OnSpawn(Dust dust)
        {
            dust.noGravity = true;
            dust.noLight = false;
            dust.alpha = Math.Min(dust.alpha, 70);
            dust.rotation = Main.rand.NextFloat(MathHelper.TwoPi);
            dust.color = VisibleColor(dust, new Color(128, 72, 210));
        }

        public override bool Update(Dust dust)
        {
            AdvanceAge(dust);

            dust.position += dust.velocity;
            dust.velocity *= 0.965f;
            dust.rotation += 0.045f * MathF.Sign(dust.velocity.X == 0f ? 1f : dust.velocity.X);
            dust.alpha += 5;
            dust.scale *= 0.99f;

            Lighting.AddLight(dust.position, dust.color.ToVector3() * dust.scale * 0.12f);

            if (dust.alpha >= 255 || dust.scale <= 0.06f)
                dust.active = false;

            return false;
        }

        public override bool PreDraw(Dust dust)
        {
            float fade = FadeFromAlpha(dust);
            if (fade <= 0f)
                return false;

            Color rim = VisibleColor(dust, new Color(128, 72, 210));
            float scale = dust.scale * MathHelper.Lerp(0.16f, 0.42f, fade);
            DrawTexture(dust, rim * (0.78f * fade), dust.rotation, scale);
            return false;
        }
    }

    public class TemporalShardDust : TestModDustBase
    {
        public override string Texture => "TestMod/Assets/Textures/Dusts/TemporalShardDust";

        public override void OnSpawn(Dust dust)
        {
            dust.noGravity = true;
            dust.noLight = false;
            dust.alpha = Math.Min(dust.alpha, 60);
            dust.color = VisibleColor(dust, new Color(150, 210, 255));
            if (dust.velocity.LengthSquared() > 0.01f)
                dust.rotation = dust.velocity.ToRotation();
        }

        public override bool Update(Dust dust)
        {
            AdvanceAge(dust);

            dust.position += dust.velocity;
            dust.velocity *= 0.92f;
            if (dust.velocity.LengthSquared() > 0.01f)
                dust.rotation = dust.velocity.ToRotation();

            dust.alpha += 7;
            dust.scale *= 0.988f;

            Lighting.AddLight(dust.position, dust.color.ToVector3() * dust.scale * 0.16f);

            if (dust.alpha >= 255 || dust.scale <= 0.06f)
                dust.active = false;

            return false;
        }

        public override bool PreDraw(Dust dust)
        {
            float fade = FadeFromAlpha(dust);
            if (fade <= 0f)
                return false;

            Color color = VisibleColor(dust, new Color(150, 210, 255));
            float scale = dust.scale * MathHelper.Lerp(0.15f, 0.42f, fade);
            DrawTexture(dust, color * (0.82f * fade), dust.rotation, scale);
            DrawTexture(dust, Color.White * (0.26f * fade), dust.rotation, scale * 0.58f);
            return false;
        }
    }

    public class ImpactLineDust : TestModDustBase
    {
        public override void OnSpawn(Dust dust)
        {
            dust.noGravity = true;
            dust.noLight = false;
            dust.alpha = Math.Min(dust.alpha, 40);
            dust.color = VisibleColor(dust, new Color(255, 220, 96));
            if (dust.velocity.LengthSquared() > 0.01f)
                dust.rotation = dust.velocity.ToRotation();
        }

        public override bool Update(Dust dust)
        {
            dust.position += dust.velocity;
            dust.velocity *= 0.88f;
            if (dust.velocity.LengthSquared() > 0.01f)
                dust.rotation = dust.velocity.ToRotation();

            dust.alpha += 18;
            dust.scale *= 0.985f;

            Lighting.AddLight(dust.position, dust.color.ToVector3() * dust.scale * 0.12f);

            if (dust.alpha >= 255 || dust.scale <= 0.05f)
                dust.active = false;

            return false;
        }

        public override bool PreDraw(Dust dust)
        {
            float fade = FadeFromAlpha(dust);
            if (fade <= 0f)
                return false;

            Color color = VisibleColor(dust, new Color(255, 220, 96));
            float speedStretch = MathHelper.Clamp(dust.velocity.Length() * 0.35f, 0.8f, 2.2f);
            Vector2 outer = new(28f * speedStretch * dust.scale * fade, 3.2f * dust.scale * fade);
            Vector2 inner = new(14f * speedStretch * dust.scale * fade, 1.2f * dust.scale * fade);

            DrawPixel(dust.position, color * (0.75f * fade), dust.rotation, outer);
            DrawPixel(dust.position, Color.White * (0.56f * fade), dust.rotation, inner);
            return false;
        }
    }

    public class ShieldShardDust : TestModDustBase
    {
        public override string Texture => "TestMod/Assets/Textures/Dusts/ShieldShardDust";

        public override void OnSpawn(Dust dust)
        {
            dust.noGravity = true;
            dust.noLight = false;
            dust.alpha = Math.Min(dust.alpha, 40);
            dust.rotation = Main.rand.NextFloat(MathHelper.TwoPi);
            dust.color = VisibleColor(dust, new Color(82, 190, 255));
        }

        public override bool Update(Dust dust)
        {
            AdvanceAge(dust);

            dust.position += dust.velocity;
            dust.velocity *= 0.925f;
            dust.rotation += dust.velocity.X * 0.045f + 0.04f;
            dust.alpha += 9;
            dust.scale *= 0.978f;

            Lighting.AddLight(dust.position, dust.color.ToVector3() * dust.scale * 0.18f);

            if (dust.alpha >= 255 || dust.scale <= 0.05f)
                dust.active = false;

            return false;
        }

        public override bool PreDraw(Dust dust)
        {
            float fade = FadeFromAlpha(dust);
            if (fade <= 0f)
                return false;

            Color color = VisibleColor(dust, new Color(82, 190, 255));
            float scale = dust.scale * MathHelper.Lerp(0.12f, 0.34f, fade);
            DrawTexture(dust, color * (0.84f * fade), dust.rotation, scale);
            DrawTexture(dust, Color.White * (0.22f * fade), dust.rotation, scale * 0.62f);
            return false;
        }
    }

    public class BloodMistDust : TestModDustBase
    {
        public override string Texture => "TestMod/Assets/Textures/Dusts/BloodMistDust";

        public override void OnSpawn(Dust dust)
        {
            dust.noGravity = false;
            dust.noLight = true;
            dust.alpha = Math.Min(dust.alpha, 85);
            dust.scale *= Main.rand.NextFloat(0.85f, 1.25f);
            dust.rotation = Main.rand.NextFloat(MathHelper.TwoPi);
            dust.color = VisibleColor(dust, new Color(130, 18, 28));
        }

        public override bool Update(Dust dust)
        {
            AdvanceAge(dust);

            dust.position += dust.velocity;
            dust.velocity.X *= 0.96f;
            dust.velocity.Y = dust.velocity.Y * 0.96f + 0.045f;
            dust.rotation += dust.velocity.X * 0.04f;
            dust.alpha += 5;
            dust.scale *= 0.982f;

            if (dust.alpha >= 255 || dust.scale <= 0.08f)
                dust.active = false;

            return false;
        }

        public override bool PreDraw(Dust dust)
        {
            float fade = FadeFromAlpha(dust);
            if (fade <= 0f)
                return false;

            Color color = VisibleColor(dust, new Color(130, 18, 28));
            float scale = dust.scale * MathHelper.Lerp(0.2f, 0.48f, fade);
            DrawTexture(dust, color * (0.82f * fade), dust.rotation, scale);
            return false;
        }
    }
}
