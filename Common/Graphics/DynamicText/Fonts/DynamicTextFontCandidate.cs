namespace TestMod.Common.Graphics.DynamicText.Fonts
{
    public enum DynamicTextFontSource
    {
        InstalledFamily,
        FilePath
    }

    public readonly struct DynamicTextFontCandidate
    {
        public readonly DynamicTextFontSource Source;
        public readonly string Value;
        public readonly string FamilyName;

        private DynamicTextFontCandidate(DynamicTextFontSource source, string value, string familyName)
        {
            Source = source;
            Value = value;
            FamilyName = familyName;
        }

        public static DynamicTextFontCandidate ByName(string familyName)
        {
            return new DynamicTextFontCandidate(DynamicTextFontSource.InstalledFamily, familyName, familyName);
        }

        public static DynamicTextFontCandidate ByPath(string path, string familyName = null)
        {
            return new DynamicTextFontCandidate(DynamicTextFontSource.FilePath, path, familyName);
        }
    }
}
