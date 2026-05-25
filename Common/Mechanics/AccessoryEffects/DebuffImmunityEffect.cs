using Terraria;
using Terraria.ID;

namespace TestMod.Common.Mechanics.AccessoryEffects
{
    // ============================================================================
    //  DebuffImmunityEffect  ——  Debuff 免疫
    // ----------------------------------------------------------------------------
    //  提供 4 种使用方式:
    //   [1] ApplyAll(player)                        —— 全套负面 buff 免疫 (60+ 个)
    //   [2] Apply(player, params int[] buffIds)     —— 仅免疫指定 buff
    //   [3] ApplyFireGroup / ApplyPoisonGroup ...   —— 预设主题组
    //   [4] 多个预设组叠加: ApplyFireGroup + ApplyPoisonGroup ...
    //
    //  使用示例:
    //
    //    // 大而全
    //    DebuffImmunityEffect.ApplyAll(player);
    //
    //    // 自选
    //    DebuffImmunityEffect.Apply(player, BuffID.OnFire, BuffID.Poisoned, BuffID.Bleeding);
    //
    //    // 主题
    //    DebuffImmunityEffect.ApplyFireGroup(player);
    // ============================================================================

    public static class DebuffImmunityEffect
    {
        // =====================================================================
        // [1] 自选: 传入任意 BuffID 列表
        // =====================================================================
        public static void Apply(Player player, params int[] buffIds)
        {
            for (int i = 0; i < buffIds.Length; i++)
            {
                int id = buffIds[i];
                if (id > 0 && id < player.buffImmune.Length)
                    player.buffImmune[id] = true;
            }
        }

        // =====================================================================
        // [2] 预设组: 火焰类
        // =====================================================================
        public static void ApplyFireGroup(Player player)
        {
            player.buffImmune[BuffID.OnFire]        = true; // 24
            player.buffImmune[BuffID.CursedInferno] = true; // 39
            player.buffImmune[BuffID.Frostburn]     = true; // 44 (寒冰之火, 习惯归在火类)
            player.buffImmune[BuffID.Burning]       = true; // 67
            player.buffImmune[BuffID.ShadowFlame]   = true; // 153
            player.buffImmune[BuffID.Daybreak]      = true; // 189
            player.buffImmune[BuffID.OnFire3]       = true; // 323 狱炎
            player.buffImmune[BuffID.Frostburn2]    = true; // 324 冻伤
        }

        // =====================================================================
        // [3] 预设组: 毒 / 流血 / 衰弱类
        // =====================================================================
        public static void ApplyPoisonGroup(Player player)
        {
            player.buffImmune[BuffID.Poisoned]   = true; // 20
            player.buffImmune[BuffID.Bleeding]   = true; // 30
            player.buffImmune[BuffID.Weak]       = true; // 33
            player.buffImmune[BuffID.Ichor]      = true; // 69 (减防, 视为衰弱)
            player.buffImmune[BuffID.Venom]      = true; // 70
            player.buffImmune[BuffID.BloodButcherer] = true; // 344
        }

        // =====================================================================
        // [4] 预设组: 控制 / 致残类
        // =====================================================================
        public static void ApplyControlGroup(Player player)
        {
            player.buffImmune[BuffID.Darkness]    = true; // 22
            player.buffImmune[BuffID.Cursed]      = true; // 23 (无法用物品)
            player.buffImmune[BuffID.Confused]    = true; // 31
            player.buffImmune[BuffID.Slow]        = true; // 32
            player.buffImmune[BuffID.Silenced]    = true; // 35
            player.buffImmune[BuffID.TheTongue]   = true; // 38
            player.buffImmune[BuffID.Chilled]     = true; // 46
            player.buffImmune[BuffID.Frozen]      = true; // 47
            player.buffImmune[BuffID.Blackout]    = true; // 80
            player.buffImmune[BuffID.ChaosState]  = true; // 88
            player.buffImmune[BuffID.Lovestruck]  = true; // 119
            player.buffImmune[BuffID.Stinky]      = true; // 120
            player.buffImmune[BuffID.Slimed]      = true; // 137
            player.buffImmune[BuffID.Electrified] = true; // 144
            player.buffImmune[BuffID.Webbed]      = true; // 149
            player.buffImmune[BuffID.Stoned]      = true; // 156
            player.buffImmune[BuffID.Dazed]       = true; // 160
            player.buffImmune[BuffID.Obstructed]  = true; // 163
            player.buffImmune[BuffID.VortexDebuff]= true; // 164
            player.buffImmune[BuffID.WindPushed]  = true; // 194
            player.buffImmune[BuffID.NoBuilding]  = true; // 199
        }

        // =====================================================================
        // [5] 预设组: 装备 / 防御削弱
        // =====================================================================
        public static void ApplyArmorBreakGroup(Player player)
        {
            player.buffImmune[BuffID.BrokenArmor]   = true; // 36
            player.buffImmune[BuffID.WitheredArmor] = true; // 195
            player.buffImmune[BuffID.WitheredWeapon]= true; // 196
            player.buffImmune[BuffID.BetsysCurse]   = true; // 203
            player.buffImmune[BuffID.Oiled]         = true; // 204
        }

        // =====================================================================
        // [6] 预设组: 环境 / 杂项
        // =====================================================================
        public static void ApplyEnvironmentGroup(Player player)
        {
            player.buffImmune[BuffID.Horrified]     = true; // 37
            player.buffImmune[BuffID.Suffocation]   = true; // 68
            player.buffImmune[BuffID.Midas]         = true; // 72
            player.buffImmune[BuffID.ManaSickness]  = true; // 94
            player.buffImmune[BuffID.Wet]           = true; // 103
            player.buffImmune[BuffID.MoonLeech]     = true; // 145
            player.buffImmune[BuffID.Rabies]        = true; // 148
            player.buffImmune[BuffID.BoneJavelin]   = true; // 169
            player.buffImmune[BuffID.StardustMinionBleed] = true; // 183
            player.buffImmune[BuffID.DryadsWardDebuff]    = true; // 186
            player.buffImmune[BuffID.OgreSpit]      = true; // 197
            player.buffImmune[BuffID.GelBalloonBuff]= true; // 320
            player.buffImmune[BuffID.NeutralHunger] = true; // 332
            player.buffImmune[BuffID.Hunger]        = true; // 333
            player.buffImmune[BuffID.Starving]      = true; // 334
            player.buffImmune[BuffID.Shimmer]       = true; // 353
        }

        // =====================================================================
        // [7] 全套: 调用所有预设组
        // =====================================================================
        public static void ApplyAll(Player player)
        {
            ApplyFireGroup(player);
            ApplyPoisonGroup(player);
            ApplyControlGroup(player);
            ApplyArmorBreakGroup(player);
            ApplyEnvironmentGroup(player);
        }
    }
}
