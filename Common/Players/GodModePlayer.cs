using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;
using TestMod.Common.Systems;

namespace TestMod.Common.Players
{
    public class GodModePlayer : ModPlayer
    {
        public bool GodModeBuff = false;
        public bool GodModeBuff2 = false;

        private bool _godModeUnlocked = false;
        private bool _prevGodModeBuff = false;
        private readonly Dictionary<int, int> _originalRarity = new();

        public const int GodModeRarity = 11;

        private static readonly int[] GodModeItems =
        {
            ItemID.Phantasm,
            ItemID.DayBreak,
            ItemID.NebulaBlaze,
            ItemID.StardustDragonStaff,
            ItemID.StardustCellStaff,
            ItemID.MoonlordTurretStaff,
            ItemID.RainbowCrystalStaff,
            ItemID.EmpressBlade,
            ItemID.RainbowWhip,
        };

        public override void ResetEffects()
        {
            if (GodModeBuff)
            {
                Player.statDefense += 3;
                Player.statLifeMax2 += 10;
                Player.endurance += 0.02f;
                Player.maxMinions += 1;
                Player.maxTurrets += 1;
                Player.GetDamage(DamageClass.Generic) += 0.03f;
                Player.GetArmorPenetration(DamageClass.Generic) += 12;
                Player.GetAttackSpeed(DamageClass.Generic) += 0.05f;
            }

            if (GodModeBuff2)
            {
                SummonCritPlayer.Enable(Player);

                Player.statDefense += 6666;
                Player.lifeRegen += 6666 * 2;
                Player.statLifeMax2 += 6666;
                Player.statManaMax2 += 666;
                Player.endurance = MathHelper.Clamp(Player.endurance + 0.66f, 0f, 0.999f);
                //Player.moveSpeed += 0.1f;
                Player.maxMinions += 66;
                Player.maxTurrets += 33;
                Player.GetDamage(DamageClass.Generic) += 6.66f;
                Player.GetAttackSpeed(DamageClass.Generic) += 6.6f;
                Player.GetCritChance(DamageClass.Generic) += 66;
            }

            if (GodModeBuff != _prevGodModeBuff)
            {
                if (GodModeBuff)
                    ApplyGodModeRarity();
                else
                    RestoreOriginalRarity();
            }

            _prevGodModeBuff = GodModeBuff;
        }

        public override void ModifyHitNPC(NPC target, ref NPC.HitModifiers modifiers)
        {
            if (GodModeBuff2)
            {
                modifiers.ScalingArmorPenetration += 1f;
                modifiers.CritDamage += 1f;
            }
        }

        public override void ModifyHitNPCWithProj(Projectile proj, NPC target, ref NPC.HitModifiers modifiers)
        {
            if (GodModeBuff2)
            {
                modifiers.ScalingArmorPenetration += 1f;
                modifiers.CritDamage += 1f;
            }
        }

        public override void PostUpdate()
        {
            if (NPC.downedMoonlord && !_godModeUnlocked)
            {
                _godModeUnlocked = true;
                GodModeBuff = true;
            }
        }

        public override void SaveData(TagCompound tag)
        {
            tag["godModeBuff"] = GodModeBuff;
            tag["godModeUnlocked"] = _godModeUnlocked;
        }

        public override void LoadData(TagCompound tag)
        {
            GodModeBuff = tag.GetBool("godModeBuff");
            _godModeUnlocked = tag.GetBool("godModeUnlocked");
        }

        private void ApplyGodModeRarity()
        {
            int onRarity = CalamityCompatSystem.CalamityLoaded
                ? CalamityCompatSystem.CalamityRarity
                : GodModeRarity;

            _originalRarity.Clear();

            for (int i = 0; i < Player.inventory.Length; i++)
            {
                Item item = Player.inventory[i];
                if (item == null || item.IsAir)
                    continue;

                if (!IsGodModeItem(item.type))
                    continue;

                _originalRarity[i] = item.rare;
                item.rare = onRarity;
            }
        }

        private void RestoreOriginalRarity()
        {
            foreach (var kv in _originalRarity)
            {
                int i = kv.Key;
                if (i >= Player.inventory.Length)
                    continue;

                Item item = Player.inventory[i];
                if (item == null || item.IsAir || !IsGodModeItem(item.type))
                    continue;

                item.rare = kv.Value;
            }

            _originalRarity.Clear();
        }

        private static bool IsGodModeItem(int itemType)
        {
            foreach (int id in GodModeItems)
            {
                if (itemType == id)
                    return true;
            }

            return false;
        }
    }
}
