using System;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;
using 武器test.Items.Accessories.Dashes;

namespace 武器test.Items.Accessories
{
	// ============================================================================
	//  OmniGuardianAccessory  ——  综合守护饰品 (天界星盘风格翅膀)
	// ----------------------------------------------------------------------------
	//  飞行行为:
	//    - 完美悬浮 (按下键)  = PreUpdateMovement 中保持 velocity.Y = -0.01f
	//                            (极小负值: 视觉上完全静止, 但 vanilla 仍判定在飞行)
	//                            如果设为 0 会被 vanilla 判定为站立 -> 触发"虚空跑步"
	//    - 上升加速 (按上键)  = 在 VerticalWingSpeeds 中放大上升参数
	//                            和 喷气背包 / 女皇之翼 同一套机制
	//    - 无限飞行           = player.empressBrooch (御翼徽章效果)
	//
	//  其它功能:
	//    [1] AsgardianAegis 风格冲刺 (独立文件 OmniguardianDash.cs)
	//    [2] 鞋子奔跑 + 移速
	//    [3] Radiance & RampartOfDeities 综合生存效果
	//    [4] 大量 debuff 免疫
	//    [5] 免疫击退、火块、熔岩
	//    [6] 全方位属性加成 (伤害 / 暴击 / 攻速 / 穿甲 / 最大HP / 最大MP / 召唤栏 / 哨兵栏 / 防御 / 免伤 / 再生 / 移速)
	//    [7] 三重闪避:
	//          - 神圣套护甲闪避    (100% 闪避一次, 30 秒冷却, vanilla 全自动管理)
	//          - 黑带闪避          (10% 几率, 无冷却)
	//          - 自定义额外闪避    (StatisVoidSash 风格, 概率+冷却可调)
	//
	//  本饰品被识别为 翅膀类 装备, 会占用翅膀槽位, 无法同时装备其他翅膀。
	//  需要的贴图:
	//    - OmniGuardianAccessory.png       (物品图标)
	//    - OmniGuardianAccessory_Wings.png (翅膀贴图)
	// ============================================================================

	[AutoloadEquip(EquipType.Wings)]
	public class OmniGuardianAccessory : ModItem
	{
		// ========================================================================
		// =====                  数值调节区 (可自由修改)                     =====
		// ========================================================================

		// ---------- 攻击属性 ----------
		public const float DamageBonus       = 1.00f;  // +100% 伤害 (所有职业)
		public const int   CritBonus         = 35;      // +35% 暴击率 (所有职业)
		public const float AttackSpeedBonus  = 0.35f;  // +35% 攻速 (所有职业)
		public const int   ArmorPenetration  = 24;     // +24 穿甲 (所有职业)

		// ---------- 防御属性 ----------
		public const float MaxLifeBonus         = 1f;    // +100% 最大生命值 (1f = 100%, 0.1f = 10%)
		public const float MaxManaBonus         = 1f;    // +100% 最大法力值
		public const float DamageReductionBonus = 0.15f;  // +15% 免伤
		public const int   DefenseBonus         = 15;    // +15 防御
		public const int   LifeRegenBaseBonus   = 30;    // +15 HP/s 基础再生 (lifeRegen 单位为 1/2 HP/s)
		public const float ManaRegenBonus       = 1.0f;  // +1.0 倍法力恢复 (作用于 manaRegenBonus)

		// ---------- 召唤属性 ----------
		public const int   ExtraMinionSlots     = 8;    // +8 召唤栏位
		public const int   ExtraSentrySlots     = 3;    // +3 哨兵栏位

		// ---------- 移动属性 ----------
		public const float MoveSpeedBonus       = 0.10f; // +10% 移动速度
		public const float RunSpeedCap          = 18.0f; // 奔跑速度上限

