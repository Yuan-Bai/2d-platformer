using System.Linq;
using NUnit.Framework;
using Platformer.Levels;

namespace Platformer.Tests
{
    /// <summary>
    /// 关卡数据模块测试（ADR-0006 的验收）：JSON 解析 + 结构/几何校验。
    /// 纯 C# 无场景依赖，EditMode 毫秒级。校验规则见 LevelValidator 类注释与设计文档 §3。
    /// </summary>
    public class LevelDataTests
    {
        // 最小合法关：9 宽 × 5 高，左右边界全 #，无尖刺
        private static readonly string[] ValidMap =
        {
            "#########",
            "#P......#",
            "#.......#",
            "#..c.CD.#",
            "#########",
        };

        // 带尖刺坑的合法关（10×6）：坑底尖刺段 3m（≤ 3.5m 红线），P 距尖刺 >2m
        private static readonly string[] SpikePitMap =
        {
            "##......##",
            "#P.......#",
            "#........#",
            "#...C..D.#",
            "#...XXX..#",
            "##########",
        };

        private static string Json(string[] map, string extra = "") =>
            "{\"map\":[" + string.Join(",", map.Select(r => "\"" + r + "\"")) + "]" + extra + "}";

        private static LevelData Parse(string json)
        {
            Assert.IsTrue(LevelData.TryParse(json, out var data, out string error), $"解析应成功：{error}");
            return data;
        }

        // ---------------- 解析 ----------------

        [Test]
        public void TryParse_ValidJson_ParsesAllFields()
        {
            var data = Parse(Json(ValidMap,
                ",\"name\":\"第一步\",\"scene\":\"01-Tutorial\",\"background\":[\"back\"]," +
                "\"movingPlatforms\":[{\"waypoints\":[{\"x\":2,\"y\":0}],\"speed\":1.5}]," +
                "\"signs\":[\"移动\"],\"difficulty\":\"tutorial\",\"maxGap\":4.5"));

            Assert.AreEqual("第一步", data.name);
            Assert.AreEqual("01-Tutorial", data.scene);
            Assert.AreEqual(9, data.Width);
            Assert.AreEqual(5, data.Height);
            Assert.AreEqual(1, data.movingPlatforms.Length);
            Assert.AreEqual(2f, data.movingPlatforms[0].waypoints[0].x);
            Assert.AreEqual(1.5f, data.movingPlatforms[0].speed);
            Assert.AreEqual("移动", data.signs[0]);
            Assert.AreEqual("tutorial", data.difficulty);
            Assert.AreEqual(4.5f, data.maxGap);
        }

        [Test]
        public void TryParse_InvalidJson_FailsWithError()
        {
            Assert.IsFalse(LevelData.TryParse("{ 不是 JSON", out _, out string error));
            Assert.IsNotEmpty(error);
        }

        [Test]
        public void TryParse_EmptyObject_NormalizesNulls()
        {
            // JsonUtility 不跑构造函数：缺失字段必须被归一化为空集合而不是 null
            var data = Parse("{}");
            Assert.IsNotNull(data.map);
            Assert.AreEqual(0, data.map.Length);
            Assert.IsNotNull(data.signs);
            Assert.IsNotNull(data.movingPlatforms);
        }

        // ---------------- 结构校验 ----------------

        [Test]
        public void Validate_ValidMap_Passes()
        {
            Assert.IsEmpty(LevelValidator.Validate(Parse(Json(ValidMap))));
        }

        [Test]
        public void Validate_TwoSpawns_Error()
        {
            var map = ValidMap.ToArray();
            map[2] = "#P......#";
            var errors = LevelValidator.Validate(Parse(Json(map)));
            Assert.IsTrue(errors.Any(e => e.Contains("P（出生点）必须恰有 1 个")), string.Join("\n", errors));
        }

        [Test]
        public void Validate_MissingDoor_Error()
        {
            var map = ValidMap.ToArray();
            map[3] = "#..c.C..#";
            var errors = LevelValidator.Validate(Parse(Json(map)));
            Assert.IsTrue(errors.Any(e => e.Contains("D（终点门）必须恰有 1 个")), string.Join("\n", errors));
        }

        [Test]
        public void Validate_MissingCheckpoint_Error()
        {
            var map = ValidMap.ToArray();
            map[3] = "#..c..D.#";
            var errors = LevelValidator.Validate(Parse(Json(map)));
            Assert.IsTrue(errors.Any(e => e.Contains("C（重生点）至少需要 1 个")), string.Join("\n", errors));
        }

        [Test]
        public void Validate_NotRectangular_Error()
        {
            var map = ValidMap.ToArray();
            map[3] = "#..c..D#"; // 少一列
            var errors = LevelValidator.Validate(Parse(Json(map)));
            Assert.IsTrue(errors.Any(e => e.Contains("必须为矩形")), string.Join("\n", errors));
        }

