using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.ID;
using TestMod.Common.Players;
using TestMod.Common.Utilities;

namespace TestMod.Common.Systems
{
    public sealed partial class LifetakingAttackSystem
    {
        public override void PostDrawTiles()
        {
            if (Main.dedServ || Main.gameMenu || Main.mapFullscreen) return;
            TimeEchoSystem.GetDrawBounds(out Vector2 minimum, out Vector2 maximum);
            Main.spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState,
                DepthStencilState.None, Main.Rasterizer, null, Main.GameViewMatrix.TransformationMatrix);
            try
            {
                foreach (Player player in Main.player)
                {
                    if (player?.active != true || player.dead || !player.TryGetModPlayer(out LifetakingPlayer state)
                        || !state.TryGetTarget(out NPC target)) continue;
                    Vector2 start = player.Center, end = target.Center;
                    float fade = Math.Min(1, state.MarkTime / 20f);
                    float flash = state.PulseTime / 10f;
                    Color color = Color.Lerp(new Color(230, 125, 45), Color.White, Math.Min(1, flash * 0.6f));
                    if (TimeEchoGeometry.ClipLine(ref start, ref end, minimum, maximum))
                    {
                        DrawUtils.DrawLine(Main.spriteBatch, start, end, color * ((0.15f + flash * 0.3f) * fade), 5);
                        DrawUtils.DrawLine(Main.spriteBatch, start, end, color * ((0.55f + flash * 0.3f) * fade), 1.4f);
                    }
                    Vector2 center = target.Top + new Vector2(0, -14);
                    if (center.X < minimum.X - 24 || center.X > maximum.X + 24
                        || center.Y < minimum.Y - 24 || center.Y > maximum.Y + 24) continue;
                    float radius = 10 + MathF.Sin(Main.GlobalTimeWrappedHourly * 3) * 1.5f;
                    Vector2 previous = center + Vector2.UnitY * radius;
                    for (int i = 1; i <= 4; i++)
                    {
                        Vector2 next = center + Vector2.UnitY.RotatedBy(i * MathHelper.PiOver2) * radius;
                        DrawUtils.DrawLine(Main.spriteBatch, previous, next, color * fade, 2);
                        previous = next;
                    }
                    DrawUtils.DrawLine(Main.spriteBatch, center - Vector2.UnitY * 6, center + Vector2.UnitY * 6, color * fade, 2);
                }
            }
            finally { Main.spriteBatch.End(); }
        }

        internal static void PlayFinish(Vector2 center)
        {
            if (Main.dedServ || Main.gameMenu || Vector2.DistanceSquared(center, Main.screenPosition
                + new Vector2(Main.screenWidth, Main.screenHeight) * 0.5f) > 2000 * 2000) return;
            for (int i = 0; i < 8; i++)
            {
                Vector2 velocity = Vector2.UnitX.RotatedBy(MathHelper.TwoPi * i / 8) * 2;
                Dust dust = Dust.NewDustPerfect(center, DustID.Torch, velocity, 100, default, 1.1f);
                dust.noGravity = true;
            }
        }
    }
}
