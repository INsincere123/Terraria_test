using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using TestMod.Common.Systems;

namespace TestMod.Common.GlobalNPCs
{
    // 只暂停地牢仪式实体，不改写 ai[]、仇恨状态或仪式碑的召唤进度。
    public class CultistRitualProtectionGlobalNPC : Terraria.ModLoader.GlobalNPC
    {
        public override bool InstancePerEntity => true;

        public override bool AppliesToEntity(NPC entity, bool lateInstantiation) =>
            entity.type == NPCID.CultistArcherBlue || entity.type == NPCID.CultistDevote ||
            entity.type == NPCID.CultistTablet;

        private bool _protected;
        private bool _originalDontTakeDamage;
        private Vector2 _originalVelocity;
        private bool _allowCleanupDeath;

        public override void SetDefaults(NPC npc)
        {
            _protected = false;
            _originalDontTakeDamage = false;
            _originalVelocity = Vector2.Zero;
            _allowCleanupDeath = false;
        }

        internal bool UpdateProtection(NPC npc)
        {
            if (_allowCleanupDeath)
                return false;
            if (!CultistSummonSystem.HasItemSummonedCultist())
            {
                Restore(npc);
                return false;
            }

            if (!_protected)
            {
                _originalDontTakeDamage = npc.dontTakeDamage;
                _originalVelocity = npc.velocity;
                _protected = true;
            }
            npc.dontTakeDamage = true;
            npc.velocity = Vector2.Zero;
            npc.justHit = false;
            return true;
        }

        internal void Restore(NPC npc)
        {
            if (!_protected)
                return;
            npc.dontTakeDamage = _originalDontTakeDamage;
            npc.velocity = _originalVelocity;
            _protected = false;
        }

        internal void KillForSummon(NPC npc)
        {
            // 只允许服务端模式二的这次清理绕过保护；普通攻击和持续伤害仍受拦截。
            _allowCleanupDeath = true;
            try
            {
                Restore(npc);
                npc.dontTakeDamage = false;
                npc.StrikeInstantKill();
                // 其他死亡拦截（如时停）也不能留下仪式成员；最终状态由服务器同步。
                npc.life = 0;
                npc.active = false;
            }
            finally
            {
                _allowCleanupDeath = false;
            }
        }

        public override bool PreAI(NPC npc) => !UpdateProtection(npc);

        // Boss 战斗远离地牢时，也保留暂停中的仪式实体。
        public override bool CheckActive(NPC npc) => !UpdateProtection(npc);

        public override bool CheckDead(NPC npc)
        {
            if (!UpdateProtection(npc))
                return true;
            // 使用物品前留下的持续伤害也不能在战斗期间杀死仪式教徒。
            npc.life = System.Math.Max(npc.life, 1);
            return false;
        }

        public override bool CanHitPlayer(NPC npc, Player target, ref int cooldownSlot) => !UpdateProtection(npc);

        public override bool? CanBeHitByItem(NPC npc, Player player, Item item) =>
            UpdateProtection(npc) ? false : null;

        public override bool? CanBeHitByProjectile(NPC npc, Projectile projectile) =>
            UpdateProtection(npc) ? false : null;
    }
}
