using Luminance.Core.Graphics;
using Terraria;
using Terraria.ModLoader;
using TestMod.Content.Rarities;

namespace TestMod.Common.Systems
{
    public sealed class EventHorizonRaritySystem : ModSystem
    {
        public override void Load()
        {
            if (Main.dedServ)
                return;

            // 离屏内容必须在世界和 UI 绘制前准备，不能在 tooltip 中切换目标并清掉当前帧。
            RenderTargetManager.RenderTargetUpdateLoopEvent += EventHorizonRarity.PrepareNameTarget;
        }

        public override void Unload()
        {
            if (Main.dedServ)
                return;

            RenderTargetManager.RenderTargetUpdateLoopEvent -= EventHorizonRarity.PrepareNameTarget;
            EventHorizonRarity.UnloadResources();
        }
    }
}
