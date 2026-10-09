using System;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using TestMod.Common.Systems;

namespace TestMod.Common.GlobalNPCs
{
    public partial class GlobalNPC
    {
        // 1.4.4 原版带电只实现了玩家效果；灾厄已提供敌怪实现时不重复处理。
        private static bool HasFallbackElectrified(NPC npc) => !CalamityCompatSystem.CalamityLoaded &&
            !npc.buffImmune[BuffID.Electrified] && npc.HasBuff(BuffID.Electrified);

        private static void Electrified_UpdateLifeRegen(NPC npc, ref int damage)
        {
            if (!HasFallbackElectrified(npc)) return;
            int damagePerSecond = npc.velocity == Vector2.Zero ? 5 : 20;
            if (npc.lifeRegen > 0) npc.lifeRegen = 0;
            npc.lifeRegen -= damagePerSecond * 2;
            damage = Math.Max(damage, damagePerSecond / 5);
        }

        public override void DrawEffects(NPC npc, ref Color drawColor)
        {
            if (Main.netMode == NetmodeID.Server || !HasFallbackElectrified(npc) || !Main.rand.NextBool(4)) return;
            Dust dust = Dust.NewDustDirect(npc.position, npc.width, npc.height, DustID.Electric,
                0f, 0f, 100, default, 0.65f);
            dust.noGravity = true;
            dust.velocity *= 0.25f;
        }
    }
}
