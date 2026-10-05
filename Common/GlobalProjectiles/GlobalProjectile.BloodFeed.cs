using System.IO;
using Terraria;
using TestMod.Common.Players;

namespace TestMod.Common.GlobalProjectiles
{
    // 暴走期间的弹幕增强：无限穿透 + 无视防御
    public partial class GlobalProjectile : Terraria.ModLoader.GlobalProjectile
    {
        private bool _bloodFeedPenetrationOverridden;
        private int _bloodFeedSavedPenetrate;
        private int _bloodFeedSavedMaxPenetrate;

        // 由主文件 PreAI 调用，确保在碰撞检测前设置穿透
        private void BloodFeed_SetBerserkPenetrate(Projectile projectile)
        {
            bool validOwner = projectile.owner >= 0 && projectile.owner < Main.maxPlayers;
            Player owner = validOwner ? Main.player[projectile.owner] : null;
            bool berserk = projectile.friendly && !projectile.hostile
                && owner != null && owner.active && !owner.dead
                && owner.GetModPlayer<BloodFeedPlayer>().IsBerserk;
            if (!berserk)
            {
                BloodFeed_RestorePenetration(projectile);
                return;
            }

            // 每实体只记录进入暴走时的剩余次数；extraUpdates 不重复覆盖快照。
            // 原本无限穿透的弹幕不接管，退出时仍然无限。
            if (!_bloodFeedPenetrationOverridden && projectile.penetrate != -1)
            {
                _bloodFeedSavedPenetrate = projectile.penetrate;
                _bloodFeedSavedMaxPenetrate = projectile.maxPenetrate;
                _bloodFeedPenetrationOverridden = true;
                if (projectile.owner == Main.myPlayer) projectile.netUpdate = true;
            }
            if (_bloodFeedPenetrationOverridden)
            {
                projectile.penetrate = projectile.maxPenetrate = -1;
            }
        }

        private void BloodFeed_RestorePenetration(Projectile projectile, bool requestSync = true)
        {
            if (!_bloodFeedPenetrationOverridden) return;

            // 仅撤销本功能写入的无限值，保留其他 AI 主动设置的有限次数。
            if (projectile.penetrate == -1) projectile.penetrate = _bloodFeedSavedPenetrate;
            if (projectile.maxPenetrate == -1) projectile.maxPenetrate = _bloodFeedSavedMaxPenetrate;
            _bloodFeedPenetrationOverridden = false;
            if (requestSync && projectile.owner == Main.myPlayer) projectile.netUpdate = true;
        }

        private void BloodFeed_WritePenetration(BinaryWriter writer)
        {
            if (!_bloodFeedPenetrationOverridden) return;
            writer.Write(_bloodFeedSavedPenetrate);
            writer.Write(_bloodFeedSavedMaxPenetrate);
        }

        private void BloodFeed_ReadPenetration(Projectile projectile, bool overridden, BinaryReader reader)
        {
            if (!overridden)
            {
                BloodFeed_RestorePenetration(projectile, requestSync: false);
                return;
            }
            _bloodFeedSavedPenetrate = reader.ReadInt32();
            _bloodFeedSavedMaxPenetrate = reader.ReadInt32();
            _bloodFeedPenetrationOverridden = true;
            projectile.penetrate = projectile.maxPenetrate = -1;
        }
    }
}
