using System;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;

namespace TestMod.Common.Systems
{
    public sealed partial class DecoySystem
    {
        private delegate void ClosestOriginal(NPC npc, bool faceTarget);
        private delegate void ClosestDetour(ClosestOriginal original, NPC npc, bool faceTarget);
        private delegate void UpgradedOriginal(NPC npc, bool faceTarget, Vector2? checkPosition);
        private delegate void UpgradedDetour(UpgradedOriginal original, NPC npc, bool faceTarget, Vector2? checkPosition);
        private delegate NPCAimedTarget DataOriginal(NPC npc, bool ignoreTank);
        private delegate NPCAimedTarget DataDetour(DataOriginal original, NPC npc, bool ignoreTank);
        private void InstallTargetHooks()
        {
            Install(typeof(NPC), nameof(NPC.TargetClosest), new[] { typeof(bool) }, (ClosestDetour)TargetClosest);
            Install(typeof(NPC), nameof(NPC.TargetClosest_WOF), new[] { typeof(bool) }, (ClosestDetour)TargetClosest);
            Install(typeof(NPC), nameof(NPC.TargetClosestUpgraded), new[] { typeof(bool), typeof(Vector2?) }, (UpgradedDetour)TargetUpgraded);
            Install(typeof(NPC), nameof(NPC.GetTargetData), new[] { typeof(bool) }, (DataDetour)TargetData);
        }
        private static bool ForceTarget(NPC npc, bool face)
        {
            if (!viewApplied || current?.Fake != true || !ReferenceEquals(current.Entity, npc) || current.Decoy.Resolve() == null)
                return false;
            Player selected = null;
            foreach (PlayerGeometry value in truth)
                if (value.StillCurrent && value.Player.active && !value.Player.dead)
                {
                    selected ??= value.Player;
                    if (value.Player.whoAmI == npc.target) { selected = value.Player; break; }
                }
            if (selected == null) return false;
            // 保持真实玩家索引，仅改变位置视图；兼容支持 NPC 目标的原版入口。
            npc.target = selected.whoAmI; npc.targetRect = selected.Hitbox;
            if (face)
            {
                npc.direction = current.Target.X < npc.Center.X ? -1 : 1;
                npc.directionY = current.Target.Y < npc.Center.Y ? -1 : 1;
            }
            return true;
        }
        private static void TargetClosest(ClosestOriginal original, NPC npc, bool face)
        {
            original(npc, face); ForceTarget(npc, face);
        }
        private static void TargetUpgraded(UpgradedOriginal original, NPC npc, bool face, Vector2? position)
        {
            original(npc, face, position); ForceTarget(npc, face);
        }
        private static NPCAimedTarget TargetData(DataOriginal original, NPC npc, bool ignoreTank)
        {
            NPCAimedTarget result = original(npc, ignoreTank);
            return ForceTarget(npc, false) ? new NPCAimedTarget(Main.player[npc.target]) : result;
        }
    }
}
