using Terraria;
using Terraria.ModLoader;
using TestMod.Common.Players;

namespace TestMod.Common.Systems
{
    /// <summary>
    /// 专注宝珠主动技能按键（与 TimeStopKeybinds / JuLingKeybind 同模式）。
    /// </summary>
    public class FocusOrbKeybind : ModSystem
    {
        public static ModKeybind AbilityKey { get; private set; }

        public override void Load()
        {
            AbilityKey = KeybindLoader.RegisterKeybind(Mod, "FocusOrb Ability", "V");
        }

        public override void Unload()
        {
            AbilityKey = null;
        }

        public override void PostUpdateInput()
        {
            Player player = Main.LocalPlayer;
            if (player == null || !player.active || player.dead) return;

            if (AbilityKey != null && AbilityKey.JustPressed)
                player.GetModPlayer<FocusOrbPlayer>().TryActivate();
        }
    }
}
