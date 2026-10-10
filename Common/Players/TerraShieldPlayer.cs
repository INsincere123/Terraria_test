using System;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;
using Terraria.ModLoader.Default;
using Terraria.ModLoader.IO;
using TestMod.Common.DataStructures;
using TestMod.Content.Items.Accessories;

namespace TestMod.Common.Players
{
    public sealed partial class TerraShieldPlayer : ModPlayer
    {
        internal bool IsEquipped { get; private set; }
        public int DodgeCooldown { get; private set; }
        private readonly TerraShieldBleedPool _bleed = new();
        public double RemainingBleed => _bleed.Remaining;
        private Item _accessory;
        private int _regenTimer, _pendingBleed;
        internal int ResolvedHurtDamage = -1;
        private double _regenPartial;
        private bool IsAuthority => Main.netMode != NetmodeID.Server && Player.whoAmI == Main.myPlayer;

        // Buff 更新早于功能饰品更新，必须读取当前功能槽，不使用上一帧标志。
        private bool EquippedNow()
        {
            if (!Player.active || Player.dead) return false;
            int type = ModContent.ItemType<TerraShield>();
            for (int slot = 3; slot < 10; slot++)
                if (Player.IsItemSlotUnlockedAndUsable(slot) && Player.armor[slot].type == type) return true;
            var slots = Player.GetModPlayer<ModAccessorySlotPlayer>();
            for (int slot = 0; slot < slots.SlotCount; slot++)
            {
                ModAccessorySlot accessorySlot = LoaderManager.Get<AccessorySlotLoader>().Get(slot, Player);
                // 此查询在 Player.Update 的当前玩家作用域中进行；只读公开功能槽。
                if (accessorySlot.IsEnabled() && accessorySlot.FunctionalItem.type == type) return true;
            }
            return false;
        }

        public override void ResetEffects()
        {
            IsEquipped = false;
            _accessory = null;
        }

        public override void PreUpdateBuffs()
        {
            if (!EquippedNow()) return;
            for (int slot = Player.buffType.Length - 1; slot >= 0; slot--)
            {
                int type = Player.buffType[slot];
                if (type > 0 && type < BuffID.Count && type != BuffID.PotionSickness && Main.debuff[type])
                    Player.DelBuff(slot);
            }
        }

        internal void ApplyAccessory(Item accessory)
        {
            IsEquipped = true;
            _accessory = accessory;
            Player.statLifeMax2 += 300;
            Player.endurance += 0.15f;
            Player.noKnockback = Player.fireWalk = Player.longInvince = Player.shinyStone = Player.pStone = true;
            Player.spikedBoots = Math.Max(Player.spikedBoots, 2);
            Player.lifeRegen += 2; // 神话护身符；闪亮石与天界石仍使用原版恢复路径。
            Player.skyStoneEffects = true;
            Player.accMerman = true;
            Player.starCloakItem ??= accessory;
            Player.starCloakItem_starVeilOverrideItem ??= accessory;
            Player.AddBuff(BuffID.Honey, 2);
            for (int type = 1; type < BuffID.Count; type++)
                if (type != BuffID.PotionSickness && Main.debuff[type]) Player.buffImmune[type] = true;
        }

        public override void PostUpdateEquips()
        {
            if (!IsEquipped) return;
            bool inWater = Player.wet && !Player.lavaWet && (!Player.mount.Active || !Player.mount.IsConsideredASlimeMount);
            // 狼人属性直接应用，不启动变身 Buff，避免多帧标志与外观副作用。
            if (!Main.dayTime && !inWater && !Player.wereWolf)
            {
                Player.lifeRegen++;
                Player.GetCritChance(DamageClass.Melee) += 2;
                Player.GetDamage(DamageClass.Melee) += 0.051f;
                Player.GetAttackSpeed(DamageClass.Melee) += 0.051f;
                Player.statDefense += 3;
                Player.moveSpeed += 0.05f;
            }
            if (inWater) Player.gills = Player.accFlipper = true;
            Player.hideWolf = Player.hideMerman = true;
        }

        private void UpdateConditionalProtection()
        {
            // 原版冰障 Buff 在饰品增加生命上限前更新；用最终上限补齐本来源。
            if (Player.statLife <= Player.statLifeMax2 * 0.5f && !Player.iceBarrier)
            {
                Player.iceBarrier = true;
                Player.endurance += 0.25f;
                if (++Player.iceBarrierFrameCounter > 2)
                {
                    Player.iceBarrierFrameCounter = 0;
                    Player.iceBarrierFrame = (byte)((Player.iceBarrierFrame + 1) % 12);
                }
            }
            if (Player.statLife > Player.statLifeMax2 * 0.25f)
            {
                Player.hasPaladinShield = true;
                // 与原版圣骑士护盾一致：由队友客户端维持本地保护 Buff。
                if (!Main.dedServ && Player.whoAmI != Main.myPlayer && Player.miscCounter % 10 == 0)
                {
                    Player local = Main.LocalPlayer;
                    if (local.active && local.team != 0 && local.team == Player.team &&
                        Vector2.Distance(Player.position, local.position) < 800)
                        local.AddBuff(BuffID.PaladinsShield, 20);
                }
            }
        }

