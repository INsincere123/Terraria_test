using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;
using TestMod.Content.Items.Accessories.Dashes;

namespace TestMod.Content.Items.Accessories
{
	// ============================================================================
	//  VoidSash  ——  照搬灾厄 Statis Void Sash 冲刺数值的高速机动型饰品
	// ----------------------------------------------------------------------------
	//  数值参考（灾厄 v2.1.2 StatisVoidSashDash）：
	//    速度      64 px/帧（起步）
	//    碰撞类型  NoCollision（纯位移，无伤害）
	//    衰减      第 10 帧起每帧 ×0.97
	//    速度上限  140 px/帧
	//    冷却      30 帧
	//  触发方式: 接管 vanilla 双击方向键（player.dashType = 1）
	// ============================================================================

	public class VoidSash : ModItem
	{
		//public override string Texture => "Terraria/Images/Item_1850"; // Master Ninja Gear

		// ── 数值调节区 ────────────────────────────────────────────────────────
		private static readonly DashConfig DashCfg = new DashConfig
		{
			// 运动
			InitialSpeed           = 64f,    // px/帧
			Duration               = 20,     // 帧（约 75 格总距离）
			Cooldown               = 30,     // 帧
			ExitInertiaRatio       = 0f,     // 衰减自然减速，不需要额外惯性
			DecelerationFactor     = 0.97f,  // 每帧 ×0.97，从第 10 帧起
			DecelerationStartFrame = 10,
			MaxSpeed               = 140f,   // StatisVoidSash 原版速度上限
			MinSpeedThreshold      = 0f,

			// 碰撞（纯移动，无伤害）
			CollisionType  = DashCollisionType.NoCollision,
			BaseDamage     = 0,
			Knockback      = 0f,
			CritChance     = 0f,
			MaxHitsPerDash = 0,
			HitboxExpand   = 0,
			HitDmgClass    = DamageClass.Generic,

			// 无敌帧（虚空腰带原版无无敌帧）
			StartIFrames   = 0,
			ActiveIFrames  = 0,
			ContactIFrames = 0,

			// 命中附加（无）
			OnHitBuffType     = 0,
			OnHitBuffDuration = 0,

			// 视觉（暗紫 / 阴影火焰，对应原版暗紫配色）
			TrailDustType    = DustID.Shadowflame,
			TrailDustPerFrame = 4,
			TrailDustScale   = 1.2f,
			BurstDustType    = DustID.Shadowflame,
			BurstDustCount   = 20,
			StartSound       = SoundID.Item8,
			HitSound         = null,
		};
		// ─────────────────────────────────────────────────────────────────────

		public override void SetDefaults()
		{
			Item.width     = 28;
			Item.height    = 28;
			Item.accessory = true;
			Item.rare      = ItemRarityID.Purple;
			Item.value     = Item.sellPrice(gold: 12);
		}

		public override void UpdateAccessory(Player player, bool hideVisual)
		{
			DashPlayer dp = player.GetModPlayer<DashPlayer>();
			dp.VanillaDashEffectId = "StandardDash";
			dp.VanillaDashConfig   = DashCfg;
			player.dashType = 0; // 同上，见 AegisShield
		}
	}
}
