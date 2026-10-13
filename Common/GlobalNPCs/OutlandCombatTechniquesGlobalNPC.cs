using System;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using TestMod.Common.Systems;

namespace TestMod.Common.GlobalNPCs
{
    public sealed class OutlandCombatTechniquesGlobalNPC : Terraria.ModLoader.GlobalNPC
    {
        public override bool InstancePerEntity => true;
        private bool lifeBonusApplied;

        public override void SetDefaults(NPC npc)
        {
            lifeBonusApplied = false;
            // 在生成/读档/网络接收生命值之前确定上限，新生成的 NPC 正常满血。
            ApplyLifeBonus(npc);
        }

        internal void ApplyLifeBonus(NPC npc, bool initializeLife = false)
        {
            if (lifeBonusApplied || !OutlandCombatTechniquesSystem.VolumeTwoUsed || !npc.isLikeATownNPC)
                return;
            npc.lifeMax += 1000;
            // 世界数据晚于 NPC 读取：补齐生成阶段应有的生命，保留其他来源造成的缺血。
            // 游戏中首次使用卷二和客户端世界状态刷新均不治疗。
            if (initializeLife)
                npc.life = Math.Min(npc.lifeMax, npc.life + 1000);
            lifeBonusApplied = true;
            if (Main.netMode == NetmodeID.Server) npc.netUpdate = true;
        }

        public override void PostAI(NPC npc) => ApplyLifeBonus(npc);

        public override void BuffTownNPC(ref float damageMult, ref int defense)
        {
            if (OutlandCombatTechniquesSystem.VolumeOneUsed)
            {
                damageMult += 0.2f;
                defense += 6;
            }
            if (OutlandCombatTechniquesSystem.VolumeTwoUsed)
            {
                damageMult *= 2.5f;
                defense += 100;
            }
        }

        public override void TownNPCAttackCooldown(NPC npc, ref int cooldown, ref int randExtraCooldown)
        {
            if (!OutlandCombatTechniquesSystem.VolumeTwoUsed || !npc.isLikeATownNPC) return;
            cooldown = Math.Max(1, (int)Math.Round(cooldown / 1.5));
            randExtraCooldown = Math.Max(0, (int)Math.Round(randExtraCooldown / 1.5));
        }
    }
}
