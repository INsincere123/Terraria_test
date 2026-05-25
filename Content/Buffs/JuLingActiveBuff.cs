using Terraria;
using Terraria.ModLoader;
using TestMod.Common.Mechanics.AccessoryEffects;

namespace TestMod.Content.Buffs
{
    /// <summary>
    /// 聚灵·灵力凝聚 主动 Buff。
    /// 持续期间：法力消耗×2、魔法伤害+66%、魔法暴击+22%，并激活暴击吸血（LifeRegen 模式）。
    /// 计时显示剩余持续时间，到期后自动消失。
    /// </summary>
    public class JuLingActiveBuff : ModBuff
    {
        // 复用原版 Buff 46（法力再生）贴图，魔法主题契合
        public override string Texture => "Terraria/Images/Buff_46";

        // ── 数值调节区 ────────────────────────────────────────────────
        private const float MagicDamageBoost  = 0.66f; // +66% 魔法伤害
        private const int   MagicCritBoost    = 22;    // +22% 魔法暴击
        private const float ManaCostIncrease  = 1.0f;  // 魔力消耗翻倍（+100%）

        private const float LifestealRate     = 0.03f; // 吸血：伤害的 3%
        private const int   LifestealCap      = 50;    // 单次吸血上限
        // ─────────────────────────────────────────────────────────────

        private static readonly LifestealConfig ActiveLifestealConfig = LifestealConfig.Default with
        {
            HitDamageRatio = LifestealRate,
            HealCap        = LifestealCap,
            CritOnly       = true,                     // 仅暴击触发
            HealType       = LifestealHealType.LifeRegen,
            // CooldownTicks = 0（无触发冷却，默认值）
        };

        public override void SetStaticDefaults()
        {
            Main.buffNoSave[Type] = true;  // 退出存档不保留
        }

        public override void Update(Player player, ref int buffIndex)
        {
            player.GetDamage(DamageClass.Magic)    += MagicDamageBoost;
            player.GetCritChance(DamageClass.Magic) += MagicCritBoost;
            player.manaCost                         += ManaCostIncrease;
            LifestealEffect.Apply(player, ActiveLifestealConfig);
        }
    }
}
