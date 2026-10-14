using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ModLoader;

namespace TestMod.Common.Systems
{
    public sealed partial class DecoySystem
    {
        private static DecoySystem reporter;
        private readonly List<string> rows = new();
        private readonly HashSet<string> warnings = new();
        private string reportPath;

        public override void PostSetupContent()
        {
            rows.Add("AI 目标视图\t" + (Ready ? "就绪" : "停用") + "\t原版及模组完整 AI；静止稻草人；2400 像素；180 tick 补充重选");
            rows.Add("伤害\t真实视图\t正式 Damage 使用原碰撞；AI 内直接 Hurt/KillMe 必须证明真实命中");
            rows.Add("来源\t严格归属\t普通敌人、Boss、分段、弹幕及子弹幕；无来源敌对弹幕独立选择，不按距离推断来源");
            rows.Add("索敌入口\t统一重定向\tTargetClosest/Upgraded/WOF、GetTargetData；不清空 ai[] 或第三方缓存");
            rows.Add("直接伤害专项\t待补充\t弹幕复用 Damage_GetHitbox/Colliding；NPC 本体接触可证明，复杂脚本范围尚无专项形状");
            rows.Add("目标缓存\t保留原值\t已发射直线轨迹不改；未知字段/静态缓存等待原 AI 重新索敌；下列字段仅为待核对线索");
            foreach (Mod mod in ModLoader.Mods)
                foreach (ModType content in mod.GetContent<ModNPC>().Cast<ModType>().Concat(mod.GetContent<ModProjectile>()))
                {
                    try
                    {
                        // 只做缺口清点，不按字段名盲改第三方缓存，也不重跑 AI。
                        string[] caches = content.GetType().GetFields(Methods).Where(f => !f.IsStatic && f.FieldType == typeof(Vector2) &&
                            new[] { "target", "aim", "destination", "cached" }.Any(word => f.Name.Contains(word, StringComparison.OrdinalIgnoreCase)))
                            .Select(f => f.Name).ToArray();
                        if (caches.Length != 0) rows.Add(content.FullName + "\t待专项核对\t可能缓存目标坐标：" + string.Join(",", caches));
                    }
                    catch (Exception exception)
                    {
                        rows.Add(content.FullName + "\t清点失败\t" + exception.GetType().Name);
                    }
                }
            reportPath = Path.Combine(Main.SavePath, "Logs", "Decoy-coverage.tsv");
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(reportPath));
                File.WriteAllLines(reportPath, new[] { "入口/内容\t状态\t说明" }.Concat(rows));
                Mod.Logger.Info($"[Decoy] 稻草人诱敌视图 {(Ready ? "已就绪" : "未启用")}；覆盖与缺口：{reportPath}");
            }
            catch (Exception exception) { Mod.Logger.Warn("[Decoy] 覆盖清单写入失败。", exception); }
        }

        private static void Note(Entity entity, string reason, bool warning)
        {
            DecoySystem system = reporter;
            if (system == null) return;
            string type = entity is Projectile p ? p.ModProjectile?.FullName ?? "Terraria/Projectile:" + p.type :
                entity is NPC n ? n.ModNPC?.FullName ?? "Terraria/NPC:" + n.type : "未知来源";
            string row = type + "\t缺口\t" + reason;
            if (!system.warnings.Add(row)) return;
            if (warning) system.Mod.Logger.Warn("[Decoy] " + type + ": " + reason);
            if (system.reportPath == null) { system.rows.Add(row); return; }
            try { File.AppendAllLines(system.reportPath, new[] { row }); }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            { /* 诊断文件不可写不能中断原 AI，也不在游戏 tick 重复输出。 */ }
        }
    }
}
