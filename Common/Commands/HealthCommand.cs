using System;
using System.Globalization;
using Terraria.Localization;
using Terraria.ModLoader;
using TestMod.Common.Players;
using TestMod.Common.Systems;

namespace TestMod.Common.Commands
{
    public sealed class HealthCommand : ModCommand
    {
        public override string Command => "HP";
        public override CommandType Type => CommandType.Chat;
        public override string Usage => "/HP=数字 | /HP=reset";
        public override string Description => Language.GetTextValue("Mods.TestMod.HealthCommand.Description");

        internal static bool TryParseValue(string text, out int value)
        {
            value = 0;
            if (string.Equals(text, "reset", StringComparison.OrdinalIgnoreCase)) return true;
            return int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out value)
                && value > 0 && value <= BaseHealthPlayer.MaximumBaseHealth;
        }

        public override void Action(CommandCaller caller, string input, string[] args)
        {
            if (args.Length != 1 || !TryParseValue(args[0], out int value))
                throw new UsageException(Language.GetTextValue("Mods.TestMod.HealthCommand.Usage"));
            if (value > 0 && !BaseHealthSystem.HealthOverrideReady)
                throw new UsageException(Language.GetTextValue("Mods.TestMod.HealthCommand.CompatibilityUnavailable"));
            caller.Player.GetModPlayer<BaseHealthPlayer>().SetBaseHealth(value);
            caller.Reply(value == 0
                ? Language.GetTextValue("Mods.TestMod.HealthCommand.Restored")
                : Language.GetTextValue("Mods.TestMod.HealthCommand.Changed", value));
        }
    }
}
