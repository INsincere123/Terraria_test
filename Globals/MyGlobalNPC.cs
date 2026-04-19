using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using System;
using System.Collections.Generic;

namespace 武器test
{
    /// <summary>
    /// 全局 NPC 钩子：破甲debuff层数管理 + debuff跳伤强化
    /// </summary>
    public class MyGlobalNPC : GlobalNPC
    {
        private const int BuffID_Celled    = 182; // 星尘细胞 debuff
        private const int BuffID_Daybroken = 189; // 破晓之光 debuff

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
                if (p.active && p.GetModPlayer<MyPlayer>().godModeBuff)
                {
                    anyGodMode = true;
                    break;
                }
            }
            if (!anyGodMode) return;

            // 🧠 Celled ×18倍
            if (npc.HasBuff(BuffID_Celled))
            {
                npc.lifeRegen -= 40 * 17;
                damage = Math.Max(damage, 20 * 18);
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