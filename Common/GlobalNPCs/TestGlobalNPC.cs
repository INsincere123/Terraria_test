using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using System;
using System.Collections.Generic;
using TestMod.Common.Players;
using Terraria.GameContent.ItemDropRules;
using TestMod.Items.Accessories;

namespace TestMod.Common.GlobalNPCs
{
    /// <summary>
    /// 全局 NPC 钩子：破甲debuff层数管理 + debuff跳伤强化
    /// </summary>
    public class TestGlobalNPC : GlobalNPC
    {
        private const int BuffID_Celled = BuffID.StardustMinionBleed; // 星尘细胞 debuff
        private const int BuffID_Daybroken = BuffID.Daybreak; // 破晓之光 debuff

        // ══════════════════════════════════════════════════════════════
        //   破甲层数字典（key = npc.whoAmI，最多10层，每层-10护甲）
        // ══════════════════════════════════════════════════════════════
        private static readonly Dictionary<int, int> _armorShredStacks = new Dictionary<int, int>();

        public static int GetArmorShredStacks(int npcWhoAmI) =>
            _armorShredStacks.TryGetValue(npcWhoAmI, out int stacks) ? stacks : 0;

        public static void AddArmorShredStack(int npcWhoAmI)
        {
            int current = GetArmorShredStacks(npcWhoAmI);
            _armorShredStacks[npcWhoAmI] = Math.Min(current + 1, 10);
        }

        // ══════════════════════════════════════════════════════════════
        //   ModifyNPCLoot — NPC掉落修改
        // ══════════════════════════════════════════════════════════════   
        public override void ModifyNPCLoot(NPC npc, NPCLoot npcLoot)
        {
            if (npc.type == NPCID.Deerclops)
            {
                npcLoot.Add(ItemDropRule.Common(ModContent.ItemType<AshenSeal>(), 4));  // 独眼巨鹿有1/4的概率掉落AshenSeal
            }

            if (NPCID.Sets.DemonEyes[npc.type] || NPCID.Sets.Zombies[npc.type])
            {
                npcLoot.Add(ItemDropRule.Common(ItemID.AbigailsFlower, 15));  // 所有僵尸、恶魔眼都有1/10的概率掉落阿比盖尔之花
            }
        }

        // ══════════════════════════════════════════════════════════════
        //   ModifyIncomingHit — 应用破甲层数减少防御
        // ══════════════════════════════════════════════════════════════
        public override void ModifyIncomingHit(NPC npc, ref NPC.HitModifiers modifiers)
        {
            int stacks = GetArmorShredStacks(npc.whoAmI);
            if (stacks > 0)
                modifiers.Defense.Flat -= stacks * 10;
        }

        // ══════════════════════════════════════════════════════════════
        //   OnKill — NPC 死亡时清理字典
        // ══════════════════════════════════════════════════════════════
        public override void OnKill(NPC npc) => _armorShredStacks.Remove(npc.whoAmI);

        // ══════════════════════════════════════════════════════════════
        //   UpdateLifeRegen — debuff跳伤强化 + 破甲debuff失效时清理层数
        // ══════════════════════════════════════════════════════════════
        public override void UpdateLifeRegen(NPC npc, ref int damage)
        {
            // 破甲debuff消失时同步清除层数
            if (!npc.HasBuff(ModContent.BuffType<Buffs.ArmorShredDebuff>()))
                _armorShredStacks.Remove(npc.whoAmI);

            // 检查是否有玩家开启 godMode
            bool anyGodMode = false;
            for (int i = 0; i < Main.maxPlayers; i++)
            {
                Player p = Main.player[i];
                if (p.active && p.GetModPlayer<CorePlayer>().godModeBuff)
                {
                    anyGodMode = true;
                    break;
                }
            }
            if (!anyGodMode) return;

            // 🧠 Celled ×36倍
            if (npc.HasBuff(BuffID_Celled))
            {
                npc.lifeRegen -= 80 * 170;
                damage = Math.Max(damage, 40 * 180);
            }

            // ☀️ Daybroken ×10倍
            if (npc.HasBuff(BuffID_Daybroken))
            {
                npc.lifeRegen -= 200 * 9;
                damage = Math.Max(damage, 100 * 10);
            }
        }
    }
}