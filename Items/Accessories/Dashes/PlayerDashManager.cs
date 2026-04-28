using System;
using System.Collections.Generic;
using Terraria.ModLoader;
using Terraria.ModLoader.Core;

namespace 武器test.Items.Accessories.Dashes
{
	// ============================================================================
	//  PlayerDashManager  ——  Dash 注册管理器 (灾厄同款架构)
	// ----------------------------------------------------------------------------
	//  Mod 加载时通过反射扫描所有 PlayerDashEffect 子类, 按 ID 注册到字典中。
	//  其他代码 (DashPlayer / DashTrigger) 用 FindById 查找 dash 效果。
	// ============================================================================

	public class PlayerDashManager : ModSystem
	{
		internal static Dictionary<string, PlayerDashEffect> Registry = new();

		public override void Load()
		{
			Registry = new Dictionary<string, PlayerDashEffect>();
			Type baseType = typeof(PlayerDashEffect);
			Type[] types = AssemblyManager.GetLoadableTypes(Mod.Code);

			foreach (Type type in types)
			{
				// 跳过非子类和抽象类
				if (!type.IsSubclassOf(baseType) || type.IsAbstract) continue;

				// 创建实例
				PlayerDashEffect effect = (PlayerDashEffect)Activator.CreateInstance(type);
				if (effect == null) continue;

				// 检查 ID 不重复且非空
				string id = effect.Id;
				if (string.IsNullOrEmpty(id)) continue;
				if (Registry.ContainsKey(id)) continue;

				Registry[id] = effect;
			}
		}

		public override void Unload()
		{
			Registry = null;
		}

		/// <summary>按 ID 查找一个 dash 效果, 没找到返回 null</summary>
		public static PlayerDashEffect FindById(string id)
		{
			if (Registry == null || string.IsNullOrEmpty(id)) return null;
			return Registry.TryGetValue(id, out var effect) ? effect : null;
		}
	}
}
