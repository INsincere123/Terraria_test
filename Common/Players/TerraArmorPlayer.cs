using System;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.GameInput;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;
using TestMod.Common.Systems;
using TestMod.Common.GlobalProjectiles;
using TestMod.Content.Buffs;
using TestMod.Content.Items.Armor.TerraArmor;

namespace TestMod.Common.Players
{
    public enum TerraArmorMode : byte { Warrior, Ranger, Mage, Summoner }

    public partial class TerraArmorPlayer : ModPlayer
    {
        public const int ReviveCooldownTicks = 120 * 60;
        private const float CloseMeleeRange = 102f;
        private int _closeMeleeHealCooldown;
        public TerraArmorMode Mode { get; private set; }
        public int ReviveCooldown { get; private set; }
        public bool FullSet { get; private set; }
        public bool IsMode(TerraArmorMode mode) => FullSet && Mode == mode;
        private float _beetleCharge;
        private int _beetleCountdown, _beetleAttackStacks, _beetleDefenseStacks, _beetleDefenseTimer;
        private bool _wasWarrior;
        internal bool HasIndependentMagmaStone { get; private set; }

        // 在原版重置装备标志之前也需要判断套装，不能依赖上一帧的 FullSet。
        internal static bool Equipped(Player player) => player.active && !player.dead &&
            player.armor[0].type == ModContent.ItemType<TerraHelmet>() &&
            player.armor[1].type == ModContent.ItemType<TerraBreastplate>() &&
            player.armor[2].type == ModContent.ItemType<TerraLeggings>();

        internal static bool EquippedMode(Player player, TerraArmorMode mode) => Equipped(player) &&
            player.TryGetModPlayer(out TerraArmorPlayer armor) && armor.Mode == mode;

        public override void ResetEffects() => FullSet = false;

        public override void PreUpdate()
        {
            if (ReviveCooldown > 0) ReviveCooldown--;
            if (_closeMeleeHealCooldown > 0) _closeMeleeHealCooldown--;
        }

        public override void UpdateDead()
        {
            FullSet = false;
            ResetBeetles();
        }

        public override void PreUpdateBuffs()
        {
            if (!Equipped(Player)) return;
            // 原版 Buff 早于盔甲更新；提前维持机制标志，防止其自行删除。
            Player.onHitDodge = true;
            Player.onHitRegen = true;
            Player.crystalLeaf = true;
            if (Mode == TerraArmorMode.Warrior) Player.setSolar = true;
            if (Mode == TerraArmorMode.Summoner) Player.setStardust = true;
            for (int index = Player.buffType.Length - 1; index >= 0; index--)
            {
                int id = Player.buffType[index];
                if (id > 0 && id < BuffID.Count && Main.debuff[id]) Player.DelBuff(index);
            }
        }

        public void ApplySet()
        {
            FullSet = true;
            HasIndependentMagmaStone = Player.magmaStone;
            Player.onHitDodge = true;
            Player.onHitRegen = true;
            Player.crystalLeaf = true;
            Player.statLifeMax2 += 200;
            Player.statManaMax2 += 200;
            Player.AddBuff(BuffID.LeafCrystal, 2);
            Player.AddBuff(ModContent.BuffType<GravityNormalizerBuff>(), 2);
            Player.GetModPlayer<GravityNormalizerPlayer>().HasGravityNormalizer = true;
            for (int id = 1; id < BuffID.Count; id++)
                if (Main.debuff[id]) Player.buffImmune[id] = true;

            switch (Mode)
            {
                case TerraArmorMode.Warrior:
                    Player.endurance += 0.15f;
                    Player.aggro += 1500;
                    Player.kbGlove = Player.autoReuseGlove = Player.meleeScaleGlove = Player.magmaStone = true;
                    MaintainSolar();
                    UpdateBeetles();
                    break;
                case TerraArmorMode.Ranger:
                    Player.setVortex = true;
                    Player.aggro -= 400;
                    break;
                case TerraArmorMode.Mage:
                    Player.ghostHeal = Player.ghostHurt = Player.setNebula = true;
                    if (Player.nebulaCD > 0) Player.nebulaCD--;
                    break;
                case TerraArmorMode.Summoner:
                    Player.setSquireT2 = Player.setSquireT3 = true;
                    Player.setHuntressT2 = Player.setHuntressT3 = true;
                    Player.setApprenticeT2 = Player.setApprenticeT3 = true;
                    Player.setMonkT2 = Player.setMonkT3 = true;
                    Player.setStardust = Player.stardustGuardian = true;
                    Player.aggro -= 400;
                    Player.whipRangeMultiplier += 0.5f;
                    MaintainGuardian();
                    break;
            }
            if (Mode != TerraArmorMode.Warrior) ResetBeetles();
            _wasWarrior = Mode == TerraArmorMode.Warrior;
            string mode = Language.GetTextValue($"Mods.TestMod.TerraArmor.Modes.{Mode}");
            Player.setBonus = Language.GetTextValue("Mods.TestMod.TerraArmor.SetBonus", mode,
                KeybindUtils.GetKeyText(TerraArmorSystem.SwitchModeKey),
                Language.GetTextValue($"Mods.TestMod.TerraArmor.Effects.{Mode}"));
        }

