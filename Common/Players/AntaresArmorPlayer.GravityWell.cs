using Terraria;
using Terraria.Audio;
using Terraria.GameInput;
using Terraria.ID;
using Terraria.ModLoader;
using TestMod.Common.Systems;
using TestMod.Content.Buffs;
using TestMod.Content.Items.Armor.UltimateArmor;

namespace TestMod.Common.Players
{
    public partial class AntaresArmorPlayer
    {
        internal const byte GravityWellRequestPacket = 22, GravityWellActivatedPacket = 23;
        private bool? gravityWellRequest;
        // 服务器只信任经装备校验的请求，不以客户端同步的 Buff 作为授权。
        private int gravityWellRemaining;

        public override void ProcessTriggers(TriggersSet triggersSet)
        {
            if (Player.whoAmI == Main.myPlayer && !Player.dead &&
                AntaresKeybinds.GravityWellKey?.JustPressed == true)
                gravityWellRequest = true;
        }

        public override void OnEnterWorld() => ClearGravityWell();
        public override void UpdateDead() => ClearGravityWell();

        private void ClearGravityWell()
        {
            gravityWellRequest = null;
            gravityWellRemaining = 0;
        }

        private bool HasCurrentFullSet() => Player.active && !Player.dead && wearingFullSet &&
            Player.armor[0].type == ModContent.ItemType<UltimateHelmet>() &&
            Player.armor[1].type == ModContent.ItemType<UltimateBreastplate>() &&
            Player.armor[2].type == ModContent.ItemType<UltimateLeggings>();

        private void UpdateGravityWell()
        {
            if (!Player.active || Player.dead)
            {
                ClearGravityWell();
                return;
            }

            if (gravityWellRequest is bool activate)
            {
                gravityWellRequest = null;
                if (!activate)
                    gravityWellRemaining = 0;
                else if (HasCurrentFullSet())
                {
                    if (Main.netMode == NetmodeID.MultiplayerClient)
                        SendGravityWellRequest(true);
                    else
                    {
                        gravityWellRemaining = GravityWellDuration;
                        if (Main.netMode == NetmodeID.Server)
                        {
                            ModPacket packet = Mod.GetPacket();
                            packet.Write(GravityWellActivatedPacket);
                            packet.Send(Player.whoAmI);
                        }
                        else ShowGravityWellActivation();
                    }
                }
            }

            if (gravityWellRemaining <= 0) return;
            if (Main.netMode != NetmodeID.Server && !Player.HasBuff(ModContent.BuffType<AntaresGravityWellBuff>()))
            {
                // 保留手动取消 Buff 的行为；取消包只允许结束发送者自己的授权窗口。
                gravityWellRemaining = 0;
                if (Main.netMode == NetmodeID.MultiplayerClient) SendGravityWellRequest(false);
                return;
            }

            if (Main.netMode != NetmodeID.MultiplayerClient && HasCurrentFullSet())
                PullNearbyEnemies();
            // 卸装不暂停计时，重新穿回也不凭现有 Buff 重新授权。
            gravityWellRemaining--;
        }

        private void SendGravityWellRequest(bool activate)
        {
            ModPacket packet = Mod.GetPacket();
            packet.Write(GravityWellRequestPacket);
            packet.Write(activate);
            packet.Send();
        }

        internal static void ReceiveGravityWellRequest(System.IO.BinaryReader reader, int sender)
        {
            bool activate = reader.ReadBoolean();
            if (Main.netMode != NetmodeID.Server || sender < 0 || sender >= Main.maxPlayers) return;
            Player player = Main.player[sender];
            if (player.active && !player.dead && player.TryGetModPlayer(out AntaresArmorPlayer state))
                state.gravityWellRequest = activate;
            // 在 PostUpdate 中合并同 tick 请求并复核本帧装备注入结果。
        }

        internal static void ReceiveGravityWellActivated()
        {
            if (Main.netMode != NetmodeID.MultiplayerClient) return;
            Player player = Main.LocalPlayer;
            if (!player.active || player.dead || !player.TryGetModPlayer(out AntaresArmorPlayer state)) return;
            state.gravityWellRemaining = GravityWellDuration;
            state.ShowGravityWellActivation();
        }

        private void ShowGravityWellActivation()
        {
            Player.AddBuff(ModContent.BuffType<AntaresGravityWellBuff>(), GravityWellDuration);
            SoundEngine.PlaySound(SoundID.Item9, Player.Center);
            SpawnGravityWaveParticles();
        }
    }
}
