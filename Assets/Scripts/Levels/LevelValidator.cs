using System.Collections.Generic;

namespace Platformer.Levels
{
    /// <summary>
    /// 关卡结构校验（ADR-0006 的测试面，纯 C# 无 Unity 依赖，EditMode 直测）。
    /// v1 范围（设计文档 §3）：
    ///   1) 结构：map 矩形、图例字符、P/D 唯一、C≥1、M/S 数量与元数据一致；
    ///   2) 出生安全：P 周围 Chebyshev 半径 2 格内无尖刺；
    ///   3) 封闭性：左右边界列必须全为实心地块（防摔出关卡），每列至少一个可站立表面（坑必有底）；
    ///   4) 尖刺连续段宽度 ≤ maxGap：本项目设计约定"坑底全铺尖刺"（设计文档 §2），
    ///      故尖刺段宽度 ≈ 坑宽——一个必须空中跨越的距离。无尖刺的下坡段视为可走通，不检查。
    /// 明确不做（诚实边界）：上台高度红线与全物理可达性——装饰性高墙无法与必经台阶区分，
    /// v1 不引入路径分析；这两项靠布置约定 + 用户试玩兜底（设计文档 §3/§13）。
    /// </summary>
    public static class LevelValidator
    {
        public const string Tutorial = "tutorial";
        public const string Easy = "easy";
        public const string Medium = "medium";
        public const string Hard = "hard";

        /// <summary>难度默认坑宽红线（米；1 字符 = 1m）。</summary>
        public static float DefaultMaxGap(string difficulty)
        {
            switch (difficulty)
            {
                case Tutorial: return 3f;
                case Hard: return 4.5f;
                default: return 3.5f; // easy / medium / 未知
            }
        }

