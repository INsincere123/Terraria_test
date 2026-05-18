using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using System;
using TestMod.Common.Players;
using Terraria.GameContent.ItemDropRules;
using TestMod.Items.Accessories;
using TestMod.Items.Weapons.Melee;
using TestMod.Common.Mechanics.ArmorShred;
using TestMod.Common.Systems;
using TestMod.Common.Utilities;
using TestMod.Items.DamageTypes;

namespace TestMod.Common.GlobalNPCs
{
    /// <summary>
    /// 全局 NPC 钩子：破甲 debuff 应用 + 掉落修改 + debuff 跳伤强化。
    /// 破甲层数数据由 ArmorShredSystem 管理，本类只负责 hook 响应。
    /// </summary>
    public class TestGlobalNPC : GlobalNPC
    {
        private const int BuffID_Celled    = BuffID.StardustMinionBleed; // 星尘细胞 debuff
        private const int BuffID_Daybroken = BuffID.Daybreak;           // 破晓之光 debuff

        // ══════════════════════════════════════════════════════════════
        //   ModifyNPCLoot — NPC 掉落修改
        // ══════════════════════════════════════════════════════════════
        public override void ModifyNPCLoot(NPC npc, NPCLoot npcLoot)
        {
            // 光之女皇 1/4 概率掉落 SwordQiSword
            if (npc.type == NPCID.HallowBoss)
            {
                npcLoot.Add(ItemDropRule.Common(ModContent.ItemType<SwordQiSword>(), 4));
                // 全程白天造成伤害时 100% 掉落天赐华冠（与泰拉棱镜相同条件）
                var rule = new LeadingConditionRule(new Conditions.EmpressOfLightIsGenuinelyEnraged());
                rule.OnSuccess(ItemDropRule.Common(ModContent.ItemType<HeavenlyCrown>()));
                npcLoot.Add(rule);
            }

            // 独眼巨鹿 1/4 概率掉落 AshenSeal
            if (npc.type == NPCID.Deerclops)
                npcLoot.Add(ItemDropRule.Common(ModContent.ItemType<AshenSeal>(), 4));

            // 所有僵尸、恶魔眼 1/15 概率掉落阿比盖尔之花
            if (NPCID.Sets.DemonEyes[npc.type] || NPCID.Sets.Zombies[npc.type])
                npcLoot.Add(ItemDropRule.Common(ItemID.AbigailsFlower, 15));
        }

        // ══════════════════════════════════════════════════════════════
        //   ModifyIncomingHit — 应用破甲层数减少防御
        // ══════════════════════════════════════════════════════════════
        public override void ModifyIncomingHit(NPC npc, ref NPC.HitModifiers modifiers)
        {
            // 破甲层数减少防御
            int stacks = ArmorShredSystem.GetStacks(npc.whoAmI);
            if (stacks > 0)
                modifiers.Defense.Flat -= stacks * ArmorShredSystem.DefensePerStack;

            // 真实伤害：100% 穿甲，对所有来源生效（含 SimpleStrikeNPC）
            // GlobalItem.ModifyHitNPC 只覆盖物品直接命中，此处补全其余路径
            if (modifiers.DamageType == TrueDamageClass.Instance)
                modifiers.ScalingArmorPenetration += 1f;
        }

        // ══════════════════════════════════════════════════════════════
        //   OnKill — NPC 死亡：清理破甲层数 + 黄泉馈灵塔 buff 派发
        // ══════════════════════════════════════════════════════════════
        public override void OnKill(NPC npc)
        {
            // 破甲层数清理（始终执行）
            ArmorShredSystem.Remove(npc.whoAmI);

            // 收集者系统：与祭坛无关，必须在祭坛范围检查之前调用（避免被提前 return 拦截）
            CollectorSystem.OnNpcKilled(npc);

            // 黄泉馈灵塔：仅在激活范围内（范围内有 Boss）才处理
            if (!ArenaAltarSystem.IsInActiveRange(npc.Center)) return;

            if (npc.friendly && !npc.boss)
            {
                // 友方 NPC 死亡（村民等）→ 给范围内存活玩家施加汲命 buff
                ArenaAltarSystem.GrantLifestealToNearby();
            }
            else if (!npc.friendly && !npc.townNPC)
            {
                // 敌方 NPC 死亡 → 给范围内存活玩家施加杀意 buff
                ArenaAltarSystem.GrantDamageBoostToNearby();
            }
        }

        // ══════════════════════════════════════════════════════════════
        //   UpdateLifeRegen — debuff 跳伤强化 + 破甲 buff 失效时清理层数
        // ══════════════════════════════════════════════════════════════
        public override void UpdateLifeRegen(NPC npc, ref int damage)
        {
            // 破甲 debuff 消失时同步清除层数
            if (!npc.HasBuff(ModContent.BuffType<Buffs.ArmorShredDebuff>()))
                ArmorShredSystem.Remove(npc.whoAmI);

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