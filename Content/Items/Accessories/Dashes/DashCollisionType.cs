namespace TestMod.Content.Items.Accessories.Dashes
{
	public enum DashCollisionType
	{
		// 纯位移，不检测碰撞伤害
		NoCollision,
		// 盾击：碰到首个 NPC 后调 OnHitNPC 回调并立即停止冲刺
		ShieldSlam,
		// 判定框：每帧主动扩展 hitbox，可命中多个 NPC（LongDash 原有风格）
		ContactHitbox,
	}
}
