using System;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ModLoader;
using TestMod.Common.Configs;
using TestMod.Common.Systems;

namespace TestMod.Content.Items.Accessories.Dashes
{
	// ============================================================================
	//  DashPlayer  ——  冲刺生命周期驱动器
	// ----------------------------------------------------------------------------
	//  三条独立触发槽：
	//    DashKey  (Main)      → LongDashEffectId    长冲刺
	//    BlinkKey (Secondary) → ShortDashEffectId   短冲刺
	//    双击方向键 (Vanilla)  → VanillaDashEffectId  vanilla 兼容双击触发
	//
	//  Vanilla 槽的触发机制（在 PostUpdateRunSpeeds 里，与灾厄相同挂载点）：
	//    - 信号来源：controlRight && releaseRight（"松开后再次按下" = 新的按键动作）
	//    - 双击窗口：自有 _dashTimeMod（±15帧），不依赖 vanilla 的 DoCommonDashHandle
	//    - HelpfulHotkeys 兼容：检查 Player.dashTime > 0（HH 在 SetControls 直接写 dashTime=±15）
	//    - 饰品设 dashType=0（与灾厄相同），彻底阻止 vanilla DashMovement 执行速度/dashDelay 逻辑
	//    - 冲刺进行中：PostUpdateRunSpeeds 继续维持 dashType=0，dashTime=0
	//
	//  饰品在 UpdateAccessory 里：
	//      dp.VanillaDashEffectId = "StandardDash";
	//      dp.VanillaDashConfig   = new DashConfig { ... };
	//      player.dashType        = 0;   // 防止 vanilla DashMovement 干扰
	// ============================================================================

	public class DashPlayer : ModPlayer
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
		// SetControls 与 PostUpdateRunSpeeds 跨帧通信：VanillaDashKey 本帧是否被按下
		private bool _vanillaDashKeyJustPressed;

		// ── 生命周期 ───────────────────────────────────────────────────────────

		public override void ResetEffects()
		{
			LongDashEffectId    = null;
			ShortDashEffectId   = null;
			VanillaDashEffectId = null;
			VanillaDashConfig   = default;
		}

		// ── 单键模式：在 SetControls 里模拟双击（与 HelpfulHotkeys 相同原理） ────
		public override void SetControls()
		{
			bool singleTap = TestModClientConfig.Instance?.SingleTapDash ?? false;
			if (!singleTap) return;
			if (DashKeybinds.VanillaDashKey == null || !DashKeybinds.VanillaDashKey.JustPressed) return;

			// 判断冲刺方向：优先按住的方向键，否则取角色朝向
			int dir;
			if      (Player.controlRight && !Player.controlLeft) dir =  1;
			else if (Player.controlLeft  && !Player.controlRight) dir = -1;
			else dir = Player.direction;

			if (dir > 0)
			{
				// 伪造右双击：dashTime=15（窗口已打开）+ releaseRight=true（按键刚松开）
				Player.dashTime    = 15;
				Player.releaseRight = true;
				Player.controlRight = true;
			}
			else
			{
				Player.dashTime    = -15;
				Player.releaseLeft  = true;
				Player.controlLeft  = true;
			}

			_vanillaDashKeyJustPressed = true;
		}

		public override void PreUpdate()
		{
			if (LongDashCooldown    > 0) LongDashCooldown--;
			if (ShortDashCooldown   > 0) ShortDashCooldown--;
			if (VanillaDashCooldown > 0) VanillaDashCooldown--;

			if (Player.whoAmI != Main.myPlayer) return;
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
					}
				}
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
				Player.dashTime = 0;
				return;
			}

			bool singleTap = TestModClientConfig.Instance?.SingleTapDash ?? false;

			// 单键模式：每帧清零 dashTime，屏蔽 vanilla 的双击判定。
			// 但本帧 VanillaDashKey 被按下时例外——SetControls 已写入 dashTime=±15，
			// 需要保留它让我们的检测（Player.dashTime > 0）能读到。
			if (singleTap && !_vanillaDashKeyJustPressed)
				Player.dashTime = 0;
			_vanillaDashKeyJustPressed = false; // 消费标志

			// 双击模式下，窗口计时器自然衰减（向 0 收缩）
			if (!singleTap)
			{
				if (_dashTimeMod > 0) _dashTimeMod--;
				else if (_dashTimeMod < 0) _dashTimeMod++;
			}

			if (string.IsNullOrEmpty(VanillaDashEffectId) || VanillaDashCooldown > 0) return;

			PlayerDashEffect effect = PlayerDashManager.FindById(VanillaDashEffectId);
			if (effect == null || !effect.CanUseDash(Player)) return;

			// ── 右冲刺 ────────────────────────────────────────────────────────
			if (Player.controlRight && Player.releaseRight)
			{
				if (singleTap)
				{
					// 单键模式：只响应 VanillaDashKey 设置的 dashTime 信号（方向键本身不触发）
					if (Player.dashTime > 0)
					{
						TryStart(effect, 1, 0, DashSlot.Vanilla);
						return;
					}
					// 方向键单独按下不做任何事（dashTime 已被清零）
				}
				else
				{
					// 双击模式：
					//   _dashTimeMod > 0    = 我们自己记录的第一次按右
					//   Player.dashTime > 0 = HelpfulHotkeys 等 mod 声明窗口已打开
					if (_dashTimeMod > 0 || Player.dashTime > 0)
					{
						_dashTimeMod = 0;
						TryStart(effect, 1, 0, DashSlot.Vanilla);
						return;
					}
					_dashTimeMod = 15; // 第一次按右，开启窗口
				}
			}
			// ── 左冲刺 ────────────────────────────────────────────────────────
			else if (Player.controlLeft && Player.releaseLeft)
			{
				if (singleTap)
				{
					if (Player.dashTime < 0)
					{
						TryStart(effect, -1, 0, DashSlot.Vanilla);
						return;
					}
				}
				else
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

		private void FinishDash()
		{
			ActiveEffect.OnDashEnd(Player);

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
