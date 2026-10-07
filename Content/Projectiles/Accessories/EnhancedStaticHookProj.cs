using System;
using System.IO;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;
using TestMod.Common.GlobalNPCs;
using TestMod.Common.Players;
using TestMod.Content.Items.Accessories;

namespace TestMod.Content.Projectiles.Accessories
{
    public class EnhancedStaticHookProj : ModProjectile
    {
        private uint _targetGeneration, _rootGeneration;
        private Vector2 _hitOffset, _playerOffset;
        private Vector2 _lastTargetCenter, _lastTargetVelocity, _lastRootCenter, _lastRootVelocity;
        private ulong _motionTick = ulong.MaxValue, _inputTick = ulong.MaxValue;
        private bool _hasMotionSample;
        private Vector2 _desiredPlayerCenter;

        private bool IsOwner => Projectile.owner == Main.myPlayer;
        private bool HasBossTarget => Projectile.ai[0] == 2f && Projectile.ai[1] > 0f;
        private Player Owner => Main.player[Projectile.owner];
        public override string Texture => "Terraria/Images/Projectile_" + ProjectileID.StaticHook;

        public override void SetStaticDefaults()
        {
            Main.projFrames[Type] = 6;
            // 远距离 Boss 钩点仍需进入绘制入口；真正绘制的链段在屏幕矩形内裁剪。
            ProjectileID.Sets.DrawScreenCheckFluff[Type] = 200000;
        }

        public override void SetDefaults()
        {
            Projectile.CloneDefaults(ProjectileID.StaticHook);
            // 不设置 AIType：保留自定义 GrappleRange，避免静止钩硬编码的 600 像素上限。
        }

        public override void OnSpawn(IEntitySource source) => Owner.GetModPlayer<EnhancedStaticHookPlayer>().Track(this);
        public override bool? CanDamage() => false;
        public override float GrappleRange() => EnhancedStaticHook.Reach;
        public override void NumGrappleHooks(Player player, ref int numHooks) => numHooks = 1;

        public override bool? CanUseGrapple(Player player)
        {
            // 本钩 OnSpawn 负责替换旧钩，不让原版静止钩的类型专用数量检查限制发射。
            return true;
        }

        public override void GrappleRetreatSpeed(Player player, ref float speed) => speed = EnhancedStaticHook.RetreatSpeed;

        public override void GrapplePullSpeed(Player player, ref float speed)
        {
            speed = EnhancedStaticHook.MovementSpeed;
            if (HasBossTarget)
                speed = Math.Max(speed, Vector2.Distance(_desiredPlayerCenter, player.Center));
        }

        public override bool PreAI()
        {
            if (!Owner.active || Owner.dead || Owner.stoned || Owner.webbed || Owner.frozen)
            {
                Projectile.Kill();
                return false;
            }

            AnimateAndRotate();
            if (Projectile.ai[0] != 0f)
                Projectile.extraUpdates = 0;

            if (HasBossTarget)
            {
                // 完全跳过原版：不检查钩点物块，也不按 2500 像素销毁。
                if (!CheckBossAttachment(checkMotion: true))
                {
                    if (IsOwner)
                        BeginRetreat();
                    else
                        Projectile.velocity = Vector2.Zero;
                }
                else
                {
                    Projectile.Center = Main.npc[(int)Projectile.ai[1] - 1].Center + _hitOffset;
                    Projectile.velocity = Vector2.Zero;
                    Projectile.timeLeft = 36000;
                    RegisterGrapple();
                    return false;
                }
                if (HasBossTarget)
                    return false;
            }

            if (Projectile.ai[0] == 1f)
            {
                // 远距离脱钩后仍以回收速度返回，不触发原版的远距离销毁。
                Vector2 delta = Owner.MountedCenter - Projectile.Center;
                float speed = EnhancedStaticHook.RetreatSpeed;
                ProjectileLoader.GrappleRetreatSpeed(Projectile, Owner, ref speed);
                if (delta.Length() <= speed)
                    Projectile.Kill();
                else
                    Projectile.velocity = delta.SafeNormalize(Vector2.Zero) * speed;
                return false;
            }

            if (Projectile.ai[0] == 0f && IsOwner && TryAttachBoss())
            {
                Projectile.extraUpdates = 0;
                RegisterGrapple();
                Owner.GetModPlayer<EnhancedStaticHookPlayer>().RefreshReduction();
                return false;
            }

            // 其余飞行/物块附着由原版处理，包括物块兼容钩子与平台黑名单。
            return true;
        }

