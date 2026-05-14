using Terraria;
using Terraria.ModLoader;

namespace TestMod.Buffs
{
    /// <summary>
    /// 超能护盾冷却 — 紧急护盾触发后的 120 秒冷却计时器。
    /// 每帧由 SuperEnergyShieldPlayer 以剩余帧数刷新，显示倒计时。
    /// </summary>
    public class SuperEnergyShieldCooldownBuff : ModBuff
    {
        public override void SetStaticDefaults()
        {
            Main.buffNoSave[Type] = true; // 重进存档不保留冷却
        }
    }
}
