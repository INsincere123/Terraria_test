using System.Collections.Generic;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using TestMod.Common.Players;
using TestMod.Items.Accessories.Effects;

namespace TestMod.Items.Accessories
{
    // ── 数值调节区 ──────────────────────────────────────────────────────────────
    // 蓄力/触发/叠层参数见 HeartssteelPlayer.cs 的调节区
    // ────────────────────────────────────────────────────────────────────────────
    public class Heartsteel : ModItem
    {
        private const float MinBodyScale  = 1f;    // 最小体型（原始大小）
        private const float MaxBodyScale  = 2f;    // 最大体型（+100%，即 2 倍原始）
        private const int   MaxHpForScale = 2000;  // 达到最大体型所需最大生命值

        public override void SetDefaults()
        {
            Item.width     = 30;
            Item.height    = 30;
            Item.defense   = 5;
            Item.accessory = true;
            Item.value     = Item.sellPrice(gold: 20);
            Item.rare      = ItemRarityID.Red;
        }

        public override void ModifyTooltips(List<TooltipLine> tooltips)
        {
            var hsp = Main.LocalPlayer.GetModPlayer<HeartssteelPlayer>();
            bool maxed = hsp.BonusMaxHP >= HeartssteelPlayer.MaxBonusHP;

            string stackColor  = maxed ? "FFD700" : "6495ED";
            string stackText   = $"[c/{stackColor}:{hsp.BonusMaxHP} / {HeartssteelPlayer.MaxBonusHP}]";
            string maxedSuffix = maxed ? "  [c/00FF00:（已达上限，不再叠层）]" : "";

            var line = new TooltipLine(Mod, "HeartssteelStacks",
                $"永久生命叠层：{stackText}{maxedSuffix}");
            int idx = tooltips.FindIndex(t => t.Name == "Tooltip0");

            if (idx >= 0)
            {
                tooltips.Insert(idx + 5, line);
            }

        }

        public override void UpdateAccessory(Player player, bool hideVisual)
        {
            // 标记本帧装备状态（HeartssteelPlayer 据此决定是否应用叠层生命）
            player.GetModPlayer<HeartssteelPlayer>().HasHeartsteel = true;

            // 免疫击退
            player.noKnockback = true;
            
            // 体型效果：随最大生命线性增长，每 100 HP → +5%，上限 +100%
            PlayerSizeEffect.Apply(player, new PlayerSizeConfig
            {
                Mode         = PlayerSizeMode.ByHealth,
                MinScale     = MinBodyScale,
                MaxScale     = MaxBodyScale,
                MaxStatValue = MaxHpForScale,
                Exponent     = 1f  // ByHealth 模式不使用此字段（线性）
            });
        }
    }
}
