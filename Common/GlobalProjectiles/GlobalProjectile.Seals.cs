using System;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using TestMod.Common.Players;

namespace TestMod.Common.GlobalProjectiles
{
    public partial class GlobalProjectile
    {
        private bool _rangerSealRocketExtraApplied;

        /// <summary>
        /// 射手之证：火箭弹幕 extraUpdates +1（每帧重新判定，卸装恢复）。
        /// owner 判定后同步本来源贡献；远端只应用增减量，不覆盖其他来源的 extraUpdates。
        /// </summary>
        private void Seals_UpdateRocketExtraUpdates(Projectile p)
        {
            if (p.owner != Main.myPlayer || p.owner < 0 || p.owner >= Main.maxPlayers) return;
            bool enabled = IsIndependentRangedFlight(p) && !p.trap && vortexAmmoCategory == AmmoID.Rocket &&
                Main.player[p.owner].active &&
                Main.player[p.owner].TryGetModPlayer(out VortexQuiverPlayer state) &&
                state.IsRangerSeal(AmmoID.Rocket);

            if (Seals_SetRocketExtraUpdates(p, enabled)) p.netUpdate = true;
        }

        private bool Seals_SetRocketExtraUpdates(Projectile p, bool enabled)
        {
            if (enabled == _rangerSealRocketExtraApplied) return false;
            p.extraUpdates = Math.Max(0, p.extraUpdates + (enabled ? 1 : -1));
            _rangerSealRocketExtraApplied = enabled;
            return true;
        }
    }
}
