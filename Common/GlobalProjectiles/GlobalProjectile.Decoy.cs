using System.IO;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using TestMod.Common.Systems;
using TestMod.Common.DataStructures;

namespace TestMod.Common.GlobalProjectiles
{
    public partial class GlobalProjectile
    {
        internal EnemySource DecoySource;
        internal bool DecoyExcluded;
        internal readonly DecoySelection DecoySelection = new();

        internal void Decoy_OnSpawn(Projectile projectile, IEntitySource source)
        {
            // 明确玩家/无关实体来源不能继承外层敌人的来源。
            DecoySource = DecoySystem.SourceForSpawn(source);
            DecoyExcluded = source is TimeEchoAttackSource;
            if (source is EntitySource_Parent { Entity: Projectile parent })
            {
                var data = parent.GetGlobalProjectile<GlobalProjectile>();
                DecoyExcluded |= data.DecoyExcluded || data.IsTimeEchoAttack;
                DecoySelection.Inherit(data.DecoySelection.Target);
            }
            else if (source is not EntitySource_Parent)
                DecoySelection.Inherit(DecoySystem.CurrentDecoy);
            if (DecoyExcluded) DecoySelection.Reset();
        }

        private void Decoy_Write(BinaryWriter writer)
        {
            writer.Write((short)(DecoySource.Valid ? DecoySource.Slot : -1));
            if (DecoySource.Valid)
            {
                writer.Write(DecoySource.Type);
                writer.Write(DecoySource.Generation);
            }
            writer.Write(DecoyExcluded);
            DecoySelection.Target.Write(writer);
        }

        private void Decoy_Read(BinaryReader reader)
        {
            int slot = reader.ReadInt16();
            EnemySource source = slot < 0 ? default : new(slot, reader.ReadInt32(), reader.ReadUInt32());
            bool excluded = reader.ReadBoolean();
            DecoyHandle target = DecoyHandle.Read(reader);
            if (Main.netMode == NetmodeID.MultiplayerClient)
            {
                DecoySource = source.Valid ? source : default;
                DecoyExcluded = excluded;
                DecoySelection.Inherit(target);
            }
        }
    }
}
