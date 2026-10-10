using System;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;
using TestMod.Common.Mechanics.AccessoryEffects;
using TestMod.Common.Mechanics.Dashes;
using TestMod.Common.Systems;
using TestMod.Content.Buffs;
using TestMod.Content.Items.Accessories;

namespace TestMod.Common.Players
{
    public partial class TerraWingsPlayer : ModPlayer
    {
        public bool Equipped { get; private set; }
        internal bool SlowModeActive => Equipped && equippedWings?.SlowModeEnabled == true;
        private TerraWings equippedWings;
        private bool hideWings;
        private ulong lastRecoveryTick = ulong.MaxValue;
        private bool recoveredFlightThisTick;
        private bool fargoGravityProvided;
        internal bool FargoMomentumProvided;

        // 冲刺状态属于玩家；效果注册表中的 TerraWingsDash 实例不保存运行字段。
        internal float DashSpeed;
        internal bool DashContactActive;
        internal Vector2 DashStartPosition;

        public override void ResetEffects()
        {
            Equipped = false;
            equippedWings = null;
            hideWings = false;
            DashContactActive = false;
            recoveredFlightThisTick = false;
            fargoGravityProvided = FargoMomentumProvided = false;
        }

        internal void Equip(TerraWings wings, bool hideVisual)
        {
            if (Equipped) return;
            Equipped = true;
            equippedWings = wings;
            hideWings = hideVisual;
            Player.accRunSpeed = Math.Max(Player.accRunSpeed, 15.6f);
            Player.moveSpeed += 0.5f;
            Player.rocketBoots = Player.vanityRocketBoots = ArmorIDs.RocketBoots.TerrasparkBoots;
            Player.rocketTimeMax = Math.Max(Player.rocketTimeMax, 10);
            Player.iceSkate = Player.waterWalk = Player.fireWalk = Player.lavaRose = true;
            Player.lavaMax += 420;
            Player.lavaImmune = Player.ignoreWater = Player.noFallDmg = Player.noKnockback = true;
            Player.hasLuck_LuckyHorseshoe = true;
            Player.spikedBoots = Math.Max(Player.spikedBoots, 2);
            Player.jumpBoost = true;
            if (!FargoAccessoryCompatSystem.TryProvide(FargoAccessoryCompatSystem.Frog, Player, wings.Item))
                Player.frogLegJumpBoost = Player.autoJump = true;
            fargoGravityProvided = FargoAccessoryCompatSystem.TryProvide(FargoAccessoryCompatSystem.Gravity, Player, wings.Item);
            FargoMomentumProvided = FargoAccessoryCompatSystem.TryProvide(FargoAccessoryCompatSystem.Momentum, Player, wings.Item);
            Player.jumpSpeedBoost += 1.8f;
            Player.hasMagiluminescence = true;
            if (wings.ExtraJumpsEnabled)
            {
                Player.GetJumpState(ExtraJump.CloudInABottle).Enable();
                Player.GetJumpState(ExtraJump.SandstormInABottle).Enable();
                Player.GetJumpState(ExtraJump.BlizzardInABottle).Enable();
                Player.GetJumpState(ExtraJump.FartInAJar).Enable();
            }
            Player.AddBuff(ModContent.BuffType<GravityNormalizerBuff>(), 2);
            Player.GetModPlayer<GravityNormalizerPlayer>().HasGravityNormalizer = true;
            GrappleEffect.Apply(Player, wings.Item);
            FastFallEffect.Apply(Player, 30f, 2f);
            Player.GetModPlayer<DashPlayer>().VanillaDashEffectId = TerraWingsDash.EffectId;
        }

        public override void PostUpdateEquips()
        {
            if (!Equipped)
            {
                StopOwnDash();
                return;
            }
            // 独立逻辑槽不随职业外观切换，兼容隐藏、时装翅膀与扩展饰品槽。
            Player.wingsLogic = equippedWings.LogicSlot;
            Player.equippedWings = equippedWings.Item;
            Player.wingTimeMax = TerraWings.FlightTime;
            SuppressVanillaDash();
        }

        public override void PostUpdateMiscEffects()
        {
            if (!Equipped || Player.dead) return;
            if (!fargoGravityProvided)
                Player.gravity = Math.Max(Player.gravity, Player.defaultGravity * 1.5f);
            // Buff 在此阶段只抬高低于默认值的重力，不会抹掉这里的 1.5 倍。
            // 消耗之前恢复，耗尽时仍能重新起飞；同一游戏 tick 最多恢复一帧，不靠重戴回满。
            if (lastRecoveryTick != Main.GameUpdateCount)
            {
                lastRecoveryTick = Main.GameUpdateCount;
                Player.wingTime = RecoverFlightTime(Player.wingTime, Player.wingTimeMax);
                recoveredFlightThisTick = Player.wingTime > 0f;
            }
            Lighting.AddLight(Player.Center, 0.9f, 0.8f, 0.5f);
        }

        internal static float RecoverFlightTime(float current, int maximum)
            => Math.Clamp(current + 1f, 0f, Math.Max(0, maximum));

