using Terraria;
using Terraria.ModLoader;

namespace TestMod.Buffs
{
    /// <summary>
    /// 鞭子爆炸的 NPC 冷却标记。
    /// 施加给触发过爆炸的敌人，期间跳过爆炸逻辑，防止每帧都炸。
    /// 无任何游戏逻辑，仅作标记用。
    /// </summary>
    public class TestWhipCooldownBuff : ModBuff
    {
        public override void SetStaticDefaults()
        {
            // 不是负面效果，对玩家无意义，隐藏显示
            Main.buffNoTimeDisplay[Type] = true;
            Main.debuff[Type] = false;
        }
    }
}
