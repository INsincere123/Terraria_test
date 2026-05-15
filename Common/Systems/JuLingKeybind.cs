using Terraria;
using Terraria.ModLoader;
using TestMod.Common.Players;

namespace TestMod.Common.Systems
{
    /// <summary>
    /// 聚灵套主动技能按键。
    /// 在 PostUpdateInput 中检测，避免与 UI 输入冲突（与 TimeStopKeybinds 同模式）。
    /// </summary>
    public class JuLingKeybind : ModSystem
    {
        public static ModKeybind AbilityKey { get; private set; }

        public override void Load()
        {
            AbilityKey = KeybindLoader.RegisterKeybind(Mod, "JuLing Ability", "Z");
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
                player.GetModPlayer<JuLingPlayer>().TryActivate();
        }
    }
}
