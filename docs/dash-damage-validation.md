# 冲刺伤害来源迁移验证

## 实现范围

- `StandardDash`、`LongDash`、`ShortDash` 的碰撞伤害分别通过 `StandardDashStrike`、`LongDashStrike`、`ShortDashStrike` 投送，统计名称为“通用冲刺伤害”“长冲刺伤害”“短冲刺伤害”。
- 原碰撞范围、伤害属性加成、固定概率暴击、击退、碰撞无敌帧、命中上限、粒子、音效和 StandardDash 配置保留。只在实际弹幕命中后登记命中目标和次数；弹幕生成失败或其他模组拒绝命中时不消耗次数。
- `DashStrikeProjectile` 在 owner 端生成后立即调用 `Projectile.Damage()`，不等待 AI 或下一帧；结束后销毁。正常弹幕流程负责命中同步，冲刺调用者不再自行发送第二份 `SendStrikeNPC`。
- `ai[0]` 指定唯一 NPC，`ai[1]` 指定伤害类型。OnSpawn 写入伤害类型和 `IsExtraHit`，使首次生成包携带来源；接收端没有 owner 的本地结算快照，`CanDamage` 返回 false，不重复扣血。
- 复用原 `CalculateHitInfo` 结果，通过 `ModifyHitInfo` 恢复伤害、来源伤害、暴击、击退和方向，避免普通弹幕修正重复改变原冲刺结算。保留普通流程的隐藏战斗文字标志，避免真实伤害重复数字。
- 使用单次穿透，不使用 local/static 免疫，也不施加单次命中的共用免疫帧。已命中标志额外防止无限穿透等效果造成重复命中。
- `IsExtraHit` 阻止本模组追加攻击、心之钢、狂战刃叠层和强制暴击次数消耗；同步调用期间的 `DashPlayer.IsResolvingContactDamage` 同时阻止本模组普通攻击吸血、处决和通用命中 debuff。StandardDash 配置中的命中 buff 仍由原调用者施加。
- 其他模组现在能够收到真实的弹幕命中回调，也可能附加各自的命中效果；本模组无法统一禁止第三方效果并同时保留统计回调。尤其需要在当前模组组合下验证第三方 `ModifyHitInfo` 回调及伤害统计。
- 未迁移饰品追加伤害或黑洞直接扣生命；未增加灾厄依赖或统计模组反射接口。

## 已执行

- 核查当前安装 tModLoader DLL：`NewProjectile` 在 OnSpawn 后发送生成包；弹幕命中通过 `CombinedHooks.OnHitNPCWithProj` 分发；PlayerLoader 同时分发通用 OnHitNPC 和弹幕专用回调。核查单次穿透的共用免疫读取/写入分支。
- `dotnet msbuild TestMod.csproj "/t:ResolveReferences;CoreCompile"` 编译通过；存在既有 GodModeToggle/GrappleEffect 魔法数字警告。
- 使用临时隔离项目链接生产 `DashStrikeProjectile.cs` 和当前安装的 tModLoader/FNA 程序集，27 项断言通过：三种弹幕的单次穿透与免疫配置、无附魔/PvP、未准备快照禁用伤害、owner/服务器/远端限制、唯一目标、在第二轮增伤/防御/暴击修正后恢复原命中结果、保留隐藏数字、已命中后禁止再次命中。DashPlayer 和 GlobalProjectile 为最小替身。
- 这些断言没有执行完整 `Projectile.Damage`、真实联网、统计模组或游戏画面，不能代替下面的运行验证。
- `git diff --check` 检查空白错误。

## 待游戏内验证

在 tModLoader -> Mod Sources -> Build + Reload 后：

1. 分别启用环境瓶、KiSpaceDamageCalc，在 Boss 战中只使用一种冲刺；确认显示对应名称且本次伤害只计入一次。KiSpace 的统计范围仍受其 Boss 战条件限制。
2. 比较相同装备/目标防御下的普通撞击和暴击伤害、击退方向；确认高暴击率和专注额度不会改变冲刺原有固定概率或消耗额度。
3. 长冲刺最多命中 16 个不同目标，短冲刺最多 8 个；同一次冲刺不能重复命中同一目标。StandardDash 按 MaxHitsPerDash 和 ShieldSlam 配置停止，并保留配置 buff/音效。
4. 让武器正在命中目标时再冲刺，确认冲刺不被共用免疫帧拦截，也不打断后续武器攻击。暴走期间仍只撞击一次。
5. 装备心之钢、狂战刃、吸血、处决和命中 debuff 饰品：冲刺不能意外触发这些普通攻击效果；随后的普通攻击仍正常。
6. 双客户端分别冲刺同一目标，确认服务器只处理各 owner 的一次伤害、各玩家统计归属正确、远端没有二次伤害或残留隐形弹幕；横向、垂直、撞墙和最后一帧撞击均验证。
