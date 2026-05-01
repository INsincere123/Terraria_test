using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace TestMod.Buffs
{
	// ============================================================================
	//  GravityNormalizerBuff  ——  重力正常化 Buff
	// ----------------------------------------------------------------------------
	//  效果: 无视太空低重力环境, 保持正常重力 (参考灾厄 GravityNormalizerPotion)
	//
	//  原理:
	//    vanilla 在 Player.Update 第 20465 行执行 gravity *= num5
	//    num5 基于玩家 Y 坐标: 越靠近太空顶部越小 (0.25 ~ 1.0)
	//    这会让太空中重力最低降至 defaultGravity * 0.25
	//
	//    PostUpdateMiscEffects (第 21657 行) 在此计算之后运行,
	//    直接把 gravity 恢复为 defaultGravity 即可完全覆盖太空的缩减效果。
	// ============================================================================

	public class GravityNormalizerBuff : ModBuff
	{
		public override void SetStaticDefaults()
		{
			// 不显示倒计时 (常驻 buff 不需要显示剩余时间)
			Main.buffNoTimeDisplay[Type] = true;
		}



		public override void Update(Player player, ref int buffIndex)
		{
			// 在 buff 更新阶段标记玩家的 ModPlayer
			player.GetModPlayer<GravityNormalizerPlayer>().HasGravityNormalizer = true;
		}
	}

	// ============================================================================
	//  GravityNormalizerPlayer  ——  重力正常化效果的 ModPlayer
	// ============================================================================

	public class GravityNormalizerPlayer : ModPlayer
	{
		public bool HasGravityNormalizer;

		public override void ResetEffects()
		{
			HasGravityNormalizer = false;
		}

		public override void PostUpdateMiscEffects()
		{
			if (!HasGravityNormalizer) return;

			// vanilla 太空重力缩减在第 20465 行: gravity *= num5 (0.25~1.0)
			// 这里在那之后把重力强制恢复为默认值, 完全消除太空低重力效果
			if (Player.gravity < Player.defaultGravity)
				Player.gravity = Player.defaultGravity;
		}
	}
}
