using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Terraria;
using Terraria.ModLoader;
using TestMod.Common.Mechanics.Dashes;
using TestMod.Common.Players;

namespace TestMod.Common.Systems
{
    // 只接入相同的独立效果，不调用会附加其他玩法的整套魂 AddEffects。
    public sealed class FargoAccessoryCompatSystem : ModSystem
    {
        internal const string Mahogany = "Enchantments.MahoganyEffect";
        internal const string Frog = "Masomode.MasoAeolusFrog";
        internal const string Gravity = "Souls.FlightMasteryGravity";
        internal const string Insignia = "Souls.FlightMasteryInsignia";
        internal const string Momentum = "Souls.NoMomentum";

        private sealed class EffectBinding
        {
            public Func<Player, Item, bool> Add;
            public Func<Player, bool> Has;
        }

        private static readonly Dictionary<string, EffectBinding> effects = new();
        private static ModItem flightMastery;
        private static ModPlayer soulsPlayerTemplate;
        private static FieldInfo noMomentum;

        public override void PostSetupContent()
        {
            Clear();
            if (!ModLoader.TryGetMod("FargowiltasSouls", out Mod fargo)) return;
            try
            {
                Type loader = fargo.Code.GetType("FargowiltasSouls.Core.AccessoryEffectSystem.AccessoryEffectLoader", true);
                MethodInfo add = loader.GetMethods(BindingFlags.Public | BindingFlags.Static).Single(m =>
                    m.Name == "AddEffect" && m.IsGenericMethodDefinition && m.GetGenericArguments().Length == 1
                    && m.ReturnType == typeof(bool)
                    && m.GetParameters().Select(p => p.ParameterType).SequenceEqual(new[] { typeof(Player), typeof(Item) }));
                MethodInfo has = loader.GetMethods(BindingFlags.Public | BindingFlags.Static).Single(m =>
                    m.Name == "HasEffect" && m.IsGenericMethodDefinition && m.GetGenericArguments().Length == 1
                    && m.ReturnType == typeof(bool)
                    && m.GetParameters().Select(p => p.ParameterType).SequenceEqual(new[] { typeof(Player) }));
                foreach (string name in new[] { Mahogany, Frog, Gravity, Insignia, Momentum })
                {
                    try
                    {
                        Type effect = fargo.Code.GetType("FargowiltasSouls.Content.Items.Accessories." + name, true);
                        effects.Add(name, new EffectBinding {
                            Add = add.MakeGenericMethod(effect).CreateDelegate<Func<Player, Item, bool>>(),
                            Has = has.MakeGenericMethod(effect).CreateDelegate<Func<Player, bool>>()
                        });
                    }
                    catch (Exception ex) { Warn(name, ex); }
                }
            }
            catch (Exception ex) { Warn("AccessoryEffectLoader", ex); }

            if (effects.ContainsKey(Momentum))
            {
                try
                {
                    soulsPlayerTemplate = fargo.GetContent<ModPlayer>().Single(p =>
                        p.GetType().FullName == "FargowiltasSouls.Core.ModPlayers.FargoSoulsPlayer");
                    noMomentum = soulsPlayerTemplate.GetType().GetField("NoMomentum", BindingFlags.Public | BindingFlags.Instance);
                    if (noMomentum?.FieldType != typeof(bool) || noMomentum.IsInitOnly)
                        throw new MissingFieldException("FargoSoulsPlayer", "NoMomentum");
                }
                catch (Exception ex)
                {
                    effects.Remove(Momentum);
                    soulsPlayerTemplate = null;
                    noMomentum = null;
                    Warn(Momentum, ex);
                }
            }
            if (fargo.TryFind("FlightMasterySoul", out ModItem wings)) flightMastery = wings;
        }

        // true 表示已交给 Fargo（包括被它的开关关闭），不能再回退本地实现绕过开关。
        // AddEffect 返回 false 也可能只是本帧已由另一件装备注册，不能据此重复叠加。
        internal static bool TryProvide(string name, Player player, Item source)
        {
            if (!effects.TryGetValue(name, out EffectBinding binding)) return false;
            try
            {
                binding.Add(player, source);
                return true;
            }
            catch (Exception ex)
            {
                effects.Remove(name);
                Warn(name, ex);
                return false;
            }
        }

        internal static bool HasEffect(string name, Player player)
        {
            if (!effects.TryGetValue(name, out EffectBinding binding)) return false;
            try { return binding.Has(player); }
            catch (Exception ex)
            {
                effects.Remove(name);
                Warn(name, ex);
                return false;
            }
        }

        // 在统一入口已选择冲刺、但所有 PostUpdateEquips 尚未执行时注入，避开注册顺序。
        // Fargo 的 NoMomentum 效果是标志，由其 FargoSoulsPlayer.PostUpdateEquips 实际结算。
        internal static void PrepareMovement(Player player)
        {
            if (!player.TryGetModPlayer(out TerraWingsPlayer wings) || !wings.Equipped
                || !wings.FargoMomentumProvided || player.mount.Active || player.dead
                || player.GetModPlayer<DashPlayer>().ActiveEffect != null || !HasEffect(Momentum, player)) return;
            try
            {
                if (noMomentum == null || !player.TryGetModPlayer(soulsPlayerTemplate, out ModPlayer current))
                    throw new InvalidOperationException("找不到当前玩家的 FargoSoulsPlayer。");
                noMomentum.SetValue(current, true);
            }
            catch (Exception ex)
            {
                effects.Remove(Momentum);
                wings.FargoMomentumProvided = false;
                Warn(Momentum, ex);
            }
        }

        internal static bool TryHorizontalWingSpeeds(Player player, ref float speed, ref float acceleration)
        {
            if (flightMastery == null) return false;
            try
            {
                flightMastery.HorizontalWingSpeeds(player, ref speed, ref acceleration);
                return true;
            }
            catch (Exception ex)
            {
                flightMastery = null;
                Warn("FlightMasterySoul.HorizontalWingSpeeds", ex);
                return false;
            }
        }

        private static void Warn(string name, Exception ex)
            => ModContent.GetInstance<FargoAccessoryCompatSystem>().Mod.Logger.Warn(
                $"[FargoAccessories] {name} 兼容停用，使用本地实现：{ex.Message}");

        private static void Clear()
        {
            effects.Clear();
            flightMastery = null;
            soulsPlayerTemplate = null;
            noMomentum = null;
        }

        public override void Unload() => Clear();
    }
}
