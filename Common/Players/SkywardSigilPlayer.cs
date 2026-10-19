using System;
using Terraria;
using Terraria.ModLoader;
using TestMod.Content.Buffs;

namespace TestMod.Common.Players
{
    public class SkywardSigilPlayer : ModPlayer
    {
        public const int MaxStacks = 6;
        public const int StackDuration = 300; // 游戏 tick：5 秒。
        public const int StackInterval = 30; // 所有目标、来源与方向共享 0.5 秒间隔。
        public const float CritDamagePerStack = 0.05f;

        public bool HasSkywardSigil;
        public int OverlookStacks { get; private set; }
        public int UpwardStacks { get; private set; }
        public int NetStacks => OverlookStacks - UpwardStacks;
        private ulong _expiresAt;
        private ulong _nextStackAt;

        public override void ResetEffects() => HasSkywardSigil = false;

        public override void PreUpdate()
        {
            if (_expiresAt != 0 && Main.GameUpdateCount >= _expiresAt)
                ClearState();
        }

        public override void PostUpdateEquips()
        {
            if (!HasSkywardSigil || Player.dead)
                ClearState();
        }

        public override void PostUpdate()
        {
            if (!HasSkywardSigil || Player.dead)
                ClearState();
            else
                UpdateDisplayBuff();
        }

        public override void ModifyHitNPC(NPC target, ref NPC.HitModifiers modifiers)
        {
            // 只修改暴击部分；新层数在 OnHit 后取得，不追溯影响触发它的攻击。
            if (HasSkywardSigil && !Player.dead && Main.GameUpdateCount < _expiresAt)
                modifiers.CritDamage += NetStacks * CritDamagePerStack;
        }

        public override void OnHitNPCWithItem(Item item, NPC target, NPC.HitInfo hit, int damageDone)
            => TryStack(target, damageDone);

        public override void OnHitNPCWithProj(Projectile proj, NPC target, NPC.HitInfo hit, int damageDone)
        {
            if (proj.owner != Player.whoAmI || proj.npcProj || proj.trap)
                return;

            var state = proj.GetGlobalProjectile<global::TestMod.Common.GlobalProjectiles.GlobalProjectile>();
            if (state.IsExtraHit || state.IsTimeEchoAttack)
                return;

            TryStack(target, damageDone);
        }

        private void TryStack(NPC target, int damageDone)
        {
            // 致死命中也算成功命中，不以命中后 active/life 拒绝最后一击。
            if (!HasSkywardSigil || Player.dead || Player.whoAmI != Main.myPlayer ||
                damageDone <= 0 || !IsBossPart(target))
                return;

            ulong now = Main.GameUpdateCount;
            if (_expiresAt != 0 && now >= _expiresAt)
                ClearState();
            if (now < _nextStackAt)
                return;

            float heightDifference = Player.Center.Y - target.Center.Y;
            if (heightDifference == 0f)
                return;

            // 新层立即抵消反向层数，避免两组同时满层后持续续时却永远净值为零。
            int net = Math.Clamp(NetStacks + (heightDifference < 0f ? 1 : -1), -MaxStacks, MaxStacks);
            OverlookStacks = Math.Max(net, 0);
            UpwardStacks = Math.Max(-net, 0);

            _expiresAt = now + StackDuration;
            _nextStackAt = now + StackInterval;
            UpdateDisplayBuff();
        }

        private static bool IsBossPart(NPC target)
        {
            if (target.boss)
                return true;
            // realLife 只用于确认 Boss 归属，高度仍读取实际命中的部位。
            int root = target.realLife;
            return root >= 0 && root < Main.maxNPCs && Main.npc[root].boss;
        }

        private void UpdateDisplayBuff()
        {
            int overlook = ModContent.BuffType<SkywardOverlookBuff>();
            int upward = ModContent.BuffType<SkywardUpwardBuff>();
            int stacks = NetStacks;
            if (stacks <= 0) Player.ClearBuff(overlook);
            if (stacks >= 0) Player.ClearBuff(upward);
            if (stacks == 0 || Main.GameUpdateCount >= _expiresAt)
                return;

            int type = stacks > 0 ? overlook : upward;
            int remaining = (int)(_expiresAt - Main.GameUpdateCount);
            int index = Player.FindBuffIndex(type);
            if (index < 0)
            {
                Player.AddBuff(type, remaining);
                index = Player.FindBuffIndex(type);
            }
            // Buff 只是状态镜像，不在 Buff.Update 再计时或添加属性。
            if (index >= 0)
                Player.buffTime[index] = remaining;
        }

        public override void UpdateDead() => ClearState();
        public override void OnEnterWorld() => ClearState();

        private void ClearState()
        {
            OverlookStacks = 0;
            UpwardStacks = 0;
            _expiresAt = 0;
            _nextStackAt = 0;
            Player.ClearBuff(ModContent.BuffType<SkywardOverlookBuff>());
            Player.ClearBuff(ModContent.BuffType<SkywardUpwardBuff>());
        }
    }
}
