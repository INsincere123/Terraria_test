using Terraria.ModLoader;

namespace TestMod.Common.Mechanics.Dashes
{
	// 命中 NPC 时传递给 OnHitNPC 钩子的上下文，StandardDash 从 DashConfig 填入初始值，
	// 子类可在 OnHitNPC 里覆盖任意字段实现特殊效果。
	public struct DashHitContext
	{
		public int        BaseDamage;
		public float      Knockback;
		public DamageClass DmgClass;
		// 命中后给玩家的无敌帧
		public int        PlayerImmunityFrames;
		public int        HitDirection;
	}
}
