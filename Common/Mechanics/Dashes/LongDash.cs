using System;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;
using TestMod.Common.Utilities;

namespace TestMod.Common.Mechanics.Dashes
{
	// ============================================================================
	//  LongDash  ——  比 AsgardianAegisDash 更强的全向冲刺 (保留惯性版)
	// ----------------------------------------------------------------------------
	//  特性 (vs 灾厄 AsgardianAegisDash):
	//    [距离]      30 格           (灾厄 28 格)
	//    [伤害]      350             (灾厄 200)
	//    [击退]      12              (灾厄 9)
	//    [无敌帧]    全程 + 撞击 16  (灾厄 12)
	//    [最大撞击]  一次最多 8 个 NPC
	//    [冷却]      25 帧           (灾厄 30 帧)
	//    [垂直冲刺]  支持 (双击上/下) (灾厄不支持)
	//
	//  关键设计 - 完整保留惯性:
	//    - 不再清空 velocity (旧版本会让冲刺中下落停止 / 冲刺后水平静止)
	//    - 用 position += 位移 直接位移玩家, 与 vanilla velocity 系统并行
	//    - 冲刺结束时给一个出场惯性 (velocity.X += DashSpeed × 0.5), 让玩家滑行减速
	//    - 重力, 翅膀飞行, 跳跃等所有惯性效果在冲刺中持续生效
	// ============================================================================

	public class LongDash : PlayerDashEffect
	{
		// ========================================================================
		// =====                  数值调节区 (可自由修改)                     =====
		// ========================================================================

		public const float DashSpeed       = 22f;   // 每帧像素 (22 = 1.375 格/帧)
		public const int   DashFrames      = 22;    // 持续帧数 (22 帧 × 22像素 = 484像素 = 30 格)
		public const int   CooldownFrames  = 15;    // 冷却 (灾厄 30)

		// 冲刺结束后保留多少 dash 速度作为出场惯性
		public const float ExitInertiaRatio = 0.7f; // 0=立刻清零, 1=完全保留, 0.5=保留一半

		// 撞击伤害
		public const int   ContactDamage     = 1350;
		public const float ContactKnockback  = 15f;
		public const int   ContactCritDenom  = 4;   // 1/4 = 25% 暴击
		public const int   MaxHitsPerDash    = 16;

		// 无敌帧
		public const int   StartIFrames    = 22;    // 起始无敌帧 (从冲刺开始算起, 固定长度)
		public const int   ActiveIFrames   = 12;
		public const int   ContactIFrames  = 30;

		// 视觉效果
		private static readonly Color DashDustColor = new(255, 198, 82);
		public const int   ContactDustCount = 35;

		// ========================================================================
		// PlayerDashEffect 接口实现
		// ========================================================================

		public override string Id => "LongDash";
		public override int    DashDuration => DashFrames;
		public override int    DashCooldown => CooldownFrames;
		public override bool   AllowVerticalDash => true;

		public override bool CanUseDash(Player player) => true;

		// =================================================
		// 起始帧
		// =================================================
		public override void OnDashStart(Player player, int direction)
		{
			if (direction != 0)
				player.direction = Math.Sign(direction);

			// 清除水平惯性, 保留垂直惯性
			// 不清的话: 反方向冲刺时旧的 velocity.X 会和 dash 位移抵消, 距离变短
			// 清掉之后: 不管之前在向哪边跑, 冲刺方向都是干净的
			// 注意: velocity.Y 不动, 这样飞行 / 下落 / 跳跃 等垂直惯性继续生效
			player.velocity.X = 0f;

			// 起始无敌帧
			player.SetImmuneTimeForAllTypes(StartIFrames);

			// 起始音效 (克苏鲁之盾的冲刺音)
			SoundEngine.PlaySound(SoundID.Item24, player.Center);

			// 起始爆裂粒子
			DustUtils.SpawnCosmicBurst(player.Center, 40, DashDustColor, 1.8f, 4.8f, 1.1f);
		}

