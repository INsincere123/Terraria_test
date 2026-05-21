using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Graphics;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace TestMod.Common.DynamicText.Fonts
{
    public sealed class DynamicTextFontSystem : ModSystem
    {
        private static readonly Dictionary<string, SystemDynamicTextFont> ResolvedFonts = [];
        private static readonly HashSet<string> FailedKeys = [];
        private static readonly List<string> PendingMissingFontReports = [];
        private static object installedFonts;
        private static int missingFontReportTimer = -1;

        public static DynamicTextFont Resolve(DynamicTextFontSpec spec, DynamicSpriteFont fallbackFont)
        {
            if (fallbackFont is null)
                return default;

            if (Main.netMode == NetmodeID.Server || !OperatingSystem.IsWindows() || spec is null || !spec.HasCandidates)
                return new DynamicTextFont(fallbackFont);

            foreach (DynamicTextFontCandidate candidate in spec.Candidates)
            {
                if (TryResolveSystemFont(candidate, spec.Size, out SystemDynamicTextFont systemFont))
                    return new DynamicTextFont(fallbackFont, systemFont);
            }

            return new DynamicTextFont(fallbackFont);
        }

        public override void OnWorldLoad()
        {
            PendingMissingFontReports.Clear();
            missingFontReportTimer = -1;

            if (Main.netMode == NetmodeID.Server)
                return;

            foreach (DynamicTextStyle style in DynamicTextStyleRegistry.RegisteredStyles)
            {
                DynamicTextFontSpec spec = style.FontSpec;
                if (spec is null || !spec.HasCandidates)
                    continue;

                if (CanResolveFontSpec(spec))
                    continue;

                PendingMissingFontReports.Add($"{style.Key}: {DescribeCandidates(spec)}");
            }

            if (PendingMissingFontReports.Count > 0)
                missingFontReportTimer = 150;   //延迟发送消息
        }

        public override void OnWorldUnload()
        {
            PendingMissingFontReports.Clear();
            missingFontReportTimer = -1;
        }

        public override void PostUpdateEverything()
        {
            if (Main.netMode == NetmodeID.Server || missingFontReportTimer < 0)
                return;

            if (missingFontReportTimer-- > 0)
                return;

            Main.NewText("Missing fonts: " + string.Join("; ", PendingMissingFontReports), 255, 190, 80);
            PendingMissingFontReports.Clear();
            missingFontReportTimer = -1;
        }

        public override void Unload()
        {
            List<Texture2D> texturesToDispose = [];
            foreach (SystemDynamicTextFont font in ResolvedFonts.Values)
                texturesToDispose.AddRange(font.DisposeAndCollectTextures());

            ResolvedFonts.Clear();
            FailedKeys.Clear();
            PendingMissingFontReports.Clear();
            missingFontReportTimer = -1;
            if (installedFonts is IDisposable disposable)
                disposable.Dispose();
            installedFonts = null;

            QueueTextureDisposal(texturesToDispose);
        }

        private static bool CanResolveFontSpec(DynamicTextFontSpec spec)
        {
            if (!OperatingSystem.IsWindows())
                return false;

            foreach (DynamicTextFontCandidate candidate in spec.Candidates)
            {
                if (TryResolveSystemFont(candidate, spec.Size, out _))
                    return true;
            }

            return false;
        }

        private static string DescribeCandidates(DynamicTextFontSpec spec)
        {
            return string.Join(" / ", spec.Candidates.Select(DescribeCandidate));
        }

        private static string DescribeCandidate(DynamicTextFontCandidate candidate)
        {
            if (candidate.Source == DynamicTextFontSource.FilePath && !string.IsNullOrWhiteSpace(candidate.FamilyName))
                return $"{candidate.Value} ({candidate.FamilyName})";

            return candidate.Value;
        }

        private static void QueueTextureDisposal(List<Texture2D> textures)
        {
            if (textures.Count <= 0)
                return;

            Main.QueueMainThreadAction(() =>
            {
                foreach (Texture2D texture in textures)
                {
                    if (texture is not null && !texture.IsDisposed)
                        texture.Dispose();
                }
            });
        }

        private static bool TryResolveSystemFont(DynamicTextFontCandidate candidate, float size, out SystemDynamicTextFont systemFont)
        {
            systemFont = null;

            if (string.IsNullOrWhiteSpace(candidate.Value))
                return false;

            string key = $"{candidate.Source}|{candidate.Value}|{candidate.FamilyName}|{size}";
            if (ResolvedFonts.TryGetValue(key, out systemFont))
                return true;

            if (FailedKeys.Contains(key))
                return false;

            try
            {
                systemFont = candidate.Source switch
                {
                    DynamicTextFontSource.InstalledFamily => CreateFromInstalledFamily(candidate, size),
                    DynamicTextFontSource.FilePath => CreateFromFile(candidate, size),
                    _ => null
                };

                if (systemFont is not null)
                {
                    ResolvedFonts[key] = systemFont;
                    return true;
                }
            }
            catch (Exception exception)
            {
                ModContent.GetInstance<global::TestMod.TestMod>().Logger.Debug($"Failed to resolve dynamic text font '{candidate.Value}': {exception.Message}");
            }

            FailedKeys.Add(key);
            return false;
        }

        private static SystemDynamicTextFont CreateFromInstalledFamily(DynamicTextFontCandidate candidate, float size)
        {
            installedFonts ??= SystemDrawingTextInterop.CreateInstalledFontCollection();
            object family = SystemDrawingTextInterop.GetFamilies(installedFonts)
                .Cast<object>()
                .FirstOrDefault(f => string.Equals(SystemDrawingTextInterop.GetFamilyName(f), candidate.Value, StringComparison.OrdinalIgnoreCase));

            if (family is null)
                return null;

            return new SystemDynamicTextFont(family, size, null, $"installed:{SystemDrawingTextInterop.GetFamilyName(family)}");
        }

        private static SystemDynamicTextFont CreateFromFile(DynamicTextFontCandidate candidate, float size)
        {
            string path = Environment.ExpandEnvironmentVariables(candidate.Value);
            if (!Path.IsPathFullyQualified(path) || !File.Exists(path))
                return null;

            object collection = SystemDrawingTextInterop.CreatePrivateFontCollection();
            SystemDrawingTextInterop.AddFontFile(collection, path);

            object family = null;
            if (!string.IsNullOrWhiteSpace(candidate.FamilyName))
            {
                family = SystemDrawingTextInterop.GetFamilies(collection)
                    .Cast<object>()
                    .FirstOrDefault(f => string.Equals(SystemDrawingTextInterop.GetFamilyName(f), candidate.FamilyName, StringComparison.OrdinalIgnoreCase));
            }

            family ??= SystemDrawingTextInterop.GetFamilies(collection).Cast<object>().FirstOrDefault();
            if (family is null)
            {
                if (collection is IDisposable disposable)
                    disposable.Dispose();
                return null;
            }

            return new SystemDynamicTextFont(family, size, collection, $"file:{path}");
        }
    }
}
