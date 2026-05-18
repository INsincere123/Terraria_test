using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace TestMod.Common.GlobalItems
{
    /// <summary>
    /// godMode 模式下的武器伤害与击退倍率。
    /// 所有修正均在 IsGodMode 为 true 时才生效。
    /// </summary>
    public partial class GlobalItem
    {
        // ══════════════════════════════════════════════════════════════
        //   ModifyWeaponDamage — godMode 伤害倍率
        // ══════════════════════════════════════════════════════════════
        public override void ModifyWeaponDamage(Item item, Player player, ref StatModifier damage)
        {
            if (!IsGodMode(player)) return;

            // 🏹 幻影弓 ×3.6
            if (item.type == ItemID.Phantasm)
                damage *= 3.6f;

            // ☀️ 破晓之光 ×14
            else if (item.type == ItemID.DayBreak)
                damage *= 14f;

            // 🌌 星云烈焰 ×1.8
            else if (item.type == ItemID.NebulaBlaze)
                damage *= 1.8f;

            // 🌟 召唤法杖
            else if (item.DamageType == DamageClass.Summon)
            {
                if      (item.type == ItemID.StardustDragonStaff)  damage *= 11f;
                else if (item.type == ItemID.StardustCellStaff)    damage *= 18f;
                else if (item.type == ItemID.MoonlordTurretStaff)  damage *= 15f;
                else if (item.type == ItemID.RainbowCrystalStaff)  damage *= 15f;
                else if (item.type == ItemID.EmpressBlade)         damage *= 15f;
            }

            // 🪢 万花筒 ×6.3
            else if (item.type == ItemID.RainbowWhip)
                damage *= 6.3f;
        }

        // ══════════════════════════════════════════════════════════════
        //   ModifyWeaponKnockback — godMode 击退倍率
        // ══════════════════════════════════════════════════════════════
        public override void ModifyWeaponKnockback(Item item, Player player, ref StatModifier knockback)
        {
            if (!IsGodMode(player)) return;

            if      (item.type == ItemID.DayBreak)             knockback *= 2f;
            else if (item.type == ItemID.StardustCellStaff)    knockback *= 2f;
            else if (item.type == ItemID.MoonlordTurretStaff)  knockback *= 5f;
            else if (item.type == ItemID.RainbowCrystalStaff)  knockback *= 2f;
            else if (item.type == ItemID.EmpressBlade)         knockback *= 2f;
        }
    }
}
