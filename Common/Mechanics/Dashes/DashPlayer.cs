using System;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ModLoader;
using TestMod.Common.Systems;

namespace TestMod.Common.Mechanics.Dashes
{
	// ============================================================================
	//  DashPlayer  ——  冲刺生命周期驱动器
	// ----------------------------------------------------------------------------
	//  三条独立触发槽：
	//    DashKey  (Main)      → LongDashEffectId    长冲刺
	//    BlinkKey (Secondary) → ShortDashEffectId   短冲刺
	//    双击方向键 (Vanilla)  → VanillaDashEffectId  vanilla 兼容双击触发
	//
	//  Vanilla 槽：双击在 PostUpdateRunSpeeds 检测；单键在装备更新后优先分发。
	//    - 信号来源：controlRight && releaseRight（"松开后再次按下" = 新的按键动作）
	//    - 双击窗口：自有 _dashTimeMod（±15帧），不依赖 vanilla 的 DoCommonDashHandle
	//    - HelpfulHotkeys 兼容：检查 Player.dashTime > 0（HH 在 SetControls 直接写 dashTime=±15）
	//    - 饰品设 dashType=0（与灾厄相同），彻底阻止 vanilla DashMovement 执行速度/dashDelay 逻辑
	//    - 单键模式由 DashInputSystem 在装备更新后统一分发，不伪造移动输入。
	//
	//  饰品在 UpdateAccessory 里：
	//      dp.VanillaDashEffectId = "StandardDash";
	//      dp.VanillaDashConfig   = new DashConfig { ... };
	//      player.dashType        = 0;   // 防止 vanilla DashMovement 干扰
	// ============================================================================

	public partial class DashPlayer : ModPlayer
	{
		// ── 三槽注册（每帧 ResetEffects 清空，UpdateAccessory 重新写入） ─────────
		public string LongDashEffectId;
		public string ShortDashEffectId;
		public string VanillaDashEffectId;

		// ── Vanilla 配置 ───────────────────────────────────────────────────────
		public DashConfig VanillaDashConfig;

		// ── 执行状态 ───────────────────────────────────────────────────────────
		public PlayerDashEffect ActiveEffect;
		public DashConfig       ActiveConfig;
		public int ElapsedFrames;
		public int DirX;
		public int DirY;

		// ── 三槽冷却 ───────────────────────────────────────────────────────────
		public int LongDashCooldown;
		public int ShortDashCooldown;
		public int VanillaDashCooldown;

		// ── 命中追踪（存在 ModPlayer，多人安全） ──────────────────────────────
		public bool[] HitTargets = new bool[Main.maxNPCs];
		public int    HitCount;

		// 仅覆盖同步的冲刺命中调用；finally 恢复，普通攻击不受影响。
		internal bool IsResolvingContactDamage;

		public bool IsDashing => ActiveEffect != null && ElapsedFrames < _activeDashMaxDuration;

		// ── 私有状态 ───────────────────────────────────────────────────────────
		private enum DashSlot { Main, Secondary, Vanilla }
		private DashSlot _triggeredBySlot;
		private int      _activeDashMaxDuration;

		// Vanilla 槽双击窗口计时器（灾厄的 dashTimeMod）：
		//   > 0 = 右冲刺窗口开启，帧数倒计时
		//   < 0 = 左冲刺窗口开启
		//   = 0 = 空闲
		private int  _dashTimeMod;

		// ── 生命周期 ───────────────────────────────────────────────────────────

		public override void ResetEffects()
		{
			LongDashEffectId    = null;
			ShortDashEffectId   = null;
			VanillaDashEffectId = null;
			VanillaDashConfig   = default;
		}

		public override void PreUpdate()
		{
			if (LongDashCooldown    > 0) LongDashCooldown--;
			if (ShortDashCooldown   > 0) ShortDashCooldown--;
			if (VanillaDashCooldown > 0) VanillaDashCooldown--;
		}

		// 装备标志已完成注入，在 PlayerLoader.PostUpdateEquips 的前置钩子调用。
		internal void PrepareEquippedDashes()
		{
			PrepareHorizontalInput();

			if (Player.whoAmI != Main.myPlayer || Player.dead) return;
			if (ActiveEffect != null) return;

			// --- 长冲刺（DashKey） ---
			if (!string.IsNullOrEmpty(LongDashEffectId)
				&& LongDashCooldown <= 0
				&& DashKeybinds.DashKey != null
				&& DashKeybinds.DashKey.JustPressed)
			{
				PlayerDashEffect effect = PlayerDashManager.FindById(LongDashEffectId);
				if (effect != null && effect.CanUseDash(Player))
				{
					(int dirX, int dirY) = ResolveDashDirection(effect);
					if (dirX != 0 || dirY != 0)
					{
						TryStart(effect, dirX, dirY, DashSlot.Main);
						return;
					}
				}
			}

			// --- 短冲刺（BlinkKey） ---
			if (!string.IsNullOrEmpty(ShortDashEffectId)
				&& ShortDashCooldown <= 0
				&& DashKeybinds.BlinkKey != null
				&& DashKeybinds.BlinkKey.JustPressed)
			{
				PlayerDashEffect effect = PlayerDashManager.FindById(ShortDashEffectId);
				if (effect != null && effect.CanUseDash(Player))
				{
					(int dirX, int dirY) = ResolveDashDirection(effect);
					if (dirX != 0 || dirY != 0)
					{
						TryStart(effect, dirX, dirY, DashSlot.Secondary);
						return;
					}
				}
			}

			if (SingleTapMode && PeekHorizontalRequest() != 0 && VanillaDashCooldown <= 0
				&& !string.IsNullOrEmpty(VanillaDashEffectId))
			{
				PlayerDashEffect effect = PlayerDashManager.FindById(VanillaDashEffectId);
				if (effect != null && effect.CanUseDash(Player))
					TryStart(effect, ConsumeHorizontalRequest(), 0, DashSlot.Vanilla);
			}
		}

