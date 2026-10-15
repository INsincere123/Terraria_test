using System;
using System.IO;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using TestMod.Common.GlobalNPCs;
using TestMod.Common.Systems;
using TestMod.Content.Items.Weapons.Melee;
using TestMod.Content.Projectiles.Melee;

namespace TestMod.Common.Players
{
    public sealed partial class LifetakingPlayer
    {
        internal const byte RequestPacketId = 14, StatePacketId = 15;
        private bool initializedOnServer;
        private ulong lastPulsePacket;

        private ModPacket RequestPacket(byte action)
        {
            ModPacket packet = Mod.GetPacket();
            packet.Write(RequestPacketId);
            packet.Write(action);
            return packet;
        }

        internal void RequestThrow(Vector2 velocity)
        {
            if (Cooldown != 0) return;
            Vector2 direction = velocity.SafeNormalize(new Vector2(Player.direction, 0));
            if (Main.netMode == NetmodeID.SinglePlayer) Throw(direction);
            else if (Main.netMode == NetmodeID.MultiplayerClient)
            {
                Cooldown = CooldownDuration; // 本地立即锁住使用，最终以服务器回复为准。
                ModPacket packet = RequestPacket(1);
                packet.Write(direction.X); packet.Write(direction.Y);
                packet.Send();
            }
        }

        private void Throw(Vector2 direction)
        {
            Item item = Player.HeldItem;
            if (!LifetakingAttackSystem.Ready || Cooldown != 0 || !Player.active || Player.dead || Player.CCed
                || item.type != ModContent.ItemType<LifetakingSpear>() || !float.IsFinite(direction.X)
                || !float.IsFinite(direction.Y)) return;
            int index = Projectile.NewProjectile(Player.GetSource_ItemUse(item), Player.MountedCenter,
                direction.SafeNormalize(Vector2.UnitX * Player.direction) * item.shootSpeed,
                ModContent.ProjectileType<LifetakingSpearProjectile>(), Player.GetWeaponDamage(item),
                Player.GetWeaponKnockback(item, item.knockBack), Player.whoAmI);
            if (index >= Main.maxProjectiles) return;
            Projectile p = Main.projectile[index];
            p.CritChance = Player.GetWeaponCrit(item);
            p.ArmorPenetration = Player.GetWeaponArmorPenetration(item);
            p.netUpdate = true;
            authorizedSpear = p.identity;
            Cooldown = CooldownDuration;
            if (Main.netMode == NetmodeID.Server) SendState();
        }

        internal void RequestMark(Projectile spear, NPC target)
        {
            if (!target.active || target.life <= 0 || target.friendly) return;
            if (Main.netMode == NetmodeID.SinglePlayer)
            {
                if (authorizedSpear != spear.identity) return;
                authorizedSpear = -1;
                SetMark(target);
            }
            else if (Main.netMode == NetmodeID.MultiplayerClient)
            {
                // 等服务器确认连接后才能伤害；请求不携带伤害、持续时间或任意玩家编号。
                ModPacket packet = RequestPacket(2);
                packet.Write(spear.identity); packet.Write((short)target.whoAmI);
                packet.Write(target.GetGlobalNPC<HookTargetIdentity>().Generation);
                packet.Send();
            }
        }

        public override void SyncPlayer(int toWho, int fromWho, bool newPlayer)
        {
            if (Main.netMode == NetmodeID.Server) SendState(toWho);
        }

        private void SendState(int toWho = -1)
        {
            if (Main.netMode != NetmodeID.Server) return;
            ModPacket packet = Mod.GetPacket();
            packet.Write(StatePacketId); packet.Write((byte)Player.whoAmI);
            packet.Write((ushort)Cooldown); packet.Write((ushort)MarkTime);
            packet.Write((short)targetSlot); packet.Write(targetType);
            packet.Write(targetGeneration); packet.Write(MarkSerial); packet.Write((byte)PulseTime);
            packet.Write(initializedOnServer);
            packet.Send(toWho);
        }

