using System;
using System.Collections.Generic;
using System.Reflection;
using MonoMod.RuntimeDetour;
using Terraria;
using Terraria.ModLoader;
using TestMod.Common.Mechanics.Dashes;

namespace TestMod.Common.Systems
{
    // 只接管输入；各系统仍自行检查装备、冷却并执行移动/命中/同步。
    public partial class DashInputSystem : ModSystem
    {
        private readonly List<IDisposable> hooks = new();
        private delegate void EquipsOriginal(Player player);
        private delegate void EquipsDetour(EquipsOriginal original, Player player);
        private delegate void DashOriginal(Player player, out int direction, out bool dashing,
            Player.DashStartAction start);
        private delegate void DashDetour(DashOriginal original, Player player, out int direction,
            out bool dashing, Player.DashStartAction start);

        public override void PostSetupContent()
        {
            // 有显式顺序，灾厄即便更晚安装 On 钩子也不能抢先消费自己的按键。
            var order = new DetourConfig("TestMod.DashInput", priority: int.MaxValue,
                before: new[] { "CalamityMod" });
            var common = typeof(Player).GetMethod("DoCommonDashHandle", BindingFlags.Instance | BindingFlags.NonPublic,
                null, new[] { typeof(int).MakeByRefType(), typeof(bool).MakeByRefType(), typeof(Player.DashStartAction) }, null);
            if (common == null || common.ReturnType != typeof(void))
                throw new MissingMethodException("Player.DoCommonDashHandle");
            hooks.Add(new Hook(common, (DashDetour)HandleVanillaInput, order));
            hooks.Add(new Hook(typeof(PlayerLoader).GetMethod(nameof(PlayerLoader.PostUpdateEquips), new[] { typeof(Player) }),
                (EquipsDetour)BeforePostUpdateEquips, order));
            LoadOptionalDashInputs();
        }

        private static void BeforePostUpdateEquips(EquipsOriginal original, Player player)
        {
            if (player.TryGetModPlayer(out DashPlayer state)) state.PrepareEquippedDashes();
            original(player);
        }

        private static bool OwnsHorizontalInput(Player player) => !Main.dedServ
            && player.whoAmI == Main.myPlayer && DashPlayer.SingleTapMode;

        private static void HandleVanillaInput(DashOriginal original, Player player, out int direction,
            out bool dashing, Player.DashStartAction start)
        {
            if (!OwnsHorizontalInput(player))
            {
                original(player, out direction, out dashing, start);
                return;
            }

            direction = player.GetModPlayer<DashPlayer>().ConsumeHorizontalRequest();
            dashing = direction != 0;
            if (!dashing) return;
            player.ChangeDir(direction);
            player.dashTime = 0;
            player.timeSinceLastDashStarted = 0;
            start?.Invoke(direction);
        }

        private void TryLoadInput(string name, Func<IDisposable> install)
        {
            try { hooks.Add(install()); }
            catch (Exception exception)
            {
                Mod.Logger.Warn($"[DashInput] {name} 输入适配停用：签名或 IL 不匹配，保留该入口原行为。", exception);
            }
        }

        public override void OnWorldUnload()
        {
            foreach (Player player in Main.player)
                if (player != null && player.TryGetModPlayer(out DashPlayer state)) state.ResetDashSession();
        }

        public override void Unload()
        {
            for (int i = hooks.Count - 1; i >= 0; i--) hooks[i].Dispose();
            hooks.Clear();
        }
    }
}
