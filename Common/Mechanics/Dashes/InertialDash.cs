using System;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;
using TestMod.Content.Projectiles.Dashes;

namespace TestMod.Common.Mechanics.Dashes
{
    /// <summary>长短冲刺共用的额外位移、命中及出场惯性流程；不用于速度型冲刺。</summary>
    public abstract class InertialDash<TStrike> : PlayerDashEffect where TStrike : DashStrikeProjectile
    {
        // 注册表共享效果实例；这里只保存不可变参数，不保存任何玩家运行状态。
        protected readonly record struct DashSettings(float Speed, float ExitInertiaRatio,
            int Damage, float Knockback, int CritDenominator, int MaxHits,
            int StartIFrames, int ActiveIFrames, int ContactIFrames);

        protected abstract DashSettings Settings { get; }
        protected abstract void PlayStartEffects(Player player);
        protected abstract void SpawnTrail(Player player, Vector2 unitDirection);
        protected abstract void SpawnContactEffects(NPC target);

        public override bool AllowVerticalDash => true;
        public override bool CanUseDash(Player player) => true;

        public override void OnDashStart(Player player, int direction)
        {
            if (direction != 0) player.direction = Math.Sign(direction);
            // 仅清水平惯性；跳跃、下落与飞行的垂直速度继续生效。
            player.velocity.X = 0f;
            DashImmunity.Grant(player, Settings.StartIFrames);
            PlayStartEffects(player);
        }

        public override void OnDashEffects(Player player, int dirX, int dirY, int frameCount)
        {
            Vector2 unitDir = new(dirX, dirY);
            if (unitDir.LengthSquared() == 0) return;
            unitDir.Normalize();
            DashSettings settings = Settings;
            Vector2 desiredMove = unitDir * settings.Speed;
            Vector2 actualMove = Collision.TileCollision(player.position, desiredMove, player.width, player.height,
                fallThrough: false, fall2: false, gravDir: (int)player.gravDir);

            // 额外位移与原版 velocity 并行，保留原有运动手感及碰撞顺序。
            player.position += actualMove;
            player.fallStart = (int)(player.position.Y / 16f);
            player.fallStart2 = player.fallStart;
            DashImmunity.Grant(player, settings.ActiveIFrames);
            SpawnTrail(player, unitDir);
            CheckContactDamage(player, dirX, settings);

            // 保留先命中、再判断撞墙结束的顺序。
            if (actualMove.LengthSquared() < desiredMove.LengthSquared() * 0.04f)
                player.GetModPlayer<DashPlayer>().EndDash();
        }

        public override void OnDashEnd(Player player)
        {
            DashPlayer dash = player.GetModPlayer<DashPlayer>();
            Vector2 unitDir = new(dash.DirX, dash.DirY);
            if (unitDir.LengthSquared() <= 0) return;
            unitDir.Normalize();
            DashSettings settings = Settings;
            // 按方向追加惯性，不替换现有速度，也不按松键/反向主动制动。
            Vector2 exitInertia = unitDir * settings.Speed * settings.ExitInertiaRatio;
            player.velocity.X += exitInertia.X;
            player.velocity.Y += exitInertia.Y;
        }

        private void CheckContactDamage(Player player, int dirX, DashSettings settings)
        {
            if (player.whoAmI != Main.myPlayer || Main.netMode == NetmodeID.Server) return;
            DashPlayer dash = player.GetModPlayer<DashPlayer>();
            if (dash.HitCount >= settings.MaxHits) return;
            Rectangle hitbox = new((int)player.position.X - 6, (int)player.position.Y - 6,
                player.width + 12, player.height + 12);

            for (int i = 0; i < Main.maxNPCs; i++)
            {
                NPC npc = Main.npc[i];
                if (!npc.active || npc.dontTakeDamage || npc.friendly || npc.immortal) continue;
                if (dash.HitTargets[i]) continue;
                if (!hitbox.Intersects(npc.Hitbox)) continue;

                bool crit = settings.CritDenominator > 0 && Main.rand.Next(settings.CritDenominator) == 0;
                // 保留每个目标结算前重新读取属性及原随机抽取顺序。
                int scaledDamage = (int)player.GetTotalDamage(DamageClass.Melee).ApplyTo(settings.Damage);
                int hitDirection = dirX != 0 ? dirX : (Main.rand.NextBool() ? 1 : -1);
                NPC.HitInfo hitInfo = npc.CalculateHitInfo(scaledDamage, hitDirection, crit,
                    settings.Knockback, DamageClass.Melee);
                if (!DashStrikeProjectile.Strike<TStrike>(player, npc, hitInfo, Id)) continue;
                dash.HitTargets[i] = true;
                dash.HitCount++;
                SpawnContactEffects(npc);
                SoundEngine.PlaySound(SoundID.Item14, npc.Center);
                DashImmunity.Grant(player, settings.ContactIFrames);
                if (dash.HitCount >= settings.MaxHits) break;
            }
        }
    }
}
