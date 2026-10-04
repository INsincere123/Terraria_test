using System;
using System.Globalization;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.Localization;
using Terraria.ModLoader;
using TestMod.Common.Players;
using TestMod.Common.UI.ResourceOverlays;

namespace TestMod.Common.Systems
{
    [Autoload(Side = ModSide.Client)]
    public class ShieldBarVisualSystem : ModSystem
    {
        internal static ShieldBarVisualState State { get; } = new();
        internal static Rectangle? LifeBarBounds { get; set; }
        internal static string NumberText { get; private set; } = string.Empty;
        internal static string StageText { get; private set; } = string.Empty;
        private string languageName;

        public override void Load() => ShieldBarRenderer.Load();

        public override void UpdateUI(GameTime gameTime)
        {
            Player player = Main.LocalPlayer;
            if (Main.gameMenu || player is null || !player.active || player.dead)
            {
                Clear();
                return;
            }
            EnergyShieldPlayer shield = player.GetModPlayer<EnergyShieldPlayer>();
            ShieldBarSample sample = new(shield.MaxShield > 0f, shield.NormalCurrentShield,
                shield.TemporaryCurrentShield, shield.MaxShield, shield.NormalMaxShield,
                shield.RechargeWaitRemainingFrames, shield.IsRecharging, shield.IsRechargeFragile,
                shield.ShieldHitFlashStrength);
            ShieldBarSample previous = State.Sample;
            State.Update(sample, Main.gamePaused ? 0f : (float)gameTime.ElapsedGameTime.TotalSeconds);
            if (!sample.Visible)
            {
                NumberText = StageText = string.Empty;
                LifeBarBounds = null;
                return;
            }
            string language = Language.ActiveCulture.Name;
            bool languageChanged = languageName != language;
            languageName = language;
            if (!previous.Visible || previous.Total != sample.Total || previous.Maximum != sample.Maximum)
                NumberText = $"{Math.Ceiling(sample.Total).ToString("0", CultureInfo.InvariantCulture)} / " +
                    Math.Ceiling(sample.Maximum).ToString("0", CultureInfo.InvariantCulture);
            if (!previous.Visible || previous.Stage != sample.Stage || previous.WaitTenths != sample.WaitTenths || languageChanged)
            {
                string key = "Mods.TestMod.UI.ShieldBar." + sample.Stage;
                StageText = sample.Stage is ShieldBarStage.Ready or ShieldBarStage.Recovering
                    ? Language.GetTextValue(key)
                    : Language.GetTextValue(key, (sample.WaitTenths / 10f).ToString("0.0", CultureInfo.InvariantCulture));
            }
        }

        private static void Clear()
        {
            State.Reset();
            LifeBarBounds = null;
            NumberText = StageText = string.Empty;
        }

        public override void OnWorldUnload() => Clear();

        public override void Unload()
        {
            Clear();
            languageName = null;
            ShieldBarRenderer.Unload();
        }
    }
}