		// ---------- 翅膀参数 ----------
		public const int   WingTimeMax            = 1800;   // 翅膀飞行时间上限 (因开了 empressBrooch 实际无限)
		public const float HorizontalFlightSpeed  = 20f;    // 水平飞行速度
		public const float HorizontalAccelMult    = 1.2f;     // 水平飞行加速度倍率
		public const float AscentWhenFalling      = 1.2f;   // 下落时的上升力
		public const float AscentWhenRising       = 0.1f;   // 上升时的额外加速度
		public const float MaxCanAscendMult       = 0.8f;   // 最大可加速上升的倍率
		public const float MaxAscentMult          = 3f;     // 最大上升速度倍率
		public const float ConstantAscend         = 0.1f;   // 持续上升力

		// ---------- 悬浮参数 (按下键悬浮) ----------
		public const float HoverHorizontalSpeed = 10.0f;     // 悬浮时的水平速度
		public const float HoverAccRunSpeed     = 3.0f;     // 悬浮时的水平加速度倍率

		// ---------- 上升加速 (按上键, 喷气背包风格) ----------
		public const float UpBoostMultiplier = 5f;          // 5 倍 (原版女皇之翼为 1.5 倍)

		// ---------- 冲刺参数 ----------
		// 使用 AsgardianAegis 风格的自定义冲刺 (见 OmniguardianDash.cs)
		// 冲刺详细数值在 OmniguardianDash.cs 顶部调节
		public const bool EnableAegisDash = true;

		// ---------- 生存效果 ----------
		public const float LowHpDamageReduction     = 0.35f;  // 生命 < 50% 时额外免伤
		public const int   DebuffDefensePerStack    = 20;     // 每个 debuff 增加的防御
		public const int   DebuffRegenPerStack      = 10;      // 每个 debuff 增加的再生 (1/2 HP/s 单位)
		public const int   LostHpRegenMin           = 10;      // 失血再生最低 HP/s
		public const int   LostHpRegenMax           = 100;     // 失血再生最高 HP/s
		public const int   ExtraDebuffTimeReduction = 2;      // 每帧额外减少的 debuff tick 数
		public const int   ExtraImmuneFrames        = 10;     // 额外受伤无敌帧

		// ---------- 闪避 ----------
		public const bool  EnableHallowedShadowDodge  = true;   // 启用神圣套护甲闪避 (100% 闪避一次, 30秒冷却)
		public const bool  EnableBlackBeltDodge       = true;   // 启用原版黑带闪避 (10% 几率, 无冷却)
		public const int   ExtraDodgeChanceDenominator = 10;      // 1/5 = 20% 额外闪避概率
		                                                          // 设为 0 表示禁用; 数值越大概率越低
		public const int   ExtraDodgeCooldownTicks     = 60 * 15; // 额外闪避的冷却 (15 秒)

		// ========================================================================

		public override void SetStaticDefaults()
		{
			// =================================================================
			// 注册原版翅膀属性 —— 启用按下键的 vanilla 悬浮基础设施 (取消重力)
			// 真正的"完美悬停"在 OmniGuardianPlayer.PreUpdateMovement 实现
			// =================================================================
			int wingSlot = EquipLoader.GetEquipSlot(Mod, Name, EquipType.Wings);

			ArmorIDs.Wing.Sets.Stats[wingSlot] = new WingStats(
				flyTime: WingTimeMax,
				flySpeedOverride: RunSpeedCap,
				hasHoldDownHoverFeatures: true,
				hoverFlySpeedOverride: HoverHorizontalSpeed,
				hoverAccelerationMultiplier: HoverAccRunSpeed
			);
		}

		public override void SetDefaults()
		{
			Item.width     = 30;
			Item.height    = 30;
			Item.value     = Item.sellPrice(gold: 20);
			Item.rare      = ItemRarityID.Red;
			Item.accessory = true;
		}

		// ========================================================================
		// 翅膀飞行参数重写
		// ========================================================================

		public override void HorizontalWingSpeeds(Player player, ref float speed, ref float acceleration)
		{
			speed         = HorizontalFlightSpeed;
			acceleration *= HorizontalAccelMult;
		}

