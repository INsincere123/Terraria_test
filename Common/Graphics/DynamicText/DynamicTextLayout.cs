using System;
using System.Globalization;
using System.Linq;
using Microsoft.Xna.Framework;
using Terraria.UI.Chat;
using TestMod.Common.Graphics.DynamicText.Fonts;

namespace TestMod.Common.Graphics.DynamicText
{
    public sealed class DynamicTextLayout
    {
        private static readonly DynamicTextCache<LayoutKey, DynamicTextLayout> Cache = new(256, 1024 * 1024);
        private readonly record struct LayoutKey(DynamicTextFont Font, string Text, float MaxWidth);

        public readonly record struct Glyph(string Text, float Offset, float Width);
        public string Text { get; }
        public Vector2 Size { get; }
        public bool SupportsGlyphEffects { get; }
        internal bool CanCache { get; }
        internal TextSnippet[] Snippets { get; }
        internal Color[] SnippetColors { get; }
        internal bool[] DefaultColors { get; }
        private Glyph[] glyphs;

        private DynamicTextLayout(DynamicTextFont font, string text, float maxWidth)
        {
            Text = text;
            SupportsGlyphEffects = !RequiresChatLayout(text, maxWidth);
            if (!SupportsGlyphEffects)
            {
                Snippets = ChatManager.ParseMessage(text, Color.White).ToArray();
                SnippetColors = Snippets.Select(snippet => snippet.Color).ToArray();
                DefaultColors = Snippets.Select(snippet => snippet.TextOriginal == snippet.Text && snippet.GetType() == typeof(TextSnippet)).ToArray();
                Size = font.MeasureChatLayout(Snippets, maxWidth);
                ChatManager.ConvertNormalSnippets(Snippets);
            }
            else
                Size = font.MeasureString(text);
            CanCache = Snippets is null || Snippets.All(snippet => snippet.GetType() == typeof(Terraria.GameContent.UI.Chat.PlainTagHandler.PlainSnippet));
        }

        public static bool RequiresChatLayout(string text, float maxWidth = -1f) =>
            maxWidth > 0f || text.Contains('[') || text.Contains('\n') || text.Contains('\r');

        public static DynamicTextLayout Get(DynamicTextFont font, string text, float maxWidth = -1f)
        {
            LayoutKey key = new(font, text, maxWidth > 0f ? maxWidth : -1f);
            if (Cache.TryGetValue(key, out DynamicTextLayout layout))
                return layout;
            layout = new DynamicTextLayout(font, text, key.MaxWidth);
            // 自定义 snippet 可以更新内容和尺寸，不跨帧保存其可变状态。
            if (layout.CanCache)
                Cache.Add(key, layout, 128L + text.Length * 64L);
            return layout;
        }

        public ReadOnlySpan<Glyph> GetGlyphs(DynamicTextFont font)
        {
            if (!SupportsGlyphEffects)
                return ReadOnlySpan<Glyph>.Empty;
            if (glyphs is not null)
                return glyphs;

            int[] starts = StringInfo.ParseCombiningCharacters(Text);
            glyphs = new Glyph[starts.Length];
            float offset = 0f;
            for (int i = 0; i < starts.Length; i++)
            {
                int end = i + 1 < starts.Length ? starts[i + 1] : Text.Length;
                string glyph = Text[starts[i]..end];
                if (i > 0)
                    offset += font.GetGlyphKerning(glyphs[i - 1].Text, glyph);
                float width = font.MeasureString(glyph).X;
                glyphs[i] = new Glyph(glyph, offset, width);
                offset += width;
            }
            return glyphs;
        }

        internal static void ClearCache() => Cache.Clear();
    }
}
