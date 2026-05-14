using Terraria;
using Terraria.ModLoader;

namespace TestMod.Buffs
{
    /// <summary>
    /// 黄泉·汲命 — 友方 NPC 或队友死亡后触发，短暂持续。
    /// 纯标记 buff；实际吸血结算在 ArenaAltarPlayer.OnHitNPC 中执行。
    /// </summary>
    public class HuangQuanLifestealBuff : ModBuff
    {
        public override void SetStaticDefaults()
        {
            Main.buffNoSave[Type] = true; // 重进存档不保留
        }
    }
}
