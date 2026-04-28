using System;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ModLoader;

namespace 武器test.Items.Accessories.Dashes
{
	// ============================================================================
	//  DashPlayer  ——  冲刺生命周期驱动器 (键位触发版)
	// ----------------------------------------------------------------------------
	//  不再使用双击触发 (避免和原版/其他 mod 冲突)。
	//  改用 ModKeybind, 玩家可在游戏 "设置 → 控件" 中绑定喜欢的键。
	//
	//  冲刺方向计算优先级:
	//    1. 玩家正在按方向键 -> 取按住的方向
	//    2. 玩家正在移动     -> 取速度方向
	//    3. 都没有           -> 取角色当前朝向 (Player.direction)
	//
	//  冲刺中保留所有惯性:
	//    旧版本会强制把 velocity 清零, 导致冲刺中下落/上升被取消, 冲刺结束后水平也是静止。
	//    新版本仅在 PreUpdateMovement 里 "额外" 加位移 (position += dashStep), 不动 velocity。
	//    冲刺结束时给一个"出场惯性" (velocity.X += 出场速度), 玩家滑行一段后自然停下。
	//
	//  发动方式 - 饰品在 UpdateAccessory 里:
	//      player.GetModPlayer<DashPlayer>().ActiveDashId = "OmniguardianDash";
	// ============================================================================

	public class DashPlayer : ModPlayer
	{
		/// <summary>本帧由饰品设置的 dash ID。每帧 ResetEffects 重置</summary>
		public string ActiveDashId;

		/// <summary>当前正在执行的 dash 效果</summary>
		public PlayerDashEffect CurrentDash;

		public int DashFrame;
		public int DashDirX;
		public int DashDirY;
		public int CooldownTimer;

		public bool IsDashing => CurrentDash != null && DashFrame < CurrentDash.DashDuration;

		public override void ResetEffects()
		{
			ActiveDashId = null;
		}

		// =================================================
		// PreUpdate: 监听键位 + 冷却倒数
		// =================================================
		public override void PreUpdate()
		{
			if (CooldownTimer > 0) CooldownTimer--;

			if (string.IsNullOrEmpty(ActiveDashId)) return;
			if (CurrentDash != null) return;
			if (CooldownTimer > 0) return;

			// 必须是本地玩家 (键位检测在客户端)
			if (Player.whoAmI != Main.myPlayer) return;

			// 键位是否刚按下
			if (OmniKeybinds.DashKey == null) return;
			if (!OmniKeybinds.DashKey.JustPressed) return;

			PlayerDashEffect effect = PlayerDashManager.FindById(ActiveDashId);
			if (effect == null || !effect.CanUseDash(Player)) return;

			// 决定冲刺方向
			(int dirX, int dirY) = ResolveDashDirection(effect);
			if (dirX == 0 && dirY == 0) return; // 无法决定方向, 不触发

			TryStart(effect, dirX, dirY);
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

		private void TryStart(PlayerDashEffect effect, int dirX, int dirY)
		{
			CurrentDash = effect;
			DashFrame   = 0;
			DashDirX    = dirX;
			DashDirY    = dirY;

			// 取消 vanilla dash, 防止 DashMovement 干扰
			Player.dash      = 0;
			Player.dashType  = 0;
			Player.dashDelay = 0;

			effect.OnDashStart(Player, dirX != 0 ? dirX : dirY);
		}

		// =================================================
		// PreUpdateMovement: 冲刺执行 (vanilla 调 DryCollision 之前)
		// =================================================
		public override void PreUpdateMovement()
		{
			if (CurrentDash == null) return;

			CurrentDash.OnDashEffects(Player, DashDirX, DashDirY, DashFrame);
			DashFrame++;

			if (DashFrame >= CurrentDash.DashDuration)
			{
				CurrentDash.OnDashEnd(Player);
				CooldownTimer = CurrentDash.DashCooldown;
				CurrentDash   = null;
				DashFrame     = 0;
			}
		}

		/// <summary>主动结束当前冲刺 (撞墙时由 dash effect 调用)</summary>
		public void EndDash()
		{
			if (CurrentDash == null) return;

			CurrentDash.OnDashEnd(Player);
			CooldownTimer = CurrentDash.DashCooldown;
			CurrentDash   = null;
			DashFrame     = 0;
		}
	}
}