		public override void VerticalWingSpeeds(Player player,
			ref float ascentWhenFalling,
			ref float ascentWhenRising,
			ref float maxCanAscendMultiplier,
			ref float maxAscentMultiplier,
			ref float constantAscend)
		{
			ascentWhenFalling      = AscentWhenFalling;
			ascentWhenRising       = AscentWhenRising;
			maxCanAscendMultiplier = MaxCanAscendMult;
			maxAscentMultiplier    = MaxAscentMult;
			constantAscend         = ConstantAscend;

			// =================================================================
			// 上升加速 (按上键) —— 喷气背包 / 女皇之翼 风格
			// =================================================================
			if (player.controlUp && player.controlJump)
			{
				ascentWhenRising    *= UpBoostMultiplier;
				maxAscentMultiplier *= UpBoostMultiplier;
				constantAscend      *= UpBoostMultiplier;
			}
		}

		// ========================================================================
		// 穿戴时每帧执行的所有效果
		// ========================================================================

		public override void UpdateAccessory(Player player, bool hideVisual)
		{
			var mp = player.GetModPlayer<OmniGuardianPlayer>();
			mp.Equipped = true;

			// ===== 攻击属性 =====
			player.GetDamage(DamageClass.Generic)            += DamageBonus;
			player.GetCritChance(DamageClass.Generic)        += CritBonus;
			player.GetAttackSpeed(DamageClass.Generic)       += AttackSpeedBonus;
			player.GetArmorPenetration(DamageClass.Generic)  += ArmorPenetration;

			// ===== 生命/法力/防御 =====
			// 基于 statLifeMax2 计算: 这样能与其它 +最大生命 饰品正确叠加百分比
			player.statLifeMax2 += (int)(player.statLifeMax2 * MaxLifeBonus);
			player.statManaMax2 += (int)(player.statManaMax2 * MaxManaBonus);
			player.endurance    += DamageReductionBonus;
			player.statDefense  += DefenseBonus;
			player.lifeRegen    += LifeRegenBaseBonus;
			player.manaRegenBonus += (int)(ManaRegenBonus * 100); // 单位是百分比 * 100

			// ===== 常驻 Buff =====
			player.AddBuff(BuffID.Honey, 2); // 蜂蜜 buff (每帧刷新, 实现常驻效果)
			player.maxMinions += ExtraMinionSlots;
			player.maxTurrets += ExtraSentrySlots;

			// ===== 移动 =====
			player.moveSpeed += MoveSpeedBonus;

			// ===== 无限飞行 (御翼徽章效果) =====
			player.empressBrooch = true;

			// ===== 鞋子效果 =====
			if (player.accRunSpeed < RunSpeedCap)
				player.accRunSpeed = RunSpeedCap;
			player.iceSkate    = true;    // 冰鞋
			player.waterWalk   = true;    // 水上行走
			player.fireWalk    = true;    // 免疫火块
			player.lavaImmune  = true;    // 免疫熔岩
			player.lavaMax    += 420;     // 增加熔岩免疫上限
			player.noKnockback = true;    // 免疫击退
			player.longInvince = true;    // 延长无敌帧时间

			// ===== 冲刺 (灾厄风格 dash 框架, 见 Dashes/ 文件夹) =====
			// 灾厄做法: 设 ActiveDashId + dashType=0 (禁用 vanilla 双击 dash, 避免冲突)
			if (EnableAegisDash)
			{
				player.GetModPlayer<DashPlayer>().ActiveDashId = "OmniguardianDash";
				player.dashType = 0;
			}

			// =====================================================================
			// 闪避三件套 (vanilla 内部互斥, 一次受伤至多触发一种, 优先级按顺序)
			// =====================================================================

			// 1. 神圣套护甲闪避 (100% 闪避一次, 30秒冷却, vanilla 自动管理 cooldown)
			if (EnableHallowedShadowDodge && player.shadowDodgeTimer <= 0)
				player.shadowDodge = true;

			// 2. 黑带闪避 (10% 几率, 无冷却)
			if (EnableBlackBeltDodge)
				player.blackBelt = true;

			// 3. 自定义额外闪避在 OmniGuardianPlayer.FreeDodge 实现

			// ====================================================================
			// =====                Debuff 免疫列表 (可自由增删)              =====
			// ====================================================================
			player.buffImmune[BuffID.OnFire]         = true; // 燃烧
			player.buffImmune[BuffID.OnFire3]        = true; // 神圣燃烧
			player.buffImmune[BuffID.Frostburn]      = true; // 寒霜燃烧
			player.buffImmune[BuffID.Frostburn2]     = true; // 寒霜燃烧 II
			player.buffImmune[BuffID.Burning]        = true; // 灼烧 (近熔岩)
			player.buffImmune[BuffID.Poisoned]       = true; // 中毒
			player.buffImmune[BuffID.Venom]          = true; // 剧毒
			player.buffImmune[BuffID.Bleeding]       = true; // 流血
			player.buffImmune[BuffID.Cursed]         = true; // 被诅咒
			player.buffImmune[BuffID.CursedInferno]  = true; // 诅咒地狱火
			player.buffImmune[BuffID.Confused]       = true; // 困惑
			player.buffImmune[BuffID.Silenced]       = true; // 沉默
			player.buffImmune[BuffID.Slow]           = true; // 减速
			player.buffImmune[BuffID.Weak]           = true; // 虚弱
			player.buffImmune[BuffID.BrokenArmor]    = true; // 破甲
			player.buffImmune[BuffID.Darkness]       = true; // 黑暗
			player.buffImmune[BuffID.Blackout]       = true; // 全黑
			player.buffImmune[BuffID.Chilled]        = true; // 冷冻
			player.buffImmune[BuffID.Frozen]         = true; // 冻结
			player.buffImmune[BuffID.Stoned]         = true; // 石化
			player.buffImmune[BuffID.Suffocation]    = true; // 窒息
			player.buffImmune[BuffID.Wet]            = true; // 潮湿
			player.buffImmune[BuffID.Webbed]         = true; // 蛛网
			player.buffImmune[BuffID.Electrified]    = true; // 电击
			player.buffImmune[BuffID.Dazed]          = true; // 眩晕
			player.buffImmune[BuffID.Obstructed]     = true; // 阻碍
			player.buffImmune[BuffID.WitheredArmor]  = true; // 凋零护甲
			player.buffImmune[BuffID.WitheredWeapon] = true; // 凋零武器
			player.buffImmune[BuffID.TheTongue]      = true; // 长舌
			player.buffImmune[BuffID.Rabies]         = true; // 狂犬病
			player.buffImmune[BuffID.VortexDebuff]   = true; // 星旋 debuff
			player.buffImmune[BuffID.Lovestruck]     = true; // 中爱神
			// 添加更多: player.buffImmune[BuffID.???] = true;
		}

