using System;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace TestMod.Content.Items.Accessories.Dashes
{
	// ============================================================================
	//  StandardDash  ——  由 DashConfig 完全驱动的通用冲刺实现
	// ----------------------------------------------------------------------------
	//  饰品不需要继承任何类，只需在 UpdateAccessory 里填写：
	//      dp.VanillaDashEffectId = "StandardDash";
	//      dp.VanillaDashConfig   = new DashConfig { ... };
	//      player.dashType        = 1;   // 让 vanilla 做双击检测
	//
	//  支持的行为（全部由 DashConfig 控制）：
	//    - 匀速 / 逐帧衰减 / 速度上限
	//    - NoCollision / ShieldSlam / ContactHitbox 三种碰撞模式
	//    - 命中附加 buff / 音效
	//    - 撞墙提前结束 / 速度低于阈值提前结束
	//    - 起始粒子 / 拖尾粒子
	//    - 出场惯性
	// ============================================================================

	public class StandardDash : PlayerDashEffect
	{
		public override string Id            => "StandardDash";
		public override int    DashDuration  => int.MaxValue / 2; // 由 GetMaxDuration 读 config 控制
		public override int    DashCooldown  => 0;                // 由 GetCooldown 读 config 控制
		public override bool   AllowVerticalDash => false;        // 接管 vanilla 时只有水平方向

		public override bool CanUseDash(Player player) => true;

		public override int GetMaxDuration(Player player)
			=> player.GetModPlayer<DashPlayer>().ActiveConfig.Duration;

		public override int GetCooldown(Player player)
			=> player.GetModPlayer<DashPlayer>().ActiveConfig.Cooldown;

		// ── 起始帧 ─────────────────────────────────────────────────────────────

		public override void OnDashStart(Player player, int direction)
		{
			ref DashConfig cfg = ref player.GetModPlayer<DashPlayer>().ActiveConfig;

			if (direction != 0)
				player.direction = Math.Sign(direction);

			// 清除水平惯性，保留垂直（与 LongDash 一致）
			player.velocity.X = 0f;

			if (cfg.StartIFrames > 0)
				player.SetImmuneTimeForAllTypes(cfg.StartIFrames);

			if (cfg.BurstDustCount > 0)
				SpawnBurstDust(player, ref cfg);

			if (cfg.StartSound.HasValue)
				SoundEngine.PlaySound(cfg.StartSound.Value, player.Center);
		}

		// ── 每帧执行 ───────────────────────────────────────────────────────────

		public override void OnDashEffects(Player player, int dirX, int dirY, int frameCount)
		{
			DashPlayer dp = player.GetModPlayer<DashPlayer>();
			ref DashConfig cfg = ref dp.ActiveConfig;

			// 1. 计算本帧速度（支持衰减和上限）
			float speed = ComputeSpeed(ref cfg, frameCount);

			// 速度低于阈值时提前结束
			if (cfg.MinSpeedThreshold > 0f && speed < cfg.MinSpeedThreshold)
			{
				dp.EndDash();
				return;
			}

			// 2. 方向
			Vector2 unitDir = new Vector2(dirX, dirY);
			if (unitDir == Vector2.Zero) return;
			unitDir.Normalize();

			// 3. 位移（position += TileCollision，与 LongDash/ShortDash 一致）
			Vector2 desiredMove = unitDir * speed;
			Vector2 actualMove  = Collision.TileCollision(
				player.position, desiredMove, player.width, player.height,
				fallThrough: false, fall2: false, gravDir: (int)player.gravDir);

			player.position  += actualMove;
			player.fallStart  = (int)(player.position.Y / 16f);
			player.fallStart2 = player.fallStart;

			// 4. 持续无敌帧
			if (cfg.ActiveIFrames > 0 && player.immuneTime < cfg.ActiveIFrames)
				player.SetImmuneTimeForAllTypes(cfg.ActiveIFrames);

			// 5. 拖尾粒子
			if (cfg.TrailDustPerFrame > 0)
				SpawnTrailDust(player, unitDir, ref cfg);

			// 6. 碰撞伤害
			if (cfg.CollisionType != DashCollisionType.NoCollision && cfg.BaseDamage > 0)
				CheckContactDamage(player, dp, dirX, ref cfg);

			// 7. ShieldSlam：命中后立即结束
			if (cfg.CollisionType == DashCollisionType.ShieldSlam && dp.HitCount > 0)
			{
				dp.EndDash();
				return;
			}

			// 8. 撞墙提前结束
			if (actualMove.LengthSquared() < desiredMove.LengthSquared() * 0.04f)
				dp.EndDash();
		}

		// ── 结束帧：出场惯性 ───────────────────────────────────────────────────

		public override void OnDashEnd(Player player)
		{
			DashPlayer dp = player.GetModPlayer<DashPlayer>();
			ref DashConfig cfg = ref dp.ActiveConfig;

			if (cfg.ExitInertiaRatio <= 0f) return;

			Vector2 unitDir = new Vector2(dp.DirX, dp.DirY);
			if (unitDir == Vector2.Zero) return;
			unitDir.Normalize();

			// 出场时基于初始速度 × 惯性比例，累加到 velocity（不替换）
			player.velocity.X += unitDir.X * cfg.InitialSpeed * cfg.ExitInertiaRatio;
			player.velocity.Y += unitDir.Y * cfg.InitialSpeed * cfg.ExitInertiaRatio;
		}

		// ── 内部工具 ──────────────────────────────────────────────────────────

		private static float ComputeSpeed(ref DashConfig cfg, int frameCount)
		{
			float speed = cfg.InitialSpeed;

			if (cfg.DecelerationFactor < 1f && frameCount >= cfg.DecelerationStartFrame)
				speed *= MathF.Pow(cfg.DecelerationFactor, frameCount - cfg.DecelerationStartFrame);

			if (cfg.MaxSpeed > 0f && speed > cfg.MaxSpeed)
				speed = cfg.MaxSpeed;

			return speed;
		}

		private static void CheckContactDamage(Player player, DashPlayer dp, int dirX, ref DashConfig cfg)
		{
			if (dp.HitCount >= cfg.MaxHitsPerDash) return;

			int expand = cfg.HitboxExpand;
			Rectangle hitbox = new Rectangle(
				(int)player.position.X - expand,
				(int)player.position.Y - expand,
				player.width  + expand * 2,
				player.height + expand * 2);

			for (int i = 0; i < Main.maxNPCs; i++)
			{
				NPC npc = Main.npc[i];
				if (!npc.active || npc.dontTakeDamage || npc.friendly || npc.immortal) continue;
				if (dp.HitTargets[i]) continue;
				if (!hitbox.Intersects(npc.Hitbox)) continue;

				dp.HitTargets[i] = true;
				dp.HitCount++;

				DashHitContext ctx = new DashHitContext
				{
					BaseDamage           = cfg.BaseDamage,
					Knockback            = cfg.Knockback,
					DmgClass             = cfg.HitDmgClass,
					PlayerImmunityFrames = cfg.ContactIFrames,
					HitDirection         = dirX != 0 ? dirX : (Main.rand.NextBool() ? 1 : -1),
				};

				bool crit = cfg.CritChance > 0f && Main.rand.NextFloat() < cfg.CritChance;

				int finalDamage = (int)(ctx.BaseDamage
					* player.GetDamage(ctx.DmgClass).Multiplicative
					+ player.GetDamage(ctx.DmgClass).Additive);

				NPC.HitInfo hitInfo = new NPC.HitInfo
				{
					Damage       = finalDamage,
					Knockback    = ctx.Knockback,
					HitDirection = ctx.HitDirection,
					Crit         = crit,
				};

				npc.StrikeNPC(hitInfo);

				if (Main.netMode == NetmodeID.MultiplayerClient)
					NetMessage.SendStrikeNPC(npc, hitInfo);

				if (cfg.OnHitBuffType > 0)
					npc.AddBuff(cfg.OnHitBuffType, cfg.OnHitBuffDuration);

				if (cfg.HitSound.HasValue)
					SoundEngine.PlaySound(cfg.HitSound.Value, npc.Center);

				if (ctx.PlayerImmunityFrames > 0)
					player.SetImmuneTimeForAllTypes(ctx.PlayerImmunityFrames);

				SpawnHitDust(npc, ref cfg);

				if (dp.HitCount >= cfg.MaxHitsPerDash) break;
			}
		}

		private static void SpawnBurstDust(Player player, ref DashConfig cfg)
		{
			for (int i = 0; i < cfg.BurstDustCount; i++)
			{
				Dust d = Dust.NewDustDirect(
					player.position, player.width, player.height,
					cfg.BurstDustType, 0f, 0f, 100, default, cfg.TrailDustScale);
				d.noGravity = true;
				d.velocity  = Main.rand.NextVector2Circular(4f, 4f);
			}
		}

		private static void SpawnTrailDust(Player player, Vector2 unitDir, ref DashConfig cfg)
		{
			for (int i = 0; i < cfg.TrailDustPerFrame; i++)
			{
				Dust d = Dust.NewDustDirect(
					player.position, player.width, player.height,
					cfg.TrailDustType, -unitDir.X * 3f, -unitDir.Y * 3f, 100, default, cfg.TrailDustScale);
				d.noGravity = true;
				d.velocity  *= 0.4f;
			}
		}

		private static void SpawnHitDust(NPC npc, ref DashConfig cfg)
		{
			for (int i = 0; i < 25; i++)
			{
				Dust d = Dust.NewDustDirect(
					npc.position, npc.width, npc.height,
					cfg.BurstDustType, 0f, 0f, 0, default, 2.0f);
				d.noGravity = true;
				d.velocity  = Main.rand.NextVector2Circular(5f, 5f);
			}
		}
	}
}
