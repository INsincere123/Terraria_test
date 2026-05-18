using Terraria;
using Terraria.DataStructures;
using Terraria.ModLoader;
using TestMod.Items.Accessories;

namespace TestMod.Common.Players
{
    /// <summary>
    /// 玩家死亡时扫描所有饰品槽，将 DeathBoundItem 实例标记为失效。
    /// </summary>
    public class DeathBoundPlayer : ModPlayer
    {
        public override void Kill(double damage, int hitDirection, bool pvp, PlayerDeathReason damageSource)
        {
            // armor[0-2] 为护甲，[3] 起为饰品槽；含 demon heart 等额外槽位
            for (int i = 3; i < Player.armor.Length; i++)
            {
                if (Player.armor[i].ModItem is DeathBoundItem item && item.IsActive)
                {
                    item.IsActive = false;
                    if (item.BreakMessage is string msg)
                        Main.NewText(msg, 180, 100, 100);
                }
            }
        }
    }
}
