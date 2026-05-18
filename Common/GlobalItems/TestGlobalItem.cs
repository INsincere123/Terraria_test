using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.DataStructures;
using Microsoft.Xna.Framework;
using TestMod.Rarities;
using TestMod.Common.Players;
using TestMod.Items.DamageTypes;
using TestMod.Projectiles.Ranged;

namespace TestMod.Common.GlobalItems
{
    /// <summary>
    /// 全局物品钩子：核心入口。
    /// 各关注点拆分到 partial 文件：
    ///   TestGlobalItem.GodMode.cs  — godMode 伤害/击退倍率
    ///   TestGlobalItem.Prefix.cs   — 前缀权重池
    ///   TestGlobalItem.BloodFeed.cs — 暴走/虚弱攻速
    /// </summary>
    public partial class TestGlobalItem : GlobalItem
    {
        // ══════════════════════════════════════════════════════════════
        //   内部工具
        // ══════════════════════════════════════════════════════════════

        /// <summary>当前玩家是否处于 godMode 状态。</summary>
        private static bool IsGodMode(Player player) =>
            player?.active == true && player.GetModPlayer<GodModePlayer>().GodModeBuff;

        // ══════════════════════════════════════════════════════════════
        //   SetDefaults — 固定数值调整（不受 godMode 开关影响）
        // ══════════════════════════════════════════════════════════════
        public override void SetDefaults(Item item)
        {
            // 紫晶钩（ID 1236）— 飞出速度沿用 Bobbit Hook
            if (item.type == ItemID.AmethystHook)
                item.shootSpeed = 25f;

            // 沙漠虎杖：提升基础数值
            if (item.type == ItemID.StormTigerStaff)
            {
                item.damage       = 51;
                item.knockBack    = 10;
                item.useTime      = 20;
                item.useAnimation = 20;
            }

            // 阿比盖尔之花：提升基础数值
            if (item.type == ItemID.AbigailsFlower)
            {
                item.damage       = 14;
                item.knockBack    = 2;
                item.mana         = 0;
                item.useTime      = 20;
                item.useAnimation = 20;
            }
        }

        // ══════════════════════════════════════════════════════════════
        //   PreDrawTooltipLine — 物品名称行（稀有度） + 伤害行（职业特效）
        // ══════════════════════════════════════════════════════════════
        public override bool PreDrawTooltipLine(Item item, DrawableTooltipLine line, ref int yOffset)
        {
            if (line.Mod != "Terraria") return true;

            // ── 物品名称行：自定义稀有度效果 ─────────────────────────
            if (line.Name == "ItemName")
            {
                if (item.rare == ModContent.RarityType<AntaresRarity>())
                {
                    AntaresRarity.Draw(item, line);
                    return false;
                }
                return true;
            }

            // ── 伤害行：各职业动态特效 ────────────────────────────────
            if (line.Name == "Damage")
            {
                var    sb   = Main.spriteBatch;
                float  time = Main.GlobalTimeWrappedHourly;
                var    r    = line.Rotation;
                var    o    = line.Origin;
                var    s    = line.BaseScale;
                int    x    = line.X, y = line.Y;
                string txt  = line.Text;

                DamageClass dmg = item.DamageType;
                if      (dmg == TrueDamageClass.Instance)
                    DamageLineRenderer.DrawTrue  (sb, txt, x, y, r, o, s, time);
                else if (dmg == DamageClass.Melee || dmg == DamageClass.MeleeNoSpeed)
                    DamageLineRenderer.DrawMelee (sb, txt, x, y, r, o, s, time);
                else if (dmg == DamageClass.Ranged)
                    DamageLineRenderer.DrawRanged(sb, txt, x, y, r, o, s, time);
                else if (dmg == DamageClass.Magic)
                    DamageLineRenderer.DrawMagic (sb, txt, x, y, r, o, s, time);
                else if (dmg == DamageClass.Summon || dmg == DamageClass.SummonMeleeSpeed)
                    DamageLineRenderer.DrawSummon(sb, txt, x, y, r, o, s, time);
                else
                    return true; // 其他类型保持默认

                return false;
            }

            return true;
        }

        // ══════════════════════════════════════════════════════════════
        //   ModifyHitNPC — 真实伤害无视防御 + 暴走无视防御
        // ══════════════════════════════════════════════════════════════
        public override void ModifyHitNPC(Item item, Player player, NPC target, ref NPC.HitModifiers modifiers)
        {
            // 真实伤害：100% 穿甲
            if (item.DamageType == TrueDamageClass.Instance)
                modifiers.ScalingArmorPenetration += 1f;

            // 暴走期间所有近战武器无视防御
            if (player.GetModPlayer<BloodFeedPlayer>().IsBerserk)
                modifiers.ScalingArmorPenetration += 1f;
        }

        // ══════════════════════════════════════════════════════════════
        //   Shoot — 幻影弓：把箭矢替换为自定义强化弹射物（holdout 模式）
        // ══════════════════════════════════════════════════════════════
        public override bool Shoot(Item item, Player player, EntitySource_ItemUse_WithAmmo source,
            Vector2 position, Vector2 velocity, int type, int damage, float knockback)
        {
            if (item.type != ItemID.Phantasm || !IsGodMode(player))
                return true;

            // 只生成弓体 holdout，由它负责发箭；重复触发时不重复生成
            if (player.ownedProjectileCounts[ModContent.ProjectileType<PhantasmHoldout>()] <= 0)
            {
                Projectile.NewProjectile(source, position, velocity,
                    ModContent.ProjectileType<PhantasmHoldout>(),
                    damage, knockback, player.whoAmI);
            }
            return false;
        }
    }
}
