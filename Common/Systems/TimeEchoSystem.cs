using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;
using TestMod.Common.Players;
using TestMod.Common.Utilities;

namespace TestMod.Common.Systems
{
    public sealed class TimeEchoSystem : ModSystem
    {
        public static ModKeybind RewindKey { get; private set; }
        public static ModKeybind SwapKey { get; private set; }
        private readonly bool[] activeLastTick = new bool[Main.maxPlayers];
        internal static readonly Color EchoBlue = new(105, 150, 255);
        internal static readonly Color EchoPurple = new(175, 115, 245);

        public override void Load()
        {
            RewindKey = KeybindLoader.RegisterKeybind(Mod, "TimeEchoRewind", "R");
            SwapKey = KeybindLoader.RegisterKeybind(Mod, "TimeEchoSwap", "Q");
        }

        public override void Unload()
        {
            RewindKey = SwapKey = null;
            ResetWorldState();
        }

        public override void OnWorldUnload() => ResetWorldState();

        private void ResetWorldState()
        {
            Array.Clear(activeLastTick);
            foreach (Player player in Main.player)
                if (player != null && player.TryGetModPlayer(out TimeEchoPlayer echo)) echo.ResetConnection();
        }

        public override void PostUpdatePlayers()
        {
            if (Main.gameMenu || (Main.netMode == NetmodeID.SinglePlayer && Main.gamePaused)) return;
            for (int i = 0; i < Main.maxPlayers; i++)
            {
                Player player = Main.player[i];
                // 未使用的槽位可能在模组加载前创建，modPlayers 数组没有本轮模组的数据。
                // 不能在检查 active 前直接 GetModPlayer：异常会打断后续 NPC/弹幕/Dust 更新。
                if (player == null || (!player.active && !activeLastTick[i])) continue;
                if (!player.TryGetModPlayer(out TimeEchoPlayer state))
                {
                    activeLastTick[i] = false;
                    continue;
                }
                if (player.active) state.UpdateEcho();
                else if (activeLastTick[i]) state.ResetConnection();
                activeLastTick[i] = player.active;
            }
        }

        public override void PostDrawTiles()
        {
            if (Main.dedServ || Main.gameMenu || Main.mapFullscreen) return;
            GetDrawBounds(out Vector2 minimum, out Vector2 maximum);
            // 该入口要求自行 Begin/End；不改原版渲染目标，不依赖普通玩家的屏幕剔除。
            Main.spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend, Main.DefaultSamplerState,
                DepthStencilState.None, Main.Rasterizer, null, Main.GameViewMatrix.TransformationMatrix);
            try
            {
                foreach (Player player in Main.player)
                {
                    if (player == null || !player.active || player.dead) continue;
                    if (!player.TryGetModPlayer(out TimeEchoPlayer state)) continue;
                    if (!state.HasPhantom) continue;
                    Vector2 phantomCenter = state.VisualPosition + player.Size * 0.5f;
                    if (player.whoAmI == Main.myPlayer)
                        DrawConnection(player.Center, phantomCenter, minimum, maximum, state.VisualOpacity);
                    // 装备和短残影在身体两侧保留余量；身体剔除不影响上面的连接线。
                    if (phantomCenter.X + 160f < minimum.X || phantomCenter.X - 160f > maximum.X ||
                        phantomCenter.Y + 160f < minimum.Y || phantomCenter.Y - 160f > maximum.Y) continue;
                    state.DrawPhantom();
                }
            }
            finally { Main.spriteBatch.End(); }
        }

        internal static void GetDrawBounds(out Vector2 minimum, out Vector2 maximum)
        {
            Viewport viewport = Main.instance.GraphicsDevice.Viewport;
            TimeEchoGeometry.GetDrawBounds(Main.GameViewMatrix.TransformationMatrix, viewport.Width, viewport.Height,
                Main.screenPosition, out minimum, out maximum);
        }

        private static void DrawConnection(Vector2 start, Vector2 end, Vector2 minimum, Vector2 maximum, float fade)
        {
            if (!TimeEchoGeometry.ClipLine(ref start, ref end, minimum, maximum)) return;
            if (Vector2.DistanceSquared(start, end) < 1f) return;
            float pulse = 0.13f + 0.055f * MathF.Sin(Main.GlobalTimeWrappedHourly * 2.2f);
            // DrawLine 只取 MagicPixel 的 1×1 源区域，宽度才是实际线宽，而非整张贴图的倍数。
            DrawUtils.DrawLine(Main.spriteBatch, start, end, EchoPurple * (pulse * 0.35f * fade), 5f);
            DrawUtils.DrawLine(Main.spriteBatch, start, end, EchoBlue * (pulse * fade), 1.4f);
        }

        internal static void PlayDisplacement(Vector2 origin, Vector2 destination, bool rewind)
        {
            if (Main.dedServ) return;
            GetDrawBounds(out Vector2 minimum, out Vector2 maximum);
            Burst(origin, minimum, maximum, rewind);
            Burst(destination, minimum, maximum, rewind);
            SoundEngine.PlaySound(SoundID.Item8 with { Volume = 0.55f, Pitch = rewind ? -0.25f : 0.2f }, destination);
        }

        private static void Burst(Vector2 center, Vector2 minimum, Vector2 maximum, bool rewind)
        {
            if (center.X < minimum.X || center.X > maximum.X || center.Y < minimum.Y || center.Y > maximum.Y) return;
            int count = rewind ? 24 : 16;
            for (int i = 0; i < count; i++)
            {
                Vector2 direction = (MathHelper.TwoPi * (i / (float)count)).ToRotationVector2();
                Vector2 position = center + direction * (rewind ? 28f : 8f);
                Dust dust = Dust.NewDustPerfect(position, DustID.MagicMirror, direction * (rewind ? -2.2f : 2.2f),
                    100, Color.Lerp(EchoBlue, EchoPurple, i / (float)count), 1.15f);
                dust.noGravity = true;
                dust.noLight = true;
            }
        }
    }
}
