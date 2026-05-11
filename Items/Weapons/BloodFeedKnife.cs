using System;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;
using TestMod.Common.Players;
using TestMod.Items.DamageTypes;
using TestMod.Projectiles.Melee;

namespace TestMod.Items.Weapons
{
    // 血饲匕首 —— 消耗生命值投掷追踪匕首，伤害随失血比例非线性增强
    // 贴图复用 3054 暗影焰刀（ShadowFlameKnife）
    // 弹幕复用 497 暗影焰刀弹幕（ShadowFlameKnifeProj）贴图
    public class BloodFeedKnife : ModItem
    {
        public override string Texture => "Terraria/Images/Item_3054";

        // ── 数值调节区 ────────────────────────────────────────────────
        public static int   BaseDamage      = 97;    // 基础伤害（真实伤害，无视防御）
        public static float ShootSpeed      = 22f;   // 弹幕初速度
        public static int   HpCostFlat      = 7;    // 每次投掷固定消耗 HP
        public static float HpCostRatio     = 0.02f; // 每次投掷额外消耗当前最大HP × 此值
        // ─────────────────────────────────────────────────────────────

        public override void SetDefaults()
        {
            Item.damage       = BaseDamage;
            Item.DamageType   = TrueDamageClass.Instance;
            Item.width        = 32;
            Item.height       = 32;
            Item.useTime      = 17;
            Item.useAnimation = 17;
            Item.useStyle     = ItemUseStyleID.Swing;
            Item.knockBack    = 3f;
            Item.crit         = 8;
            Item.noMelee      = true;  // 物品本身不造成近战判定，由弹幕打伤害
            Item.shoot        = ModContent.ProjectileType<BloodFeedKnifeProj>();
            Item.shootSpeed   = ShootSpeed;
            Item.value        = Item.sellPrice(gold: 20);
            Item.rare         = ItemRarityID.Pink;
            Item.autoReuse    = true;
        }

        public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source,
            Vector2 position, Vector2 velocity, int type, int damage, float knockback)
        {
            var mp = player.GetModPlayer<BloodFeedPlayer>();

            // 暴走期间由 BloodFeedPlayer.PostUpdateEquips 统一按秒扣血，武器不额外扣
            if (!mp.IsBerserk)
            {
                // ── 消耗生命值（固定 + 比例，两者取最大值避免高HP玩家占便宜）───
                int cost = Math.Max(HpCostFlat, (int)(player.statLifeMax2 * HpCostRatio));
                cost = Math.Min(cost, player.statLife - 1); // 至少留 1 HP，不致命

                if (cost > 0)
                {
                    player.statLife -= cost;
                    player.HealEffect(-cost, true);
                    NetMessage.SendData(MessageID.PlayerLifeMana, -1, -1, null, player.whoAmI);
                }
            }

            // ── 检查是否应进入暴走 ─────────────────────────────────────
            mp.TriggerBerserkCheck();

            return true;
        }

        public override void AddRecipes()
        {
            CreateRecipe()
                .AddIngredient(ItemID.DirtBlock, 1)
                .Register();
        }
    }
}