        public override void PostAI()
        {
            if (Projectile.ai[0] == 2f)
            {
                Projectile.extraUpdates = 0;
                Projectile.timeLeft = 36000;
            }
            Owner.GetModPlayer<EnhancedStaticHookPlayer>().RefreshReduction();
        }

        private void AnimateAndRotate()
        {
            Projectile.rotation = (Owner.MountedCenter - Projectile.Center).ToRotation() - MathHelper.PiOver2;
            if (Projectile.numUpdates == 0 && ++Projectile.frameCounter >= 7)
            {
                Projectile.frameCounter = 0;
                Projectile.frame = (Projectile.frame + 1) % Main.projFrames[Type];
            }
        }

        private void RegisterGrapple()
        {
            for (int i = 0; i < Owner.grapCount; i++)
                if (Owner.grappling[i] == Projectile.whoAmI)
                    return;
            if (Owner.grapCount < Owner.grappling.Length)
                Owner.grappling[Owner.grapCount++] = Projectile.whoAmI;
        }

        private void RemoveGrapple()
        {
            for (int i = Owner.grapCount - 1; i >= 0; i--)
            {
                if (Owner.grappling[i] != Projectile.whoAmI)
                    continue;
                for (int j = i; j < Owner.grapCount - 1; j++)
                    Owner.grappling[j] = Owner.grappling[j + 1];
                Owner.grappling[--Owner.grapCount] = -1;
            }
        }

        private void BeginRetreat()
        {
            Projectile.ai[0] = 1f;
            Projectile.ai[1] = Projectile.ai[2] = 0f;
            Projectile.extraUpdates = 0;
            Projectile.netUpdate = true;
            _hasMotionSample = false;
            RemoveGrapple();
            Owner.GetModPlayer<EnhancedStaticHookPlayer>().RefreshReduction();
        }

        private bool TryAttachBoss()
        {
            Vector2 start = Projectile.Center, delta = Projectile.velocity;
            if (Vector2.Distance(start, Owner.MountedCenter) > EnhancedStaticHook.Reach)
                return false;
            NPC selected = null, selectedRoot = null;
            float nearest = 1f;
            for (int i = 0; i < Main.maxNPCs; i++)
            {
                NPC npc = Main.npc[i];
                NPC root = GetBossRoot(npc);
                if (!IsSelectable(npc) || !IsSelectable(root) || Generation(npc) == 0 || Generation(root) == 0)
                    continue;
                Rectangle hitbox = npc.Hitbox;
                hitbox.Inflate(Projectile.width / 2, Projectile.height / 2);
                if (!ClipSegment(start, delta, hitbox.Left, hitbox.Top, hitbox.Right, hitbox.Bottom, out float enter, out _)
                    || enter > nearest)
                    continue;
                Vector2 hitPoint = start + delta * enter;
                if (Vector2.Distance(hitPoint, Owner.MountedCenter) > EnhancedStaticHook.Reach
                    || !Collision.CanHitLine(start, 1, 1, hitPoint, 1, 1))
                    continue;
                nearest = enter;
                selected = npc;
                selectedRoot = root;
            }
            if (selected == null)
                return false;

            Projectile.ai[0] = 2f;
            Projectile.ai[1] = selected.whoAmI + 1;
            Projectile.ai[2] = selectedRoot.whoAmI + 1;
            _targetGeneration = Generation(selected);
            _rootGeneration = Generation(selectedRoot);
            _hitOffset = start + delta * nearest - selected.Center;
            _playerOffset = Owner.Center - selected.Center;
            _desiredPlayerCenter = Owner.Center;
            Projectile.Center = selected.Center + _hitOffset;
            Projectile.velocity = Vector2.Zero;
            _hasMotionSample = false;
            Projectile.netUpdate = true;
            CheckBossAttachment(checkMotion: true);
            return true;
        }

