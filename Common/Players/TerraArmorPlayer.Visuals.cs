using System;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;
using Terraria.Graphics.Shaders;
using Terraria.ID;
using Terraria.ModLoader;
using TestMod.Common.Systems;
using TestMod.Content.Items.Armor.TerraArmor;

namespace TestMod.Common.Players
{
    public partial class TerraArmorPlayer
    {
        private Vector2[] _attackBeetlePositions, _attackBeetleVelocities, _defenseBeetlePositions, _defenseBeetleVelocities;
        private int _beetleVisualFrame;
        private bool _maintainedBeetleBuffs;

        public override void FrameEffects()
        {
            if (Main.dedServ) return;
            Player.head = ModContent.GetInstance<TerraHelmet>().MatchVisualSlot(Player.head, Mode);
            Player.body = ModContent.GetInstance<TerraBreastplate>().MatchVisualSlot(Player.body, Mode);
            Player.legs = ModContent.GetInstance<TerraLeggings>().MatchVisualSlot(Player.legs, Mode);
        }

        public override void PostUpdateRunSpeeds()
        {
            // 原版的早期减速对本套跳过，在翅膀等完成速度设置后结算一次。
            if (!IsMode(TerraArmorMode.Ranger) || !Player.vortexStealthActive || Player.mount.Active) return;
            Player.accRunSpeed *= 0.3f;
            Player.maxRunSpeed *= 0.3f;
        }

        internal bool UpdateBeetleBuff(int buffIndex)
        {
            int id = Player.buffType[buffIndex];
            if (id < BuffID.BeetleEndurance1 || id > BuffID.BeetleMight3) return false;
            int first = id <= BuffID.BeetleEndurance3 ? BuffID.BeetleEndurance1 : BuffID.BeetleMight1;
            int stacks = first == BuffID.BeetleEndurance1 ? _beetleDefenseStacks : _beetleAttackStacks;
            // 只展示独立层数；原版共享 beetleOrbs 的处理不能再结算属性或互相删除 Buff。
            Player.buffTime[buffIndex] = id == first + stacks - 1 && stacks > 0 ? 5 : 1;
            return true;
        }

        private void MaintainBeetleBuffs()
        {
            _maintainedBeetleBuffs = true;
            MaintainBeetleBuffGroup(BuffID.BeetleMight1, _beetleAttackStacks);
            MaintainBeetleBuffGroup(BuffID.BeetleEndurance1, _beetleDefenseStacks);
            if (Main.dedServ) return;
            _attackBeetlePositions ??= new Vector2[3];
            _attackBeetleVelocities ??= new Vector2[3];
            _defenseBeetlePositions ??= new Vector2[3];
            _defenseBeetleVelocities ??= new Vector2[3];
            _beetleVisualFrame = (_beetleVisualFrame + 1) % 3;
            UpdateBeetlePositions(_attackBeetlePositions, _attackBeetleVelocities, _beetleAttackStacks);
            UpdateBeetlePositions(_defenseBeetlePositions, _defenseBeetleVelocities, _beetleDefenseStacks);
        }

        private void MaintainBeetleBuffGroup(int first, int stacks)
        {
            for (int id = first; id < first + 3; id++)
                if (stacks == 0 || id != first + stacks - 1) Player.ClearBuff(id);
            if (stacks > 0) Player.AddBuff(first + stacks - 1, 5);
        }

        private void UpdateBeetlePositions(Vector2[] positions, Vector2[] velocities, int stacks)
        {
            for (int i = stacks; i < 3; i++) positions[i] = Vector2.Zero;
            for (int i = 0; i < stacks; i++)
            {
                positions[i] += velocities[i];
                velocities[i] += new Vector2(Main.rand.Next(-100, 101), Main.rand.Next(-100, 101)) * 0.005f;
                float distance = positions[i].Length();
                if (distance > 100f) velocities[i] = (velocities[i] * 9f - positions[i] * (20f / distance)) / 10f;
                else if (distance > 30f) velocities[i] = (velocities[i] * 19f - positions[i] * (10f / distance)) / 20f;
                if (velocities[i].Length() > 2f) velocities[i] *= 0.9f;
                positions[i] -= Player.velocity * 0.25f;
            }
        }

        internal void DrawBeetles(TerraArmorSystem.DrawLayer orig, ref PlayerDrawSet drawInfo)
        {
            if (!IsMode(TerraArmorMode.Warrior) || _attackBeetlePositions == null)
            {
                orig(ref drawInfo);
                return;
            }
            // 仅在原版绘制调用中临时提供两个独立甲虫群，避免影响原版受伤和命中结算。
            int orbs = Player.beetleOrbs, frame = Player.beetleFrame;
            bool offense = Player.beetleOffense, defense = Player.beetleDefense;
            Vector2[] positions = Player.beetlePos, velocities = Player.beetleVel;
            try
            {
                Player.beetleFrame = _beetleVisualFrame;
                Player.beetleOffense = true;
                Player.beetleDefense = false;
                Player.beetleOrbs = _beetleAttackStacks;
                Player.beetlePos = _attackBeetlePositions;
                Player.beetleVel = _attackBeetleVelocities;
                orig(ref drawInfo);
                Player.beetleOffense = false;
                Player.beetleDefense = true;
                Player.beetleOrbs = _beetleDefenseStacks;
                Player.beetlePos = _defenseBeetlePositions;
                Player.beetleVel = _defenseBeetleVelocities;
                orig(ref drawInfo);
            }
            finally
            {
                Player.beetleOrbs = orbs;
                Player.beetleFrame = frame;
                Player.beetleOffense = offense;
                Player.beetleDefense = defense;
                Player.beetlePos = positions;
                Player.beetleVel = velocities;
            }
        }

        private void UpdateSolarVisuals(bool charged)
        {
            if (Main.dedServ) return;
            if (charged)
            {
                for (int i = 0; i < 16; i++)
                {
                    Dust dust = Dust.NewDustDirect(Player.position, Player.width, Player.height, DustID.Torch, Alpha: 100);
                    dust.noGravity = true;
                    dust.scale = 1.7f;
                    dust.fadeIn = 0.5f;
                    dust.velocity *= 5f;
                    dust.shader = GameShaders.Armor.GetSecondaryShader(Player.ArmorSetDye(), Player);
                }
            }
            for (int i = Player.solarShields; i < 3; i++) Player.solarShieldPos[i] = Vector2.Zero;
            for (int i = 0; i < Player.solarShields; i++)
            {
                Player.solarShieldPos[i] += Player.solarShieldVel[i];
                Vector2 target = (Player.miscCounter / 100f * MathHelper.TwoPi + i * MathHelper.TwoPi / Player.solarShields)
                    .ToRotationVector2() * 6f;
                target.X = Player.direction * (Player.mount.Active && Player.mount.Type == MountID.Wolf ? 50 : 20);
                Player.solarShieldVel[i] = (target - Player.solarShieldPos[i]) * 0.2f;
            }
        }
    }
}
