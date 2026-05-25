using System;
using System.Collections.Generic;

namespace TestMod.Common.Graphics.DynamicText.Fonts
{
    public sealed class DynamicTextFontSpec
    {
        public static readonly DynamicTextFontSpec Default = new(0f, []);

        public float Size { get; }
        public IReadOnlyList<DynamicTextFontCandidate> Candidates { get; }
        public bool HasCandidates => Candidates.Count > 0;

        private DynamicTextFontSpec(float size, IReadOnlyList<DynamicTextFontCandidate> candidates)
        {
            Size = size;
            Candidates = candidates;
        }

        public static DynamicTextFontSpec Create(float size, params DynamicTextFontCandidate[] candidates)
        {
            if (candidates is null || candidates.Length == 0)
                return Default;

            return new DynamicTextFontSpec(MathF.Max(1f, size), candidates);
        }
    }
}
