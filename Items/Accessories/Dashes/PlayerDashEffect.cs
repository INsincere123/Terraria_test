using Terraria;
using Terraria.ModLoader;

namespace TestMod.Items.Accessories.Dashes
{
	// ============================================================================
	//  PlayerDashEffect  ——  灾厄风格的 dash 抽象基类
	// ----------------------------------------------------------------------------
	//  每一种自定义冲刺都继承自这个类, 实现 ID 和各个生命周期方法。
	//  PlayerDashManager 通过反射加载所有子类, 用 ID 字符串注册到字典里供查找。
	//
	//  生命周期:
	//    1. CanUseDash(player)       —— 玩家是否满足发动条件 (装备/状态/资源等)
	//    2. OnDashStart(player, dir) —— 冲刺刚开始的那一帧 (初始化粒子/音效/速度)
	//    3. OnDashEffects(player)    —— 冲刺持续中, 每帧调用 (位移/伤害/视觉)
	//    4. OnDashEnd(player)        —— 冲刺结束的最后一帧 (清理状态)
	//
	//  冲刺时长由 DashDuration 决定 (帧数), 冷却由 DashCooldown 决定。
	// ============================================================================

	public abstract class PlayerDashEffect
	{
		/// <summary>冲刺的唯一字符串 ID, 用作字典 key</summary>
		public abstract string Id { get; }

		/// <summary>冲刺总持续帧数 (执行 OnDashEffects 的次数)</summary>
		public virtual int DashDuration => 22;

		/// <summary>冲刺结束后的冷却帧数 (期间无法再发动)</summary>
		public virtual int DashCooldown => 30;

		/// <summary>运行时最大持续帧数；默认返回 DashDuration，子类可结合 player 状态动态调整</summary>
		public virtual int GetMaxDuration(Player player) => DashDuration;

		/// <summary>运行时冷却帧数；默认返回 DashCooldown，StandardDash 覆盖以读取 DashConfig</summary>
		public virtual int GetCooldown(Player player) => DashCooldown;

		/// <summary>是否允许垂直冲刺 (上下双击)</summary>
		public virtual bool AllowVerticalDash => false;

		/// <summary>玩家当前是否能发动这个冲刺 (检查饰品/状态/资源)</summary>
		public abstract bool CanUseDash(Player player);

		/// <summary>冲刺刚开始那一帧调用 (设置初始速度/起始无敌帧/音效/起始粒子等)</summary>
		public virtual void OnDashStart(Player player, int direction) { }

		/// <summary>冲刺持续中每帧调用 (核心位移逻辑/拖尾粒子/碰撞伤害)</summary>
		/// <param name="player">玩家</param>
		/// <param name="direction">水平方向 (-1左, +1右, 0垂直冲刺时为 0)</param>
		/// <param name="verticalDirection">垂直方向 (-1上, +1下, 0非垂直冲刺)</param>
		/// <param name="frameCount">已经执行了几帧 (从 0 开始)</param>
		public virtual void OnDashEffects(Player player, int direction, int verticalDirection, int frameCount) { }

		/// <summary>冲刺结束那一帧调用 (清理状态/落地处理)</summary>
		public virtual void OnDashEnd(Player player) { }
	}
}
