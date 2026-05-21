using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.UI;
using TestMod.Common.DynamicText.Fonts;

namespace TestMod.Common.DynamicText
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

    public sealed class DynamicWorldTextSystem : ModSystem
    {
        private const int MaxActiveTexts = 90;
        private static readonly List<DynamicWorldTextInstance> ActiveTexts = [];

        public static void Spawn(DynamicWorldTextRequest request)
        {
            if (Main.netMode == NetmodeID.Server || string.IsNullOrWhiteSpace(request.Text))
                return;

            if (ActiveTexts.Count >= MaxActiveTexts)
                ActiveTexts.RemoveAt(0);

            ActiveTexts.Add(new DynamicWorldTextInstance(request));
        }

        public static void SpawnCombatText(Rectangle hitbox, int damage, bool crit, Color? color, string styleKey)
        {
            Vector2 center = hitbox.Center.ToVector2();
            Vector2 jitter = Main.rand.NextVector2Circular(hitbox.Width * 0.22f + 4f, hitbox.Height * 0.18f + 4f);
            Spawn(new DynamicWorldTextRequest(
                damage.ToString(),
                center + jitter,
                styleKey,
                color,
                crit,
                seed: damage * 17 + hitbox.X * 3 + hitbox.Y));
        }

        public override void Unload()
        {
            ActiveTexts.Clear();
        }

        public override void PreUpdateEntities()
        {
            if (Main.netMode == NetmodeID.Server)
                return;

            for (int i = ActiveTexts.Count - 1; i >= 0; i--)
            {
                ActiveTexts[i].Update();
                if (ActiveTexts[i].Expired)
                    ActiveTexts.RemoveAt(i);
            }
        }

        public override void ModifyInterfaceLayers(List<GameInterfaceLayer> layers)
        {
            int index = layers.FindIndex(layer => layer.Name == "Vanilla: Combat Text");
            if (index < 0)
                index = layers.Count;

            layers.Insert(index, new LegacyGameInterfaceLayer("TestMod: Dynamic World Text", DrawWorldTexts, InterfaceScaleType.Game));
        }

        private static bool DrawWorldTexts()
        {
            if (Main.netMode == NetmodeID.Server || ActiveTexts.Count <= 0)
                return true;

            Main.spriteBatch.End();
            Main.spriteBatch.Begin(
                SpriteSortMode.Deferred,
                BlendState.AlphaBlend,
                Main.DefaultSamplerState,
                DepthStencilState.None,
                Main.Rasterizer,
                null,
                Main.GameViewMatrix.TransformationMatrix);

            foreach (DynamicWorldTextInstance text in ActiveTexts)
                text.Draw(Main.spriteBatch);

            Main.spriteBatch.End();
            Main.spriteBatch.Begin(
                SpriteSortMode.Deferred,
                BlendState.AlphaBlend,
                Main.DefaultSamplerState,
                DepthStencilState.None,
                Main.Rasterizer,
                null,
                Main.UIScaleMatrix);

            return true;
        }

        private sealed class DynamicWorldTextInstance
        {
            private readonly DynamicTextStyle style;
            private readonly Color? overrideColor;
            private readonly bool crit;
            private readonly float requestScale;
            private readonly int lifetime;
            private readonly int seed;
            private int time;
            private Vector2 position;
            private Vector2 velocity;

            public string Text { get; }
            public bool Expired => time >= lifetime;

            public DynamicWorldTextInstance(DynamicWorldTextRequest request)
            {
                style = DynamicTextStyleRegistry.Get(request.StyleKey);
                Text = request.Text;
                position = request.WorldPosition;
                velocity = request.Velocity ?? style.Velocity;
                overrideColor = request.OverrideColor;
                crit = request.Crit;
                lifetime = request.Lifetime ?? style.Lifetime;
                requestScale = request.Scale;
                seed = request.Seed == 0 ? Text.GetHashCode() ^ (int)request.WorldPosition.X ^ ((int)request.WorldPosition.Y << 4) : request.Seed;
            }

            public void Update()
            {
                position += velocity;
                velocity *= style.Drag;
                velocity.Y += style.Gravity;
                time++;
            }

            public void Draw(SpriteBatch spriteBatch)
            {
                float progress = lifetime <= 0 ? 1f : time / (float)lifetime;
                float opacity = style.GetWorldOpacity(progress);
                if (opacity <= 0f)
                    return;

                DynamicSpriteFont fallbackFont = FontAssets.CombatText[crit ? 1 : 0].Value;
                DynamicTextFont font = DynamicTextFontSystem.Resolve(style.FontSpec, fallbackFont);
                Vector2 textSize = font.MeasureString(Text);
                Vector2 scale = Vector2.One * style.GetWorldScale(progress, crit, requestScale);
                Vector2 drawPosition = position - Main.screenPosition;
                Vector2 origin = textSize * 0.5f;
                Color primary = overrideColor ?? style.PrimaryColor;

                DynamicTextDrawContext context = new(
                    spriteBatch,
                    font,
                    Text,
                    drawPosition,
                    0f,
                    origin,
                    scale,
                    Main.GlobalTimeWrappedHourly + time / 60f,
                    progress,
                    opacity,
                    seed,
                    -1f,
                    1f,
                    primary,
                    style.SecondaryColor,
                    style.ShadowColor,
                    DynamicTextSurface.World);

                style.Draw(context);
            }
        }
    }
}
