using Terraria.ModLoader;
using TestMod.Common.Systems;

namespace TestMod.Common.Players
{
    public sealed class AbyssImmunityPlayer : ModPlayer
    {
        // 每帧由功能装备重新注入；不同来源合并，不保存或额外发送装备标志。
        public bool Enabled;

        public override void ResetEffects() => Enabled = false;
        public override void UpdateDead() => Enabled = false;
        public override void OnEnterWorld() => Enabled = false;

        public override void PostUpdateEquips()
        {
            if (Enabled && !Player.dead)
                CalamityCompatSystem.ApplyAbyssEquipmentEffects(Player);
        }
    }
}
