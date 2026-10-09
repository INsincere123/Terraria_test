using System;
using System.Collections.Generic;
using System.Reflection;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using MonoMod.RuntimeDetour;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;
using TestMod.Common.GlobalProjectiles;
using TestMod.Common.Players;

namespace TestMod.Common.Systems
{
    public class TerraArmorSystem : ModSystem
    {
        public static ModKeybind SwitchModeKey { get; private set; }
        private readonly List<IDisposable> _hooks = new();
        internal delegate void DrawLayer(ref PlayerDrawSet drawInfo);
        private delegate void DrawLayerHook(DrawLayer orig, ref PlayerDrawSet drawInfo);

        public override void Load()
        {
            SwitchModeKey = KeybindLoader.RegisterKeybind(Mod, "TerraArmorMode", "N");
            try
            {
                AddIL(typeof(Player), nameof(Player.UpdateArmorSets), PatchArmorSets);
                AddIL(typeof(Player), nameof(Player.UpdateBuffs), PatchBeetleBuffs);
                AddIL(typeof(Player), nameof(Player.Update), PatchVortexRunSpeeds);
                AddIL(typeof(Player), nameof(Player.KillMe), PatchRevive);
                AddIL(typeof(Projectile), nameof(Projectile.Damage), PatchSummonCrit);
                AddIL(typeof(Projectile), "AI_120_StardustGuardian", PatchGuardian);
                _hooks.Add(new Hook(FindMethod(typeof(Player), nameof(Player.NebulaLevelup)),
                    (Action<Action<Player, int>, Player, int>)NebulaLevelup));
                if (!Main.dedServ)
                {
                    _hooks.Add(new Hook(FindMethod(typeof(PlayerDrawLayers), nameof(PlayerDrawLayers.DrawPlayer_37_BeetleBuff)),
                        (DrawLayerHook)DrawBeetles));
                    AddIL(typeof(Player), "ItemCheck_EmitUseVisuals", PatchMagmaItemVisuals);
                    AddIL(typeof(Projectile), nameof(Projectile.EmitEnchantmentVisualsAt), PatchMagmaProjectileVisuals);
                }
            }
            catch (Exception exception)
            {
                DisposeHooks();
                throw new InvalidOperationException("泰拉套原版机制适配失败，请核对 tModLoader 版本。", exception);
            }
        }

        private static MethodInfo FindMethod(Type type, string name) =>
            type.GetMethod(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static)
            ?? throw new MissingMethodException(type.FullName, name);

        private void AddIL(Type type, string name, ILContext.Manipulator patch) =>
            _hooks.Add(new ILHook(FindMethod(type, name), patch));

        private void DisposeHooks()
        {
            for (int i = _hooks.Count - 1; i >= 0; i--) _hooks[i].Dispose();
            _hooks.Clear();
        }

        public override void Unload()
        {
            DisposeHooks();
            SwitchModeKey = null;
        }

        private static void PatchArmorSets(ILContext il)
        {
            var cursor = new ILCursor(il);
            if (!cursor.TryGotoNext(MoveType.Before, i => i.MatchStfld<Player>(nameof(Player.vortexStealthActive))))
                throw new InvalidOperationException("找不到星璇潜行重置。");
            cursor.Emit(OpCodes.Ldarg_0);
            cursor.EmitDelegate<Func<bool, Player, bool>>((value, player) =>
                TerraArmorPlayer.EquippedMode(player, TerraArmorMode.Ranger) ? player.vortexStealthActive : value);

            cursor.Index = 0;
            Instruction solarReset = null;
            int solarResets = 0;
            foreach (Instruction instruction in il.Body.Instructions)
            {
                if (!instruction.MatchStfld<Player>(nameof(Player.solarCounter)) || !instruction.Previous.MatchLdcI4(0)) continue;
                solarReset = instruction;
                solarResets++;
            }
            if (solarResets != 2) throw new InvalidOperationException($"耀斑充能归零检查数量异常：{solarResets}。");
            // 第一处是原版充能完成后的归零；最后一处才是非原版耀斑装备的清理分支。
            cursor.Goto(solarReset, MoveType.Before);
            cursor.Emit(OpCodes.Ldarg_0);
            cursor.EmitDelegate<Func<int, Player, int>>((value, player) =>
                TerraArmorPlayer.EquippedMode(player, TerraArmorMode.Warrior) ? player.solarCounter : value);

            cursor.Index = 0;
            if (!cursor.TryGotoNext(MoveType.After, i => i.MatchLdfld<Player>(nameof(Player.crystalLeaf))))
                throw new InvalidOperationException("找不到叶绿水晶清理判断。");
            cursor.Emit(OpCodes.Ldarg_0);
            cursor.EmitDelegate<Func<bool, Player, bool>>((clear, player) => clear && !TerraArmorPlayer.Equipped(player));

            cursor.Index = 0;
            int matches = 0;
            while (cursor.TryGotoNext(MoveType.After, i => i.MatchLdcI4(BuffID.StardustGuardianMinion),
                i => i.MatchCall<Player>(nameof(Player.FindBuffIndex))))
            {
                cursor.Emit(OpCodes.Ldarg_0);
                cursor.EmitDelegate<Func<int, Player, int>>((index, player) =>
                    TerraArmorPlayer.EquippedMode(player, TerraArmorMode.Summoner) ? -1 : index);
                matches++;
            }
            if (matches != 3) throw new InvalidOperationException($"星尘护卫 Buff 检查数量异常：{matches}。");
        }

