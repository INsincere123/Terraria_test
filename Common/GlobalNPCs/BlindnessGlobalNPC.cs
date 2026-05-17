using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ModLoader;
using TestMod.Buffs;

namespace TestMod.Common.GlobalNPCs
{
    public class BlindnessGlobalNPC : GlobalNPC
    {
        // ── 供 BlindnessGlobalProjectile 读取 ─────────────────────────────
        internal static bool    AnyBlindBossActive { get; private set; }
        internal static Vector2 BlindBossCenter    { get; private set; }
        internal static Vector2 CurrentFakeOffset  { get; private set; }

        // ── 帧级重置 ──────────────────────────────────────────────────────
        private static ulong _flagFrame = ulong.MaxValue;

        // ── 顺序复用的坐标暂存字典 ────────────────────────────────────────
        private static readonly Dictionary<int, Vector2> _blindPositions = new();

        // ── 每个 Boss 独立的漂移状态（键 = npc.whoAmI）────────────────────
        private static readonly Dictionary<int, (Vector2 offset, Vector2 velocity)> _fakeStates = new();

        private const float DriftSpeed  = 4f;    // 漂移速度（像素/帧）
        private const float MaxRadius   = 60f;   // 假坐标最大偏移半径
        private const float TurnRate    = 0.25f; // 每帧最大转向弧度

        // ══════════════════════════════════════════════════════════════════
        public override bool PreAI(NPC npc)
        {
            if (Main.GameUpdateCount != _flagFrame)
            {
                _flagFrame         = Main.GameUpdateCount;
                AnyBlindBossActive = false;
            }

            if (!npc.boss || !npc.HasBuff(ModContent.BuffType<BlindnessDebuff>()))
                return true;

            AnyBlindBossActive = true;
            BlindBossCenter    = npc.Center;
            CurrentFakeOffset  = ComputeSmoothOffset(npc.whoAmI);

            SpoofPlayerPositions(npc.Center, npc.Center + CurrentFakeOffset, _blindPositions);
            return true;
        }

        public override void PostAI(NPC npc)
        {
            RestorePlayerPositions(_blindPositions);
        }

        // ── 平滑漂移：每帧对 velocity 小幅随机转向，维持恒定速度 ───────────
        private static Vector2 ComputeSmoothOffset(int whoAmI)
        {
            if (!_fakeStates.TryGetValue(whoAmI, out var state))
                state = (Vector2.Zero, Main.rand.NextVector2Unit() * DriftSpeed);

            state.velocity = state.velocity
                .RotatedBy(Main.rand.NextFloat(-TurnRate, TurnRate))
                .SafeNormalize(Vector2.UnitX) * DriftSpeed;

            state.offset += state.velocity;

            if (state.offset.LengthSquared() > MaxRadius * MaxRadius)
                state.offset = state.offset.SafeNormalize(Vector2.UnitX) * MaxRadius;

            _fakeStates[whoAmI] = state;
            return state.offset;
        }

        // ── 静态辅助：供 BlindnessGlobalProjectile 复用 ───────────────────

        /// <summary>
        /// 记录玩家真实坐标，将其替换为 fakeTarget。
        /// realAnchor 仅用于距离过滤（超过 1600px 的玩家不欺骗，避免 Boss 脱战）。
        /// </summary>
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
                storage[i]    = player.Center;
                player.Center = fakeTarget;
            }
        }

        /// <summary>将 storage 中记录的真实坐标还原，并清空字典。</summary>
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
