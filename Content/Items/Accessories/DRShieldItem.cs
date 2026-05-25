using Terraria;
using Terraria.ModLoader;
using TestMod.Content.Items.Accessories.Effects;

namespace TestMod.Content.Items.Accessories
{
    // 贴图复用 4760 中士联盾（BouncingShield）
    public class DRShieldItem : ModItem
    {
        public override string Texture => "Terraria/Images/Item_4760";

        // ── 数值调节区 ────────────────────────────────────────────────
        public static int   OrbCount        = 6;     // 环绕球数量
        public static float OrbRadius       = 90f;   // 轨道半径（px，16px = 1格）
        public static float RotationSpeed   = 0.03f; // 旋转速度（rad/帧，0.03 ≈ 1.7°/帧）
        public static float DetectionRadius = 20f;   // 拦截半径（px，敌方射弹进入此范围即被标记）
        public static int   FlatDR          = 40;    // 固定伤害减免（被标记射弹命中时从伤害中直接扣除）
        public static float MultDR          = 0.15f; // 乘法伤害减免（0~1，叠加在固定减免之后，0.15 = 再减15%）
        public static int   OrbHP           = 350;   // 每颗球耐久（按原版公式扣减，高防高免伤更耐打）
        public static int   RespawnCooldown = 300;   // 球破碎后的重生冷却（帧，60帧 = 1秒）
        // ─────────────────────────────────────────────────────────────

        public override void SetDefaults()
        {
            Item.width     = 28;
            Item.height    = 30;
            Item.accessory = true;
            Item.rare      = Terraria.ID.ItemRarityID.Purple;
            Item.value     = Item.sellPrice(gold: 10);
        }

        public override void UpdateAccessory(Player player, bool hideVisual)
        {
            DRShieldEffect.Apply(player, new DRShieldConfig
            {
                OrbCount        = OrbCount,
                OrbRadius       = OrbRadius,
                RotationSpeed   = RotationSpeed,
                DetectionRadius = DetectionRadius,
                FlatDR          = FlatDR,
                MultDR          = MultDR,
                OrbHP           = OrbHP,
                RespawnCooldown = RespawnCooldown,
            });
        }
    }
}
