using System;
using Terraria;
using TestMod.Common.Configs;
using TestMod.Common.Systems;

namespace TestMod.Common.Mechanics.Dashes
{
    public partial class DashPlayer
    {
        private ulong inputTick = ulong.MaxValue;
        private bool lastSingleTapMode;
        private int horizontalRequest;
        private bool horizontalConsumed;
        private int calamityStartupDirection;

        internal static bool SingleTapMode => TestModClientConfig.Instance?.SingleTapDash ?? false;

        // 输入只属于当前玩家、当前 tick；不把冷却中的按键排队到下一帧。
        private void PrepareHorizontalInput()
        {
            bool mode = SingleTapMode;
            if (inputTick == Main.GameUpdateCount && lastSingleTapMode == mode) return;
            bool modeChanged = lastSingleTapMode != mode;
            if (modeChanged)
            {
                _dashTimeMod = 0;
                calamityStartupDirection = 0;
                if (Player.whoAmI == Main.myPlayer)
                    Player.dashTime = 0; // 仅本地切换模式时丢弃旧窗口。
            }
            lastSingleTapMode = mode;
            inputTick = Main.GameUpdateCount;
            horizontalRequest = 0;
            horizontalConsumed = false;
            if (modeChanged || !mode || Player.whoAmI != Main.myPlayer || !Player.active || Player.dead
                || Main.gameMenu || Player.CCed || Player.shimmering
                || DashKeybinds.VanillaDashKey?.JustPressed != true) return;

            horizontalRequest = ResolveHorizontalDirection(Player);
        }

        internal static int ResolveHorizontalDirection(Player player)
        {
            if (player.controlRight != player.controlLeft) return player.controlRight ? 1 : -1;
            if (Math.Abs(player.velocity.X) > 0.01f) return Math.Sign(player.velocity.X);
            return player.direction < 0 ? -1 : 1;
        }

        internal int PeekHorizontalRequest()
        {
            PrepareHorizontalInput();
            return horizontalConsumed || ActiveEffect != null ? 0 : horizontalRequest;
        }

        internal int ConsumeHorizontalRequest()
        {
            int direction = PeekHorizontalRequest();
            horizontalConsumed = true;
            return direction;
        }

        internal int TakeCalamityHorizontalRequest(bool force)
        {
            // 蓄力完成是同一次冲刺的内部续执行，不再次要求按键、不再次消费。
            if (force) return calamityStartupDirection != 0 ? calamityStartupDirection : ResolveHorizontalDirection(Player);
            int direction = ConsumeHorizontalRequest();
            if (direction != 0) calamityStartupDirection = direction;
            return direction;
        }

        internal void ClearDashInput()
        {
            _dashTimeMod = 0;
            inputTick = Main.GameUpdateCount;
            lastSingleTapMode = SingleTapMode;
            horizontalRequest = 0;
            horizontalConsumed = true;
            calamityStartupDirection = 0;
        }

        public override void UpdateDead()
        {
            if (ActiveEffect != null) FinishDash(false);
            ClearDashInput();
        }

        public override void OnEnterWorld() => ResetDashSession();

        internal void ResetDashSession()
        {
            ActiveEffect = null;
            ActiveConfig = default;
            ElapsedFrames = 0;
            LongDashCooldown = ShortDashCooldown = VanillaDashCooldown = 0;
            HitCount = 0;
            Array.Clear(HitTargets);
            ClearDashInput();
        }
    }
}
