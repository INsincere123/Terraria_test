# ColoredDamageTypes 真实伤害颜色兼容

## 问题与修复

本机 ColoredDamageTypes 1.8.5 在 `CheckDamageColor` 中直接用 `DamageClass.ToString()` 索引运行中的 `zCrossModConfig.CrossModDamageConfig`。本次 client.log 已记录自动发现 `TestMod/TrueDamageClass`，但用户旧配置只有 Calamity 伤害类型，命中时缺少 `TestMod.Content.Items.DamageTypes.TrueDamageClass` 键。

核对已安装 DLL 后确认，公开 `AddDamageType` 调用只登记 `CrossModDamageConfig_Orig` 默认表；不能单独保证当前配置也有该条目。进入世界时的配置重载、配置界面重置或替换实例/字典，都可能再次缺项。

`Common/Compatibility/ColoredDamageTypesCompatSystem.cs` 在内容加载完成后通过公开接口登记真实伤害默认颜色；用可选反射补当前配置的缺项。颜色沿用 TestMod 真实伤害的白色回退。输入轮询后检查当前实例和字典，进入世界时也检查；只补缺键，不改已有自定义颜色、其他伤害类型或用户 JSON 文件。卸载时清理反射元数据；对方缺失或专用服务器跳过。

`build.txt` 将 ColoredDamageTypes 加入 `weakReferences`，保持可选依赖并明确加载顺序。不添加程序集引用，不改变伤害类型或计算规则。

公开接口参考：[ColoredDamageTypes 跨模组注册说明](https://github.com/PvtFudgepants/Tmodloader-ColoredDamageTypes#cross-mod-compatibility)。私有配置字段和构造器以本机 1.8.5 DLL 为核查依据；若后续版本改变结构，兼容层记录一次警告并停止补项。

## 已执行的检查（2026-10-05）

- 按当前 targets 导入链执行 `ResolveReferences;CoreCompile`，通过；只有原有魔法数字警告。`git diff --check` 通过。
- 隔离 C# 进程直接链接兼容层、伤害类和样式生产源码，使用已安装 tModLoader DLL 和从本机 `.tmod` 读取的 ColoredDamageTypes 1.8.5 DLL，通过 14 项断言。
- 实际对方 DLL 重现未修复时的 `KeyNotFoundException`；修复后普通/暴击查询成功。另验证默认表登记、自定义颜色保留、进入世界修补、字典替换、其他条目保留、配置实例替换、重复登记、卸载、未加载对方及专用服务器跳过。
- 测试只初始化隔离进程的存档路径和必要内容/模组注册状态，没有游戏会话，不写用户配置。它不能证明实际游戏加载顺序、文字绘制、第三方钩子或双客户端运行表现。

## 待游戏内验证

1. Build + Reload 后，同时启用 ColoredDamageTypes 与 TestMod，用血饲匕首命中敌人，确认日志不再出现缺少真实伤害颜色键的异常。
2. 带原有 ColoredDamageTypes 配置退出再进入世界，并重置/保存其跨模组颜色配置；再次测试普通命中与暴击。已有自定义颜色应保留。
3. 分别启用/停用可选文本模组，确认战斗数字无缺失或重复；本次不改变已有文字接管和 owner 边界。
4. 两客户端确认各自旧配置可正常命中；专用服务器和未启用 ColoredDamageTypes 时正常加载 TestMod。
