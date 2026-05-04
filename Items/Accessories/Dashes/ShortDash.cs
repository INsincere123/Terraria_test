using System;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace TestMod.Items.Accessories.Dashes
{
	// ============================================================================
	//  ShortDash  ——  短冲刺 / 走位版 (手感与长冲刺完全一致)
	// ----------------------------------------------------------------------------
	//  设计定位:
	//    - 距离短 (~8 格), 绑定独立按键 (BlinkKey), 用于精确走位
	//    - 全向冲刺 / 有接触伤害 / 有出场惯性 —— 手感逻辑与 LongDash 完全一致
	//    - 所有参数独立, 在下方调节区自由调整
	//
	//  对比 LongDash (长冲刺):
	//    [速度]   18/帧  vs  22/帧
	//    [持续]   7 帧   vs  22 帧   => 距离 ~7.9格 vs ~30格
	//    [冷却]   20 帧  vs  15 帧
	//    其余手感 (方向/惯性/伤害/粒子/撞墙结束) 完全一致
	// ============================================================================

	public class ShortDash : PlayerDashEffect
	{
		// ========================================================================
		// =====                  数值调节区 (可自由修改)                     =====
		// ========================================================================

		public const float DashSpeed      = 18f;  // 每帧像素 (18 × 10帧 = 180px ≈ 11.25格)
		public const int   DashFrames     = 10;    // 持续帧数
		public const int   CooldownFrames = 15;   // 冷却帧数

		// 冲刺结束后保留多少 dash 速度作为出场惯性 (0=清零, 1=完全保留)
		public const float ExitInertiaRatio = 0.7f;

		// 撞击伤害
		public const int   ContactDamage    = 1350;
		public const float ContactKnockback = 15f;
		public const int   ContactCritDenom = 4;   // 1/N 概率暴击, 设 0 禁用
		public const int   MaxHitsPerDash   = 8;

		// 无敌帧
		public const int StartIFrames   = 16;
		public const int ActiveIFrames  = 12;
		public const int ContactIFrames = 30;

		// 视觉 (蓝白系, 与长冲刺金火焰区分)
		public const int TrailDustType    = DustID.AncientLight;
		public const int ContactDustType  = DustID.PortalBoltTrail;
		public const int ContactDustCount = 35;

		// ========================================================================
		// PlayerDashEffect 接口实现
		// ========================================================================

		public override string Id            => "ShortDash";
		public override int    DashDuration  => DashFrames;
		public override int    DashCooldown  => CooldownFrames;
		public override bool   AllowVerticalDash => true;

		private bool[] hitTargets = new bool[Main.maxNPCs];
		private int    totalHits;

		public override bool CanUseDash(Player player) => true;

		// =================================================
		// 起始帧
		// =================================================
		public override void OnDashStart(Player player, int direction)
		{
			Array.Clear(hitTargets, 0, hitTargets.Length);
			totalHits = 0;

			if (direction != 0)
				player.direction = Math.Sign(direction);

			// 清除水平惯性, 保留垂直惯性 (与长冲刺一致)
			player.velocity.X = 0f;

			player.SetImmuneTimeForAllTypes(StartIFrames);

			SoundEngine.PlaySound(SoundID.Item8, player.Center);

			for (int i = 0; i < 40; i++)
			{
				Dust d = Dust.NewDustDirect(
					player.position, player.width, player.height,
					TrailDustType, 0f, 0f, 100, default, 1.6f);
				d.noGravity = true;
				d.velocity  = Main.rand.NextVector2Circular(4f, 4f);
			}
		}

		// =================================================
		// 每帧执行 (与长冲刺逻辑完全一致, 仅速度常量不同)
		// =================================================
		public override void OnDashEffects(Player player, int dirX, int dirY, int frameCount)
		{
			Vector2 unitDir = new Vector2(dirX, dirY);
			if (unitDir.LengthSquared() == 0) return;
			unitDir.Normalize();

			Vector2 desiredMove = unitDir * DashSpeed;

			Vector2 actualMove = Collision.TileCollision(
				player.position, desiredMove, player.width, player.height,
				fallThrough: false, fall2: false, gravDir: (int)player.gravDir);

			player.position += actualMove;

			player.fallStart  = (int)(player.position.Y / 16f);
			player.fallStart2 = player.fallStart;

			if (player.immuneTime < ActiveIFrames)
				player.SetImmuneTimeForAllTypes(ActiveIFrames);

			for (int i = 0; i < 3; i++)
			{
				Dust d = Dust.NewDustDirect(
					player.position, player.width, player.height,
					TrailDustType, -unitDir.X * 3f, -unitDir.Y * 3f, 100, default, 1.6f);
				d.noGravity = true;
				d.velocity *= 0.4f;
			}

			CheckContactDamage(player, dirX);

			// 撞墙提前结束
			if (actualMove.LengthSquared() < desiredMove.LengthSquared() * 0.04f)
			{
				player.GetModPlayer<DashPlayer>().EndDash();
			}
		}

		// =================================================
		// 冲刺结束: 出场惯性 (与长冲刺一致)
		// =================================================
		public override void OnDashEnd(Player player)
		{
			DashPlayer dp = player.GetModPlayer<DashPlayer>();
			Vector2 unitDir = new Vector2(dp.DashDirX, dp.DashDirY);

			if (unitDir.LengthSquared() > 0)
			{
				unitDir.Normalize();
				Vector2 exitInertia = unitDir * DashSpeed * ExitInertiaRatio;
				player.velocity.X += exitInertia.X;
				player.velocity.Y += exitInertia.Y;
			}

			Array.Clear(hitTargets, 0, hitTargets.Length);
			totalHits = 0;
		}

		// =================================================
		// 撞击伤害检测 (与长冲刺一致)
		// =================================================
		private void CheckContactDamage(Player player, int dirX)
		{
			if (totalHits >= MaxHitsPerDash) return;

			Rectangle hitbox = new Rectangle(
				(int)player.position.X - 6,
				(int)player.position.Y - 6,
				player.width  + 12,
				player.height + 12);

			for (int i = 0; i < Main.maxNPCs; i++)
			{
				NPC npc = Main.npc[i];
				if (!npc.active || npc.dontTakeDamage || npc.friendly || npc.immortal) continue;
				if (hitTargets[i]) continue;
				if (!hitbox.Intersects(npc.Hitbox)) continue;

				hitTargets[i] = true;
				totalHits++;

				bool crit = ContactCritDenom > 0 && Main.rand.Next(ContactCritDenom) == 0;

				int finalDamage = (int)(ContactDamage
					* player.GetDamage(DamageClass.Melee).Multiplicative
					+ player.GetDamage(DamageClass.Melee).Additive);

				NPC.HitInfo hitInfo = new NPC.HitInfo
				{
					Damage       = finalDamage,
					Knockback    = ContactKnockback,
					HitDirection = dirX != 0 ? dirX : (Main.rand.NextBool() ? 1 : -1),
					Crit         = crit,
					InstantKill  = false,
				};

				npc.StrikeNPC(hitInfo);

				if (Main.netMode == NetmodeID.MultiplayerClient)
					NetMessage.SendStrikeNPC(npc, hitInfo);

				for (int j = 0; j < ContactDustCount; j++)
				{
					Dust d = Dust.NewDustDirect(
						npc.position, npc.width, npc.height,
						ContactDustType, 0f, 0f, 0, default, 2.2f);
					d.noGravity = true;
					d.velocity  = Main.rand.NextVector2Circular(5f, 5f);
				}

				SoundEngine.PlaySound(SoundID.Item14, npc.Center);
				player.SetImmuneTimeForAllTypes(ContactIFrames);

				if (totalHits >= MaxHitsPerDash) break;
			}
		}
	}
}
