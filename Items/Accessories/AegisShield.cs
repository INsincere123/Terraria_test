using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;
using TestMod.Items.Accessories.Dashes;

namespace TestMod.Items.Accessories
{
	// ============================================================================
	//  AegisShield  ——  照搬灾厄 Asgardian Aegis 冲刺数值的盾击型饰品
	// ----------------------------------------------------------------------------
	//  数值参考（灾厄 v2.1.2 AsgardianAegisDash）：
	//    速度      23.3 px/帧
	//    碰撞类型  ShieldSlam（撞第一个 NPC 立即停止）
	//    碰撞伤害  200（近战类型，乘玩家近战加成）
	//    击退      9
	//    无敌帧    命中 12 帧
	//    冷却      30 帧
	//  触发方式: 接管 vanilla 双击方向键（player.dashType = 1）
	// ============================================================================

	public class AegisShield : ModItem
	{
		// 直接复用盾类贴图
		//public override string Texture => "Terraria/Images/Item_1256"; // Paladin's Shield

		// ── 数值调节区 ────────────────────────────────────────────────────────
		private static readonly DashConfig DashCfg = new DashConfig
		{
			// 运动
			InitialSpeed         = 23.3f,   // px/帧
			Duration             = 25,       // 最大持续帧，通常 ShieldSlam 命中更早
			Cooldown             = 30,       // 帧
			ExitInertiaRatio     = 0f,       // 盾击后不保留冲刺速度
			DecelerationFactor   = 1f,
			DecelerationStartFrame = 0,
			MaxSpeed             = 0f,
			MinSpeedThreshold    = 0f,

			// 碰撞
			CollisionType  = DashCollisionType.ShieldSlam,
			BaseDamage     = 200,
			Knockback      = 9f,
			CritChance     = 0f,
			MaxHitsPerDash = 1,             // ShieldSlam 只打一个
			HitboxExpand   = 6,
			HitDmgClass    = DamageClass.Melee,

			// 无敌帧
			StartIFrames   = 10,
			ActiveIFrames  = 8,
			ContactIFrames = 12,            // AsgardianAegis.ShieldSlamIFrames

			// 命中附加
			OnHitBuffType     = 0,
			OnHitBuffDuration = 0,

			// 视觉（冰蓝 + 洋红，灾厄配色）
			TrailDustType   = DustID.IceTorch,
			TrailDustPerFrame = 3,
			TrailDustScale  = 1.4f,
			BurstDustType   = DustID.IceTorch,
			BurstDustCount  = 30,
			StartSound      = SoundID.Item24,
			HitSound        = SoundID.Item14,
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
			// dashType = 0：与灾厄相同。
			// 置 0 后 DashMovement() 在 dash==0 处提前退出，不再执行固定速度/dashDelay 逻辑。
			// HelpfulHotkeys 兼容性由 PostUpdateRunSpeeds 里检测 Player.dashTime > 0 保证
			// （HH 在 SetControls 直接写 dashTime=±15，不依赖 dashType 的值）。
			player.dashType = 0;
		}
	}
}
