using System;
using System.Reflection;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using MonoMod.RuntimeDetour;
using Terraria;
using Terraria.DataStructures;
using Terraria.ModLoader;
using TestMod.Common.Players;

namespace TestMod.Common.Systems
{
    /// <summary>最终扣血与实际治疗入口；查询治疗值、闪避判定都不产生流血副作用。</summary>
    public sealed class TerraShieldSystem : ModSystem
    {
        private Hook _hurtHook;
        private Hook _reportedHurtHook;
        private ILHook _healHook;

        private delegate double HurtResolved(Player player, PlayerDeathReason source, int damage, int direction,
            out Player.HurtInfo info, bool pvp, bool quiet, int cooldown, bool dodgeable,
            float penetration, float scalingPenetration, float knockback);
        private delegate double HurtResolvedHook(HurtResolved orig, Player player, PlayerDeathReason source,
            int damage, int direction, out Player.HurtInfo info, bool pvp, bool quiet, int cooldown,
            bool dodgeable, float penetration, float scalingPenetration, float knockback);

        public override void Load()
        {
            try
            {
                MethodInfo hurt = typeof(Player).GetMethod(nameof(Player.Hurt), new[] { typeof(Player.HurtInfo), typeof(bool) })
                    ?? throw new MissingMethodException("Player.Hurt(HurtInfo, bool)");
                _hurtHook = new Hook(hurt, (Action<Action<Player, Player.HurtInfo, bool>, Player, Player.HurtInfo, bool>)Hurt);
                MethodInfo reported = typeof(Player).GetMethod(nameof(Player.Hurt), new[] { typeof(PlayerDeathReason),
                    typeof(int), typeof(int), typeof(Player.HurtInfo).MakeByRefType(), typeof(bool), typeof(bool),
                    typeof(int), typeof(bool), typeof(float), typeof(float), typeof(float) })
                    ?? throw new MissingMethodException("Player.Hurt(..., out HurtInfo, ...)");
                _reportedHurtHook = new Hook(reported, (HurtResolvedHook)ReportHurt);
                MethodInfo heal = typeof(Player).GetMethod("ApplyLifeAndOrMana", BindingFlags.Instance | BindingFlags.NonPublic,
                    null, new[] { typeof(Item) }, null) ?? throw new MissingMethodException("Player.ApplyLifeAndOrMana(Item)");
                _healHook = new ILHook(heal, PatchHealing);
            }
            catch (Exception exception)
            {
                DisposeHooks();
                throw new InvalidOperationException("泰拉护盾受伤/治疗入口适配失败，请核对 tModLoader 版本。", exception);
            }
        }

        public override void Unload() => DisposeHooks();

        private void DisposeHooks()
        {
            _healHook?.Dispose();
            _reportedHurtHook?.Dispose();
            _hurtHook?.Dispose();
            _healHook = null;
            _reportedHurtHook = null;
            _hurtHook = null;
        }

        private static void Hurt(Action<Player, Player.HurtInfo, bool> orig, Player player, Player.HurtInfo info, bool quiet)
        {
            if (player.TryGetModPlayer(out TerraShieldPlayer shield)) shield.HurtWithBleed(orig, info, quiet);
            else orig(player, info, quiet);
        }

        private static double ReportHurt(HurtResolved orig, Player player, PlayerDeathReason source,
            int damage, int direction, out Player.HurtInfo info, bool pvp, bool quiet, int cooldown,
            bool dodgeable, float penetration, float scalingPenetration, float knockback)
        {
            if (!player.TryGetModPlayer(out TerraShieldPlayer shield))
                return orig(player, source, damage, direction, out info, pvp, quiet, cooldown,
                    dodgeable, penetration, scalingPenetration, knockback);
            int previous = shield.ResolvedHurtDamage;
            shield.ResolvedHurtDamage = -1;
            try
            {
                double result = orig(player, source, damage, direction, out info, pvp, quiet, cooldown,
                    dodgeable, penetration, scalingPenetration, knockback);
                if (result > 0 && shield.ResolvedHurtDamage >= 0)
                {
                    info.Damage = shield.ResolvedHurtDamage;
                    return info.Damage;
                }
                return result;
            }
            finally { shield.ResolvedHurtDamage = previous; }
        }

        private static void PatchHealing(ILContext il)
        {
            var cursor = new ILCursor(il);
            // 只匹配生命实际增加的赋值，不匹配随后的上限截断；读取最终治疗局部值。
            if (!cursor.TryGotoNext(MoveType.Before, i => i.MatchStfld<Player>(nameof(Player.statLife))) ||
                cursor.Prev.OpCode != OpCodes.Add || !cursor.Prev.Previous.MatchLdloc(out int healLocal))
                throw new InvalidOperationException("找不到实际药水恢复生命的赋值及治疗量。");
            Instruction lifeStore = cursor.Next;
            cursor.Index -= 2;
            // 奇异药水会用随机结果覆盖 GetHealLife，必须在随机取值后补本饰品倍率。
            cursor.Emit(OpCodes.Ldarg_0);
            cursor.Emit(OpCodes.Ldarg_1);
            cursor.Emit(OpCodes.Ldloc, healLocal);
            cursor.EmitDelegate<Func<Player, Item, int, int>>((player, item, amount) =>
                player.TryGetModPlayer(out TerraShieldPlayer shield) ? shield.AdjustSpecialPotionHeal(item, amount) : amount);
            cursor.Emit(OpCodes.Stloc, healLocal);
            cursor.Goto(lifeStore, MoveType.Before);
            cursor.Emit(OpCodes.Ldarg_0);
            cursor.Emit(OpCodes.Ldarg_1);
            cursor.Emit(OpCodes.Ldloc, healLocal);
            cursor.EmitDelegate<Action<Player, Item, int>>((player, item, amount) =>
            {
                if (player.TryGetModPlayer(out TerraShieldPlayer shield)) shield.OnHealingPotionApplied(item, amount);
            });
        }
    }
}
