using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;
using TestMod.Common.DataStructures;
using TestMod.Common.Systems;

namespace TestMod.Common.Players
{
    public sealed partial class TimeEchoPlayer
    {
        private Player visualPlayer;
        private bool isVisualProxy;
        private float proxyOpacity;
        private Vector2 visualPosition, interpolationStart, interpolationTarget;
        private int interpolationTick, visibleTicks;
        private bool visualActive;
        private readonly Vector2[] trail = new Vector2[2];
        internal Vector2 VisualPosition => visualPosition;
        internal float VisualOpacity => Math.Min(visibleTicks / 15f, 1f);

        private void ClearVisuals()
        {
            visualActive = false;
            visibleTicks = interpolationTick = 0;
            Array.Clear(trail);
        }

        private void ReleaseVisuals()
        {
            ClearVisuals();
            visualPlayer = null;
        }

        private void ReceiveVisualTarget(TimeEchoSnapshot snapshot, bool previouslyVisible)
        {
            interpolationStart = visualPosition;
            interpolationTarget = snapshot.Position;
            interpolationTick = 0;
            // 远距离跳变不插值；正常六 tick 采样只平滑表现，不更改服务器技能目标。
            if (!previouslyVisible || !visualActive || Vector2.DistanceSquared(visualPosition, snapshot.Position) > 256f * 256f)
            {
                visualPosition = interpolationStart = interpolationTarget;
                Array.Fill(trail, visualPosition);
            }
        }

        private void UpdateVisuals()
        {
            if (Main.dedServ) return;
            if (!HasPhantom)
            {
                ClearVisuals();
                return;
            }
            Vector2 position = Main.netMode == NetmodeID.MultiplayerClient
                ? Vector2.Lerp(interpolationStart, interpolationTarget, Math.Min(++interpolationTick / 6f, 1f))
                : Phantom.Position;
            if (!visualActive || Vector2.DistanceSquared(position, visualPosition) > 256f * 256f)
                Array.Fill(trail, position);
            else
            {
                trail[1] = trail[0];
                trail[0] = visualPosition;
            }
            visualPosition = position;
            visualActive = true;
            visibleTicks = Math.Min(visibleTicks + 1, 15);
        }

        internal void DrawPhantom()
        {
            if (!visualActive) return;
            visualPlayer ??= new Player();
            PrepareVisualPlayer(visualPlayer, Phantom);
            TimeEchoPlayer proxy = visualPlayer.GetModPlayer<TimeEchoPlayer>();
            proxy.isVisualProxy = true;
            try
            {
                for (int i = trail.Length - 1; i >= 0; i--)
                {
                    if (Vector2.DistanceSquared(trail[i], visualPosition) < 4f) continue;
                    proxy.proxyOpacity = VisualOpacity * (i == 0 ? 0.10f : 0.055f);
                    Main.PlayerRenderer.DrawPlayer(Main.Camera, visualPlayer, trail[i], visualPlayer.fullRotation,
                        visualPlayer.fullRotationOrigin, 0.5f);
                }
                proxy.proxyOpacity = VisualOpacity * 0.40f;
                Main.PlayerRenderer.DrawPlayer(Main.Camera, visualPlayer, visualPosition, visualPlayer.fullRotation,
                    visualPlayer.fullRotationOrigin, 0.5f);
                DrawEchoWeapon();
                DrawAttackReadyGlow();
            }
            finally { proxy.isVisualProxy = false; }
        }

        private void DrawAttackReadyGlow()
        {
            if (Main.dedServ || !HasAbility || !HasPhantom || Player.dead || Player.statLife <= 0 ||
                attackState.WaitTicks != 0 || !TimeEchoAttackSystem.Ready || visualPlayer.HandPosition is not Vector2 hand)
                return;

            // 原版手部坐标包含历史朝向、反重力和身体旋转；只画柔光，不产生环境照明或残留粒子。
            Texture2D texture = ModContent.Request<Texture2D>("TestMod/Assets/Particles/SourceTextures/SoftBloom").Value;
            float pulse = 0.82f + 0.18f * MathF.Sin(Main.GlobalTimeWrappedHourly * 2.6f);
            Color glow = Color.Lerp(new Color(255, 40, 40), new Color(255, 130, 160), 0.4f);
            glow.A = 0; // 预乘 AlphaBlend 下叠加柔光，保持现有 SpriteBatch 状态。
            Vector2 position = hand - Main.screenPosition;
            Vector2 origin = texture.Size() * 0.5f;
            Main.EntitySpriteDraw(texture, position, null, glow * (0.48f * pulse * VisualOpacity),
                0f, origin, 42f / texture.Width, SpriteEffects.None);
            Main.EntitySpriteDraw(texture, position, null, glow * (0.80f * pulse * VisualOpacity),
                0f, origin, 16f / texture.Width, SpriteEffects.None);
        }