        [Test]
        public void Validate_IllegalChar_Error()
        {
            var map = ValidMap.ToArray();
            map[2] = "#..Z....#";
            var errors = LevelValidator.Validate(Parse(Json(map)));
            Assert.IsTrue(errors.Any(e => e.Contains("非法字符 'Z'")), string.Join("\n", errors));
        }

        [Test]
        public void Validate_MovingCountMismatch_Error()
        {
            var map = ValidMap.ToArray();
            map[2] = "#..M....#";
            var errors = LevelValidator.Validate(Parse(Json(map)));
            Assert.IsTrue(errors.Any(e => e.Contains("M（移动平台）1 个 ≠ movingPlatforms 条目 0 条")), string.Join("\n", errors));
        }

        [Test]
        public void Validate_SignCountMismatch_Error()
        {
            var map = ValidMap.ToArray();
            map[2] = "#..S....#";
            var errors = LevelValidator.Validate(Parse(Json(map)));
            Assert.IsTrue(errors.Any(e => e.Contains("S（路牌）1 个 ≠ signs 条目 0 条")), string.Join("\n", errors));
        }

        [Test]
        public void Validate_SpawnNearHazard_Error()
        {
            // P 在 (1,1)；X 在 (2,2)：Chebyshev 距离 1，触发出生安全红线
            var map = new[]
            {
                "#########",
                "#P......#",
                "#.X.....#",
                "#...C.D.#",
                "#########",
            };
            var errors = LevelValidator.Validate(Parse(Json(map)));
            Assert.IsTrue(errors.Any(e => e.Contains("出生点") && e.Contains("尖刺")), string.Join("\n", errors));
        }

        [Test]
        public void Validate_OpenSideBorder_Error()
        {
            var map = ValidMap.ToArray();
            map[2] = ".......#."; // 左边界列 0 不是 #
            var errors = LevelValidator.Validate(Parse(Json(map)));
            Assert.IsTrue(errors.Any(e => e.Contains("左右边界必须全为实心地块")), string.Join("\n", errors));
        }

        [Test]
        public void Validate_ColumnWithoutSurface_Error()
        {
            // 第 2 列无任何 #/=：没有底
            var map = new[]
            {
                "##..#####",
                "#P......#",
                "#.......#",
                "#..C.D..#",
                "##.######",
            };
            var errors = LevelValidator.Validate(Parse(Json(map)));
            Assert.IsTrue(errors.Any(e => e.Contains("没有任何可站立表面")), string.Join("\n", errors));
        }

        // ---------------- 几何红线：尖刺段宽度 ----------------

        [Test]
        public void Validate_SpikeRunTooWide_Error()
        {
            var map = SpikePitMap.ToArray();
            map[4] = "#.XXXXXX.#"; // 尖刺段 6m > easy 默认红线 3.5m
            var errors = LevelValidator.Validate(Parse(Json(map)));
            Assert.IsTrue(errors.Any(e => e.Contains("尖刺段宽 6m 超过红线 3.5m")), string.Join("\n", errors));
        }

        [Test]
        public void Validate_SpikeRun_WithMaxGapOverride_Passes()
        {
            var map = SpikePitMap.ToArray();
            map[4] = "#.XXXXXX.#";
            Assert.IsEmpty(LevelValidator.Validate(Parse(Json(map, ",\"maxGap\":7"))));
        }

        [Test]
        public void Validate_SpikeRun_WithinLimit_Passes()
        {
            Assert.IsEmpty(LevelValidator.Validate(Parse(Json(SpikePitMap)))); // 3m ≤ 3.5m
        }

        [Test]
        public void Validate_WidePitWithoutSpikes_Passes()
        {
            // 无尖刺的低谷 = 可走通（设计约定：坑底全铺尖刺才构成跳越红线，设计文档 §2），不检查
            var map = new[]
            {
                "##......##",
                "#P.......#",
                "#........#",
                "#...C..D.#",
                "#........#",
                "##########",
            };
            Assert.IsEmpty(LevelValidator.Validate(Parse(Json(map))));
        }

        [Test]
        public void DefaultMaxGap_ByDifficulty()
        {
            Assert.AreEqual(3f, LevelValidator.DefaultMaxGap(LevelValidator.Tutorial));
            Assert.AreEqual(3.5f, LevelValidator.DefaultMaxGap(LevelValidator.Easy));
            Assert.AreEqual(3.5f, LevelValidator.DefaultMaxGap(LevelValidator.Medium));
            Assert.AreEqual(4.5f, LevelValidator.DefaultMaxGap(LevelValidator.Hard));
            Assert.AreEqual(3.5f, LevelValidator.DefaultMaxGap("未知难度"));
        }
    }
}
