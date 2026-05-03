using System;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ModLoader;
using TestMod.Common.Systems;

namespace TestMod.Items.Accessories.Dashes
{
	// ============================================================================
	//  DashPlayer  ——  冲刺生命周期驱动器 (双键位版)
	// ----------------------------------------------------------------------------
	//  支持两个独立按键触发两种冲刺:
	//    DashKey   -> ActiveDashId   (长冲刺 / 进攻冲刺)
	//    BlinkKey  -> ActiveBlinkId  (短冲刺 / 走位闪现)
	//
	//  两个冲刺共用同一个执行槽 (CurrentDash), 同一时间只能跑一个。
	//  各自拥有独立冷却 (CooldownTimer / BlinkCooldownTimer)。
	//
	//  冲刺方向计算优先级:
	//    1. 玩家正在按方向键 -> 取按住的方向
	//    2. 玩家正在移动     -> 取速度方向
	//    3. 都没有           -> 取角色当前朝向 (Player.direction)
	//
	//  发动方式 - 饰品在 UpdateAccessory 里:
	//      player.GetModPlayer<DashPlayer>().ActiveDashId  = "LongDash";
	//      player.GetModPlayer<DashPlayer>().ActiveBlinkId = "ShortDash";
	// ============================================================================

	public class DashPlayer : ModPlayer
	{
		/// <summary>本帧由饰品设置的长冲刺 ID (DashKey 触发)。每帧 ResetEffects 重置</summary>
		public string ActiveDashId;

		/// <summary>本帧由饰品设置的短冲刺 ID (BlinkKey 触发)。每帧 ResetEffects 重置</summary>
		public string ActiveBlinkId;

		/// <summary>当前正在执行的 dash 效果 (长冲刺和短冲刺共用)</summary>
		public PlayerDashEffect CurrentDash;

		public int DashFrame;
		public int DashDirX;
		public int DashDirY;

		/// <summary>长冲刺冷却计时器</summary>
		public int CooldownTimer;

		/// <summary>短冲刺独立冷却计时器</summary>
		public int BlinkCooldownTimer;

		public bool IsDashing => CurrentDash != null && DashFrame < CurrentDash.DashDuration;

		public override void ResetEffects()
		{
			ActiveDashId  = null;
			ActiveBlinkId = null;
		}

		// =================================================
		// PreUpdate: 监听键位 + 冷却倒数
		// =================================================
		public override void PreUpdate()
		{
			if (CooldownTimer      > 0) CooldownTimer--;
			if (BlinkCooldownTimer > 0) BlinkCooldownTimer--;

			// 必须是本地玩家 (键位检测在客户端)
			if (Player.whoAmI != Main.myPlayer) return;

			// 已经在冲刺中, 不重复触发
			if (CurrentDash != null) return;

			// --- 优先检测长冲刺 (DashKey) ---
			if (!string.IsNullOrEmpty(ActiveDashId)
				&& CooldownTimer <= 0
				&& OmniKeybinds.DashKey != null
				&& OmniKeybinds.DashKey.JustPressed)
			{
				PlayerDashEffect effect = PlayerDashManager.FindById(ActiveDashId);
				if (effect != null && effect.CanUseDash(Player))
				{
					(int dirX, int dirY) = ResolveDashDirection(effect);
					if (dirX != 0 || dirY != 0)
					{
						TryStart(effect, dirX, dirY, isLong: true);
						return; // 本帧已触发长冲刺, 不再检测短冲刺
					}
				}
			}

			// --- 检测短冲刺 (BlinkKey) ---
			if (!string.IsNullOrEmpty(ActiveBlinkId)
				&& BlinkCooldownTimer <= 0
				&& OmniKeybinds.BlinkKey != null
				&& OmniKeybinds.BlinkKey.JustPressed)
			{
				PlayerDashEffect effect = PlayerDashManager.FindById(ActiveBlinkId);
				if (effect != null && effect.CanUseDash(Player))
				{
					(int dirX, int dirY) = ResolveDashDirection(effect);
					if (dirX != 0 || dirY != 0)
					{
						TryStart(effect, dirX, dirY, isLong: false);
					}
				}
			}
		}

		// =================================================
		// 冲刺方向决策
		// =================================================
		private (int, int) ResolveDashDirection(PlayerDashEffect effect)
		{
			int x = 0, y = 0;

			// 1. 优先使用按住的方向键
			if (Player.controlLeft)  x = -1;
			if (Player.controlRight) x = 1;
			if (effect.AllowVerticalDash)
			{
				if (Player.controlUp)   y = -1;
				if (Player.controlDown) y = 1;
			}

			// 同时按住时优先水平
			if (x != 0 && y != 0) y = 0;

			if (x != 0 || y != 0) return (x, y);

			// 2. 没按方向键 -> 用速度方向 (惯性方向)
			if (Math.Abs(Player.velocity.X) > 0.5f)
				return (Math.Sign(Player.velocity.X), 0);

			// 3. 退回角色朝向
			return (Player.direction, 0);
		}

		/// <param name="isLong">true = 长冲刺 (用 CooldownTimer); false = 短冲刺 (用 BlinkCooldownTimer)</param>
		private void TryStart(PlayerDashEffect effect, int dirX, int dirY, bool isLong)
		{
			CurrentDash = effect;
			DashFrame   = 0;
			DashDirX    = dirX;
			DashDirY    = dirY;

			// 标记这次冲刺属于哪个冷却槽, 结束时写回正确的计时器
			_currentDashIsLong = isLong;

			// 取消 vanilla dash, 防止 DashMovement 干扰
			Player.dash      = 0;
			Player.dashType  = 0;
			Player.dashDelay = 0;

			effect.OnDashStart(Player, dirX != 0 ? dirX : dirY);
		}

		// 记录当前冲刺是长冲刺还是短冲刺, 用于结束时写入正确冷却
		private bool _currentDashIsLong;

		// =================================================
		// PreUpdateMovement: 冲刺执行 (vanilla 调 DryCollision 之前)
		// =================================================
		public override void PreUpdateMovement()
		{
			if (CurrentDash == null) return;

			CurrentDash.OnDashEffects(Player, DashDirX, DashDirY, DashFrame);
			DashFrame++;

			// OnDashEffects 内部可能调用 EndDash() 提前结束 (如撞墙), 这会把 CurrentDash 设为 null
			if (CurrentDash == null) return;

			if (DashFrame >= CurrentDash.DashDuration)
			{
				FinishDash();
			}
		}

		/// <summary>主动结束当前冲刺 (撞墙时由 dash effect 调用)</summary>
		public void EndDash()
		{
			if (CurrentDash == null) return;
			FinishDash();
		}

		private void FinishDash()
		{
			CurrentDash.OnDashEnd(Player);

			// 写入对应的冷却计时器
			if (_currentDashIsLong)
				CooldownTimer      = CurrentDash.DashCooldown;
			else
				BlinkCooldownTimer = CurrentDash.DashCooldown;

			CurrentDash = null;
			DashFrame   = 0;
		}
	}
}