		public override void AddRecipes()
		{
			CreateRecipe()
				.AddIngredient(ItemID.Wood, 10)
				.AddTile(TileID.WorkBenches)
				.Register();
		}
	}

	// ============================================================================
	//                          ModPlayer 处理类
	// ============================================================================

	public class OmniGuardianPlayer : ModPlayer
	{
		public bool Equipped;
		public int  ExtraDodgeCooldown; // 额外闪避的内部冷却计时

		public override void ResetEffects()
		{
			Equipped = false;
		}

		// 冷却需要持续衰减
		public override void PostUpdate()
		{
			if (ExtraDodgeCooldown > 0)
				ExtraDodgeCooldown--;
		}

		// =================================================
		// 完美悬浮 —— 在所有 velocity 计算结束后保持 Y 极微小负值
		// 关键: 不能设为 0!
		//   - vanilla 检测 velocity.Y == 0f 会判定为"在地面" -> 触发跑步动画 + 翅膀停扇 (虚空跑步)
		//   - 用 -0.001f 极小负值: 视觉上完全静止, 但 vanilla 仍认为在飞行
		// 仅修改 Y 分量, 水平 (X) 完全不受干扰
		// =================================================
		public override void PreUpdateMovement()
		{
			if (!Equipped) return;

			// 条件: 飞行中 (有翅膀逻辑生效) + 按住下键 + 按住跳跃键
			if (Player.wingsLogic > 0 && Player.controlDown && Player.controlJump)
			{
				Player.velocity.Y = -0.0001f;       // 极微小向上速度: 维持飞行状态, 视觉静止
				Player.gfxOffY    = 0f;           // 防止视觉抖动
				Player.fallStart  = (int)(Player.position.Y / 16f); // 防止跌落伤害
			}
		}

