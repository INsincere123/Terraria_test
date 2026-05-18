using Terraria.ModLoader;

namespace TestMod.Items.DamageTypes
{
    // 真实伤害类型
    //   · 只继承通用加成（职业无关，不与任何职业绑定）
    //   · BloodFeedPlayer / TrueDamagePlayer 负责动态取最强职业加成注入 Generic
    //   · 武器前缀通过 GlobalItem.ChoosePrefix 独立实现，不影响此处继承关系
    //   · 使用标准暴击计算
    //   · 穿透防御由 GlobalProjectile / GlobalItem 中的 ModifyHitNPC 负责
    public class TrueDamageClass : DamageClass
    {
        public static TrueDamageClass Instance => ModContent.GetInstance<TrueDamageClass>();

        public override StatInheritanceData GetModifierInheritance(DamageClass damageClass)
        {
            // 数值继承：只继承通用加成（职业无关设计）
            if (damageClass == Generic)
                return StatInheritanceData.Full;

            return StatInheritanceData.None;
        }

        // 前缀继承：告知引擎按近战处理前缀资格，使哥布林工匠可以重铸此类武器
        // 与 GetModifierInheritance 完全独立，不会引入近战伤害/暴击加成
        // ChoosePrefix 会覆盖实际选中的词缀，此方法只负责"让重铸按钮出现"
        public override bool GetPrefixInheritance(DamageClass damageClass)
            => damageClass == DamageClass.Melee;

        // 效果继承：继承 Throwing，使 CountsAsClass 检查可见（套装效果、饰品特效等）
        // Throwing 在 1.4 原版无防具套装，副作用最小
        public override bool GetEffectInheritance(DamageClass damageClass)
            => damageClass == DamageClass.Throwing;

        public override bool UseStandardCritCalcs => true;
    }
}
