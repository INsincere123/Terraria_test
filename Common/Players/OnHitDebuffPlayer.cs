using System.Collections.Generic;
using Terraria;
using Terraria.ModLoader;

namespace TestMod.Common.Players
{
    /// <summary>
    /// 通用命中 debuff 附着器。
    /// 饰品在 UpdateAccessory 里调用 AddDebuff，本类在任意命中时统一施加。
    /// 条目每帧由 ResetEffects 清空，无需手动移除。
    /// </summary>
    public class OnHitDebuffPlayer : ModPlayer
    {
        private readonly List<(int type, int duration)> _debuffs = new();

        /// <summary>注册一个"命中时附加"的 debuff。可多次调用叠加多个。</summary>
        public static void AddDebuff(Player player, int buffType, int duration)
        {
            player.GetModPlayer<OnHitDebuffPlayer>()._debuffs.Add((buffType, duration));
        }

        public override void ResetEffects()
        {
            _debuffs.Clear();
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone)
        {
            foreach (var (type, duration) in _debuffs)
                target.AddBuff(type, duration);
        }

        public override void OnHitNPCWithProj(Projectile proj, NPC target, NPC.HitInfo hit, int damageDone)
        {
            foreach (var (type, duration) in _debuffs)
                target.AddBuff(type, duration);
        }
    }
}
