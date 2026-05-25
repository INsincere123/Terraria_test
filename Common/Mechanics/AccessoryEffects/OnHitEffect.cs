using Terraria;
using Terraria.ModLoader;

namespace TestMod.Common.Mechanics.AccessoryEffects
{
    /// <summary>
    /// 追加攻击效果的抽象基类。
    /// 继承此类并实现 <see cref="Trigger"/> 方法，然后在装备的 UpdateEquip 中调用
    /// <see cref="OnHitEffectsPlayer.Activate{T}"/> 即可注册追加攻击。
    /// </summary>
    public abstract class OnHitEffect
    {
        // ======================================================
        //  调参区
        // ======================================================

        /// <summary>
        /// 全局冷却帧数。仅对穿透弹幕生效，0 表示不限制。
        /// 子类在构造函数中赋值。
        /// </summary>
        public readonly int GlobalCooldown;

        // ======================================================
        //  运行时状态（由 OnHitEffectsPlayer 管理）
        // ======================================================

        /// <summary>本帧是否被装备激活。由 OnHitEffectsPlayer.ResetEffects / Activate 维护。</summary>
        public bool Active;

        /// <summary>当前全局冷却剩余帧数。由 OnHitEffectsPlayer.PostUpdateEquips 每帧递减。</summary>
        public int GlobalCooldownTimer;

        // ======================================================
        //  构造
        // ======================================================

        /// <param name="globalCooldown">全局冷却帧数，仅对穿透弹幕生效，0 = 不限制。</param>
        protected OnHitEffect(int globalCooldown = 0)
        {
            GlobalCooldown = globalCooldown;
        }

        // ======================================================
        //  子类实现
        // ======================================================

        /// <summary>
        /// 追加攻击触发时调用。在此生成炮台/弹幕或执行其他逻辑。
        /// </summary>
        /// <param name="player">触发追加攻击的玩家。</param>
        /// <param name="target">被命中的敌人。</param>
        /// <param name="hit">命中信息。</param>
        /// <param name="damageDone">实际造成的伤害。</param>
        /// <param name="sourceProjectile">
        /// 触发命中的弹幕，如果是近战武器直接命中则为 null。
        /// </param>
        public abstract void Trigger(Player player, NPC target, NPC.HitInfo hit, int damageDone, Projectile sourceProjectile);

        /// <summary>
        /// 此 Effect 自身发出的弹幕类型列表。
        /// 这些弹幕命中敌人时不会再次触发追加攻击，防止循环触发。
        /// 子类按需 override，默认返回空数组。
        /// </summary>
        public virtual int[] ExcludedProjectileTypes => System.Array.Empty<int>();
    }
}
