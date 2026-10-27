using System.Collections.Generic;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using TestMod.Common.Mechanics.Dashes;
using TestMod.Common.Players;

namespace TestMod.Content.Items.Accessories
{
    public sealed class ShieldOfCthulhuPlus : ModItem
    {
        internal static readonly AccessoryDashConfig DashConfiguration = new(
            Source: AccessoryDashSource.Cthulhu, // 装备来源；用于选择优先级、卸装取消及区分残影来源。
            Motion: AccessoryDashMotion.Segmented, // 分段减速模式，减至正常跑速后结束；贴墙起步速度减半。
            StartSpeed: 21.5f, // 水平起速，单位像素/tick；方向由本次冲刺输入决定。
            FastRetention: 0.99f, // 起步后，水平速度绝对值大于FastThreshold时，每帧保留当前速度的98.8%。
            SlowRetention: 0.96f, // 水平速度不超过FastThreshold但仍高于正常跑速时，每帧保留当前速度的96%。
            FastThreshold: 11f, // 高低速段分界，单位像素/tick；正常跑速取accRunSpeed与maxRunSpeed的较大值。
            Duration: 0, // 当前分段模式不使用固定时长，实际由速度衰减、撞墙或状态失效结束。
            Cooldown: 60, // 冲刺结束后冷却60tick（1秒），从结束时开始计时。
            ExitRetention: 0f, // 仅固定时长模式使用的结束残速上限倍率；当前分段模式不使用，不表示结束清零速度。
            Damage: 60, // 每目标每次冲刺的基础真实伤害，另受真实伤害总增伤和暴击影响。
            Knockback: 2f, // 撞击基础击退为0，不额外推开敌怪。
            HitboxExpand: 6, // 位移起终点玩家矩形并集不向外扩张，作为撞击检测范围。
            ContactImmunity: 12, // 成功撞击后补足12 tickNPC接触免疫，不额外免疫弹幕；结束/卸装保留剩余。
            Visual: AccessoryDashVisual.Shadow); // 使用人物残影，不额外生成帷幕烟雾或泰拉冲刺粒子。

        public override string Texture => "Terraria/Images/Item_" + ItemID.EoCShield;

        public override void SetDefaults()
        {
            Item.width = Item.height = 32;
            Item.accessory = true;
            Item.defense = 3;
            Item.rare = ItemRarityID.Pink;
            Item.value = Item.sellPrice(gold: 5);
            Item.shieldSlot = ArmorIDs.Shield.ShieldofCthulhu;
        }

        public override void UpdateAccessory(Player player, bool hideVisual) =>
            player.GetModPlayer<CompositeAccessoryPlayer>().EquipShield();

        public override void ModifyTooltips(List<TooltipLine> tooltips) => PanicToggleAccessory.AddDashHint(tooltips, Mod);

        public override void AddRecipes() => CreateRecipe()
            .AddIngredient(ItemID.BrainOfConfusion).AddIngredient(ItemID.WormScarf)
            .AddIngredient(ItemID.EoCShield).AddIngredient(ItemID.ObsidianShield)
            .AddTile(TileID.TinkerersWorkbench).Register();
    }
}
