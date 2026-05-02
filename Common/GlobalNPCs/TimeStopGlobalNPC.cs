using TestMod.Common.Players;
using Terraria;
using Terraria.ModLoader;

namespace TestMod.Common.GlobalNPCs
{
    /// <summary>
    /// 时停期间拦截 NPC 的 AI、伤害判定、死亡判定。
    /// 排除：小镇 NPC、友方 NPC（避免视觉怪异）。
    /// </summary>
    public class TimeStopGlobalNPC : GlobalNPC
    {
        // 时停不需要每实例数据，单纯用 LocalPlayer 的状态来查询
        public override bool InstancePerEntity => false;

        /// <summary>
        /// 当前是否应该冻结此 NPC。
        /// </summary>
        private static bool ShouldFreeze(NPC npc)
        {
            // 跳过小镇 NPC 和友方 NPC，避免他们也僵死
            if (npc.townNPC || npc.friendly) return false;

            // 查询本机玩家的时停状态
            // 注：单人 / 本机视角下，Main.LocalPlayer 即玩家本人；
            // 多人将来需要扩展为遍历所有激活时停的玩家
            if (!Main.LocalPlayer.active) return false;

            TimeStopPlayer modPlayer = Main.LocalPlayer.GetModPlayer<TimeStopPlayer>();
            return modPlayer.TimeStopActive;
        }

        public override bool PreAI(NPC npc)
        {
            if (ShouldFreeze(npc))
            {
                // 回滚位置，等同于"它没动过"
                npc.position = npc.oldPosition;
                // 锁动画帧
                npc.frameCounter = 0;
                // 清零速度，避免离开时停后继续按冻结期间残留速度乱飞
                npc.velocity = npc.oldVelocity;
                return false;
            }
            return true;
        }

        /// <summary>
        /// 时停期间不能被打死。被打到血量耗尽时，把血量锁回 1 并阻止死亡。
        /// </summary>
        public override bool CheckDead(NPC npc)
        {
            if (ShouldFreeze(npc))
            {
                npc.life = 1;
                return false;
            }
            return true;
        }

        /// <summary>
        /// 时停期间 NPC 不伤害玩家（接触伤害禁用）。
        /// </summary>
        public override bool CanHitPlayer(NPC npc, Player target, ref int CooldownSlot)
        {
            if (ShouldFreeze(npc))
                return false;
            return true;
        }

        /// <summary>
        /// 配合 CheckDead：当 NPC 已被锁定为 life=1 时，禁止再被命中。
        /// 否则会出现"打一下血量被锁回1，又被打一下，又锁回1"的 OnHit 反复触发。
        /// </summary>
        public override bool? CanBeHitByItem(NPC npc, Player player, Item item)
        {
            if (ShouldFreeze(npc) && npc.life == 1)
                return false;
            return null;
        }

        public override bool? CanBeHitByProjectile(NPC npc, Projectile projectile)
        {
            if (ShouldFreeze(npc) && npc.life == 1)
                return false;
            return null;
        }
    }
}