        /// <summary>校验。通过 = 返回空列表；失败 = 每条错误含定位与说明。</summary>
        public static List<string> Validate(LevelData data)
        {
            var errors = new List<string>();
            if (data == null)
            {
                errors.Add("关卡数据为空");
                return errors;
            }

            int width = data.Width;
            int height = data.Height;
            if (height <= 0)
            {
                errors.Add("map 为空");
                return errors;
            }
            if (width <= 0)
            {
                errors.Add("map 首行为空");
                return errors;
            }

            // ---- 1. 结构：矩形 + 图例字符 ----
            for (int r = 0; r < height; r++)
            {
                var line = data.map[r];
                if (line == null || line.Length != width)
                {
                    errors.Add($"第 {r} 行长度 {(line == null ? 0 : line.Length)} ≠ 首行 {width}（map 必须为矩形）");
                    continue;
                }
                for (int c = 0; c < width; c++)
                {
                    if (!IsLegendChar(line[c]))
                        errors.Add($"({c},{r}) 非法字符 '{line[c]}'");
                }
            }

            // ---- 2. 结构：P/D 唯一、C≥1、M/S 数量匹配 ----
            var counts = new Dictionary<char, int>();
            for (int r = 0; r < height; r++)
            {
                var line = data.map[r];
                if (line == null || line.Length != width) continue;
                for (int c = 0; c < width; c++)
                {
                    char ch = line[c];
                    if (ch == LevelData.Empty || ch == LevelData.Solid || ch == LevelData.OneWay) continue;
                    counts[ch] = counts.TryGetValue(ch, out int n) ? n + 1 : 1;
                }
            }

            int p = CountOf(counts, LevelData.Spawn);
            int d = CountOf(counts, LevelData.Door);
            int cp = CountOf(counts, LevelData.Checkpoint);
            int m = CountOf(counts, LevelData.Moving);
            int s = CountOf(counts, LevelData.Sign);
            if (p != 1) errors.Add($"P（出生点）必须恰有 1 个，实际 {p}");
            if (d != 1) errors.Add($"D（终点门）必须恰有 1 个，实际 {d}");
            if (cp < 1) errors.Add("C（重生点）至少需要 1 个");
            if (m != (data.movingPlatforms?.Length ?? 0))
                errors.Add($"M（移动平台）{m} 个 ≠ movingPlatforms 条目 {data.movingPlatforms?.Length ?? 0} 条");
            if (s != (data.signs?.Length ?? 0))
                errors.Add($"S（路牌）{s} 个 ≠ signs 条目 {data.signs?.Length ?? 0} 条");

            // ---- 3. 出生安全：P 周围半径 2 格内无 X ----
            if (p == 1 && TryFind(data, LevelData.Spawn, out int pCol, out int pRow))
            {
                bool spawnUnsafe = false;
                for (int dy = -2; dy <= 2 && !spawnUnsafe; dy++)
                for (int dx = -2; dx <= 2 && !spawnUnsafe; dx++)
                {
                    if (dx == 0 && dy == 0) continue;
                    if (data.Cell(pCol + dx, pRow + dy) == LevelData.Hazard)
                    {
                        errors.Add($"出生点 ({pCol},{pRow}) 2m 内有尖刺（({pCol + dx},{pRow + dy})）");
                        spawnUnsafe = true;
                    }
                }
            }

            // ---- 4. 封闭性：左右边界列全 #；每列至少一个可站立表面 ----
            bool borderOk = true;
            for (int r = 0; r < height && borderOk; r++)
            {
                var line = data.map[r];
                if (line == null || line.Length != width) continue;
                if (line[0] != LevelData.Solid || line[width - 1] != LevelData.Solid)
                {
                    errors.Add($"左右边界必须全为实心地块（第 {r} 行边界列非 #），防摔出关卡外");
                    borderOk = false;
                }
            }

            var surface = new int[width]; // 每列最顶的可站立行号（0=顶部）；-1 = 无表面
            for (int c = 0; c < width; c++)
            {
                surface[c] = -1;
                for (int r = 0; r < height; r++)
                {
                    if (LevelData.IsStandable(data.Cell(c, r)))
                    {
                        surface[c] = r;
                        break;
                    }
                }
            }
            for (int c = 0; c < width; c++)
            {
                if (surface[c] < 0)
                    errors.Add($"第 {c} 列没有任何可站立表面（坑必须有底）");
            }

            // ---- 5. 尖刺连续段宽度红线（= 坑宽，见类注释） ----
            float maxGap = data.maxGap > 0f ? data.maxGap : DefaultMaxGap(data.difficulty);
            for (int r = 0; r < height; r++)
            {
                var line = data.map[r];
                if (line == null || line.Length != width) continue;
                int run = 0;
                for (int c = 0; c <= width; c++)
                {
                    bool spike = c < width && line[c] == LevelData.Hazard;
                    if (spike)
                    {
                        run++;
                    }
                    else if (run > 0)
                    {
                        if (run > maxGap)
                            errors.Add($"尖刺段宽 {run}m 超过红线 {maxGap}m（第 {r} 行，列 {c - run}~{c - 1}）");
                        run = 0;
                    }
                }
            }

            return errors;
        }

        private static int CountOf(Dictionary<char, int> counts, char key) =>
            counts.TryGetValue(key, out int n) ? n : 0;

        private static bool TryFind(LevelData data, char target, out int col, out int row)
        {
            for (int r = 0; r < data.Height; r++)
            {
                var line = data.map[r];
                if (line == null) continue;
                for (int c = 0; c < line.Length; c++)
                {
                    if (line[c] == target)
                    {
                        col = c;
                        row = r;
                        return true;
                    }
                }
            }
            col = row = -1;
            return false;
        }

        private static bool IsLegendChar(char c) =>
            c == LevelData.Solid || c == LevelData.OneWay || c == LevelData.Empty ||
            c == LevelData.Spawn || c == LevelData.Door || c == LevelData.Checkpoint ||
            c == LevelData.Bumper || c == LevelData.Moving || c == LevelData.Hazard ||
            c == LevelData.Cherry || c == LevelData.Sign;
    }
}