        private void PrepareVisualPlayer(Player proxy, TimeEchoSnapshot pose)
        {
            // 只复制绘制值，不调用装备/玩家更新，也不逐帧 Clone 背包。
            proxy.whoAmI = Player.whoAmI;
            proxy.active = true;
            proxy.dead = proxy.invis = proxy.immune = false;
            proxy.position = visualPosition;
            proxy.velocity = Vector2.Zero;
            proxy.width = Player.width; proxy.height = Player.height;
            proxy.direction = pose.Direction;
            proxy.gravDir = pose.Gravity;
            proxy.bodyFrame = pose.BodyFrame; proxy.legFrame = pose.LegFrame;
            proxy.headFrame = Player.headFrame; proxy.hairFrame = Player.hairFrame;
            proxy.wingFrame = pose.WingFrame;
            proxy.fullRotation = pose.Rotation; proxy.fullRotationOrigin = pose.RotationOrigin;
            proxy.skinVariant = Player.skinVariant;
            proxy.skinColor = Player.skinColor; proxy.eyeColor = Player.eyeColor;
            proxy.hair = Player.hair; proxy.hairColor = Player.hairColor;
            proxy.shirtColor = Player.shirtColor; proxy.underShirtColor = Player.underShirtColor;
            proxy.pantsColor = Player.pantsColor; proxy.shoeColor = Player.shoeColor;
            proxy.hairDye = Player.hairDye; proxy.hairDyeColor = Player.hairDyeColor;
            proxy.skinDyePacked = Player.skinDyePacked;
            proxy.head = Player.head; proxy.body = Player.body; proxy.legs = Player.legs;
            proxy.handon = Player.handon; proxy.handoff = Player.handoff;
            proxy.back = Player.back; proxy.front = Player.front;
            proxy.shoe = Player.shoe; proxy.waist = Player.waist; proxy.shield = Player.shield;
            proxy.neck = Player.neck; proxy.face = Player.face; proxy.balloon = Player.balloon;
            proxy.backpack = Player.backpack; proxy.tail = Player.tail;
            proxy.faceHead = Player.faceHead; proxy.faceFlower = Player.faceFlower;
            proxy.balloonFront = Player.balloonFront; proxy.beard = Player.beard;
            proxy.wings = Player.wings; proxy.wearsRobe = Player.wearsRobe;
            proxy.cHead = Player.cHead; proxy.cBody = Player.cBody; proxy.cLegs = Player.cLegs;
            proxy.cHandOn = Player.cHandOn; proxy.cHandOff = Player.cHandOff;
            proxy.cBack = Player.cBack; proxy.cFront = Player.cFront;
            proxy.cShoe = Player.cShoe; proxy.cWaist = Player.cWaist; proxy.cShield = Player.cShield;
            proxy.cNeck = Player.cNeck; proxy.cFace = Player.cFace;
            proxy.cFaceHead = Player.cFaceHead; proxy.cFaceFlower = Player.cFaceFlower;
            proxy.cBalloon = Player.cBalloon; proxy.cBalloonFront = Player.cBalloonFront;
            proxy.cWings = Player.cWings; proxy.cBackpack = Player.cBackpack; proxy.cTail = Player.cTail;
            proxy.hideMisc = Player.hideMisc;
            Array.Copy(Player.armor, proxy.armor, Math.Min(Player.armor.Length, proxy.armor.Length));
            Array.Copy(Player.dye, proxy.dye, Math.Min(Player.dye.Length, proxy.dye.Length));
            Array.Copy(Player.hideVisibleAccessory, proxy.hideVisibleAccessory, proxy.hideVisibleAccessory.Length);
            // 空手、无坐骑/宠物，仅身体及可见装备；不注入任何玩家运行效果。
            proxy.selectedItem = 0;
            proxy.heldProj = -1;
            proxy.itemAnimation = proxy.itemTime = 0;
            proxy.compositeFrontArm = proxy.compositeBackArm = default;
            if (TryGetAttackPose(out TimeEchoMeleeFrame attack, out float angle))
            {
                proxy.bodyFrame = attack.BodyFrame;
                proxy.compositeFrontArm = attack.FrontArm;
                proxy.compositeBackArm = attack.BackArm;
                proxy.compositeFrontArm.rotation += angle;
                proxy.compositeBackArm.rotation += angle;
            }
        }

        public override void DrawEffects(PlayerDrawSet drawInfo, ref float r, ref float g, ref float b, ref float a, ref bool fullBright)
        {
            if (isVisualProxy) fullBright = true;
        }

        public override void TransformDrawData(ref PlayerDrawSet drawInfo)
        {
            if (!isVisualProxy) return;
            Color tint = Color.Lerp(TimeEchoSystem.EchoBlue, TimeEchoSystem.EchoPurple, 0.4f);
            for (int i = 0; i < drawInfo.DrawDataCache.Count; i++)
            {
                DrawData data = drawInfo.DrawDataCache[i];
                // shadow 已抑制原版附属特效；用统一透明度覆盖身体色，染色 shader 仍然保留。
                data.color = tint * proxyOpacity;
                drawInfo.DrawDataCache[i] = data;
            }
        }
    }
}
