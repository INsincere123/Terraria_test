using System;
using System.Collections;
using System.Reflection;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ModLoader;
using TestMod.Content.Items.DamageTypes;

namespace TestMod.Common.Compatibility
{
    // ColoredDamageTypes 1.8.5 的 AddDamageType 只更新默认表，命中时却读取当前配置。
    // 旧配置及进入世界时的配置重载可能缺少新伤害类型，因此只补当前表的缺项。
    public class ColoredDamageTypesCompatSystem : ModSystem
    {
        private FieldInfo _configInstanceField;
        private FieldInfo _colorDictionaryField;
        private ConstructorInfo _colorEntryConstructor;
        private string _damageTypeKey;
        private Color _trueDamageColor;

        public override void PostSetupContent()
        {
            if (Main.dedServ || !ModLoader.TryGetMod("ColoredDamageTypes", out Mod coloredDamageTypes))
                return;

            try
            {
                _trueDamageColor = TestModTextStyles.GetFallbackColor(TestModTextStyles.DamageTrue);
                coloredDamageTypes.Call("AddDamageType", TrueDamageClass.Instance,
                    _trueDamageColor, _trueDamageColor, _trueDamageColor);

                // 可选反射仅访问颜色配置，不引用外部程序集或改变实际 DamageType。
                Type configType = coloredDamageTypes.Code.GetType("ColoredDamageTypes.zCrossModConfig", throwOnError: true);
                _configInstanceField = configType.GetField("Instance", BindingFlags.Public | BindingFlags.Static)
                    ?? throw new MissingFieldException(configType.FullName, "Instance");
                _colorDictionaryField = configType.GetField("CrossModDamageConfig", BindingFlags.Public | BindingFlags.Instance)
                    ?? throw new MissingFieldException(configType.FullName, "CrossModDamageConfig");
                Type entryType = _colorDictionaryField.FieldType.GetGenericArguments()[1];
                _colorEntryConstructor = entryType.GetConstructor(new[] { typeof(string), typeof(Color), typeof(Color), typeof(Color) })
                    ?? throw new MissingMethodException(entryType.FullName, ".ctor(string, Color, Color, Color)");
                _damageTypeKey = TrueDamageClass.Instance.ToString(); // 与对方 CheckDamageColor 的字典键一致。
                EnsureColorEntry();
            }
            catch (Exception exception)
            {
                DisableCompatibility(exception);
            }
        }

        public override void OnWorldLoad() => EnsureColorEntry();

        // 输入轮询后检查一次，覆盖进入世界时配置重载，以及配置界面替换/重置字典。
        // 反射元数据只在内容加载时解析；正常帧只读取当前实例和字典并查一个键。
        public override void PostUpdateInput() => EnsureColorEntry();

        private void EnsureColorEntry()
        {
            if (_configInstanceField == null) return;

            try
            {
                object config = _configInstanceField.GetValue(null);
                if (config == null) return;
                if (_colorDictionaryField.GetValue(config) is not IDictionary colors)
                    throw new InvalidOperationException("ColoredDamageTypes.CrossModDamageConfig is not a dictionary.");

                // 不替换已有条目，不修改其他伤害类型；不直接写入用户的 JSON 配置。
                if (!colors.Contains(_damageTypeKey))
                    colors.Add(_damageTypeKey, _colorEntryConstructor.Invoke(new object[]
                    {
                        _damageTypeKey, _trueDamageColor, _trueDamageColor, _trueDamageColor
                    }));
            }
            catch (Exception exception)
            {
                DisableCompatibility(exception);
            }
        }

        private void DisableCompatibility(Exception exception)
        {
            Unload();
            Mod.Logger.Warn($"ColoredDamageTypes compatibility could not register true damage colors: {exception}");
        }

        public override void Unload()
        {
            _configInstanceField = null;
            _colorDictionaryField = null;
            _colorEntryConstructor = null;
            _damageTypeKey = null;
            _trueDamageColor = default;
        }
    }
}
