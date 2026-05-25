using Terraria;
using Terraria.ModLoader;

namespace TestMod.Content.Buffs
{
    /// <summary>
    /// 黄泉·杀意 — 范围内击杀敌方 NPC 后触发，短暂持续。
    /// 纯标记 buff；实际伤害加成在 ArenaAltarPlayer.ResetEffects 中执行。
    /// </summary>
    public class HuangQuanDamageBoostBuff : ModBuff
    {
        public override void SetStaticDefaults()
        {
            Main.buffNoSave[Type] = true; // 重进存档不保留
        }
    }
}