        private static uint Generation(NPC npc) => npc.GetGlobalNPC<HookTargetIdentity>().Generation;
        private static bool IsSelectable(NPC npc) => npc != null && npc.active && npc.life > 0
            && !npc.friendly && !npc.dontTakeDamage && !npc.immortal;

        private static NPC GetBossRoot(NPC npc)
        {
            if (!npc.active)
                return null;
            int root = npc.realLife;
            // 原版附属部位采用各自真实的关联方式，不能将附近的小怪当成 Boss 身体。
            switch (npc.type)
            {
                case NPCID.SkeletronHand:
                case NPCID.PrimeCannon:
                case NPCID.PrimeSaw:
                case NPCID.PrimeVice:
                case NPCID.PrimeLaser:
                    root = (int)npc.ai[1];
                    break;
                case NPCID.MoonLordHand:
                case NPCID.MoonLordHead:
                    root = (int)npc.ai[3];
                    break;
                case NPCID.GolemHead:
                case NPCID.GolemHeadFree:
                case NPCID.GolemFistLeft:
                case NPCID.GolemFistRight:
                    root = NPC.golemBoss;
                    break;
                case NPCID.PlanterasHook:
                case NPCID.PlanterasTentacle:
                    root = NPC.plantBoss;
                    break;
                case NPCID.EaterofWorldsBody:
                case NPCID.EaterofWorldsTail:
                    NPC segment = npc;
                    for (int i = 0; i < Main.maxNPCs; i++)
                    {
                        int previous = (int)segment.ai[1];
                        if (previous < 0 || previous >= Main.maxNPCs || !Main.npc[previous].active)
                            return null;
                        segment = Main.npc[previous];
                        if (segment.type == NPCID.EaterofWorldsHead)
                            return segment;
                        if (segment.type != NPCID.EaterofWorldsBody)
                            return null;
                    }
                    return null;
            }
            if (root >= 0 && root < Main.maxNPCs && Main.npc[root].active && Main.npc[root].boss)
                return Main.npc[root];
            return npc.boss ? npc : null;
        }

        private bool CheckBossAttachment(bool checkMotion = false)
        {
            int targetIndex = (int)Projectile.ai[1] - 1, rootIndex = (int)Projectile.ai[2] - 1;
            if (!HasBossTarget || targetIndex < 0 || targetIndex >= Main.maxNPCs || rootIndex < 0 || rootIndex >= Main.maxNPCs)
                return false;
            NPC target = Main.npc[targetIndex], root = Main.npc[rootIndex];
            if (!IsSelectable(target) || !IsSelectable(root) || Generation(target) != _targetGeneration
                || Generation(root) != _rootGeneration || GetBossRoot(target) != root)
                return false;
            // Main 先更新玩家再更新 NPC；位移采样仅在 NPC 更新后的弹幕 PreAI 中进行。
            // 玩家减伤/移动阶段只核查身份和可选中状态，不能提前消耗本 tick 的位移检查。
            if (!checkMotion || _motionTick == Main.GameUpdateCount)
                return true;

            // 拥有者每游戏 tick 判定一次；远端网络位置修正不独立触发脱钩。
            bool teleported = IsOwner && _hasMotionSample
                && (HasTeleported(target.Center - _lastTargetCenter, _lastTargetVelocity, target.velocity)
                    || HasTeleported(root.Center - _lastRootCenter, _lastRootVelocity, root.velocity));
            _motionTick = Main.GameUpdateCount;
            _lastTargetCenter = target.Center;
            _lastTargetVelocity = target.velocity;
            _lastRootCenter = root.Center;
            _lastRootVelocity = root.velocity;
            _hasMotionSample = true;
            if (teleported)
            {
                BeginRetreat();
                return false;
            }
            return true;
        }

