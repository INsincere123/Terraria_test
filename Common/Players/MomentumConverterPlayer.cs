using System;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;
using Terraria.GameInput;
using Terraria.ID;
using Terraria.ModLoader;
using TestMod.Common.Systems;

namespace TestMod.Common.Players
{
    public sealed partial class MomentumConverterPlayer : ModPlayer
    {
        public const int MaximumCharges = 2;
        public const int RechargeFrames = 120;
        public const int ProtectionDuration = 30;

        public bool Equipped;
        public int Charges { get; private set; } = MaximumCharges;
        public float RechargeProgress => rechargeTimer / (float)RechargeFrames;

        private int rechargeTimer;
        private int protectionTimer;
        private bool conversionRequested;
        private Vector2 requestedVelocity;
        private Vector2 requestedDirection;
        private ulong requestFrame;
        private bool movementSyncPending;

        public override void OnEnterWorld()
            => ResetSession();

        internal void ResetSession()
        {
            Equipped = false;
            Charges = MaximumCharges;
            rechargeTimer = 0;
            ClearTransientState();
        }

        public override void ResetEffects()
        {
            Equipped = false;
            ApplyProtection();
        }

        public override void PreUpdate()
        {
            if (Player.dead)
            {
                ClearTransientState();
                return;
            }
            if (protectionTimer > 0)
                protectionTimer--;
            // 原版落地伤害检查早于本帧 ResetEffects / UpdateAccessory。
            ApplyProtection();
            if (shadowTimer > 0)
                shadowTimer--;
        }

        public override void ProcessTriggers(TriggersSet triggersSet)
        {
            if (Player.dead || Player.whoAmI != Main.myPlayer || MomentumConverterKeybind.ActivateKey?.JustPressed != true)
                return;

            // 记录按键瞬间的合速度；后续的跑速、重力或其他移动覆写不改变转换额度。
            requestedVelocity = Player.velocity;
            // MouseWorld 已包含倒重力的屏幕到世界映射，此处不再次乘 gravDir。
            requestedDirection = Main.MouseWorld - Player.Center;
            requestFrame = Main.GameUpdateCount;
            conversionRequested = true;
        }

        public override void PostUpdateEquips()
        {
            if (!Player.dead && Equipped && Charges < MaximumCharges)
            {
                rechargeTimer++;
                if (rechargeTimer >= RechargeFrames)
                {
                    Charges++;
                    rechargeTimer = 0;
                }
            }
            ApplyProtection();
        }

        internal void ConsumeConversionRequest()
        {
            if (!conversionRequested)
                return;
            conversionRequested = false;

            if (requestFrame != Main.GameUpdateCount || Main.dedServ || Player.whoAmI != Main.myPlayer || !Equipped || Player.dead || Charges <= 0 ||
                (Player.mount.Active && Player.mount.Cart) || Player.grapCount > 0 || Player.grappling[0] >= 0 ||
                !TryCalculateVelocity(requestedVelocity, requestedDirection, Charges, out Vector2 velocity))
                return;

            byte usedFromCharges = (byte)Charges;
            Charges--;
            Player.velocity = velocity;
            protectionTimer = ProtectionDuration;
            ApplyProtection();
            PlayConversionVisuals(Player.Center, requestedVelocity, velocity);
            SendConversionEvent(Player.Center, requestedVelocity, velocity, usedFromCharges);
            movementSyncPending = Main.netMode == NetmodeID.MultiplayerClient;
        }

        // 独立纯计算入口，方便对斜向合速度、效率及无效输入进行隔离检查。
        internal static bool TryCalculateVelocity(Vector2 source, Vector2 direction, int charges, out Vector2 result)
        {
            result = Vector2.Zero;
            float speedSquared = source.LengthSquared();
            float directionSquared = direction.LengthSquared();
            if (charges < 1 || charges > MaximumCharges || !float.IsFinite(speedSquared) || !float.IsFinite(directionSquared) ||
                speedSquared <= 0f || directionSquared <= 0f)
                return false;
            float efficiency = charges == MaximumCharges ? 1f : 0.5f;
            result = direction / MathF.Sqrt(directionSquared) * (MathF.Sqrt(speedSquared) * efficiency);
            return float.IsFinite(result.X) && float.IsFinite(result.Y);
        }

        private void ApplyProtection()
        {
            if (Player.dead || protectionTimer <= 0)
                return;
            Player.noFallDmg = true;
            Player.noKnockback = true;
            Player.fallStart = (int)(Player.position.Y / 16f);
            Player.fallStart2 = Player.fallStart;
        }

        public override bool ImmuneTo(PlayerDeathReason damageSource, int cooldownCounter, bool dodgeable)
            // 仅拦截普通坠落来源；Other=5 同时用于石化初始伤害，不能一并免疫。
            => !Player.dead && protectionTimer > 0 && damageSource.SourceOtherIndex == 0;

        public override void ModifyHurt(ref Player.HurtModifiers modifiers)
        {
            if (!Player.dead && protectionTimer > 0)
                modifiers.Knockback *= 0f;
        }

        public override void PostUpdate()
        {
            if (movementSyncPending)
            {
                movementSyncPending = false;
                // 发送原版玩家移动包；此时碰撞已完成，不同步碰撞前的虚拟位移。
                NetMessage.SendData(MessageID.PlayerControls, number: Player.whoAmI);
            }
        }

        public override void UpdateDead()
        {
            Equipped = false;
            ClearTransientState();
        }

        private void ClearTransientState()
        {
            protectionTimer = 0;
            conversionRequested = false;
            requestedVelocity = requestedDirection = Vector2.Zero;
            requestFrame = 0;
            movementSyncPending = false;
            shadowTimer = 0;
            drawingConversionShadow = false;
            Array.Clear(shadowPositions);
        }
    }
}
