using System;
using System.Collections.Generic;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace TestMod.Content.Items.Accessories.Effects
{
    /// <summary>
    /// 追加攻击系统的驱动层。
    /// 负责：实例注册、每帧状态重置、冷却计时、命中分发。
    ///
    /// 使用方式：
    ///   在装备的 UpdateEquip 中调用 OnHitEffectsPlayer.Activate&lt;MyEffect&gt;(player)。
    /// </summary>
    public class OnHitEffectsPlayer : ModPlayer
    {
        // ======================================================
        //  调参区
        // ======================================================

        /// <summary>
        /// per-NPC 命中冷却帧数（仅穿透弹幕生效）。
        /// 同一敌人在此帧数内不会再次触发追加攻击。
        /// </summary>
        private const int NPC_CD_FRAMES = 10;

        // ======================================================
        //  静态注册表（加载时初始化，运行时只读）
        // ======================================================

        /// <summary>
        /// key = 具体 OnHitEffect 子类的 Type，value = 唯一实例。
        /// 由 LoadRegistry() 在 Mod.Load() 时自动扫描填充，运行时不再修改结构。
        /// </summary>
        private static readonly Dictionary<Type, OnHitEffect> Registry = new();

        // ======================================================
        //  运行时状态
        // ======================================================

        /// <summary>
        /// per-NPC 命中冷却。下标 = NPC.whoAmI，值 > 0 时跳过触发。
        /// 仅穿透弹幕命中时写入。
        /// </summary>
        private int[] npcCooldowns = new int[Main.maxNPCs];

        // ======================================================
        //  静态初始化：加载时反射扫描所有 OnHitEffect 子类
        // ======================================================

        /// <summary>
        /// 由 TestMod.Load() 调用一次，扫描程序集中所有 OnHitEffect 子类并创建唯一实例。
        /// </summary>
        public static void LoadRegistry()
        {
            Registry.Clear();
            foreach (Type type in typeof(OnHitEffect).Assembly.GetTypes())
            {
                if (type.IsAbstract || !type.IsSubclassOf(typeof(OnHitEffect)))
                    continue;

                OnHitEffect instance = (OnHitEffect)Activator.CreateInstance(type);
                Registry[type] = instance;
            }
        }

        /// <summary>由 TestMod.Unload() 调用，清空注册表防止内存泄漏。</summary>
        public static void UnloadRegistry() => Registry.Clear();

        // ======================================================
        //  装备激活接口（供装备 UpdateAccessory 调用）
        // ======================================================

        /// <summary>
        /// 在装备的 UpdateAccessory 中调用此方法来激活对应的追加攻击。
        /// 每帧 ResetEffects 后 Active 自动重置为 false，需每帧重新激活。
        /// </summary>
        public static void Activate<T>(Player player) where T : OnHitEffect
        {
            if (Registry.TryGetValue(typeof(T), out OnHitEffect effect))
                effect.Active = true;
        }

        // ======================================================
        //  ModPlayer 生命周期
        // ======================================================

        public override void ResetEffects()
        {
            // 每帧重置所有 effect 的激活状态
            foreach (OnHitEffect effect in Registry.Values)
                effect.Active = false;
        }

        public override void PostUpdateEquips()
        {
            // 每帧递减各 effect 的全局冷却
            foreach (OnHitEffect effect in Registry.Values)
            {
                if (effect.GlobalCooldownTimer > 0)
                    effect.GlobalCooldownTimer--;
            }

            // 每帧递减 per-NPC 冷却
            for (int i = 0; i < npcCooldowns.Length; i++)
            {
                if (npcCooldowns[i] > 0)
                    npcCooldowns[i]--;
            }
        }

        // ======================================================
        //  命中分发
        // ======================================================

        // 由 GlobalProjectile.OnHitNPC 调用，覆盖所有伤害类型（包括 TrueDamageClass）
        // ModPlayer.OnHitNPCWithProj 对自定义 DamageClass 存在兼容性问题，改用 GlobalProjectile 路径
        public void DispatchProjectileHit(Projectile proj, NPC target, NPC.HitInfo hit, int damageDone)
        {
            if (target.friendly || target.type == NPCID.TargetDummy)
                return;

            bool isPenetrating = proj.maxPenetrate > 1 || proj.maxPenetrate == -1;
            ProcessHit(target, hit, damageDone, proj, isPenetrating);
        }

        public override void OnHitNPCWithItem(Item item, NPC target, NPC.HitInfo hit, int damageDone)
        {
            if (target.friendly || target.type == NPCID.TargetDummy)
                return;

            // 近战直接命中，非穿透
            ProcessHit(target, hit, damageDone, sourceProjectile: null, isPenetrating: false);
        }

        /// <summary>
        /// 核心分发逻辑。
        /// 先检查触发弹幕是否在任意 Effect 的排除列表中，是则整体跳过，防止循环触发。
        /// 再对所有已激活的 Effect 逐一检查两层冷却门控后触发。
        /// </summary>
        private void ProcessHit(NPC target, NPC.HitInfo hit, int damageDone, Projectile sourceProjectile, bool isPenetrating)
        {
            // 排除检查：若触发弹幕属于任意 Effect 的自身弹幕，直接跳过整次命中
            if (sourceProjectile != null)
            {
                foreach (OnHitEffect effect in Registry.Values)
                {
                    if (Array.IndexOf(effect.ExcludedProjectileTypes, sourceProjectile.type) >= 0)
                        return;
                }
            }

            foreach (OnHitEffect effect in Registry.Values)
            {
                if (!effect.Active)
                    continue;

                // 门控 1：per-NPC 冷却（仅穿透弹幕）
                if (isPenetrating && npcCooldowns[target.whoAmI] > 0)
                    continue;

                // 门控 2：全局冷却（仅穿透弹幕，且该 effect 设置了全局冷却）
                if (isPenetrating && effect.GlobalCooldown > 0 && effect.GlobalCooldownTimer > 0)
                    continue;

                // 两关都通过，触发
                effect.Trigger(Player, target, hit, damageDone, sourceProjectile);

                // 穿透弹幕才写入冷却
                if (isPenetrating)
                {
                    npcCooldowns[target.whoAmI] = NPC_CD_FRAMES;

                    if (effect.GlobalCooldown > 0)
                        effect.GlobalCooldownTimer = effect.GlobalCooldown;
                }
            }
        }
    }
}
