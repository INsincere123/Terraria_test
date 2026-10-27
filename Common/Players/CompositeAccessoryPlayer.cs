using System;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ModLoader.Default;
using TestMod.Common.Mechanics.Dashes;
using TestMod.Content.Items.Accessories;

namespace TestMod.Common.Players
{
    public sealed partial class CompositeAccessoryPlayer : ModPlayer
    {
        internal bool ShieldEquipped, VeilEquipped;
        private Item veilSource;
        private float dodgeFailure = 1f;
        private float healingMultiplier = 1f;

        public override void ResetEffects()
        {
            ShieldEquipped = VeilEquipped = false;
            veilSource = null;
            dodgeFailure = healingMultiplier = 1f;
        }

        internal static bool ProvidesObsidianSkin(Item item) => item?.ModItem is
            StarfallObsidianEmblem or ShieldOfCthulhuPlus or OldOnesVeil;

        // Buff 在饰品更新前结算，读取当前功能槽，避免首次装备与卸装多一帧延迟。
        public override void PreUpdateBuffs()
        {
            if (!Player.active || Player.dead) return;
            for (int i = 3; i < 10; i++)
                if (Player.IsItemSlotUnlockedAndUsable(i) && ProvidesObsidianSkin(Player.armor[i]))
                {
                    Player.AddBuff(BuffID.ObsidianSkin, 2);
                    return;
                }
            var slots = Player.GetModPlayer<ModAccessorySlotPlayer>();
            for (int i = 0; i < slots.SlotCount; i++)
            {
                ModAccessorySlot slot = LoaderManager.Get<AccessorySlotLoader>().Get(i, Player);
                if (slot.IsEnabled() && ProvidesObsidianSkin(slot.FunctionalItem))
                {
                    Player.AddBuff(BuffID.ObsidianSkin, 2);
                    return;
                }
            }
        }

        private void ApplyObsidianShield()
        {
            Player.noKnockback = Player.fireWalk = true;
            // 同时维持标志，保证 Buff 槽已满时基础保护仍有效。
            Player.lavaImmune = true;
            Player.buffImmune[BuffID.OnFire] = true;
        }

        private void ApplyEmblem(PanicToggleAccessory accessory)
        {
            ApplyObsidianShield();
            Player.pStone = Player.longInvince = true;
            Player.lifeRegen += 2;
            if (accessory.PanicEnabled) Player.panic = true;
            Player.honeyCombItem ??= accessory.Item;
            Player.starCloakItem ??= accessory.Item;
            Player.starCloakItem_starVeilOverrideItem ??= accessory.Item;
        }

        internal void EquipEmblem(StarfallObsidianEmblem accessory) => ApplyEmblem(accessory);

        internal void EquipShield()
        {
            ShieldEquipped = true;
            Player.GetModPlayer<AccessoryDashPlayer>().Offer(ShieldOfCthulhuPlus.DashConfiguration);
            ApplyObsidianShield();
            Player.endurance += 0.15f;
            dodgeFailure *= 0.85f;
        }

        internal void EquipVeil(OldOnesVeil accessory)
        {
            VeilEquipped = true;
            Player.GetModPlayer<AccessoryDashPlayer>().Offer(OldOnesVeil.DashConfiguration);
            veilSource = accessory.Item;
            ApplyEmblem(accessory);
            Player.endurance += 0.15f;
            dodgeFailure *= 0.78f;
            healingMultiplier *= 1.2f;
            Player.spikedBoots = Math.Max(Player.spikedBoots, 2);
            // 原版十字章护盾免疫集合，不扩展到全部减益。
            foreach (int type in AnkhImmunities) Player.buffImmune[type] = true;
        }

        private static readonly int[] AnkhImmunities = {
            BuffID.Chilled, BuffID.Weak, BuffID.BrokenArmor, BuffID.Bleeding, BuffID.Poisoned,
            BuffID.Slow, BuffID.Confused, BuffID.Silenced, BuffID.Cursed, BuffID.Darkness, BuffID.Stoned
        };

        // 此方法先于所有 PostUpdateEquips 执行，装备槽顺序不参与冲刺优先级。
        internal void PrepareDashSelection()
        {
            DashPlayer dash = Player.GetModPlayer<DashPlayer>();
            var shared = Player.GetModPlayer<AccessoryDashPlayer>();
            if (string.IsNullOrEmpty(dash.VanillaDashEffectId) && shared.Selected != null)
                dash.VanillaDashEffectId = AccessoryDash.EffectId;
            if (dash.ActiveEffect is AccessoryDash active && !active.CanUseDash(Player)) dash.EndDash();
            if (dash.VanillaDashEffectId == AccessoryDash.EffectId) Player.dashType = 0;
        }

        public override bool FreeDodge(Player.HurtInfo info)
        {
            if (Player.whoAmI != Main.myPlayer || Main.netMode == NetmodeID.Server
                || !info.Dodgeable || dodgeFailure >= 1f || Main.rand.NextFloat() < dodgeFailure) return false;
            Player.NinjaDodge();
            return true;
        }

        public override void GetHealLife(Item item, bool quickHeal, ref int healValue)
        {
            if (item.potion && item.healLife > 0) healValue = ScalePotionHealing(healValue);
        }

        internal int ScalePotionHealing(int amount) => amount <= 0 || healingMultiplier == 1f ? amount
            : (int)Math.Min(int.MaxValue, Math.Floor(amount * (double)healingMultiplier + 0.00001d));

        internal int AdjustSpecialPotionHeal(Item item, int amount) =>
            item.type == ItemID.StrangeBrew && item.potion ? ScalePotionHealing(amount) : amount;

        public override void OnHurt(Player.HurtInfo info)
        {
            if (!VeilEquipped || Player.whoAmI != Main.myPlayer || Main.netMode == NetmodeID.Server
                || Player.brainOfConfusionItem is { IsAir: false }
                || Player.GetModPlayer<TerraShieldPlayer>().IsEquipped) return;
            // 不设置 brainOfConfusionItem，以免额外获得原版随机闪避及攻击增益。
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
            Projectile.NewProjectile(Player.GetSource_Accessory_OnHurt(veilSource, info.DamageSource),
                Player.Center + new Vector2(Main.rand.Next(-40, 40), -Main.rand.Next(20, 60)),
                Player.velocity * 0.3f, ProjectileID.BrainOfConfusion, 0, 0, Player.whoAmI);
        }

        public override void PostUpdate()
        {
            if (!ShieldEquipped && !VeilEquipped) remoteDashVisual = false;
            UpdateDashTrail(HasDashVisual);
        }

        public override void UpdateDead() => ResetSession();
        public override void OnEnterWorld() => ResetSession();
        internal void ResetSession()
        {
            DashPlayer dash = Player.GetModPlayer<DashPlayer>();
            var shared = Player.GetModPlayer<AccessoryDashPlayer>();
            if (shared.IsSourceActive(AccessoryDashSource.Cthulhu) || shared.IsSourceActive(AccessoryDashSource.Veil)) dash.EndDash();
            ShieldEquipped = VeilEquipped = remoteDashVisual = false;
            veilSource = null;
            dodgeFailure = healingMultiplier = 1f;
            ClearDashTrail();
        }
    }
}
