using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using TestMod.Content.Projectiles.Accessories;

namespace TestMod.Common.Players
{
    public class EnhancedStaticHookPlayer : ModPlayer
    {
        private EnhancedStaticHookProj _hook;
        private float _appliedReduction;

        public override void ResetEffects() => _appliedReduction = 0f;
        public override void OnEnterWorld()
        {
            _hook = null;
            _appliedReduction = 0f;
        }

        internal void Track(EnhancedStaticHookProj hook)
        {
            // 只在发射时扫描，替换该玩家的旧钩；切换抓钩后也不混入第二个拉力钩点。
            for (int i = 0; i < Main.maxProjectiles; i++)
            {
                Projectile oldHook = Main.projectile[i];
                if (oldHook != hook.Projectile && oldHook.active && oldHook.owner == Player.whoAmI
                    && oldHook.aiStyle == ProjAIStyleID.Hook)
                    oldHook.Kill();
            }
            _hook = hook;
            RefreshReduction();
        }

        internal void Forget(EnhancedStaticHookProj hook)
        {
            if (_hook != hook)
                return;
            _hook = null;
            RefreshReduction();
        }

        internal void RefreshReduction()
        {
            float reduction = 0f;
            if (!Player.dead && HasLiveHook)
                reduction = _hook.GetAttachedReduction();
            // 状态在玩家更新后变化时也立刻撤销本 tick 的加成，不留下松钩窗口。
            Player.endurance += reduction - _appliedReduction;
            _appliedReduction = reduction;
        }

        public override void PostUpdateMiscEffects() => RefreshReduction();

        private bool HasLiveHook => _hook?.Projectile.active == true
            && _hook.Projectile.ModProjectile == _hook && _hook.Projectile.owner == Player.whoAmI;

        public override void UpdateDead()
        {
            if (HasLiveHook)
                _hook.Projectile.Kill();
            _hook = null;
            _appliedReduction = 0f;
        }
    }
}
