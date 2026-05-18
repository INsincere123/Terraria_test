using Terraria.Audio;
using Terraria.ModLoader;

namespace TestMod.Items.Accessories.Dashes
{
	// ============================================================================
	//  DashConfig  ——  StandardDash 的全参数结构体
	// ----------------------------------------------------------------------------
	//  饰品在 UpdateAccessory 里把这个 struct 写入 DashPlayer.VanillaDashConfig。
	//  DashPlayer.TryStart 开始冲刺时将其拷贝到 DashPlayer.ActiveConfig，
	//  整个冲刺过程中 StandardDash 从 ActiveConfig 读取参数。
	// ============================================================================

	public struct DashConfig
	{
		// ── 运动 ─────────────────────────────────────────────────────────────────
		/// <summary>初始冲刺速度 (px/帧)</summary>
		public float InitialSpeed;

		/// <summary>最大持续帧数；StandardDash 内部也可提前结束（撞墙/速度过低/ShieldSlam命中）</summary>
		public int Duration;

		/// <summary>冷却帧数</summary>
		public int Cooldown;

		/// <summary>结束时保留的速度比例，注入 velocity 形成出场惯性 (0=不保留, 1=完全保留)</summary>
		public float ExitInertiaRatio;

		/// <summary>每帧速度衰减系数，从第 DecelerationStartFrame 帧开始乘 (1.0=匀速, 0.97=逐帧减速)</summary>
		public float DecelerationFactor;

		/// <summary>从第几帧开始启用衰减 (含该帧)</summary>
		public int DecelerationStartFrame;

		/// <summary>速度硬上限 px/帧 (0 = 不限)</summary>
		public float MaxSpeed;

		/// <summary>低于此速度时提前结束 (0 = 不检测)</summary>
		public float MinSpeedThreshold;

		// ── 碰撞 / 伤害 ──────────────────────────────────────────────────────────
		public DashCollisionType CollisionType;

		/// <summary>碰撞伤害基数（乘以玩家近战/对应伤害加成后才是最终伤害）</summary>
		public int BaseDamage;

		public float Knockback;

		/// <summary>暴击概率 0–1 (0 = 不暴击)</summary>
		public float CritChance;

		/// <summary>单次冲刺最多命中 NPC 数 (ShieldSlam 通常填 1)</summary>
		public int MaxHitsPerDash;

		/// <summary>碰撞 hitbox 向四周扩展像素数</summary>
		public int HitboxExpand;

		/// <summary>伤害类型，影响玩家加成的读取</summary>
		public DamageClass HitDmgClass;

		// ── 无敌帧 ───────────────────────────────────────────────────────────────
		/// <summary>冲刺开始瞬间给玩家的无敌帧</summary>
		public int StartIFrames;

		/// <summary>冲刺持续中每帧维持的最低无敌帧（比此值低才刷新）</summary>
		public int ActiveIFrames;

		/// <summary>命中 NPC 后给玩家的无敌帧</summary>
		public int ContactIFrames;

		// ── 命中附加效果 ──────────────────────────────────────────────────────────
		/// <summary>命中时对 NPC 施加的 buff 类型 ID (0 = 无)</summary>
		public int OnHitBuffType;

		/// <summary>命中 buff 持续帧数</summary>
		public int OnHitBuffDuration;

		// ── 视觉 ─────────────────────────────────────────────────────────────────
		/// <summary>拖尾粒子类型（DustID）</summary>
		public int TrailDustType;

		/// <summary>每帧拖尾粒子数</summary>
		public int TrailDustPerFrame;

		/// <summary>拖尾粒子缩放</summary>
		public float TrailDustScale;

		/// <summary>起始爆裂粒子类型（DustID）；命中时也用这个类型</summary>
		public int BurstDustType;

		/// <summary>起始爆裂粒子数量</summary>
		public int BurstDustCount;

		/// <summary>起始音效（null = 无）</summary>
		public SoundStyle? StartSound;

		/// <summary>命中音效（null = 无）</summary>
		public SoundStyle? HitSound;
	}
}