        private void SuppressVanillaDash()
        {
            if (Player.GetModPlayer<DashPlayer>().VanillaDashEffectId == TerraWingsDash.EffectId)
                Player.dashType = 0;
        }

        public override void PostUpdateRunSpeeds()
        {
            if (!Equipped) return;
            SuppressVanillaDash();
            if (Player.mount.Active || Player.GetModPlayer<DashPlayer>().ActiveEffect != null) return;
            if (SlowModeActive)
            {
                Player.accRunSpeed = Math.Min(Player.accRunSpeed, TerraWings.SlowModeSpeed);
                Player.maxRunSpeed = Math.Min(Player.maxRunSpeed, TerraWings.SlowModeSpeed);
            }
            Player.runAcceleration *= 1.75f;
            if (FargoMomentumProvided) return;
            Player.runAcceleration *= 5f;
            Player.runSlowdown *= 5f;
            bool sameDirection = Player.velocity.X > 0f && Player.controlRight && !Player.controlLeft
                || Player.velocity.X < 0f && Player.controlLeft && !Player.controlRight;
            if (!sameDirection) Player.runSlowdown += 7f;
        }

        public override void PreUpdateMovement()
        {
            if (!Equipped || Player.dead || Player.mount.Active || Player.CCed || Player.shimmering
                || Player.tongued || Player.pulley || Player.grapCount > 0) return;
            // 在原版水平/翅膀加速后、碰撞位移前限制惯性；冲刺保留其独立速度。
            if (SlowModeActive && Player.GetModPlayer<DashPlayer>().ActiveEffect == null)
                Player.velocity.X = Math.Clamp(Player.velocity.X, -TerraWings.SlowModeSpeed, TerraWings.SlowModeSpeed);
            // 下落与悬停互斥；这里只处理本饰品的悬停，避免影响其他来源的 PerfectHover。
            if (Player.wingsLogic == equippedWings.LogicSlot && (Player.wingTime > 0f || recoveredFlightThisTick)
                && Player.controlDown && Player.controlJump && Player.velocity.Y != 0f)
            {
                Player.velocity.Y = -0.0001f * Player.gravDir;
                Player.gfxOffY = 0f;
                Player.fallStart = Player.fallStart2 = (int)(Player.position.Y / 16f);
            }
        }

        public override void PostUpdate()
        {
            if (!Equipped || Player.dead)
            {
                remoteDashVisual = false;
                StopOwnDash();
                ClearDashTrail();
                return;
            }
            if (Player.mount.Active || Player.CCed || Player.shimmering || Player.tongued || Player.pulley || Player.grapCount > 0)
                StopOwnDash();

            bool showDash = HasDashVisual;
            if (DashContactActive && Player.GetModPlayer<DashPlayer>().ActiveEffect is TerraWingsDash dash)
                dash.AfterMovement(Player);
            UpdateDashTrail(showDash);
        }

        public override bool CanBeHitByNPC(NPC npc, ref int cooldownSlot)
            // 原版 NPC 接触检测早于 PreUpdateMovement，不能依赖该钩子本帧才置起的标志。
            => !(Equipped && Player.GetModPlayer<DashPlayer>().ActiveEffect is TerraWingsDash dash
                && dash.CanUseDash(Player));

        public override void UpdateVisibleAccessories()
        {
            // 原版隐藏翅膀在空中仍会显示；本饰品明确隐藏，随后时装槽仍可覆盖。
            if (Equipped && hideWings && equippedWings.OwnsSlot(Player.wings)) Player.wings = 0;
        }

        public override void FrameEffects()
        {
            if (Main.dedServ) return;
            TerraWings wings = ModContent.GetInstance<TerraWings>();
            if (wings.OwnsSlot(Player.wings)) Player.wings = wings.CurrentVisualSlot(Player);
        }

        public override void ModifyDrawInfo(ref PlayerDrawSet drawInfo)
        {
            if (Main.dedServ) return;
            TerraWings wings = ModContent.GetInstance<TerraWings>();
            // 只替换本饰品默认外观的翼染料；不写 Player.cWings，也不影响其他绘制部位。
            if (Player.wings == wings.LogicSlot && drawInfo.cWings == 0)
                drawInfo.cWings = wings.EmeraldShader;
        }

        private void StopOwnDash()
        {
            DashPlayer dash = Player.GetModPlayer<DashPlayer>();
            if (dash.ActiveEffect is TerraWingsDash) dash.EndDash();
            DashContactActive = false;
            DashSpeed = 0f;
        }

        public override void UpdateDead()
        {
            Equipped = false;
            remoteDashVisual = false;
            StopOwnDash();
            ClearDashTrail();
        }

        internal void ResetSession()
        {
            Equipped = false;
            equippedWings = null;
            remoteDashVisual = false;
            StopOwnDash();
            ClearDashTrail();
            lastRecoveryTick = ulong.MaxValue;
        }

        public override void OnEnterWorld() => ResetSession();
    }
}