        private static void PatchRevive(ILContext il)
        {
            var cursor = new ILCursor(il);
            if (!cursor.TryGotoNext(MoveType.After, i => i.MatchCall(typeof(PlayerLoader), nameof(PlayerLoader.PreKill))))
                throw new InvalidOperationException("找不到统一复活判断。");
            cursor.Emit(OpCodes.Ldarg_0);
            cursor.EmitDelegate<Func<bool, Player, bool>>((willDie, player) =>
            {
                // 先让所有模组的 PreKill 完成；已被其他复活救下时不消耗泰拉冷却。
                if (!willDie) return false;
                if (!player.TryGetModPlayer(out TerraArmorPlayer armor) || !armor.TryRevive()) return true;
                return false;
            });
        }

        private static void PatchGuardian(ILContext il)
        {
            var cursor = new ILCursor(il);
            int matches = 0;
            while (cursor.TryGotoNext(MoveType.After, i => i.MatchLdcR4(500f) || i.MatchLdcR4(150f) || i.MatchLdcR4(100f)))
            {
                cursor.Emit(OpCodes.Ldarg_0);
                cursor.EmitDelegate<Func<float, Projectile, float>>((range, projectile) =>
                    projectile.type == ProjectileID.StardustGuardian && TerraArmorProjectile.TryGetArmor(projectile, out var armor) &&
                    armor.IsMode(TerraArmorMode.Summoner) ? range * 2f : range);
                matches++;
            }
            if (matches < 5) throw new InvalidOperationException($"星尘护卫范围常量数量异常：{matches}。");
        }

        private static void PatchSummonCrit(ILContext il)
        {
            var cursor = new ILCursor(il);
            if (!cursor.TryGotoNext(MoveType.Before, i => i.MatchCall(typeof(CombinedHooks), nameof(CombinedHooks.ModifyHitNPCWithProj))))
                throw new InvalidOperationException("找不到射弹命中修正入口。");
            // 此调用的三个参数为 this、目标 NPC 和 modifiers 引用。
            if (!cursor.Prev.Previous.MatchLdloc(out int npcLocal))
                throw new InvalidOperationException("无法识别射弹目标局部变量。");
            if (!cursor.TryGotoNext(MoveType.Before, i => i.MatchCall(typeof(NPC.HitModifiers), nameof(NPC.HitModifiers.ToHitInfo))))
                throw new InvalidOperationException("找不到射弹暴击结算入口。");
            int callIndex = cursor.Index;
            cursor.Index -= 1;
            while (cursor.Index > 0 && !cursor.Next.MatchLdfld<Projectile>(nameof(Projectile.damage))) cursor.Index--;
            cursor.Index++;
            if (cursor.Next.OpCode == OpCodes.Conv_R4) cursor.Index++;
            if (callIndex - cursor.Index > 25 || !cursor.Next.MatchLdloc(out _))
                throw new InvalidOperationException("无法识别原版暴击参数。");
            cursor.Index++;
            cursor.Emit(OpCodes.Ldarg_0);
            cursor.Emit(OpCodes.Ldloc, npcLocal);
            cursor.EmitDelegate<Func<bool, Projectile, NPC, bool>>(TerraArmorProjectile.RollSummonCrit);
        }

        private static void NebulaLevelup(Action<Player, int> orig, Player player, int buff)
        {
            if (player.whoAmI == Main.myPlayer && TerraArmorPlayer.EquippedMode(player, TerraArmorMode.Mage))
            {
                // 让原版负责增益时长、低级 Buff 替换及队友分享，只将此次拾取前的层数设为 2。
                if (buff == BuffID.NebulaUpLife1) player.nebulaLevelLife = 2;
                else if (buff == BuffID.NebulaUpMana1) player.nebulaLevelMana = 2;
                else if (buff == BuffID.NebulaUpDmg1) player.nebulaLevelDamage = 2;
            }
            orig(player, buff);
        }

