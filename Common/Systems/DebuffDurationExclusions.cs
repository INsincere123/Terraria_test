using System;
using System.Collections.Generic;
using Terraria.ModLoader;
using TestMod.Content.Buffs;

namespace TestMod.Common.Systems
{
    /// <summary>只排除本项目的额外 Debuff 衰减，不改变原生倒计时、免疫或其他模组效果。</summary>
    public class DebuffDurationExclusions : ModSystem
    {
        private static readonly HashSet<int> ExcludedBuffs = new();

        public override void PostSetupContent()
        {
            ExcludedBuffs.Clear();
            ExcludedBuffs.Add(ModContent.BuffType<ClassicGrapeBeerBuff>());
            if (!ModLoader.TryGetMod("CalamityMod", out Mod calamity)) return;

            // 使用公开注册内容，不反射灾厄私有酒精表，也不新增硬依赖。
            foreach (ModBuff buff in calamity.GetContent<ModBuff>())
            {
                string ns = buff.GetType().Namespace;
                if (ns == "CalamityMod.Buffs.Alcohol" ||
                    ns?.StartsWith("CalamityMod.Buffs.Alcohol.", StringComparison.Ordinal) == true)
                    ExcludedBuffs.Add(buff.Type);
            }
        }

        internal static bool ShouldSkipReduction(int buffType) => ExcludedBuffs.Contains(buffType);

        public override void Unload() => ExcludedBuffs.Clear();
    }
}