        public override void FrameEffects()
        {
            if (IsEquipped) Player.hideWolf = Player.hideMerman = true;
        }

        public override void ModifyHitByNPC(NPC npc, ref Player.HurtModifiers modifiers) =>
            LimitWhaleBoneDamage(ref modifiers);

        public override void ModifyHitByProjectile(Projectile proj, ref Player.HurtModifiers modifiers)
        {
            // 旅人归途原作对巨鲸的这十种攻击固定使用 20% 上限，不需要硬引用。
            var source = proj.ModProjectile;
            bool whaleAttack = source?.Mod.Name == "ContinentOfJourney" &&
                source.Name.StartsWith("Whale_Master_", StringComparison.Ordinal) &&
                int.TryParse(source.Name.AsSpan("Whale_Master_".Length), out int attack) && attack is >= 12 and <= 21;
            LimitWhaleBoneDamage(ref modifiers, whaleAttack);
        }

        private void LimitWhaleBoneDamage(ref Player.HurtModifiers modifiers, bool whaleAttack = false)
        {
            if (!IsEquipped) return;
            float fraction = !Main.expertMode || whaleAttack ? 0.2f : Main.masterMode ? 0.4f : 0.3f;
            // 原作的 SetMaxDamage 在防御/免伤后、ModifyHurtInfo 护盾吸收前封顶；
            // 流血仅接收其后实际剩余的伤害，不会将被上限免去的部分重新计入。
            modifiers.SetMaxDamage(Math.Max(1, (int)(Player.statLifeMax2 * fraction)));
        }

        public override bool FreeDodge(Player.HurtInfo info)
        {
            if (!IsAuthority || !IsEquipped || Main.rand.Next(TerraShield.ExtraDodgeChanceDenominator) != 0) return false;
            Player.NinjaDodge();
            return true;
        }

        public override bool ConsumableDodge(Player.HurtInfo info)
        {
            if (!IsAuthority || !IsEquipped || DodgeCooldown > 0) return false;
            DodgeCooldown = TerraShield.ExtraDodgeCooldownTicks;
            Player.BrainOfConfusionDodge();
            SendState(force: true);
            return true;
        }

        public override void GetHealLife(Item item, bool quickHeal, ref int healValue)
        {
            if (IsEquipped && item.potion && item.healLife > 0)
                healValue = (int)Math.Min(int.MaxValue, (long)healValue * 2);
        }

        // 仅实际生命恢复入口调用；GetHealLife 的查询不改变任何流血状态。
        internal int AdjustSpecialPotionHeal(Item item, int healValue) =>
            IsEquipped && item.type == ItemID.StrangeBrew && item.potion && healValue > 0
                ? (int)Math.Min(int.MaxValue, (long)healValue * 2) : healValue;

        internal void OnHealingPotionApplied(Item item, int healValue)
        {
            if (!IsAuthority || !IsEquipped || !item.potion || item.healLife <= 0 || healValue <= 0) return;
            _bleed.Clear(healValue * 0.5d);
            SendState(force: true);
        }

        internal void HurtWithBleed(Action<Player, Player.HurtInfo, bool> orig, Player.HurtInfo info, bool quiet)
        {
            int previous = _pendingBleed;
            _pendingBleed = 0;
            try
            {
                // 此入口在最终护盾吸收、取消与全部闪避之后，尚未真正扣生命。
                if (IsAuthority && IsEquipped && !Player.dead && !info.Cancelled && info.Damage > 5)
                {
                    _pendingBleed = info.Damage - 5;
                    info.Damage = 5;
                }
                orig(Player, info, quiet);
                // 外层 Hurt 的 out 参数仍是传值前的副本，交给作用域同步其最终报告值。
                ResolvedHurtDamage = info.Damage;
            }
            finally { _pendingBleed = previous; }
        }

        public override void OnHurt(Player.HurtInfo info)
        {
            if (!IsAuthority) return;
            if (_pendingBleed > 0)
            {
                _bleed.Add(_pendingBleed);
                _pendingBleed = 0;
                SendState(force: true);
            }
            if (IsEquipped && (Player.brainOfConfusionItem == null || Player.brainOfConfusionItem.IsAir))
                ConfuseNearbyEnemies(info);
        }

