using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Terraria;

namespace TestMod.Common.GlobalProjectiles
{
    /// <summary>
    /// 方案四：追踪弹幕失明。
    /// 当任意 Boss 处于失明状态时（global::TestMod.Common.GlobalNPCs.GlobalNPC.AnyBlindBossActive），
    /// 对所有敌方追踪弹幕在 AI 执行前替换玩家坐标为假坐标，AI 执行后立即还原。
    /// 非追踪弹幕已由 GlobalNPC 在 Boss 发射时就打歪了，无需处理。
    /// </summary>
    public partial class GlobalProjectile
    {
        // 已知的追踪 aiStyle。aiStyle=12 覆盖蜜蜂/魔法导弹等标准追踪弹幕。
        // 如有其他追踪 aiStyle 需要处理，在此追加。
        private static readonly HashSet<int> HomingAiStyles = new HashSet<int> { 12 };

        // 顺序复用的静态字典（弹幕 AI 单线程顺序执行，无并发问题）
        private static readonly Dictionary<int, Vector2> _projBlindPositions = new();

        // 本实例本帧是否执行了欺骗，控制 PostAI 是否需要还原
        private bool _didSpoof;

        // ══════════════════════════════════════════════════════════════════
        private bool Blindness_PreAI(Projectile projectile)
        {
            _didSpoof = false;

            if (!projectile.hostile
                || !global::TestMod.Common.GlobalNPCs.GlobalNPC.AnyBlindBossActive
                || !HomingAiStyles.Contains(projectile.aiStyle))
                return true;

            global::TestMod.Common.GlobalNPCs.GlobalNPC.SpoofPlayerPositions(
                global::TestMod.Common.GlobalNPCs.GlobalNPC.BlindBossCenter,
                global::TestMod.Common.GlobalNPCs.GlobalNPC.BlindBossCenter + global::TestMod.Common.GlobalNPCs.GlobalNPC.CurrentFakeOffset,
                _projBlindPositions);
            _didSpoof = true;
            return true;
        }

        private void Blindness_PostAI(Projectile projectile)
        {
            if (_didSpoof)
                global::TestMod.Common.GlobalNPCs.GlobalNPC.RestorePlayerPositions(_projBlindPositions);
        }
    }
}
