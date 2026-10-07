using System;
using System.Collections.Generic;
using MonoMod.RuntimeDetour;
using Terraria;
using Terraria.ModLoader;
using Terraria.UI;
using TestMod.Common.Players;
using TestMod.Common.Utilities;

namespace TestMod.Common.Systems
{
    public sealed class MomentumConverterSystem : ModSystem
    {
        private Hook movementHook;
        private delegate void MovementOriginal(Player player);
        private delegate void MovementHook(MovementOriginal original, Player player);

        public override void Load()
        {
            var method = typeof(PlayerLoader).GetMethod(nameof(PlayerLoader.PreUpdateMovement), new[] { typeof(Player) });
            if (method is null)
                throw new MissingMethodException(typeof(PlayerLoader).FullName, nameof(PlayerLoader.PreUpdateMovement));
            movementHook = new Hook(method, (MovementHook)FinishMovementPreparation);
        }

        public override void Unload()
        {
            movementHook?.Dispose();
            movementHook = null;
        }

        public override void OnWorldUnload()
        {
            foreach (Player player in Main.player)
            {
                if (player is not null && player.TryGetModPlayer(out MomentumConverterPlayer state))
                    state.ResetSession();
            }
        }

        private static void FinishMovementPreparation(MovementOriginal original, Player player)
        {
            // 全部 ModPlayer 移动覆写之后、原版碰撞之前仅转换一次；不依赖注册顺序。
            original(player);
            player.GetModPlayer<MomentumConverterPlayer>().ConsumeConversionRequest();
        }

        public override void ModifyInterfaceLayers(List<GameInterfaceLayer> layers)
        {
            if (Main.dedServ)
                return;
            int index = layers.FindIndex(layer => layer.Name == "Vanilla: Mouse Text");
            if (index < 0)
                index = layers.Count;
            layers.Insert(index, new LegacyGameInterfaceLayer("TestMod: Momentum Charges", DrawChargeBar, InterfaceScaleType.Game));
        }

        private static bool DrawChargeBar()
        {
            if (Main.dedServ || Main.gameMenu || Main.mapFullscreen || !Main.LocalPlayer.active || Main.LocalPlayer.dead)
                return true;
            MomentumConverterPlayer state = Main.LocalPlayer.GetModPlayer<MomentumConverterPlayer>();
            if (state.Equipped)
                DrawUtils.DrawMomentumChargeBar(Main.spriteBatch, Main.LocalPlayer, state.Charges, state.RechargeProgress);
            return true;
        }
    }
}
