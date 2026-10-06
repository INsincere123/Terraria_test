using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent.UI.Elements;
using Terraria.ID;
using Terraria.Localization;
using Terraria.UI;
using TestMod.Common.Systems;

namespace TestMod.Common.UI
{
    internal sealed class BatchReforgeUI : UIState
    {
        internal BatchReforgeKind Kind { get; }
        internal BatchReforgeUI(BatchReforgeKind kind) => Kind = kind;
        private UIPanel panel;
        private UIList list;
        private UIText stats;
        private UIText status;
        private UITextPanel<string> confirm;
        private PrefixRow selected;
        private int timer;
        private bool busy;
        private bool storageReady;
        private int eligible;
        private bool refinementEnabled;
        private bool fusionEnabled;

        private string Text(string key, params object[] args) =>
            Language.GetTextValue("Mods.TestMod.UI." +
                (Kind == BatchReforgeKind.Weapon ? "WeaponReforge." : "AccessoryReforge.") + key, args);

        public override void OnInitialize()
        {
            panel = new UIPanel();
            panel.Width.Set(440, 0);
            panel.Height.Set(440, 0);
            panel.HAlign = panel.VAlign = 0.5f;
            Append(panel);

            var title = new UIText(Text("Title"));
            title.Top.Set(4, 0);
            panel.Append(title);
            var hint = new UIText(Text("Hint"), 0.75f);
            hint.Top.Set(34, 0);
            panel.Append(hint);
            stats = new UIText(Text("SelectPrefix"), 0.8f);
            stats.Top.Set(59, 0);
            panel.Append(stats);

            var container = new UIPanel();
            container.Top.Set(86, 0);
            container.Width.Set(0, 1);
            container.Height.Set(250, 0);
            container.SetPadding(6);
            panel.Append(container);
            list = new UIList();
            list.Width.Set(-22, 1);
            list.Height.Set(0, 1);
            list.ListPadding = 3;
            container.Append(list);
            var scrollbar = new UIScrollbar();
            scrollbar.Left.Set(-20, 1);
            scrollbar.Height.Set(0, 1);
            scrollbar.SetView(100, 1000);
            container.Append(scrollbar);
            list.SetScrollbar(scrollbar);

            status = new UIText("", 0.75f);
            status.Top.Set(348, 0);
            panel.Append(status);
            confirm = Button("Confirm", 0, () => Execute());
            Button("Refresh", 152, () => Refresh());
            Button("Cancel", 288, () => Terraria.ModLoader.ModContent.GetInstance<BatchReforgeUISystem>().Close());
            Refresh();
        }

        private UITextPanel<string> Button(string key, float left, System.Action action)
        {
            var button = new UITextPanel<string>(Text(key), 0.85f);
            button.Left.Set(left, 0);
            button.Top.Set(377, 0);
            button.Width.Set(key == "Confirm" ? 140 : 120, 0);
            button.Height.Set(36, 0);
            button.OnLeftClick += (_, _) => action();
            panel.Append(button);
            return button;
        }

        private void Refresh()
        {
            if (busy)
                return;
            list.Clear();
            selected = null;
            eligible = 0;
            refinementEnabled = PrefixAvailabilitySystem.RefinementEnabled;
            fusionEnabled = PrefixAvailabilitySystem.FusionEnabled;
            storageReady = BatchReforgeService.TryOptions(Main.LocalPlayer, Kind, out var options);
            bool hasPreview = options.Exists(option => option.Preview != null);
            status.SetText(storageReady ? (!hasPreview ? Text("Empty") : "") : Text("StorageError"));
            foreach (var option in options)
            {
                var row = new PrefixRow(option,
                    Kind == BatchReforgeKind.Weapon ? Text("Highest") : "",
                    Kind == BatchReforgeKind.Weapon ? Text("HighestHint") : "");
                row.OnLeftClick += (_, _) =>
                {
                    if (busy)
                        return;
                    if (selected != null)
                        selected.Selected = false;
                    selected = row;
                    row.Selected = true;
                    status.SetText("");
                    UpdateCounts();
                    SoundEngine.PlaySound(SoundID.MenuTick);
                };
                list.Add(row);
            }
            UpdateCounts();
        }

        private void UpdateCounts()
        {
            eligible = 0;
            if (selected == null)
            {
                stats.SetText(Text("SelectPrefix"));
                return;
            }
            storageReady = BatchReforgeService.TryCounts(Main.LocalPlayer, Kind, selected.Option.Prefix, out var counts);
            if (!storageReady)
            {
                status.SetText(Text("StorageError"));
                return;
            }
            eligible = counts.Changed;
            stats.SetText(Text("Counts", counts.Changed, counts.Matching, counts.Skipped));
        }

        private void Execute()
        {
            if (busy || selected == null || !storageReady || eligible == 0)
                return;
            busy = true;
            try
            {
                string error = BatchReforgeService.Execute(Main.LocalPlayer, Kind, selected.Option.Prefix, out var counts);
                if (error != null)
                {
                    status.SetText(Text(error));
                    UpdateCounts();
                    return;
                }
                Main.NewText(Text("Success", counts.Changed, counts.Matching, counts.Skipped), Color.LightGreen);
                SoundEngine.PlaySound(SoundID.Item37);
                Terraria.ModLoader.ModContent.GetInstance<BatchReforgeUISystem>().Close();
            }
            finally
            {
                busy = false;
            }
        }

        public override void Update(GameTime gameTime)
        {
            if (refinementEnabled != PrefixAvailabilitySystem.RefinementEnabled ||
                fusionEnabled != PrefixAvailabilitySystem.FusionEnabled)
                Refresh();
            base.Update(gameTime);
            if (++timer >= 30)
            {
                timer = 0;
                UpdateCounts();
            }
            confirm.BackgroundColor = selected != null && storageReady && eligible > 0
                ? new Color(60, 110, 160) : new Color(55, 55, 65);
            if (panel.ContainsPoint(Main.MouseScreen))
                Main.LocalPlayer.mouseInterface = true;
        }

        private sealed class PrefixRow : UITextPanel<string>
        {
            internal readonly BatchReforgeService.Option Option;
            private readonly string highestHint;
            internal bool Selected;

            internal PrefixRow(BatchReforgeService.Option option, string highest, string highestHint)
                : base(option.Prefix == BatchReforgeService.HighestPrefix ? highest : Lang.prefix[option.Prefix].Value, 0.85f)
            {
                Option = option;
                this.highestHint = highestHint;
                Width.Set(0, 1);
                Height.Set(34, 0);
            }

            // 固定前缀顺序，选择不会让滚动列表跳动。
            public override int CompareTo(object obj) =>
                obj is PrefixRow row ? Option.Prefix.CompareTo(row.Option.Prefix) : 0;

            protected override void DrawSelf(SpriteBatch spriteBatch)
            {
                BackgroundColor = Selected ? Color.SteelBlue : new Color(45, 65, 95);
                BorderColor = Selected ? Color.LightSkyBlue : Color.Black;
                base.DrawSelf(spriteBatch);
                if (IsMouseHovering)
                {
                    if (Option.Prefix == BatchReforgeService.HighestPrefix)
                    {
                        Main.HoverItem.TurnToAir();
                        Main.hoverItemName = highestHint;
                    }
                    else
                    {
                        Main.HoverItem = Option.Preview.Clone();
                        Main.hoverItemName = Main.HoverItem.Name;
                    }
                }
            }
        }
    }
}