        internal static void ReceiveRequest(BinaryReader reader, int sender)
        {
            byte action = reader.ReadByte();
            if (Main.netMode != NetmodeID.Server || sender < 0 || sender >= Main.maxPlayers) return;
            Player player = Main.player[sender];
            if (!player.active || !player.TryGetModPlayer(out LifetakingPlayer state)) return;
            switch (action)
            {
                case 0:
                    int saved = Math.Clamp((int)reader.ReadUInt16(), 0, CooldownDuration);
                    if (!state.initializedOnServer)
                    {
                        state.initializedOnServer = true;
                        state.Cooldown = Math.Max(state.Cooldown, saved);
                        state.SendState();
                    }
                    break;
                case 1:
                    Vector2 direction = new(reader.ReadSingle(), reader.ReadSingle());
                    if (state.initializedOnServer) state.Throw(direction);
                    state.SendState(sender);
                    break;
                case 2:
                    int identity = reader.ReadInt32(), slot = reader.ReadInt16();
                    uint generation = reader.ReadUInt32();
                    if (!player.dead && identity == state.authorizedSpear && slot >= 0 && slot < Main.maxNPCs)
                    {
                        NPC target = Main.npc[slot];
                        Projectile spear = null;
                        foreach (Projectile candidate in Main.ActiveProjectiles)
                            if (candidate.owner == sender && candidate.identity == identity
                                && candidate.type == ModContent.ProjectileType<LifetakingSpearProjectile>())
                            { spear = candidate; break; }
                        // 注册的本次投矛才能创建连接；校验服务端实体、代次和实际相遇区域。
                        if (spear != null && target.active && target.life > 0 && !target.friendly && !target.dontTakeDamage
                            && generation != 0 && generation == target.GetGlobalNPC<HookTargetIdentity>().Generation
                            && Vector2.DistanceSquared(spear.Center, target.Center)
                                <= MathF.Pow(128 + target.Size.Length() * 0.5f, 2))
                        {
                            state.authorizedSpear = -1;
                            state.SetMark(target);
                            spear.ai[0] = 1; spear.ai[1] = slot;
                            spear.timeLeft = MarkDuration * spear.MaxUpdates;
                            spear.netUpdate = true;
                        }
                    }
                    state.SendState();
                    break;
                case 3:
                    uint serial = reader.ReadUInt32();
                    if (serial == state.MarkSerial && state.TryGetTarget(out _)
                        && Main.GameUpdateCount - state.lastPulsePacket >= 4)
                    {
                        state.lastPulsePacket = Main.GameUpdateCount;
                        state.PulseTime = 10;
                        state.SendState();
                    }
                    break;
            }
        }

        internal static void ReceiveState(BinaryReader reader)
        {
            int index = reader.ReadByte(), cooldown = reader.ReadUInt16(), time = reader.ReadUInt16();
            int slot = reader.ReadInt16(), type = reader.ReadInt32();
            uint generation = reader.ReadUInt32(), serial = reader.ReadUInt32();
            int pulse = reader.ReadByte();
            bool initialized = reader.ReadBoolean();
            if (Main.netMode != NetmodeID.MultiplayerClient || index >= Main.maxPlayers
                || !Main.player[index].TryGetModPlayer(out LifetakingPlayer state)) return;
            // 入场初次同步不能把角色尚未提交的存档冷却清零。
            if (initialized || index != Main.myPlayer) state.Cooldown = Math.Clamp(cooldown, 0, CooldownDuration);
            if (time == 0 && state.MarkTime > 0 && state.TryGetTarget(out NPC oldTarget))
                LifetakingAttackSystem.PlayFinish(oldTarget.Center);
            state.MarkTime = Math.Clamp(time, 0, MarkDuration);
            state.targetSlot = slot; state.targetType = type; state.targetGeneration = generation;
            state.MarkSerial = serial; state.PulseTime = Math.Clamp(pulse, 0, 12);
        }
    }
}
