using Microsoft.Xna.Framework;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;
using TestMod.Common.Systems;

namespace TestMod.Common.Players
{
    // ============================================================================
    //  SuperRelocatorPlayer  ——  超级定位器核心逻辑
    // ----------------------------------------------------------------------------
    //  传送流程：
    //    1. 快捷键按下 → 计算目标坐标 → 碰撞检测 → 执行传送
    //    2. 检查即时输入：按着方向键/跳跃 → 继承传送前速度，不冻结
    //    3. 否则开始冻结（1秒），每帧锁定位置与速度
    //
    //  冻结解除条件：
    //    · 0~0.1s 内有输入 → 继承传送前速度后解除
    //    · 0.1~1s 内有输入 → 直接解除，速度保持 0
    //    · 到时自然解除，速度保持 0
    //    · 受击立即解除，速度保持 0
    //    · 再次传送重置冻结状态，从头开始
    // ============================================================================
    public class SuperRelocatorPlayer : ModPlayer
    {
        // ── 数值调节区 ──────────────────────────────────────────────────────────
        private const int FreezeDuration = 60; // 冻结时间（帧），1 秒
        private const int InheritWindow  = 9;  // 速度继承窗口（帧），约 0.15 秒
        // ────────────────────────────────────────────────────────────────────────

        public bool HasSuperRelocator; // 每帧由 UpdateInventory 置 true，ResetEffects 清零

        private int     _freezeTimer;           // 剩余冻结帧数，0 = 未冻结
        private Vector2 _frozenPosition;        // 传送落点，冻结期间每帧贴回此处
        private Vector2 _oldCenter;             // 传送前玩家中心，用于计算传送方向
        private float   _preTeleportSpeed;      // 传送前速度大小，继承时乘以传送方向

        public override void ResetEffects()
        {
            HasSuperRelocator = false;
        }

        // 受击立即打断冻结
        public override void OnHurt(Player.HurtInfo info)
        {
            _freezeTimer = 0;
        }

        public override void PostUpdate()
        {
            // 传送触发：仅本地客户端处理输入
            if (Main.myPlayer == Player.whoAmI)
                HandleTeleportInput();

            // 冻结状态机：死亡时自动跳过
            HandleFreezeState();
        }

        // ── 传送触发块 ───────────────────────────────────────────────────────────
        private void HandleTeleportInput()
        {
            if (!SuperRelocatorKeybind.Hotkey.JustPressed || !HasSuperRelocator)
                return;

            // ── 目标坐标计算（参考灾厄常态定位器）──
            Vector2 pos;
            pos.X = Main.mouseX + Main.screenPosition.X;
            if (Player.gravDir == 1f)
                pos.Y = Main.mouseY + Main.screenPosition.Y - Player.height;
            else
                pos.Y = Main.screenPosition.Y + Main.screenHeight - Main.mouseY;
            pos.X -= Player.width / 2f;

            // 世界边界检查
            if (pos.X <= 50f || pos.X >= Main.maxTilesX * 16 - 50f ||
                pos.Y <= 50f || pos.Y >= Main.maxTilesY * 16 - 50f)
                return;

            // 固体碰撞检查
            if (Collision.SolidCollision(pos, Player.width, Player.height))
                return;

            // 保存传送前速度大小与出发位置
            _preTeleportSpeed = Player.velocity.Length();
            _oldCenter        = Player.Center;

            // 执行传送（style 4 = Rod of Discord 视觉效果）
            Player.Teleport(pos, 4, 0);
            NetMessage.SendData(MessageID.TeleportEntity, -1, -1, null,
                0, Player.whoAmI, pos.X, pos.Y, 1, 0, 0);
            SoundEngine.PlaySound(SoundID.Item8, Player.Center);

            // 检查即时输入（传送时按着方向键/跳跃 → 继承速度，不冻结）
            bool immediateInput = Player.controlLeft || Player.controlRight ||
                                  Player.controlUp   || Player.controlDown  ||
                                  Player.controlJump;
            if (immediateInput)
            {
                Player.velocity = TeleportDirectionVelocity(pos);
                _freezeTimer    = 0;
            }
            else
            {
                _freezeTimer    = FreezeDuration;
                _frozenPosition = pos;
                Player.velocity = Vector2.Zero;
            }
        }

        // ── 冻结状态机 ───────────────────────────────────────────────────────────
        private void HandleFreezeState()
        {
            if (_freezeTimer <= 0 || Player.dead)
            {
                _freezeTimer = 0;
                return;
            }

            _freezeTimer--;
            int framesElapsed = FreezeDuration - _freezeTimer;

            // 仅本地客户端检测输入
            bool anyInput = Main.myPlayer == Player.whoAmI &&
                            (Player.controlLeft || Player.controlRight ||
                             Player.controlUp   || Player.controlDown  ||
                             Player.controlJump);

            if (anyInput)
            {
                // 窗口期内：沿传送方向继承传送前速度大小
                if (framesElapsed <= InheritWindow)
                    Player.velocity = TeleportDirectionVelocity(_frozenPosition);
                // 窗口期外：velocity 保持 0，玩家从新位置重新运动
                _freezeTimer = 0;
                return;
            }

            // 无输入：锁定位置与速度
            Player.velocity  = Vector2.Zero;
            Player.position  = _frozenPosition;
            // 重置摔落起点，防止解冻后误算摔落伤害
            Player.fallStart = (int)(_frozenPosition.Y / 16f);
        }
        // 传送方向单位向量 × 传送前速度大小
        // 若传送前静止（speed ≈ 0）或传送到原地，则返回零向量
        private Vector2 TeleportDirectionVelocity(Vector2 destination)
        {
            if (_preTeleportSpeed < 0.01f)
                return Vector2.Zero;

            Vector2 dir = destination - _oldCenter;
            return dir.LengthSquared() > 0f
                ? Vector2.Normalize(dir) * _preTeleportSpeed
                : Vector2.Zero;
        }
    }
}
