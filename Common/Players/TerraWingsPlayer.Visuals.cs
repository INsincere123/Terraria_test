using Microsoft.Xna.Framework;
using Terraria;
using Terraria.Graphics;

namespace TestMod.Common.Players
{
    public partial class TerraWingsPlayer
    {
        private const int DashShadowCount = 5;
        private const int DashShadowFadeTicks = 10;
        private readonly Vector2[] dashShadowPositions = new Vector2[DashShadowCount];
        private int dashShadowCount;
        private int dashShadowTimer;
        private bool dashTrailRunning;
        private bool drawingDashShadow;
        private ulong lastDashTrailTick = ulong.MaxValue;

        internal void BeginDashTrail()
        {
            if (Main.dedServ) return;
            ClearDashTrail();
            dashTrailRunning = true;
            PushDashShadow();
            dashShadowTimer = DashShadowFadeTicks;
        }

        private void PushDashShadow()
        {
            for (int i = DashShadowCount - 1; i > 0; i--)
                dashShadowPositions[i] = dashShadowPositions[i - 1];
            dashShadowPositions[0] = Player.position;
            if (dashShadowCount < DashShadowCount) dashShadowCount++;
        }

        private void UpdateDashTrail(bool active)
        {
            if (Main.dedServ) return;
            if (!Equipped || Player.dead || Player.mount.Active || Player.CCed || Player.shimmering
                || Player.tongued || Player.pulley || Player.grapCount > 0)
            {
                ClearDashTrail();
                return;
            }
            if (lastDashTrailTick == Main.GameUpdateCount) return;
            lastDashTrailTick = Main.GameUpdateCount;
            if (active)
            {
                if (!dashTrailRunning) dashShadowCount = 0;
                PushDashShadow();
                dashShadowTimer = DashShadowFadeTicks;
            }
            else if (dashShadowTimer > 0 && --dashShadowTimer == 0)
                dashShadowCount = 0;
            dashTrailRunning = active;
        }

        private void ClearDashTrail()
        {
            dashShadowCount = dashShadowTimer = 0;
            dashTrailRunning = false;
            lastDashTrailTick = ulong.MaxValue;
        }

        public override void DrawPlayer(Camera camera)
        {
            if (Main.dedServ || !Equipped || Player.dead || Player.mount.Active || Player.invis || Player.stoned
                || dashShadowTimer <= 0 || drawingDashShadow) return;
            // 单独记录实际位移，避免原版残影标志被潜行/人物帧处理清除。
            // 直接复用人物绘制，保留当前装备、翅膀和染料；不改变 SpriteBatch。
            float fade = dashShadowTimer / (float)DashShadowFadeTicks;
            drawingDashShadow = true;
            try
            {
                for (int i = dashShadowCount - 1; i >= 0; i--)
                {
                    if (Vector2.DistanceSquared(dashShadowPositions[i], Player.position) < 1f) continue;
                    float opacity = fade * MathHelper.Lerp(0.42f, 0.14f, i / (float)(DashShadowCount - 1));
                    Main.PlayerRenderer.DrawPlayer(camera, Player, dashShadowPositions[i], Player.fullRotation,
                        Player.fullRotationOrigin, 1f - opacity);
                }
            }
            finally
            {
                drawingDashShadow = false;
            }
        }
    }
}