        private static bool HasTeleported(Vector2 displacement, Vector2 previousVelocity, Vector2 currentVelocity)
            => Math.Min((displacement - previousVelocity).Length(), (displacement - currentVelocity).Length()) > 64f;

        internal float GetAttachedReduction()
        {
            if (Projectile.ai[0] != 2f || Owner.dead || Owner.stoned || Owner.webbed || Owner.frozen)
                return 0f;
            if (HasBossTarget)
                return CheckBossAttachment() ? 0.2f : 0f;
            Point tilePoint = Projectile.Center.ToTileCoordinates();
            if (!WorldGen.InWorld(tilePoint.X, tilePoint.Y, 1))
                return 0f;
            Tile tile = Main.tile[tilePoint.X, tilePoint.Y];
            bool latch = tile.HasUnactuatedTile && (Main.tileSolid[tile.TileType] || tile.TileType == TileID.MinecartTrack);
            return (ProjectileLoader.GrappleCanLatchOnTo(Projectile, Owner, tilePoint.X, tilePoint.Y) ?? latch) ? 0.1f : 0f;
        }

        public override void GrappleTargetPoint(Player player, ref float grappleX, ref float grappleY)
        {
            // Player 已将调用前的弹幕中心累加到参数中；钩点更新后仍须减掉旧贡献。
            Vector2 contributedCenter = Projectile.Center;
            Vector2 destination = player.Center;
            if (HasBossTarget && CheckBossAttachment())
            {
                NPC target = Main.npc[(int)Projectile.ai[1] - 1];
                Projectile.Center = target.Center + _hitOffset;
                if (_inputTick != Main.GameUpdateCount)
                {
                    _playerOffset += GetRadialInput(player, false);
                    _inputTick = Main.GameUpdateCount;
                }
                destination = target.Center + _playerOffset;
                // 原版 TileCollision 只扫描起点附近。高速 Boss 随动须分段检查，避免跨过远处墙体。
                destination = player.Center + SweepPlayerMovement(player, destination - player.Center);
            }
            else if (Projectile.ai[0] == 2f)
                destination += GetRadialInput(player, true);
            _desiredPlayerCenter = destination;
            // 参数是已累加的钩点总和，替换本钩的贡献，不能覆盖其他钩点。
            grappleX += destination.X - contributedCenter.X;
            grappleY += destination.Y - contributedCenter.Y;
        }

        private Vector2 GetRadialInput(Player player, bool limitLength)
        {
            Vector2 input = new Vector2((player.controlRight ? 1 : 0) - (player.controlLeft ? 1 : 0),
                ((player.controlDown ? 1 : 0) - (player.controlUp ? 1 : 0)) * player.gravDir).SafeNormalize(Vector2.Zero);
            Vector2 toHook = Projectile.Center - player.Center;
            Vector2 direction = toHook.SafeNormalize(Vector2.Zero);
            Vector2 movement = direction * Vector2.Dot(direction, input) * EnhancedStaticHook.MovementSpeed;
            if (limitLength && Vector2.Dot(movement, toHook) < 0f)
                movement = movement.SafeNormalize(Vector2.Zero) * Math.Max(0f, Math.Min(movement.Length(), EnhancedStaticHook.Reach - toHook.Length()));
            return movement;
        }

