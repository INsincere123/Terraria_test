using System.Collections.Generic;
using System.Text;
using Terraria;
using Terraria.ModLoader;

namespace TestMod.Common.Systems
{
    /// <summary>
    /// 按键绑定相关工具方法。
    /// </summary>
    public static class KeybindUtils
    {
        /// <summary>
        /// 获取 ModKeybind 的当前绑定键字符串。
        /// 多个绑定时用 " / " 拼接，未绑定时返回"未绑定"。
        /// 用法：KeybindUtils.GetKeyText(TimeStopKeybinds.TimeStopKey)
        /// </summary>
        public static string GetKeyText(ModKeybind keybind)
        {
            if (Main.dedServ || keybind is null)
                return "";

            List<string> keys = keybind.GetAssignedKeys();
            if (keys.Count == 0)
                return "未绑定";

            StringBuilder sb = new StringBuilder();
            sb.Append(keys[0]);
            for (int i = 1; i < keys.Count; i++)
                sb.Append(" / ").Append(keys[i]);
            return sb.ToString();
        }
    }
}
