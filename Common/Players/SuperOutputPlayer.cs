using Terraria;
using Terraria.ModLoader;

namespace TestMod.Common.Players
{
    /// <summary>
    /// 超级输出机制 - 核心结算
    /// 装备/药水buff每帧将 SuperOutputActive 置为 true，
    /// 在 PostUpdateEquips 末尾统一读取防御 → 转换为伤害加成 → 防御清零
    ///
    /// 防御清零仅作用于 statDefense（加法减伤），
    /// 对 endurance（乘法免伤）以及其他减伤效果完全不影响
    /// </summary>
    public class SuperOutputPlayer : ModPlayer
    {
        // ========== 可调常数区 ==========
        /// <summary>每点防御转换的伤害加成（0.01f = 每点防御 +1% 伤害）</summary>
        public const float DefenseToDamageRatio = 0.01f;
        // ================================

        /// <summary>
        /// 是否启用超级输出。每帧由装备/buff写入true，ResetEffects自动重置为false。
        /// 脱装备/掉buff时效果立即消失。
        /// </summary>
        public bool SuperOutputActive;

        public override void ResetEffects()
        {
            SuperOutputActive = false;
        }

        public override void PostUpdateMiscEffects()
        {
            if (!SuperOutputActive)
                return;

            // 此时所有装备/护甲套装的防御加成已结算完毕
            int defense = Player.statDefense;

            if (defense <= 0)
                return;

            // 防御转全伤害加成
            Player.GetDamage(DamageClass.Generic) += defense * DefenseToDamageRatio;

            // 防御清零（只清 statDefense，endurance 与其他减伤不受影响）
            Player.statDefense -= defense;
        }
    }
}