        private void MaintainSolar()
        {
            Player.setSolar = true;
            bool charged = false;
            if (++Player.solarCounter >= 180)
            {
                if (Player.solarShields < 3)
                {
                    for (int id = BuffID.SolarShield1; id <= BuffID.SolarShield3; id++) Player.ClearBuff(id);
                    Player.solarShields++;
                    Player.AddBuff(BuffID.SolarShield1 + Player.solarShields - 1, 5);
                    charged = true;
                    Player.solarCounter = 0;
                }
                else Player.solarCounter = 180;
            }
            if (Player.dashDelay >= 0)
            {
                Player.solarDashing = false;
                Player.solarDashConsumedFlare = false;
            }
            if (Player.solarShields > 0 || (Player.solarDashing && Player.dashDelay < 0)) Player.dashType = 3;
            UpdateSolarVisuals(charged);
        }

        private void MaintainGuardian()
        {
            Player.AddBuff(BuffID.StardustGuardianMinion, 2);
            if (Player.whoAmI != Main.myPlayer || Player.ownedProjectileCounts[ProjectileID.StardustGuardian] > 0) return;
            int index = Projectile.NewProjectile(Player.GetSource_Misc("TerraArmor"), Player.Center,
                new Vector2(0, -1), ProjectileID.StardustGuardian, 30, 10, Player.whoAmI);
            if (index < Main.maxProjectiles) Main.projectile[index].originalDamage = 30;
        }

        private void UpdateBeetles()
        {
            _beetleCharge = MathHelper.Clamp(_beetleCharge - 3f - _beetleCountdown / 20, 0f, 6400f);
            _beetleCountdown++;
            int stacks = _beetleCharge > 5200 ? 3 : _beetleCharge > 1600 ? 2 : _beetleCharge > 400 ? 1 : 0;
            if (stacks < _beetleAttackStacks) _beetleCountdown = 0;
            else if (stacks > _beetleAttackStacks) _beetleCharge += 200;
            _beetleAttackStacks = stacks;
            Player.GetDamage(DamageClass.Melee) += 0.1f * stacks;
            Player.GetAttackSpeed(DamageClass.Melee) += 0.1f * stacks;
            if (++_beetleDefenseTimer >= 180)
            {
                if (_beetleDefenseStacks < 3) _beetleDefenseStacks++;
                _beetleDefenseTimer = _beetleDefenseStacks < 3 ? 0 : 180;
            }
            MaintainBeetleBuffs();
        }

        private void ResetBeetles()
        {
            if (_maintainedBeetleBuffs)
            {
                if (!Player.beetleDefense)
                    for (int id = BuffID.BeetleEndurance1; id <= BuffID.BeetleEndurance3; id++) Player.ClearBuff(id);
                if (!Player.beetleOffense)
                    for (int id = BuffID.BeetleMight1; id <= BuffID.BeetleMight3; id++) Player.ClearBuff(id);
                _maintainedBeetleBuffs = false;
            }
            _beetleCharge = 0;
            _beetleCountdown = _beetleAttackStacks = _beetleDefenseStacks = _beetleDefenseTimer = 0;
        }

        public override void PostUpdateEquips()
        {
            if (!FullSet && _wasWarrior) ResetBeetles();
            if (!FullSet) _wasWarrior = false;
        }

        public override void ModifyHurt(ref Player.HurtModifiers modifiers)
        {
            if (IsMode(TerraArmorMode.Warrior)) modifiers.FinalDamage *= 1f - 0.15f * _beetleDefenseStacks;
        }

        public override void OnHurt(Player.HurtInfo info)
        {
            if (!IsMode(TerraArmorMode.Warrior) || _beetleDefenseStacks <= 0) return;
            _beetleDefenseStacks--;
            _beetleDefenseTimer = 0;
        }

