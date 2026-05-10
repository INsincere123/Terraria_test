using System;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using TestMod.Projectiles.Melee;

namespace TestMod.Items.Weapons
{
    // 剑气·裂斩 —— 命中敌人后留下剑气来回切割，重复命中叠层强化
    // 贴图复用 5005 泰拉棱镜（TerraPrism）
    public class SwordQiSword : ModItem
    {
        public override string Texture => "Terraria/Images/Item_5005";

        // ── 数值调节区 ────────────────────────────────────────────────
        public static float QiDamageRatio = 0.45f; // 剑气基础伤害 = 剑基础伤害 × 此值
        public static int   QiDuration    = 60 * 8;   // 剑气持续帧数（60帧 = 1秒）
        public static int   MaxStacks     = 100;   // 叠层上限
        // ─────────────────────────────────────────────────────────────

        public override void SetDefaults()
        {
            Item.damage       = 147;
            Item.DamageType   = DamageClass.Melee;
            Item.width        = 40;
            Item.height       = 40;
            Item.useTime      = 17;
            Item.useAnimation = 17;
            Item.useStyle     = ItemUseStyleID.Swing;
            Item.knockBack    = 7f;
            Item.crit         = 8;
            Item.scale        = 1.73f; // 挥砍 hitbox 与贴图同步放大（1.0 = 原始大小）
            Item.value        = Item.sellPrice(gold: 65);
            Item.rare         = ItemRarityID.Pink;
            Item.autoReuse    = true;
            Item.useTurn      = true;
        }

        public override bool? UseItem(Player player)
        {
            // autoReuse 下引擎不会在每次连击时重新判断朝向，手动强制朝鼠标方向
            player.ChangeDir(Main.MouseWorld.X > player.Center.X ? 1 : -1);
            return null;
        }

        public override void OnHitNPC(Player player, NPC target, NPC.HitInfo hit, int damageDone)
        {
            int qiType   = ModContent.ProjectileType<SwordQiProjectile>();
            int firstIdx = -1;
            int qiCount  = 0;

            for (int i = 0; i < Main.maxProjectiles; i++)
            {
                Projectile p = Main.projectile[i];
                if (!p.active || p.owner != player.whoAmI || p.type != qiType) continue;
                if ((int)p.ai[0] != target.whoAmI) continue;

                p.timeLeft = QiDuration; // 刷新该目标上所有剑气的持续时间
                if (firstIdx < 0) firstIdx = i;
                qiCount++;
            }

            if (firstIdx >= 0)
            {
                Projectile main = Main.projectile[firstIdx];
                main.localAI[0] = Math.Min(main.localAI[0] + 1f, MaxStacks);

                // 叠到 100 层且目前只有一个剑气：生成第二个，相位偏移 π（对侧摆动）
                if (main.localAI[0] >= MaxStacks && qiCount == 1)
                {
                    int qiDamage = Math.Max(1, (int)(player.HeldItem.damage * QiDamageRatio));
                    int newIdx   = Projectile.NewProjectile(
                        player.GetSource_ItemUse(player.HeldItem),
                        target.Center, Vector2.Zero,
                        qiType, qiDamage, 0f,
                        player.whoAmI, target.whoAmI,
                        MathHelper.Pi / SwordQiProjectile.OscillationSpeed); // ai[1] 初始值，产生 π 相位差
                    if (newIdx >= 0 && newIdx < Main.maxProjectiles)
                        Main.projectile[newIdx].localAI[0] = MaxStacks; // 同步叠层数以保持颜色一致
                }
                return;
            }

            // 目标上无剑气：生成第一个
            int dmg = Math.Max(1, (int)(player.HeldItem.damage * QiDamageRatio));
            Projectile.NewProjectile(
                player.GetSource_ItemUse(player.HeldItem),
                target.Center, Vector2.Zero,
                qiType, dmg, 0f,
                player.whoAmI, target.whoAmI);
        }

        public override void AddRecipes()
        {
            CreateRecipe()
                .AddIngredient(ItemID.FairyQueenTrophy, 2)
                .Register();
        }
    }
}
