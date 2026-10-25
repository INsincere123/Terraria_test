using System.Collections.Generic;
using Terraria;
using Terraria.ModLoader;

namespace TestMod.Common.Systems
{
    public class ClassicGrapeBeerSystem : ModSystem
    {
        // 固定的经典版排除表。只解析内容身份，不读取灾厄的追踪参数或排除集合。
        private static readonly string[] ClassicExclusions = {
            "NukeOfBliss", "PrismaticEnergyBlast", "PrismEnergyBullet", "PrismMine",
            "ScorchedEarthRocket", "UltimaRay", "SproutingArrowMain"
        };
        private static readonly HashSet<int> ExcludedTypes = new();
        private static int _originalBuff;

        public override void PostSetupContent()
        {
            ExcludedTypes.Clear();
            _originalBuff = 0;
            if (!ModLoader.TryGetMod("CalamityMod", out Mod calamity)) return;
            foreach (string name in ClassicExclusions)
                if (calamity.TryFind(name, out ModProjectile projectile)) ExcludedTypes.Add(projectile.Type);
            if (calamity.TryFind("GrapeBeerBuff", out ModBuff buff)) _originalBuff = buff.Type;
            else Mod.Logger.Warn("经典葡萄啤酒：未找到灾厄 GrapeBeerBuff，无法排除原版饮品 Buff；独立追踪参数保持不变。");
        }

        internal static bool IsExcluded(int type) => ExcludedTypes.Contains(type);

        internal static void RemoveOriginalBuff(Player player)
        {
            if (_originalBuff > 0 && player.HasBuff(_originalBuff)) player.ClearBuff(_originalBuff);
        }

        public override void Unload()
        {
            ExcludedTypes.Clear();
            _originalBuff = 0;
        }
    }
}