        internal bool IsCloseMeleeHit(DamageClass damageClass, NPC target)
        {
            if (!IsMode(TerraArmorMode.Warrior) || !Equipped(Player) ||
                !damageClass.CountsAsClass(DamageClass.Melee) || target.friendly || target.immortal ||
                target.dontTakeDamage || target.type == NPCID.TargetDummy) return false;
            // 按碰撞箱最近点测距，大型敌人无需靠近其中心。
            Rectangle hitbox = target.Hitbox;
            Vector2 closest = Vector2.Clamp(Player.Center, new Vector2(hitbox.Left, hitbox.Top),
                new Vector2(hitbox.Right, hitbox.Bottom));
            return Vector2.DistanceSquared(Player.Center, closest) <= CloseMeleeRange * CloseMeleeRange;
        }

        internal void HealCloseMeleeHit(DamageClass damageClass, NPC target, int damageDone)
        {
            if (Player.whoAmI != Main.myPlayer || _closeMeleeHealCooldown > 0 || damageDone <= 0 ||
                !IsCloseMeleeHit(damageClass, target)) return;
            int heal = Math.Min(10 + Player.statLifeMax2 / 100, Player.statLifeMax2 - Player.statLife);
            if (heal <= 0) return;
            // 所有近战物品及弹幕共用玩家冷却，仅实际回血时启动。
            _closeMeleeHealCooldown = 10;
            Player.Heal(heal);
            if (Main.netMode == NetmodeID.MultiplayerClient)
                NetMessage.SendData(MessageID.PlayerLifeMana, number: Player.whoAmI);
        }

        public override void ModifyHitNPCWithItem(Item item, NPC target, ref NPC.HitModifiers modifiers)
        {
            if (item.damage > 0 && IsCloseMeleeHit(item.DamageType, target))
                modifiers.FinalDamage *= 1.05f;
        }

        internal void RegisterMeleeHit(DamageClass damageClass, NPC target, int damage)
        {
            if (!IsMode(TerraArmorMode.Warrior) || !damageClass.CountsAsClass(DamageClass.Melee) ||
                (target.immortal && target.type != NPCID.TargetDummy)) return;
            _beetleCharge += damage * (_beetleAttackStacks == 0 ? 3 : _beetleAttackStacks == 1 ? 2 : 1);
            _beetleCountdown = 0;
        }

        public override void OnHitNPCWithItem(Item item, NPC target, NPC.HitInfo hit, int damageDone)
        {
            RegisterMeleeHit(item.DamageType, target, damageDone);
            HealCloseMeleeHit(item.DamageType, target, damageDone);
            if (Player.whoAmI == Main.myPlayer && IsMode(TerraArmorMode.Ranger) && item.CountsAsClass(DamageClass.Ranged))
                TerraArmorProjectile.SpawnPhantasm(Player, target, Player.GetSource_ItemUse(item),
                    Player.GetWeaponDamage(item));
        }

        public override void ProcessTriggers(TriggersSet triggersSet)
        {
            if (Player.whoAmI != Main.myPlayer || !Equipped(Player) || TerraArmorSystem.SwitchModeKey?.JustPressed != true) return;
            SetMode((TerraArmorMode)(((int)Mode + 1) % 4));
            Main.NewText(Language.GetTextValue("Mods.TestMod.TerraArmor.ModeChanged",
                Language.GetTextValue($"Mods.TestMod.TerraArmor.Modes.{Mode}")), new Color(160, 240, 140));
        }

        private void SetMode(TerraArmorMode mode)
        {
            if (mode == Mode) return;
            Mode = mode;
            ResetBeetles();
        }

        internal bool TryRevive()
        {
            if (Player.whoAmI != Main.myPlayer || !Equipped(Player) || ReviveCooldown > 0) return false;
            ReviveCooldown = ReviveCooldownTicks;
            Player.statLife = Player.statLifeMax2;
            Player.immune = true;
            Player.immuneTime = 180;
            for (int i = 0; i < Player.hurtCooldowns.Length; i++) Player.hurtCooldowns[i] = 180;
            Player.HealEffect(Player.statLife);
            if (Main.netMode == NetmodeID.MultiplayerClient)
            {
                SendState();
                NetMessage.SendData(MessageID.PlayerLifeMana, number: Player.whoAmI);
            }
            return true;
        }

        public override void SaveData(TagCompound tag)
        {
            tag["TerraMode"] = (byte)Mode;
            tag["TerraReviveCooldown"] = ReviveCooldown;
        }

        public override void LoadData(TagCompound tag)
        {
            Mode = (TerraArmorMode)Math.Clamp((int)tag.GetByte("TerraMode"), 0, 3);
            ReviveCooldown = Math.Clamp(tag.GetInt("TerraReviveCooldown"), 0, ReviveCooldownTicks);
        }

        public override void OnEnterWorld()
        {
            _closeMeleeHealCooldown = 0;
            ResetBeetles();
            if (Main.netMode == NetmodeID.MultiplayerClient) SendState();
        }
    }
}
