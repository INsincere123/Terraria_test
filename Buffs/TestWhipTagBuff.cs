using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace TestMod.Buffs
{
    /// <summary>
    /// 测试鞭子的 tag debuff，施加给被鞭子打中的敌人。
    /// 本身无逻辑，效果由 GlobalProjectile.WhipTag.cs 处理。
    /// </summary>
    public class TestWhipTagBuff : ModBuff
    {
        public override void SetStaticDefaults()
        {
            BuffID.Sets.IsATagBuff[Type] = true;
        }
    }
}