		public override void UpdateLifeRegen()
		{
			if (!Equipped) return;

			float lostFraction = 1f - ((float)Player.statLife / Math.Max(1, Player.statLifeMax2));
			if (lostFraction < 0f) lostFraction = 0f;
			if (lostFraction > 1f) lostFraction = 1f;

			int minR = OmniGuardianAccessory.LostHpRegenMin;
			int maxR = OmniGuardianAccessory.LostHpRegenMax;
			int bonus = minR + (int)Math.Round(lostFraction * (maxR - minR));

			Player.lifeRegen += bonus * 2; // lifeRegen 单位是 1/2 HP/s
		}

		public override void PostUpdateMiscEffects()
		{
			if (!Equipped) return;

			// debuff 计数 + 加速衰减
			int debuffCount = 0;
			for (int i = 0; i < Player.MaxBuffs; i++)
			{
				int bt = Player.buffType[i];
				if (bt <= 0) continue;
				if (Main.debuff[bt])
				{
					debuffCount++;
					Player.buffTime[i] -= OmniGuardianAccessory.ExtraDebuffTimeReduction;
					if (Player.buffTime[i] < 1) Player.buffTime[i] = 1;
				}
			}

			if (debuffCount > 0)
			{
				Player.statDefense += debuffCount * OmniGuardianAccessory.DebuffDefensePerStack;
				Player.lifeRegen   += debuffCount * OmniGuardianAccessory.DebuffRegenPerStack;
			}

			if (Player.statLife < Player.statLifeMax2 / 2)
			{
				Player.endurance += OmniGuardianAccessory.LowHpDamageReduction;
			}

			if (OmniGuardianAccessory.ExtraImmuneFrames > 0 && Player.immune && Player.immuneTime > 0)
			{
				if (Player.immuneTime < OmniGuardianAccessory.ExtraImmuneFrames + 1)
				{
					Player.immuneTime += OmniGuardianAccessory.ExtraImmuneFrames;
				}
			}
		}

		// =================================================
		// 自定义闪避: 在受伤前判定, 命中则跳过本次伤害
		// 神圣套闪避 (shadowDodge) 和黑带闪避 (blackBelt) 都由 vanilla 自动处理
		// 这里再额外加一层独立的概率闪避
		// =================================================
		public override bool FreeDodge(Player.HurtInfo info)
		{
			if (!Equipped) return false;
			if (OmniGuardianAccessory.ExtraDodgeChanceDenominator <= 0) return false;
			if (ExtraDodgeCooldown > 0) return false;

			if (Main.rand.Next(OmniGuardianAccessory.ExtraDodgeChanceDenominator) == 0)
			{
				ExtraDodgeCooldown = OmniGuardianAccessory.ExtraDodgeCooldownTicks;

				// 触发原版忍者闪避动画 (黑带的视觉效果, 自带粒子)
				Player.SetImmuneTimeForAllTypes(Player.longInvince ? 80 : 40);
				Player.NinjaDodge();
				return true; // 跳过这次伤害
			}

			return false;
		}
	}
}
