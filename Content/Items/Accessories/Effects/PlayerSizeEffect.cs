using Microsoft.Xna.Framework;
using System;
using Terraria;
using Terraria.DataStructures;
using Terraria.ModLoader;
using TestMod.Common.Utilities;

namespace TestMod.Content.Items.Accessories.Effects
{
    // ============================================================================
    //  PlayerSizeEffect  ——  玩家体型缩放（碰撞箱 + 视觉同步放大）
    // ----------------------------------------------------------------------------
    //  使用示例（固定倍率）:
    //    PlayerSizeEffect.Apply(player, new PlayerSizeConfig {
    //        Mode       = PlayerSizeMode.Fixed,
    //        FixedScale = 1.5f,   // 体型放大 50%
    //    });
    //
    //  使用示例（随防御力，先慢后快）:
    //    PlayerSizeEffect.Apply(player, new PlayerSizeConfig {
    //        Mode         = PlayerSizeMode.ByDefense,
    //        MinScale     = 1f,
    //        MaxScale     = 2f,
    //        MaxStatValue = 200,   // 200 防御时达到最大体型
    //        Exponent     = 2f,    // 平方曲线，先慢后快
    //    });
    //
    //  使用示例（随最大生命，线性）:
    //    PlayerSizeEffect.Apply(player, new PlayerSizeConfig {
    //        Mode         = PlayerSizeMode.ByHealth,
    //        MinScale     = 1f,
    //        MaxScale     = 2f,
    //        MaxStatValue = 2000,  // 2000 生命时达到最大体型
    //    });
    //
    //  注意:
    //   - 骑坐骑时自动跳过（避免与坐骑碰撞箱冲突）
    //   - 碰撞箱以脚底为锚点扩展，向上和两侧生长，不下沉
    //   - 视觉缩放与碰撞箱使用同一个 scale 值，保证对齐
    // ============================================================================

    public enum PlayerSizeMode
    {
        Fixed,      // 固定倍率
        ByDefense,  // 随防御力变化，先慢后快（PowerCurve）
        ByHealth    // 随最大生命变化，线性
    }

    public struct PlayerSizeConfig
    {
        // Fixed 模式
        public PlayerSizeMode Mode;
        public float FixedScale;    // Mode == Fixed 时直接使用

        // 动态模式公共参数
        public float MinScale;      // 最小倍率（通常 1.0 = 原始大小）
        public float MaxScale;      // 最大倍率（2.0 = 放大 100%）
        public int   MaxStatValue;  // ByDefense: 达到 MaxScale 所需防御值
                                    // ByHealth:  达到 MaxScale 所需最大生命

        // ByDefense 专用
        public float Exponent;      // 曲线指数（2 = 先慢后快，值越大前段越平缓）
    }

    public static class PlayerSizeEffect
    {
        // 饰品在 UpdateAccessory 中调用，注册本帧的体型配置
        public static void Apply(Player player, PlayerSizeConfig config)
        {
            var mp = player.GetModPlayer<OmniEffectsPlayer>();
            mp.EnablePlayerSize  = true;
            mp.PlayerSizeConfig  = config;
        }

        // 根据配置和玩家当前状态计算 scale 倍率
        public static float ComputeScale(Player player, in PlayerSizeConfig cfg)
        {
            return cfg.Mode switch
            {
                PlayerSizeMode.Fixed     => cfg.FixedScale,
                PlayerSizeMode.ByDefense => ComputeByDefense(player, cfg),
                PlayerSizeMode.ByHealth  => ComputeByHealth(player, cfg),
                _                        => 1f
            };
        }

        // 先慢后快：PowerCurve(t, exp, range) = 1 + range * t^exp
        // 利用 (MinScale - 1) 偏移让结果落在 [MinScale, MaxScale]
        private static float ComputeByDefense(Player player, in PlayerSizeConfig cfg)
        {
            float t = Math.Clamp(player.statDefense / cfg.MaxStatValue, 0f, 1f);
            return (cfg.MinScale - 1f) + CurveUtils.PowerCurve(t, cfg.Exponent, cfg.MaxScale - cfg.MinScale);
        }

        // 线性：每 MaxStatValue 分之一的生命对应一份体型增量
        private static float ComputeByHealth(Player player, in PlayerSizeConfig cfg)
        {
            float t = Math.Clamp(player.statLifeMax2 / (float)cfg.MaxStatValue, 0f, 1f);
            return cfg.MinScale + (cfg.MaxScale - cfg.MinScale) * t;
        }

        // 在 OmniEffectsPlayer.PostUpdateMiscEffects 中调用（参考灾厄 FlamsteedRing 模式）
        //
        // 执行时机说明：
        //   PostUpdateMiscEffects 在原版 TileCollision 之后、height 重置之后运行。
        //   此时 position.Y 已是"用 height=42 站在地面的正确值"。
        //   用固定偏移 -= (newHeight - defaultHeight) 将碰撞箱向上扩展，脚底位置不变。
        //   每帧重新赋值，保证 height 重置不会导致振荡。
        //
        // 若未装备（EnablePlayerSize == false），在 PostUpdate 中还原 width/height。
        public static void UpdateHitbox(Player player, OmniEffectsPlayer mp)
        {
            if (player.mount.Active)
                return;

            if (!mp.EnablePlayerSize)
                return;

            float scale = ComputeScale(player, mp.PlayerSizeConfig);
            mp.PlayerSizeCurrentScale = scale;

            int newWidth  = (int)(Player.defaultWidth  * scale);
            int newHeight = (int)(Player.defaultHeight * scale);

            // 灾厄模式：只调整 position.Y（height 每帧被原版重置，必须每帧补偿）
            // width 不会被原版每帧重置，直接赋值即可，不能每帧累加 position.X 偏移
            player.position.Y -= newHeight - Player.defaultHeight;
            player.width       = newWidth;
            player.height      = newHeight;
        }

        // 在 OmniEffectsPlayer.PostUpdate 中调用
        // 卸下装备的帧里 EnablePlayerSize=false，此时主动还原尺寸，防止 width 残留
        public static void RestoreHitbox(Player player, OmniEffectsPlayer mp)
        {
            if (mp.EnablePlayerSize || player.mount.Active)
                return;

            if (player.width != Player.defaultWidth || player.height != Player.defaultHeight)
            {
                // width 不调整 position.X（对应 UpdateHitbox 里也不调整）
                // height 归位时反向补偿，防止玩家瞬间下沉
                player.position.Y += player.height - Player.defaultHeight;
                player.width       = Player.defaultWidth;
                player.height      = Player.defaultHeight;
            }
        }

        // 在 OmniEffectsPlayer.TransformDrawData 中调用
        // X 锚点用默认宽度中心（避免受 player.width 修改影响偏移）
        // Y 锚点用当前碰撞箱底部（tile collision 已维持地面接触，即脚底位置）
        public static void ApplyDrawScale(ref PlayerDrawSet drawInfo, float scale)
        {
            if (MathF.Abs(scale - 1f) < 0.001f) return;

            Player p = drawInfo.drawPlayer;
            Vector2 pivot = new Vector2(
                p.position.X + Player.defaultWidth * 0.5f,  // 原始水平中心，不受 width 扩展影响
                p.position.Y + p.height                      // 当前碰撞箱底部 = 地面接触点
            ) - Main.screenPosition;

            for (int i = 0; i < drawInfo.DrawDataCache.Count; i++)
            {
                DrawData d = drawInfo.DrawDataCache[i];
                d.position = pivot + (d.position - pivot) * scale;
                d.scale   *= scale;
                drawInfo.DrawDataCache[i] = d;
            }
        }
    }
}
