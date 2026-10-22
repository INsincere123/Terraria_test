using System;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace TestMod.Common.Systems
{
    public sealed partial class WallofShadowArenaSystem
    {
        internal static Guid InheritSession(IEntitySource source)
        {
            if (Session == Guid.Empty || source is not EntitySource_Parent parent) return Guid.Empty;
            if (parent.Entity is NPC npc && npc.TryGetGlobalNPC(out WallArenaNPC state)) return state.SessionId;
            if (parent.Entity is Projectile projectile && projectile.TryGetGlobalProjectile(out WallArenaProjectile p)) return p.SessionId;
            return Guid.Empty;
        }

        private static void CleanupEntities(Guid session)
        {
            foreach (Projectile projectile in Main.projectile)
                if (projectile.active && projectile.TryGetGlobalProjectile(out WallArenaProjectile state) && state.SessionId == session)
                {
                    // 不运行OnKill，防止清理时额外产生攻击。
                    projectile.active = false;
                    if (Main.netMode == NetmodeID.Server)
                        NetMessage.SendData(MessageID.KillProjectile, number: projectile.identity, number2: projectile.owner);
                }
            foreach (NPC npc in Main.npc)
                if (npc.active && npc.TryGetGlobalNPC(out WallArenaNPC state) && state.SessionId == session)
                {
                    npc.active = false;
                    if (Main.netMode == NetmodeID.Server) NetMessage.SendData(MessageID.SyncNPC, number: npc.whoAmI);
                }
        }
    }

    public sealed class WallArenaNPC : GlobalNPC
    {
        public override bool InstancePerEntity => true;
        internal Guid SessionId;
        public override void OnSpawn(NPC npc, IEntitySource source) => SessionId = WallofShadowArenaSystem.InheritSession(source);
    }
    public sealed class WallArenaProjectile : GlobalProjectile
    {
        public override bool InstancePerEntity => true;
        internal Guid SessionId;
        public override void OnSpawn(Projectile projectile, IEntitySource source) => SessionId = WallofShadowArenaSystem.InheritSession(source);
    }
    public sealed class WallArenaTileProtection : GlobalTile
    {
        public override bool CanKillTile(int i, int j, int type, ref bool blockDamaged) => !WallofShadowArenaSystem.Protects(i,j);
        public override bool CanPlace(int i, int j, int type) => !WallofShadowArenaSystem.Protects(i,j);
        public override bool CanExplode(int i, int j, int type) => !WallofShadowArenaSystem.Protects(i,j);
        public override bool CanReplace(int i, int j, int type, int tileTypeBeingPlaced) => !WallofShadowArenaSystem.Protects(i,j);
    }
    public sealed class WallArenaWallProtection : GlobalWall
    {
        public override bool CanPlace(int i, int j, int type) => !WallofShadowArenaSystem.Protects(i,j);
        public override bool CanExplode(int i, int j, int type) => !WallofShadowArenaSystem.Protects(i,j);
        public override void KillWall(int i, int j, int type, ref bool fail)
        {
            if (WallofShadowArenaSystem.Protects(i,j)) fail = true;
        }
    }
    public sealed class WallArenaMarkerTile : ModTile
    {
        public override string Texture => "Terraria/Images/Tiles_1";
        public override void SetStaticDefaults()
        {
            Main.tileSolid[Type] = true;
            Main.tileBlockLight[Type] = true;
            Main.tileFrameImportant[Type] = true;
            AddMapEntry(new Microsoft.Xna.Framework.Color(50,50,50));
        }
        public override bool CanKillTile(int i, int j, ref bool blockDamaged) => false;
        public override bool CanExplode(int i, int j) => false;
    }
    public sealed class WallArenaToolProtection : GlobalItem
    {
        public override bool CanUseItem(Item item, Player player)
        {
            var area = WallofShadowArenaSystem.ProtectedArea;
            if (area.IsEmpty) return true;
            bool targetsArena = WallofShadowArenaSystem.Protects(Player.tileTargetX, Player.tileTargetY);
            bool inside = new Microsoft.Xna.Framework.Rectangle(area.X * 16, area.Y * 16,
                area.Width * 16, area.Height * 16).Intersects(player.Hitbox);
            if (!inside && !targetsArena) return true;
            return !item.mech && item.paint == 0 && item.type is not (
                ItemID.EmptyBucket or ItemID.WaterBucket or ItemID.LavaBucket or ItemID.HoneyBucket or
                ItemID.BottomlessBucket or ItemID.BottomlessLavaBucket or ItemID.BottomlessHoneyBucket or
                ItemID.BottomlessShimmerBucket or ItemID.SuperAbsorbantSponge or ItemID.LavaAbsorbantSponge or
                ItemID.HoneyAbsorbantSponge or ItemID.UltraAbsorbantSponge);
        }
    }
}
