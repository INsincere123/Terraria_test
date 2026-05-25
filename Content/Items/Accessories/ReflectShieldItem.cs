using Terraria;
using Terraria.ModLoader;
using TestMod.Content.Items.Accessories.Effects;

namespace TestMod.Content.Items.Accessories
{
    // 反射护盾·反弹型 —— 拦截敌方射弹并反转方向射回，伤害倍增
    // 贴图复用 4760 中士联盾（BouncingShield）
    public class ReflectShieldItem : ModItem
    {
        public override string Texture => "Terraria/Images/Item_4760";

        // ── 数值调节区 ────────────────────────────────────────────────
        public static int   OrbCount          = 6;    // 环绕球数量
        public static float OrbRadius         = 110f; // 轨道半径（px，16px = 1格）
        public static float RotationSpeed     = 0.025f;// 旋转速度（rad/帧，0.025 ≈ 1.4°/帧）
        public static int   OrbHP             = 350;  // 每颗球耐久（按原版公式扣减，高防高免伤更耐打）
        public static int   RespawnCooldown   = 300;  // 球破碎后的重生冷却（帧，60帧 = 1秒）
        public static float ReflectDamageMult = 1.5f; // 反弹后射弹伤害倍率（1.0 = 原伤害，1.5 = 伤害×1.5）
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
            ReflectShieldEffect.Apply(player, new ReflectShieldConfig
            {
                OrbCount          = OrbCount,
                OrbRadius         = OrbRadius,
                RotationSpeed     = RotationSpeed,
                OrbHP             = OrbHP,
                RespawnCooldown   = RespawnCooldown,
                ShouldReflect     = true,
                ReflectDamageMult = ReflectDamageMult,
            });
        }
    }
}