        private static Vector2 SweepPlayerMovement(Player player, Vector2 displacement)
        {
            int steps = Math.Max(1, (int)Math.Ceiling(displacement.Length() / 8f));
            Vector2 step = displacement / steps;
            Vector2 moved = Vector2.Zero;
            bool ignorePlatforms = player.gravDir == -1f || player.GoingDownWithGrapple || displacement.Y > 0f;
            for (int i = 0; i < steps; i++)
            {
                Vector2 permitted = Collision.TileCollision(player.position + moved, step, player.width, player.height,
                    player.controlDown || ignorePlatforms, ignorePlatforms, (int)player.gravDir);
                moved += permitted;
                if (Vector2.DistanceSquared(permitted, step) > 0.0001f)
                    break;
            }
            return moved;
        }

        public override void SendExtraAI(BinaryWriter writer)
        {
            writer.Write(_targetGeneration);
            writer.Write(_rootGeneration);
            writer.WriteVector2(_hitOffset);
            writer.WriteVector2(_playerOffset);
        }

        public override void ReceiveExtraAI(BinaryReader reader)
        {
            _targetGeneration = reader.ReadUInt32();
            _rootGeneration = reader.ReadUInt32();
            _hitOffset = reader.ReadVector2();
            _playerOffset = reader.ReadVector2();
            _hasMotionSample = false;
            _motionTick = _inputTick = ulong.MaxValue;
            if (Projectile.ai[0] != 2f)
                RemoveGrapple();
            Owner.GetModPlayer<EnhancedStaticHookPlayer>().RefreshReduction();
        }

        public override void OnKill(int timeLeft)
        {
            RemoveGrapple();
            Owner.GetModPlayer<EnhancedStaticHookPlayer>().Forget(this);
        }

        public override bool PreDrawExtras()
        {
            if (Main.dedServ)
                return false;
            Texture2D chain = TextureAssets.Chains[16].Value;
            Vector2 start = Projectile.Center, delta = Owner.MountedCenter - start;
            float length = delta.Length();
            Vector2 view = Main.Camera.ScaledPosition, size = Main.Camera.ScaledSize;
            float padding = chain.Height + chain.Width;
            if (length < chain.Height + 1f || !ClipSegment(start, delta, view.X - padding, view.Y - padding,
                view.X + size.X + padding, view.Y + size.Y + padding, out float enter, out float exit))
                return false;
            Vector2 direction = delta / length;
            float rotation = direction.ToRotation() - MathHelper.PiOver2;
            int first = Math.Max(1, (int)Math.Floor(enter * length / chain.Height));
            int last = Math.Min((int)Math.Floor((length - 1f) / chain.Height), (int)Math.Ceiling(exit * length / chain.Height));
            for (int i = first; i <= last; i++)
            {
                Vector2 point = start + direction * (i * chain.Height);
                Color light = Lighting.GetColor((int)(point.X / 16f), (int)(point.Y / 16f));
                // 暗枪钢色：保留原链条纹理的高光/阴影，冷灰色调避免压成均匀炭黑。
                Color color = new Color(light.R * 112 / 255, light.G * 120 / 255, light.B * 132 / 255);
                Main.EntitySpriteDraw(chain, point - Main.screenPosition, null, color, rotation,
                    chain.Size() * 0.5f, 1f, SpriteEffects.None);
            }
            return false;
        }

        // 线段与矩形裁剪同时用于扫掠碰撞及可见链段定位，不逐段遍历整条长绳。
        private static bool ClipSegment(Vector2 start, Vector2 delta, float left, float top, float right, float bottom,
            out float enter, out float exit)
        {
            enter = 0f;
            exit = 1f;
            return ClipAxis(start.X, delta.X, left, right, ref enter, ref exit)
                && ClipAxis(start.Y, delta.Y, top, bottom, ref enter, ref exit);
        }

        private static bool ClipAxis(float start, float delta, float min, float max, ref float enter, ref float exit)
        {
            if (Math.Abs(delta) < 0.0001f)
                return start >= min && start <= max;
            float a = (min - start) / delta, b = (max - start) / delta;
            if (a > b)
                (a, b) = (b, a);
            enter = Math.Max(enter, a);
            exit = Math.Min(exit, b);
            return enter <= exit;
        }
    }
}
