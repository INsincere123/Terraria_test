using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Terraria;
using Terraria.ModLoader;

namespace TestMod.Common.Players
{
    /// <summary>
    /// 精确飞行系统：方向键直接控制移动，瞬间加速/减速，可减速键。
    /// 使用方法：
    ///   - 坐骑/直接强制启用（无需开关键）：
    ///       player.GetModPlayer&lt;PreciseFlightPlayer&gt;().forceEnable = true;
    ///   - 饰品/套装/装备等可开关启用（受开关键控制）：
    ///       player.GetModPlayer&lt;PreciseFlightPlayer&gt;().allowToggle = true;
    /// 两个字段都需要每帧设置（与 player.wingsLogic 等字段同样的用法）。
    /// </summary>
    public class PreciseFlightPlayer : ModPlayer
    {
        // ====================================================================
        // 可调参数
        // ====================================================================

        /// <summary>水平最大速度（像素/帧）。原版灾厄是 12f。</summary>
        public const float MaxSpeedX = 12f;

        /// <summary>垂直最大速度（像素/帧）。</summary>
        public const float MaxSpeedY = 12f;

        /// <summary>减速键按下时的速度倍率（0~1）。原版灾厄是 0.5f。</summary>
        public const float SlowdownMultiplier = 0.5f;

        /// <summary>是否允许穿透液体（无水阻力）。</summary>
        public const bool IgnoreLiquids = true;

        /// <summary>是否允许穿透蜘蛛网。</summary>
        public const bool IgnoreCobwebs = true;

        /// <summary>启用/禁用时是否在玩家身上播放尘埃特效。</summary>
        public const bool SpawnToggleDust = true;

        /// <summary>切换状态时播放的尘埃ID（181 = 红色torch dust，可改）。</summary>
        public const int ToggleDustID = 182;

        // ====================================================================
        // 公开状态字段
        // ====================================================================

        /// <summary>本帧由饰品/套装等调用，表示"允许通过开关键启用此功能"。</summary>
        public bool allowToggle;

        /// <summary>本帧由坐骑等调用，表示"无视开关键，强制启用"。</summary>
        public bool forceEnable;

        /// <summary>当前开关状态（玩家按开关键切换的状态）。每次进游戏默认 false。</summary>
        public bool toggleState;

        /// <summary>本帧实际是否处于精确飞行状态（合并了 forceEnable / allowToggle+toggleState）。</summary>
        public bool ActiveThisFrame { get; private set; }

        private bool _prevActive; // 上一帧是否激活，用于检测关闭瞬间

        // ====================================================================
        // 生命周期
        // ====================================================================

        public override void ResetEffects()
        {
            allowToggle = false;
            forceEnable = false;
            // toggleState 不重置，由玩家手动切换
        }

        public override void OnEnterWorld()
        {
            // 每次进游戏默认关闭
            toggleState = false;
        }

        public override void PreUpdate()
        {
            // 处理开关键（按一次切换）。仅在 allowToggle 为 true 时响应，避免裸按时触发。
            if (allowToggle && PreciseFlightKeybinds.ToggleHotkey?.JustPressed == true)
            {
                toggleState = !toggleState;
                if (SpawnToggleDust)
                    SpawnStateDust();
            }

            // 计算本帧是否激活
            ActiveThisFrame = forceEnable || (allowToggle && toggleState);

            // 关闭瞬间：填满飞行时间，避免精确飞行期间 wingTime 被清零导致关闭后无法飞行
            if (_prevActive && !ActiveThisFrame)
                Player.wingTime = Player.wingTimeMax;

            _prevActive = ActiveThisFrame;

            // 如果 allowToggle 这一帧没人设（例如饰品取下来了），但 toggleState 还是 true，
            // 等下一次玩家戴上饰品时仍然保持开启。如果想让"取下饰品自动关闭"，
            // 可以在 ResetEffects 时根据需要重置 toggleState，这里选择保留。
        }

        // ====================================================================
        // 核心移动覆写
        // PreUpdateMovement 在原版重力/翅膀/水阻力等计算之前调用，
        // 这里直接接管 velocity，并清空翅膀逻辑防冲突。
        // ====================================================================

