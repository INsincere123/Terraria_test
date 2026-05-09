using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using TestMod.Projectiles.Accessories;

namespace TestMod.Items.Accessories
{
    public class TestHook : ModItem
    {
        // ── 调参区 ────────────────────────────────────────────────────
        public const float LaunchSpeed   = 25f; // 飞出速度（px/帧）
        public const float Reach         = 40f; // 射程（格，×16 = px）
        public const float PullSpeed     = 24f; // 拉人速度（px/帧）
        public const float ReelbackSpeed = 28f; // 缩回速度（px/帧）
        public const int   MaxHooks      = 1;   // 同时存在的最大钩数
        // ─────────────────────────────────────────────────────────────

        // 复用圣诞钩贴图（ItemID 1916）
        public override string Texture => "Terraria/Images/Item_1916";

        public override void SetDefaults()
        {
            Item.CloneDefaults(ItemID.ChristmasHook);
            Item.shoot      = ModContent.ProjectileType<TestHookProj>();
            Item.shootSpeed = LaunchSpeed;
        }
    }
}
