using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ModLoader;
using TestMod.Buffs;

namespace TestMod.Common.GlobalNPCs
{
    public partial class TestGlobalNPC
    {
        internal static bool AnyBlindBossActive { get; private set; }
        internal static Vector2 BlindBossCenter { get; private set; }
        internal static Vector2 CurrentFakeOffset { get; private set; }

        private static ulong _blindFlagFrame = ulong.MaxValue;
        private static readonly Dictionary<int, Vector2> _blindPositions = new();
        private static readonly Dictionary<int, (Vector2 offset, Vector2 velocity)> _blindFakeStates = new();

        private const float BlindDriftSpeed = 4f;
        private const float BlindMaxRadius = 60f;
        private const float BlindTurnRate = 0.25f;

        private static void Blindness_PreAI(NPC npc)
        {
            if (Main.GameUpdateCount != _blindFlagFrame)
            {
                _blindFlagFrame = Main.GameUpdateCount;
                AnyBlindBossActive = false;
            }

            if (!npc.boss || !npc.HasBuff(ModContent.BuffType<BlindnessDebuff>()))
                return;

            AnyBlindBossActive = true;
            BlindBossCenter = npc.Center;
            CurrentFakeOffset = Blindness_ComputeSmoothOffset(npc.whoAmI);

            SpoofPlayerPositions(npc.Center, npc.Center + CurrentFakeOffset, _blindPositions);
        }

        private static void Blindness_PostAI(NPC npc)
        {
            RestorePlayerPositions(_blindPositions);
        }

        private static Vector2 Blindness_ComputeSmoothOffset(int whoAmI)
        {
            if (!_blindFakeStates.TryGetValue(whoAmI, out var state))
                state = (Vector2.Zero, Main.rand.NextVector2Unit() * BlindDriftSpeed);

            state.velocity = state.velocity
                .RotatedBy(Main.rand.NextFloat(-BlindTurnRate, BlindTurnRate))
                .SafeNormalize(Vector2.UnitX) * BlindDriftSpeed;

            state.offset += state.velocity;

            if (state.offset.LengthSquared() > BlindMaxRadius * BlindMaxRadius)
                state.offset = state.offset.SafeNormalize(Vector2.UnitX) * BlindMaxRadius;

            _blindFakeStates[whoAmI] = state;
            return state.offset;
        }

        internal static void SpoofPlayerPositions(Vector2 realAnchor, Vector2 fakeTarget, Dictionary<int, Vector2> storage)
        {
            storage.Clear();
            for (int i = 0; i < Main.maxPlayers; i++)
            {
                Player player = Main.player[i];
                if (!player.active || player.dead)
                    continue;
                if (Vector2.DistanceSquared(player.Center, realAnchor) > 1600f * 1600f)
                    continue;

                storage[i] = player.Center;
                player.Center = fakeTarget;
            }
        }

        internal static void RestorePlayerPositions(Dictionary<int, Vector2> storage)
        {
            if (storage.Count == 0)
                return;

            foreach (var kvp in storage)
            {
                if (Main.player[kvp.Key].active)
                    Main.player[kvp.Key].Center = kvp.Value;
            }

            storage.Clear();
        }
    }
}
