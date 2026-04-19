using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.DataStructures;
using Microsoft.Xna.Framework;

namespace 武器test
{
    /// <summary>
    /// 全局物品钩子：武器面板强化（伤害、击退、发射逻辑）
    /// </summary>
    public class MyGlobalItem : GlobalItem
    {
        // ══════════════════════════════════════════════════════════════
        //   SetDefaults — 固定数值调整（不受 godMode 开关影响）
        // ══════════════════════════════════════════════════════════════
        public override void SetDefaults(Item item)
        {
            // 沙漠虎杖：提升基础数值和攻速
            if (item.type == ItemID.StormTigerStaff)
            {
                item.damage = 55;
                item.knockBack = 8;
                item.useTime = 12;
                item.useAnimation = 12;
            }
        }

        /// <summary>神模开关检查</summary>
        private bool IsGodMode(Player player) =>
            player?.active == true && player.GetModPlayer<MyPlayer>().godModeBuff;

        // ══════════════════════════════════════════════════════════════
        //   ModifyWeaponDamage — 伤害倍率（godMode 开启时生效）
        // ══════════════════════════════════════════════════════════════
        public override void ModifyWeaponDamage(Item item, Player player, ref StatModifier damage)
        {
            if (!IsGodMode(player)) return;

            // 🏹 幻影弓 ×4
            if (item.type == ItemID.Phantasm)
                damage *= 4f;

            // ☀️ 破晓之光 ×12
            else if (item.type == ItemID.DayBreak)
                damage *= 12f;

            // 🌟 召唤法杖
            else if (item.DamageType == DamageClass.Summon)
            {
                if (item.type == ItemID.StardustDragonStaff) damage *= 11f;
                else if (item.type == ItemID.StardustCellStaff) damage *= 18f;
                else if (item.type == ItemID.MoonlordTurretStaff) damage *= 15f;
                else if (item.type == ItemID.RainbowCrystalStaff) damage *= 15f;
            }

            // ✨ 泰拉棱镜 ×15
            else if (item.type == ItemID.EmpressBlade)
                damage *= 15f;

            // 🪢 万花筒 ×13
            else if (item.type == ItemID.RainbowWhip)
                damage *= 13f;
        }

        // ══════════════════════════════════════════════════════════════
        //   ModifyWeaponKnockback — 击退倍率（godMode 开启时生效）
        // ══════════════════════════════════════════════════════════════
        public override void ModifyWeaponKnockback(Item item, Player player, ref StatModifier knockback)
        {
            if (!IsGodMode(player)) return;

            if (item.type == ItemID.DayBreak) knockback *= 2f;
            else if (item.type == ItemID.StardustCellStaff) knockback *= 2f;
            else if (item.type == ItemID.MoonlordTurretStaff) knockback *= 5f;
            else if (item.type == ItemID.RainbowCrystalStaff) knockback *= 2f;
            else if (item.type == ItemID.EmpressBlade) knockback *= 2f;
        }

        // ══════════════════════════════════════════════════════════════
        //   Shoot — 幻影弓：把所有箭矢转换为自定义强化弹射物
        //   参考灾厄 Monsoon 的做法，以玩家持握点为发射源
        //   生成2支扇形散射箭，各自独立追踪/破甲/分裂
        // ══════════════════════════════════════════════════════════════
        public override bool Shoot(Item item, Player player, EntitySource_ItemUse_WithAmmo source,
    Vector2 position, Vector2 velocity, int type, int damage, float knockback)
        {
            if (item.type != ItemID.Phantasm || !IsGodMode(player))
                return true;

            // 只生成弓体 holdout，由它负责发箭
            if (player.ownedProjectileCounts[ModContent.ProjectileType<Projectiles.PhantasmHoldout>()] <= 0)
            {
                Projectile.NewProjectile(source, position, velocity,
                    ModContent.ProjectileType<Projectiles.PhantasmHoldout>(),
                    damage, knockback, player.whoAmI);
            }
            return false;
        }
    }
}