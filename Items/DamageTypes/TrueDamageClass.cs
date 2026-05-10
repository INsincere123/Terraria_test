using Terraria.ModLoader;

namespace TestMod.Items.DamageTypes
{
    // 真实伤害类型
    //   · 只继承通用加成（职业无关，不与任何职业绑定）
    //   · BloodFeedPlayer / TrueDamagePlayer 负责动态取最强职业加成注入 Generic
    //   · 武器前缀通过 TestGlobalItem.ChoosePrefix 独立实现，不影响此处继承关系
    //   · 使用标准暴击计算
    //   · 穿透防御由 GlobalProjectile / GlobalItem 中的 ModifyHitNPC 负责
    public class TrueDamageClass : DamageClass
    {
        public static TrueDamageClass Instance => ModContent.GetInstance<TrueDamageClass>();

        public override StatInheritanceData GetModifierInheritance(DamageClass damageClass)
        {
            if (damageClass == Generic)
                return StatInheritanceData.Full;

            return StatInheritanceData.None;
        }

        public override bool UseStandardCritCalcs => true;
    }
}