        public override void PreUpdateMovement()
        {
            if (!ActiveThisFrame)
                return;

            // ----- 1. 禁用翅膀飞行防冲突 -----
            // wingsLogic = 0 让原版"翅膀飞行"逻辑（消耗 wingTime 等）完全跳过。
            // wingTimeMax 留着不动以便取消精确飞行后翅膀仍可用。
            Player.wingsLogic = 0;
            Player.wingTime = 0f;
            Player.rocketTime = 0;
            Player.rocketDelay = 0;
            Player.rocketDelay2 = 0;

            // ----- 2. 取消重力 -----
            // 直接置 0，但保留 gravDir 让重力翻转的玩家也能正常上下。
            Player.gravity = 0f;

            // 取消下落伤害
            Player.noFallDmg = true;
            Player.fallStart = (int)(Player.position.Y / 16f);
            Player.fallStart2 = Player.fallStart;

            // ----- 3. 计算输入方向 -----
            int dirX = (Player.controlRight ? 1 : 0) - (Player.controlLeft ? 1 : 0);
            int dirY = (Player.controlDown ? 1 : 0) - (Player.controlUp ? 1 : 0);

            // 注意：Terraria 中 Y 轴向下为正，所以 controlUp -> -Y，controlDown -> +Y。
            // 但是当玩家重力反转（gravDir == -1）时输入方向也要翻转，保持"按上=往天上飞"。
            if (Player.gravDir < 0f)
                dirY = -dirY;

            // ----- 4. 计算目标速度 -----
            float speedMult = 1f;
            if (PreciseFlightKeybinds.SlowdownHotkey?.Current == true)
                speedMult = SlowdownMultiplier;

            float targetVX = dirX * MaxSpeedX * speedMult;
            float targetVY = dirY * MaxSpeedY * speedMult;

            // 瞬间加速/减速：直接赋值（这是灾厄椅子"精确走位"的精髓）
            Player.velocity.X = targetVX;
            Player.velocity.Y = targetVY;

            // ----- 5. 穿透液体/蜘蛛网 -----
            if (IgnoreCobwebs)
                Player.webbed = false;

            if (IgnoreLiquids)
            {
                // 取消水中的速度衰减：原版会在 wet 状态下让水平速度 *= 0.5 之类。
                // 这里通过保留 wet 状态但每帧覆写 velocity 已经达到效果，
                // 不需要额外动作。但要清掉"窒息"提示性变量以防意外。
                Player.lavaWet = false; // 防止岩浆烧伤（如果不想要可改回）
                Player.honeyWet = false;
            }

            // ----- 6. 杂项视觉 -----
            // 让玩家面朝移动方向（仅在水平有输入时）
            if (dirX != 0)
                Player.ChangeDir(dirX);
        }

        // ====================================================================
        // PostUpdateRunSpeeds 调用时机比 PreUpdateMovement 更晚，
        // 在这里设置穿平台标志才不会被原版重置。
        // ====================================================================
        public override void PostUpdateRunSpeeds()
        {
            if (!ActiveThisFrame)
                return;

            // 精确飞行激活期间始终无视平台，不依赖按键方向。
            // GoingDownWithGrapple：告诉碰撞系统"正在穿过平台"，阻止平台捕获玩家。
            // stairFall：禁用台阶/平台的停落逻辑。
            Player.GoingDownWithGrapple = true;
            Player.stairFall = true;
        }

        // ====================================================================
        // 防止"惯性"残留：在飞行状态结束后，如果原版逻辑在 PostUpdate 给 velocity 加了奇怪的值，
        // 也不需要清理，因为下一帧又会被覆写。这里不需要 PostUpdate。
        // ====================================================================

        private void SpawnStateDust()
        {
            for (int i = 0; i < 12; i++)
            {
                int d = Dust.NewDust(Player.position, Player.width, Player.height, ToggleDustID,
                    0f, 0f, 100, default, 1.4f);
                Main.dust[d].noGravity = true;
                Main.dust[d].velocity *= 1.5f;
            }
        }
    }

    /// <summary>
    /// 精确飞行系统的按键。注册在自己的 ModSystem 里，不污染其他 keybind 文件。
    /// </summary>
    public class PreciseFlightKeybinds : ModSystem
    {
        public static ModKeybind ToggleHotkey { get; private set; }
        public static ModKeybind SlowdownHotkey { get; private set; }

        public override void Load()
        {
            // 默认按键（玩家可在 设置 → 控件 中修改）
            ToggleHotkey = KeybindLoader.RegisterKeybind(Mod, "PreciseFlightToggle", "G");
            SlowdownHotkey = KeybindLoader.RegisterKeybind(Mod, "PreciseFlightSlowdown", "RightShift");
        }

        public override void Unload()
        {
            ToggleHotkey = null;
            SlowdownHotkey = null;
        }
    }
}