        private void ConfuseNearbyEnemies(Player.HurtInfo info)
        {
            // 不设置 brainOfConfusionItem，避免再带入原版随机闪避。
            foreach (NPC npc in Main.ActiveNPCs)
            {
                if (npc.friendly || Main.rand.Next(500) >= 300 + info.Damage * 2) continue;
                float range = Main.rand.Next(200 + info.Damage / 2, 301 + info.Damage * 2);
                if (range > 500) range = 500 + (range - 500) * 0.75f;
                if (range > 700) range = 700 + (range - 700) * 0.5f;
                if (range > 900) range = 900 + (range - 900) * 0.25f;
                if (Vector2.Distance(npc.Center, Player.Center) < range)
                    npc.AddBuff(BuffID.Confused, Main.rand.Next(90 + info.Damage / 3, 300 + info.Damage / 2));
            }
            Projectile.NewProjectile(Player.GetSource_Accessory_OnHurt(_accessory, info.DamageSource),
                Player.Center + new Vector2(Main.rand.Next(-40, 40), -Main.rand.Next(20, 60)),
                Player.velocity * 0.3f, ProjectileID.BrainOfConfusion, 0, 0, Player.whoAmI);
        }

        public override void PostUpdateMiscEffects()
        {
            if (!IsEquipped) return;
            UpdateConditionalProtection();
            for (int slot = 0; slot < Player.buffType.Length; slot++)
            {
                int type = Player.buffType[slot];
                if (type > 0 && type != BuffID.PotionSickness && Main.debuff[type])
                    Player.buffTime[slot] = Math.Max(1, Player.buffTime[slot] - TerraShield.ExtraDebuffTimeReduction);
            }
        }

        public override void PostUpdate()
        {
            if (!IsAuthority) return;
            if (DodgeCooldown > 0) DodgeCooldown--;
            if (!IsEquipped) ResetRegen();
            ApplyBleedDamage(IsEquipped ? _bleed.Tick() : _bleed.Flush());
            if (!Player.dead && IsEquipped)
            {
                // 流血可能在本帧跨过紧急阈值，先检查再回血/喝药，不依赖 ModPlayer 顺序。
                Player.GetModPlayer<SuperEnergyShieldPlayer>().CheckEmergencyTrigger();
                if (++_regenTimer >= 60)
                {
                    _regenTimer = 0;
                    if (Player.statLife < Player.statLifeMax2)
                    {
                        _regenPartial += Player.statLifeMax2 * 0.01d;
                        int heal = (int)_regenPartial;
                        _regenPartial -= heal;
                        Player.Heal(heal);
                    }
                    else _regenPartial = 0;
                }
                if (Player.statLife > 0 && Player.statLife < Player.statLifeMax2 * 0.25f && Player.potionDelay == 0)
                    Player.QuickHeal();
            }
            SendState();
        }

        private void ApplyBleedDamage(int damage)
        {
            if (damage <= 0 || Player.dead) return;
            Player.statLife -= damage;
            if (Player.statLife <= 0)
            {
                Player.KillMe(PlayerDeathReason.ByCustomReason(NetworkText.FromKey(
                    "Mods.TestMod.TerraShieldEffects.BleedDeath", Player.name)), damage, 0);
                // 已结清部分不因外部复活重新扣除，真正死亡时清理剩余。
                if (Player.dead) _bleed.Reset();
            }
            if (Main.netMode == NetmodeID.MultiplayerClient)
                NetMessage.SendData(MessageID.PlayerLifeMana, number: Player.whoAmI);
        }

        private void ResetRegen() { _regenTimer = 0; _regenPartial = 0; }

        public override void UpdateDead()
        {
            IsEquipped = false;
            ResetRegen();
            _pendingBleed = 0;
            if (!IsAuthority) return;
            _bleed.Reset();
            if (DodgeCooldown > 0) DodgeCooldown--;
            SendState();
        }

        public override void OnEnterWorld()
        {
            ResetRegen();
            _pendingBleed = 0;
            ResolvedHurtDamage = -1;
            _hasSentState = false;
        }

        public override void SaveData(TagCompound tag)
        {
            tag["dodgeCooldown"] = DodgeCooldown;
            tag["bleedBuffer"] = _bleed.Buffer;
            tag["bleedPartial"] = _bleed.Partial;
        }

        public override void LoadData(TagCompound tag)
        {
            DodgeCooldown = Math.Clamp(tag.GetInt("dodgeCooldown"), 0, TerraShield.ExtraDodgeCooldownTicks);
            _bleed.Restore(tag.GetDouble("bleedBuffer"), tag.GetDouble("bleedPartial"));
        }
    }
}
