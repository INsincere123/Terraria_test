using Microsoft.Xna.Framework.Input;
using TestMod.Common.Players;
using Terraria;
using Terraria.ModLoader;

namespace TestMod.Common.Systems
{
    /// <summary>
    /// 时停按键。参考现有 DashKeybinds / AntaresKeybinds 风格。
    /// 在 ModSystem.PostUpdateInput 检测，避免与 UI 输入冲突。
    /// </summary>
    public class TimeStopKeybinds : ModSystem
    {
        public static ModKeybind TimeStopKey { get; private set; }

        public override void Load()
        {
            // 默认绑定到 X 键，玩家可在游戏内重映射
            TimeStopKey = KeybindLoader.RegisterKeybind(Mod, "Activate Time Stop", "X");
        }

        public override void Unload()
        {
            TimeStopKey = null;
        }

        public override void PostUpdateInput()
        {
            Player player = Main.LocalPlayer;
            if (player == null || !player.active || player.dead) return;

            if (TimeStopKey != null && TimeStopKey.JustPressed)
            {
                TimeStopPlayer modPlayer = player.GetModPlayer<TimeStopPlayer>();

                // 没有能力直接静默忽略；激活成功在 TryActivateTimeStop 内部播放音效
                modPlayer.TryActivateTimeStop();

                // 注：可选地在这里给"按键按下但冷却中"的情况做提示，目前按需求保持沉默
            }
        }
    }
}
