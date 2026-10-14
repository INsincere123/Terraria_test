using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;
using Terraria.Localization;

namespace TestMod.Common.Systems
{
    public sealed partial class DecoySystem
    {
        private static Func<Projectile, Rectangle> damageHitbox;
        // 专项范围判定返回 null 表示无法证明；不以本体矩形替换任意自定义范围攻击。
        private static readonly Dictionary<Type, Func<Entity, Player, bool?>> directHitShapes = new();
        private delegate double HurtOriginal(Player player, PlayerDeathReason source, int damage, int direction,
            out Player.HurtInfo info, bool pvp, bool quiet, int cooldown, bool dodgeable, float penetration,
            float scalingPenetration, float knockback);
        private delegate double HurtDetour(HurtOriginal original, Player player, PlayerDeathReason source, int damage,
            int direction, out Player.HurtInfo info, bool pvp, bool quiet, int cooldown, bool dodgeable,
            float penetration, float scalingPenetration, float knockback);
        private delegate void HurtInfoOriginal(Player player, Player.HurtInfo info, bool quiet);
        private delegate void HurtInfoDetour(HurtInfoOriginal original, Player player, Player.HurtInfo info, bool quiet);
        private delegate void DeathOriginal(Player player, PlayerDeathReason source, double damage, int direction, bool pvp);
        private delegate void DeathDetour(DeathOriginal original, Player player, PlayerDeathReason source, double damage, int direction, bool pvp);
        private delegate void TeleportOriginal(Player player, Vector2 destination, int style, int extra);
        private delegate void TeleportDetour(TeleportOriginal original, Player player, Vector2 destination, int style, int extra);
        private delegate void MoveOriginal(Player player, Vector2 destination);
        private delegate void MoveDetour(MoveOriginal original, Player player, Vector2 destination);
        private delegate void BuffOriginal(Player player, int type, int time, bool quiet, bool foodHack);
        private delegate void BuffDetour(BuffOriginal original, Player player, int type, int time, bool quiet, bool foodHack);
        private delegate void SendOriginal(int message, int remote, int ignore, NetworkText text, int number,
            float number2, float number3, float number4, int number5, int number6, int number7);
        private delegate void SendDetour(SendOriginal original, int message, int remote, int ignore, NetworkText text,
            int number, float number2, float number3, float number4, int number5, int number6, int number7);

        private void InstallDamageHooks()
        {
            damageHitbox = typeof(Projectile).GetMethod("Damage_GetHitbox", Methods, null, Type.EmptyTypes, null)
                ?.CreateDelegate<Func<Projectile, Rectangle>>() ?? throw new MissingMethodException("Projectile.Damage_GetHitbox");
            Install(typeof(Projectile), nameof(Projectile.Damage), Type.EmptyTypes, (ProjectileDetour)DamageProjectile);
            Install(typeof(Player), nameof(Player.Hurt), new[] { typeof(PlayerDeathReason), typeof(int), typeof(int),
                typeof(Player.HurtInfo).MakeByRefType(), typeof(bool), typeof(bool), typeof(int), typeof(bool),
                typeof(float), typeof(float), typeof(float) }, (HurtDetour)HurtPlayer);
            Install(typeof(Player), nameof(Player.Hurt), new[] { typeof(Player.HurtInfo), typeof(bool) }, (HurtInfoDetour)HurtPlayerInfo);
            Install(typeof(Player), nameof(Player.KillMe), new[] { typeof(PlayerDeathReason), typeof(double), typeof(int), typeof(bool) }, (DeathDetour)KillPlayer);
            Install(typeof(Player), nameof(Player.Teleport), new[] { typeof(Vector2), typeof(int), typeof(int) }, (TeleportDetour)TeleportPlayer);
            Install(typeof(Player), nameof(Player.UnityTeleport), new[] { typeof(Vector2) }, (MoveDetour)MovePlayer);
            Install(typeof(Player), nameof(Player.DoQueenSlimeHookTeleport), new[] { typeof(Vector2) }, (MoveDetour)MovePlayer);
            Install(typeof(Player), nameof(Player.AddBuff), new[] { typeof(int), typeof(int), typeof(bool), typeof(bool) }, (BuffDetour)AddPlayerBuff);
            Install(typeof(NetMessage), nameof(NetMessage.SendData), new[] { typeof(int), typeof(int), typeof(int),
                typeof(NetworkText), typeof(int), typeof(float), typeof(float), typeof(float), typeof(int), typeof(int), typeof(int) }, (SendDetour)SendRealData);
        }

        private static void DamageProjectile(ProjectileOriginal original, Projectile projectile)
        {
            // 正式 Damage 完整使用真实玩家，保留 ModifyDamageHitbox、Colliding、免疫与命中回调。
            Frame frame = Enter(projectile, false, true);
            try { original(projectile); }
            finally { Leave(frame); }
        }

