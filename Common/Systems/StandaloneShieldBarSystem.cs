using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ModLoader;
using Terraria.UI;
using TestMod.Common.Configs;
using TestMod.Common.UI.ResourceOverlays;

namespace TestMod.Common.Systems
{
    [Autoload(Side = ModSide.Client)]
    public class StandaloneShieldBarSystem : ModSystem
    {
        private const int GapFromLifeBar = 12;
        private readonly LegacyGameInterfaceLayer layer = new("TestMod: Shield Bar", DrawShieldBar, InterfaceScaleType.UI);

        public override void ModifyInterfaceLayers(List<GameInterfaceLayer> layers)
        {
            // 每次实际绘制重新捕获锚点，避免布局切换或跳过生命条后使用旧位置。
            ShieldBarVisualSystem.LifeBarBounds = null;
            int index = layers.FindIndex(item => item.Name == "Vanilla: Resource Bars");
            if (index >= 0) layers.Insert(index + 1, layer);
        }

        private static bool DrawShieldBar()
        {
            if (Main.dedServ || Main.gameMenu || Main.mapFullscreen || Main.LocalPlayer.dead)
                return true;
            TestModClientConfig config = TestModClientConfig.Instance;
            ShieldBarVisualState state = ShieldBarVisualSystem.State;
            if (config is null || !config.ShowStandaloneShieldBar || !state.Sample.Visible)
                return true;

            float scale = Math.Max(0.01f, Main.UIScale);
            float uiWidth = Main.graphics.GraphicsDevice.Viewport.Width / scale;
            float uiHeight = Main.graphics.GraphicsDevice.Viewport.Height / scale;
            Rectangle? life = ShieldBarVisualSystem.LifeBarBounds;
            Vector2 position = life.HasValue
                ? new Vector2(life.Value.Left - ShieldBarRenderer.Width - GapFromLifeBar, life.Value.Y - 2)
                : new Vector2(uiWidth - 306 - ShieldBarRenderer.Width - GapFromLifeBar, 24);
            position += new Vector2(config.StandaloneShieldBarOffsetX, config.StandaloneShieldBarOffsetY);
            position.X = Math.Clamp(position.X, 8f, Math.Max(8f, uiWidth - ShieldBarRenderer.Width - 8f));
            position.Y = Math.Clamp(position.Y, 21f, Math.Max(21f, uiHeight - ShieldBarRenderer.Height - 22f));
            Rectangle bounds = new((int)Math.Round(position.X), (int)Math.Round(position.Y), ShieldBarRenderer.Width, ShieldBarRenderer.Height);
            ShieldBarRenderer.DrawStandalone(Main.spriteBatch, bounds, state);
            ShieldBarRenderer.DrawCenteredText(Main.spriteBatch, ShieldBarVisualSystem.NumberText,
                new Vector2(bounds.Center.X, bounds.Y - 19), new Color(221, 236, 247), 0.58f);
            Color stageColor = state.Sample.Fragile ? ShieldBarRenderer.FragileColor :
                state.Sample.Stage == ShieldBarStage.Ready ? ShieldBarRenderer.NormalColor : new Color(174, 194, 210);
            ShieldBarRenderer.DrawCenteredText(Main.spriteBatch, ShieldBarVisualSystem.StageText,
                new Vector2(bounds.Center.X, bounds.Bottom + 2), stageColor, 0.56f);
            return true;
        }
    }
}