        private static void PatchBeetleBuffs(ILContext il)
        {
            var cursor = new ILCursor(il);
            if (!cursor.TryGotoNext(MoveType.Before,
                i => i.MatchLdarg(0), i => i.MatchLdfld<Player>(nameof(Player.buffType)),
                i => i.MatchLdloc(out _), i => i.OpCode == OpCodes.Ldelem_I4,
                i => i.MatchLdarg(0), i => i.MatchLdloca(out _),
                i => i.MatchCall(typeof(BuffLoader), nameof(BuffLoader.Update))))
                throw new InvalidOperationException("找不到 Buff 扩展更新入口。");
            Instruction buffHooks = cursor.Next;
            cursor.Index = 0;
            int buffIndex = -1;
            if (!cursor.TryGotoNext(MoveType.Before,
                i => i.MatchLdarg(0), i => i.MatchLdfld<Player>(nameof(Player.buffType)),
                i => i.MatchLdloc(out buffIndex), i => i.OpCode == OpCodes.Ldelem_I4,
                i => i.MatchLdcI4(BuffID.ObsidianSkin)))
                throw new InvalidOperationException("找不到原版 Buff 分发入口。");
            cursor.Emit(OpCodes.Ldarg_0);
            cursor.Emit(OpCodes.Ldloc, buffIndex);
            cursor.EmitDelegate<Func<Player, int, bool>>((player, index) =>
                TerraArmorPlayer.EquippedMode(player, TerraArmorMode.Warrior) &&
                player.GetModPlayer<TerraArmorPlayer>().UpdateBeetleBuff(index));
            cursor.Emit(OpCodes.Brtrue, buffHooks);
        }

        private static void PatchVortexRunSpeeds(ILContext il)
        {
            var cursor = new ILCursor(il);
            foreach (string field in new[] { nameof(Player.accRunSpeed), nameof(Player.maxRunSpeed) })
            {
                cursor.Index = 0;
                if (!cursor.TryGotoNext(MoveType.After, i => i.MatchLdfld<Player>(field), i => i.MatchLdcR4(0.3f)) ||
                    cursor.Next.OpCode != OpCodes.Mul || !cursor.Next.Next.MatchStfld<Player>(field))
                    throw new InvalidOperationException($"找不到星璇潜行减速：{field}。");
                cursor.Emit(OpCodes.Ldarg_0);
                cursor.EmitDelegate<Func<float, Player, float>>((multiplier, player) =>
                    TerraArmorPlayer.EquippedMode(player, TerraArmorMode.Ranger) ? 1f : multiplier);
            }
        }

        private static void DrawBeetles(DrawLayer orig, ref PlayerDrawSet drawInfo)
        {
            if (drawInfo.drawPlayer.TryGetModPlayer(out TerraArmorPlayer armor)) armor.DrawBeetles(orig, ref drawInfo);
            else orig(ref drawInfo);
        }

        private static bool MagmaVisuals(bool enabled, Player player) => enabled &&
            (!TerraArmorPlayer.EquippedMode(player, TerraArmorMode.Warrior) ||
             player.GetModPlayer<TerraArmorPlayer>().HasIndependentMagmaStone);

        private static void PatchMagmaItemVisuals(ILContext il)
        {
            var cursor = new ILCursor(il);
            if (!cursor.TryGotoNext(MoveType.After, i => i.MatchLdfld<Player>(nameof(Player.magmaStone))))
                throw new InvalidOperationException("找不到烈火手套物品装饰判断。");
            cursor.Emit(OpCodes.Ldarg_0);
            cursor.EmitDelegate<Func<bool, Player, bool>>(MagmaVisuals);
        }

        private static void PatchMagmaProjectileVisuals(ILContext il)
        {
            var cursor = new ILCursor(il);
            if (!cursor.TryGotoNext(MoveType.After, i => i.MatchLdfld<Player>(nameof(Player.magmaStone))))
                throw new InvalidOperationException("找不到烈火手套射弹装饰判断。");
            cursor.Emit(OpCodes.Ldarg_0);
            cursor.EmitDelegate<Func<bool, Projectile, bool>>((enabled, projectile) =>
                enabled && (!TerraArmorProjectile.TryGetArmor(projectile, out var armor) || MagmaVisuals(true, armor.Player)));
        }
    }
}