        private static Entity CausingEntity(PlayerDeathReason reason)
        {
            if (reason != null)
            {
                int npc = reason.SourceNPCIndex, projectile = reason.SourceProjectileLocalIndex;
                if (npc >= 0 && npc < Main.maxNPCs && Main.npc[npc] is NPC enemy) return enemy;
                if (projectile >= 0 && projectile < Main.maxProjectiles && Main.projectile[projectile] is Projectile shot &&
                    shot.type == reason.SourceProjectileType) return shot;
                // 明确的玩家来源不能继承 敌人上下文。
                if (reason.SourcePlayerIndex >= 0 && projectile < 0) return null;
            }
            return current?.Entity;
        }

        private static bool RealHit(Entity actor, Player player)
        {
            if (actor == null || player == null || !player.active || player.dead) return false;
            try
            {
                Type type = actor is Projectile p ? p.ModProjectile?.GetType() : (actor as NPC)?.ModNPC?.GetType();
                if (type != null && directHitShapes.TryGetValue(type, out var adapter))
                {
                    bool? hit = adapter(actor, player);
                    if (hit.HasValue) return hit.Value;
                }
                if (actor is Projectile projectile)
                    return damageHitbox != null && projectile.Colliding(damageHitbox(projectile), player.Hitbox);
                if (actor is NPC npc && npc.Hitbox.Intersects(player.Hitbox)) return true;
            }
            catch (Exception exception)
            {
                Note(actor, "真实碰撞复核失败：" + exception.GetType().Name, true);
                return false;
            }
            Note(actor, "AI 内直接受伤无法证明真实命中；跳过本次伤害，复杂范围需专项形状", true);
            return false;
        }

        private static double HurtPlayer(HurtOriginal original, Player player, PlayerDeathReason source, int damage,
            int direction, out Player.HurtInfo info, bool pvp, bool quiet, int cooldown, bool dodgeable,
            float penetration, float scalingPenetration, float knockback)
        {
            bool check = viewApplied;
            Entity actor = check ? CausingEntity(source) : null;
            Frame frame = Enter(player, false, true);
            try
            {
                if (check && !RealHit(actor, player)) { info = default; return 0; }
                return original(player, source, damage, direction, out info, pvp, quiet, cooldown, dodgeable,
                    penetration, scalingPenetration, knockback);
            }
            finally { Leave(frame); }
        }

        private static void HurtPlayerInfo(HurtInfoOriginal original, Player player, Player.HurtInfo info, bool quiet)
        {
            bool check = viewApplied;
            Entity actor = check ? CausingEntity(info.DamageSource) : null;
            Frame frame = Enter(player, false, true);
            try
            {
                if (check && !RealHit(actor, player)) return;
                original(player, info, quiet);
            }
            finally { Leave(frame); }
        }

        private static void KillPlayer(DeathOriginal original, Player player, PlayerDeathReason source, double damage, int direction, bool pvp)
        {
            bool check = viewApplied;
            Entity actor = check ? CausingEntity(source) : null;
            Frame frame = Enter(player, false, true);
            try { if (!check || RealHit(actor, player)) original(player, source, damage, direction, pvp); }
            finally { Leave(frame); }
        }

        private static void TeleportPlayer(TeleportOriginal original, Player player, Vector2 destination, int style, int extra)
        {
            Frame frame = Enter(player, false, true);
            try { original(player, destination, style, extra); }
            finally { Leave(frame); }
        }

        private static void MovePlayer(MoveOriginal original, Player player, Vector2 destination)
        {
            Frame frame = Enter(player, false, true);
            try { original(player, destination); }
            finally { Leave(frame); }
        }

        private static void AddPlayerBuff(BuffOriginal original, Player player, int type, int time, bool quiet, bool foodHack)
        {
            bool check = viewApplied && type >= 0 && type < Main.debuff.Length && Main.debuff[type];
            Entity actor = current?.Entity;
            Frame frame = Enter(player, false, true);
            try { if (!check || RealHit(actor, player)) original(player, type, time, quiet, foodHack); }
            finally { Leave(frame); }
        }

        private static void SendRealData(SendOriginal original, int message, int remote, int ignore, NetworkText text,
            int number, float number2, float number3, float number4, int number5, int number6, int number7)
        {
            if (!viewApplied)
            {
                original(message, remote, ignore, text, number, number2, number3, number4, number5, number6, number7);
                return;
            }
            Frame frame = Enter(null, false, true);
            try { original(message, remote, ignore, text, number, number2, number3, number4, number5, number6, number7); }
            finally { Leave(frame); }
        }
    }
}