		// ── Vanilla 槽检测（与灾厄相同挂载点） ──────────────────────────────
		public override void PostUpdateRunSpeeds()
		{
			if (Player.whoAmI != Main.myPlayer) return;

			// 冲刺进行中：压制 vanilla，阻止 DashMovement 干扰
			if (ActiveEffect != null)
			{
				Player.dashType = 0; // → dash=0 → vanilla exits cleanly
				return;
			}

			if (SingleTapMode) return;
			if (_dashTimeMod > 0) _dashTimeMod--;
			else if (_dashTimeMod < 0) _dashTimeMod++;

			if (string.IsNullOrEmpty(VanillaDashEffectId) || VanillaDashCooldown > 0) return;

			PlayerDashEffect effect = PlayerDashManager.FindById(VanillaDashEffectId);
			if (effect == null || !effect.CanUseDash(Player)) return;

			// ── 右冲刺 ────────────────────────────────────────────────────────
			if (Player.controlRight && Player.releaseRight)
			{
				// _dashTimeMod 是自身窗口，Player.dashTime 接受 HelpfulHotkeys 的信号。
				if (_dashTimeMod > 0 || Player.dashTime > 0)
				{
					_dashTimeMod = 0;
					TryStart(effect, 1, 0, DashSlot.Vanilla);
					return;
				}
				_dashTimeMod = 15; // 第一次按右，开启窗口
			}
			// ── 左冲刺 ────────────────────────────────────────────────────────
			else if (Player.controlLeft && Player.releaseLeft)
			{
				if (_dashTimeMod < 0 || Player.dashTime < 0)
				{
					_dashTimeMod = 0;
					TryStart(effect, -1, 0, DashSlot.Vanilla);
					return;
				}
				_dashTimeMod = -15;
			}
		}

		public override void PreUpdateMovement()
		{
			if (ActiveEffect == null) return;

			ActiveEffect.OnDashEffects(Player, DirX, DirY, ElapsedFrames);
			ElapsedFrames++;

			if (ActiveEffect == null) return;

			if (ElapsedFrames >= _activeDashMaxDuration)
				FinishDash();
		}

		public void EndDash()
		{
			if (ActiveEffect == null) return;
			FinishDash();
		}

		// 时间位移结束当前冲刺，同时清除尚未消费的双击/单键窗口；保留正常结束冷却。
		internal void CancelForTimeEcho()
		{
			EndDash();
			ClearDashInput();
		}

		// ── 私有工具 ───────────────────────────────────────────────────────────

		private (int, int) ResolveDashDirection(PlayerDashEffect effect)
		{
			int x = 0, y = 0;

			if (Player.controlLeft)  x = -1;
			if (Player.controlRight) x = 1;
			if (effect.AllowVerticalDash)
			{
				if (Player.controlUp)   y = -1;
				if (Player.controlDown) y = 1;
			}

			if (x != 0 && y != 0) y = 0;

			if (x != 0 || y != 0) return (x, y);

			if (Math.Abs(Player.velocity.X) > 0.5f)
				return (Math.Sign(Player.velocity.X), 0);

			return (Player.direction, 0);
		}

		private void TryStart(PlayerDashEffect effect, int dirX, int dirY, DashSlot slot)
		{
			ConsumeHorizontalRequest();
			ActiveEffect     = effect;
			ElapsedFrames    = 0;
			DirX             = dirX;
			DirY             = dirY;
			_triggeredBySlot = slot;

			// ActiveConfig 必须在 GetMaxDuration / GetCooldown 之前赋值，
			// 因为 StandardDash 的这两个方法从 ActiveConfig 里读取配置值。
			if (slot == DashSlot.Vanilla)
				ActiveConfig = VanillaDashConfig;

			_activeDashMaxDuration = effect.GetMaxDuration(Player);

			Array.Clear(HitTargets, 0, HitTargets.Length);
			HitCount = 0;

			// 清空 vanilla dash 状态，防止 DashMovement 残留干扰
			Player.dash      = 0;
			Player.dashType  = 0;
			Player.dashDelay = 0;
			Player.dashTime  = 0;

			effect.OnDashStart(Player, dirX != 0 ? dirX : dirY);
		}

		private void FinishDash(bool applyEndEffects = true)
		{
			if (applyEndEffects) ActiveEffect.OnDashEnd(Player);

			int cooldown = ActiveEffect.GetCooldown(Player);
			switch (_triggeredBySlot)
			{
				case DashSlot.Main:      LongDashCooldown    = cooldown; break;
				case DashSlot.Secondary: ShortDashCooldown   = cooldown; break;
				case DashSlot.Vanilla:   VanillaDashCooldown = cooldown; break;
			}

			ActiveEffect  = null;
			ElapsedFrames = 0;

			Array.Clear(HitTargets, 0, HitTargets.Length);
			HitCount = 0;
		}
	}
}