		// =================================================
		// 每帧执行 (核心位移)
		// =================================================
		public override void OnDashEffects(Player player, int dirX, int dirY, int frameCount)
		{
			Vector2 unitDir = new Vector2(dirX, dirY);
			if (unitDir.LengthSquared() == 0) return;
			unitDir.Normalize();

			// 1. 计算 dash 位移 (额外加在 vanilla velocity 之上, 不替换它)
			Vector2 desiredMove = unitDir * DashSpeed;

			// 2. 用 Collision.TileCollision 处理墙壁阻挡
			Vector2 actualMove = Collision.TileCollision(
				player.position, desiredMove, player.width, player.height,
				fallThrough: false, fall2: false, gravDir: (int)player.gravDir);

			// 3. 直接修改 position (vanilla velocity 系统继续生效, 重力/翅膀都正常)
			player.position += actualMove;

			// 4. 防止跌落伤害 (因为 dash 自身的位移不应触发跌落判定)
			player.fallStart  = (int)(player.position.Y / 16f);
			player.fallStart2 = player.fallStart;

			// 5. 维持持续无敌
			if (player.immuneTime < ActiveIFrames)
				player.SetImmuneTimeForAllTypes(ActiveIFrames);

			// 6. 拖尾粒子
			for (int i = 0; i < 3; i++)
			{
				Vector2 position = player.Center + Main.rand.NextVector2Circular(player.width * 0.45f, player.height * 0.45f);
				Vector2 velocity = -unitDir * Main.rand.NextFloat(2.2f, 4.6f) + Main.rand.NextVector2Circular(0.8f, 0.8f);
				DustUtils.SpawnImpactLine(position, velocity, DashDustColor, Main.rand.NextFloat(0.86f, 1.18f), 50);
			}

			// 7. 撞击伤害检测
			CheckContactDamage(player, dirX);

			// 8. 撞墙提前结束 (实际位移远小于期望)
			if (actualMove.LengthSquared() < desiredMove.LengthSquared() * 0.04f)
			{
				player.GetModPlayer<DashPlayer>().EndDash();
			}
		}

		// =================================================
		// 冲刺结束: 给出场惯性
		// =================================================
		public override void OnDashEnd(Player player)
		{
			DashPlayer dp = player.GetModPlayer<DashPlayer>();
			Vector2 unitDir = new Vector2(dp.DirX, dp.DirY);

			if (unitDir.LengthSquared() > 0)
			{
				unitDir.Normalize();
				// 把 dash 速度的一部分加到 velocity, 让玩家自然滑行减速
				// 注意: 不替换 velocity, 用 += 累加, 这样原有的飞行/跳跃惯性都保留
				Vector2 exitInertia = unitDir * DashSpeed * ExitInertiaRatio;
				player.velocity.X += exitInertia.X;
				player.velocity.Y += exitInertia.Y;
			}
		}

		// =================================================
		// 撞击伤害检测
		// =================================================
		private void CheckContactDamage(Player player, int dirX)
		{
			DashPlayer dp = player.GetModPlayer<DashPlayer>();
			if (dp.HitCount >= MaxHitsPerDash) return;

			Rectangle hitbox = new Rectangle(
				(int)player.position.X - 6,
				(int)player.position.Y - 6,
				player.width  + 12,
				player.height + 12);

			for (int i = 0; i < Main.maxNPCs; i++)
			{
				NPC npc = Main.npc[i];
				if (!npc.active || npc.dontTakeDamage || npc.friendly || npc.immortal) continue;
				if (dp.HitTargets[i]) continue;
				if (!hitbox.Intersects(npc.Hitbox)) continue;

				dp.HitTargets[i] = true;
				dp.HitCount++;

				bool crit = ContactCritDenom > 0 && Main.rand.Next(ContactCritDenom) == 0;

				// ApplyTo 包含继承的伤害属性；最终防御与暴击由 NPC 统一结算一次。
				int scaledDamage = (int)player.GetTotalDamage(DamageClass.Melee).ApplyTo(ContactDamage);
				int hitDirection = dirX != 0 ? dirX : (Main.rand.NextBool() ? 1 : -1);
				NPC.HitInfo hitInfo = npc.CalculateHitInfo(
					scaledDamage, hitDirection, crit, ContactKnockback, DamageClass.Melee);

				npc.StrikeNPC(hitInfo);

				if (Main.netMode == NetmodeID.MultiplayerClient)
					NetMessage.SendStrikeNPC(npc, hitInfo);

				// 撞击爆炸粒子
				DustUtils.SpawnImpactBurst(npc.Center, ContactDustCount, DashDustColor, 2.6f, 7.2f, 1.25f);

				SoundEngine.PlaySound(SoundID.Item14, npc.Center);
				player.SetImmuneTimeForAllTypes(ContactIFrames);

				if (dp.HitCount >= MaxHitsPerDash) break;
			}
		}
	}
}
