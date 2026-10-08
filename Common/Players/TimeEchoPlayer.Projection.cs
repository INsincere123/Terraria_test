using System.Collections.Generic;
using Terraria;
using TestMod.Common.GlobalNPCs;

namespace TestMod.Common.Players
{
    public sealed partial class TimeEchoPlayer
    {
        // 静态免疫按原弹幕类型共享，但与玩家原攻击的全局静态表隔离。
        private readonly Dictionary<(int Type, int Npc), (NPC Entity, uint Generation, ulong Until)> projectionImmunity = new();
        private ulong projectionSpawnTick = ulong.MaxValue;
        private int projectionSpawns;

        internal bool TryStartProjection()
        {
            if (projectionSpawnTick != Main.GameUpdateCount)
            { projectionSpawnTick = Main.GameUpdateCount; projectionSpawns = 0; }
            return projectionSpawns++ < 64;
        }

        internal bool HasProjectionImmunity(int type, NPC target)
            => projectionImmunity.TryGetValue((type, target.whoAmI), out var entry) &&
                ReferenceEquals(entry.Entity, target) && entry.Generation == target.GetGlobalNPC<HookTargetIdentity>().Generation &&
                entry.Until > Main.GameUpdateCount;

        internal void SetProjectionImmunity(int type, NPC target, int ticks)
            => projectionImmunity[(type, target.whoAmI)] =
                (target, target.GetGlobalNPC<HookTargetIdentity>().Generation, Main.GameUpdateCount + (ulong)ticks);
    }
}
