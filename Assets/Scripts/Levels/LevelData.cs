using System;
using UnityEngine;

namespace Platformer.Levels
{
    /// <summary>移动平台路径点（JSON 元数据，单位：米，相对平台初始位置）。</summary>
    [Serializable]
    public sealed class WaypointDef
    {
        public float x;
        public float y;
    }

    /// <summary>移动平台定义（JSON movingPlatforms 条目，按地图中 M 的出现顺序一一对应）。</summary>
    [Serializable]
    public sealed class MovingPlatformDef
    {
        public WaypointDef[] waypoints;
        public float speed = 2f;
    }

    /// <summary>
    /// 关卡数据（ADR-0006）：单文件 JSON 的强类型镜像。地形在 map 字符串行数组里，
    /// 严格 1 字符 = 1m = 1 图块；元数据（移动平台/路牌/难度/红线覆盖）结构化。
    /// 解析与校验（LevelValidator）同为纯 C# 运行时模块：Editor 生成器与 EditMode 测试共用同一个接缝。
    /// </summary>
    [Serializable]
    public sealed class LevelData
    {
        // 图例（关卡数据格式 v1，与设计文档 §3 绑定，改动需同步生成器与校验器）
        public const char Solid = '#';
        public const char OneWay = '=';
        public const char Empty = '.';
        public const char Spawn = 'P';
        public const char Door = 'D';
        public const char Checkpoint = 'C';
        public const char Bumper = 'B';
        public const char Moving = 'M';
        public const char Hazard = 'X';
        public const char Cherry = 'c';
        public const char Sign = 'S';

        public string name;
        public string scene;
        public string[] background;
        public MovingPlatformDef[] movingPlatforms;
        public string[] signs;
        public string difficulty = "easy";
        public float maxGap; // 可选覆盖：坑宽红线（米）；0 = 按难度默认表
        public string[] map;

        public int Width => map != null && map.Length > 0 ? map[0].Length : 0;
        public int Height => map != null ? map.Length : 0;

        /// <summary>字符是否为可站立表面（实心地块 / 单向平台）。</summary>
        public static bool IsStandable(char c) => c == Solid || c == OneWay;

        /// <summary>
        /// JSON 解析。注意：JsonUtility 不执行构造函数/字段初始化器，
        /// 缺失字段会保持 null——解析后必须归一化（此处完成）。
        /// </summary>
        public static bool TryParse(string json, out LevelData data, out string error)
        {
            data = null;
            error = null;
            if (string.IsNullOrEmpty(json))
            {
                error = "关卡 JSON 为空";
                return false;
            }

            try
            {
                data = JsonUtility.FromJson<LevelData>(json);
            }
            catch (Exception e)
            {
                error = $"JSON 解析失败：{e.Message}";
                return false;
            }

            if (data == null)
            {
                error = "JSON 解析失败：根对象缺失";
                return false;
            }

            data.name = data.name ?? "";
            data.scene = data.scene ?? "";
            data.background = data.background ?? Array.Empty<string>();
            data.movingPlatforms = data.movingPlatforms ?? Array.Empty<MovingPlatformDef>();
            data.signs = data.signs ?? Array.Empty<string>();
            data.map = data.map ?? Array.Empty<string>();
            return true;
        }

        /// <summary>map 行 r（0=顶部）第 col 列字符；越界返回 '\0'。</summary>
        public char Cell(int col, int row)
        {
            if (map == null || row < 0 || row >= map.Length) return '\0';
            var line = map[row];
            if (line == null || col < 0 || col >= line.Length) return '\0';
            return line[col];
        }
    }
}
