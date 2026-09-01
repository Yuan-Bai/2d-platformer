using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Tilemaps;
using UnityEngine.UI;
using Platformer;
using Platformer.Levels;
using Platformer.Player;

namespace Platformer.EditorTools
{
    /// <summary>
    /// 关卡生成器（ADR-0006 + ADR-0009）：Assets/Levels/*.json → Assets/Scenes/Levels/*.unity。
    /// 菜单 Tools/Platformer/Build All Levels：解析+校验 → 装配 4 个关卡内容场景 → 更新 Build Settings 顺序。
    /// ADR-0009 起关卡场景只装关卡内容（地形/机关/SpawnPoint/LevelConfig/CameraBounds/视差），
    /// 玩家/相机/HUD 常驻 00-Bootstrap；组件装配统一走 LevelKit 工厂（单一事实源）。
    /// 生成后的场景归用户自由编辑（混合分工）；重新生成会整体覆盖该场景。
    /// </summary>
    public static class LevelBuilder
    {
        private const string LevelsFolder = "Assets/Levels";
        private const string ScenesFolder = "Assets/Scenes/Levels";
        private const string TilesFolder = "Assets/Tiles";
        private const string BootstrapScene = "Assets/Scenes/00-Bootstrap.unity";

        // 地形选块（设计文档 §2，用户目视确认）：表面 = 草顶（3 变体按列轮换），内部 = 纯岩
        private static readonly string[] GrassTiles = { "tileset_0", "tileset_1", "tileset_2" };
        private const string RockTileName = "tileset_3";

        private static readonly string[] LevelOrder =
        {
            "01-Tutorial", "02-ForestPath", "03-SkyBridge", "04-ThornForest",
        };

        [MenuItem("Tools/Platformer/Build All Levels")]
        public static void BuildAll()
        {
            if (!AssetDatabase.IsValidFolder(LevelsFolder))
            {
                Debug.LogError($"LevelBuilder: 找不到 {LevelsFolder}（关卡 JSON 在阶段 3 提供）");
                return;
            }

            var datas = new List<LevelData>();
            foreach (var file in Directory.GetFiles(LevelsFolder, "*.json").OrderBy(f => f))
            {
                if (!LevelData.TryParse(File.ReadAllText(file), out var data, out string parseError))
                {
                    Debug.LogError($"LevelBuilder: {Path.GetFileName(file)} {parseError}");
                    continue;
                }
                var errors = LevelValidator.Validate(data);
                if (errors.Count > 0)
                {
                    Debug.LogError($"LevelBuilder: {Path.GetFileName(file)} 校验失败，跳过生成：\n  " +
                                   string.Join("\n  ", errors));
                    continue;
                }
                if (Array.IndexOf(LevelOrder, data.scene) < 0)
                {
                    Debug.LogError($"LevelBuilder: {Path.GetFileName(file)} 的 scene '{data.scene}' 不在关卡链清单中");
                    continue;
                }
                datas.Add(data);
            }

            if (datas.Count == 0)
            {
                Debug.LogError("LevelBuilder: 没有可用的关卡 JSON");
                return;
            }

            if (!AssetDatabase.IsValidFolder(ScenesFolder))
                AssetDatabase.CreateFolder("Assets/Scenes", "Levels");

            // 关卡链（ADR-0009）：顺序由 GameFlowController 常驻列表决定，关卡场景不再内嵌 nextScene
            foreach (var data in datas)
                BuildLevelScene(data);

            // Build Settings = [00-Bootstrap, 关卡链, ...其他既有场景]。
            // 合并而非覆盖：保留既有列表里的非关卡场景（测试夹具 TestLevelA/B 等；
            // PlayMode 测试的 LoadScene 依赖它们在 Build Settings，进入 Play 后运行时列表才可加载）。
            var scenes = new List<EditorBuildSettingsScene>();
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(BootstrapScene) != null)
                scenes.Add(new EditorBuildSettingsScene(BootstrapScene, true));
            else
                Debug.LogWarning("LevelBuilder: 缺 00-Bootstrap 场景 —— 先跑 Tools/Platformer/Create Bootstrap Scene");
            scenes.AddRange(LevelOrder.Select(n => new EditorBuildSettingsScene($"{ScenesFolder}/{n}.unity", true)));
            foreach (var existing in EditorBuildSettings.scenes)
                if (!scenes.Exists(s => s.path == existing.path))
                    scenes.Add(existing);
            EditorBuildSettings.scenes = scenes.ToArray();

            Debug.Log($"LevelBuilder: 完成。关卡 {datas.Count} 个 → {ScenesFolder}；" +
                      $"Build Settings 已按 [Bootstrap + 关卡链] 排序（共 {scenes.Count} 个场景）。");
        }

        // ---------------- 单个关卡场景 ----------------

        private static void BuildLevelScene(LevelData data)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            int height = data.Height;
            int width = data.Width;
            int GridY(int row) => height - 1 - row; // map 行 0（顶部）→ tilemap 网格 y

            CreateTerrain(data);

            // 实体分组容器（层级收纳）：机关按类型挂到根级分组下，
            // 避免 31 个实体平铺场景根节点（此前 hierarchy 难以辨认的直接原因）。
            // 仅影响场景组织，不影响任何组件引用/碰撞/相机逻辑。
            var groups = new Dictionary<string, Transform>();
            Transform Group(string key)
            {
                if (!groups.TryGetValue(key, out var t))
                {
                    t = new GameObject(key).transform;
                    groups[key] = t;
                }
                return t;
            }

            // 机关与物件（行主序扫描：M/S 的出现顺序与元数据数组一一对应，设计文档 §3）
            int signIndex = 0;
            int movingIndex = 0;
            BuildOneWayRuns(data, Group);
            for (int r = 0; r < height; r++)
            {
                for (int c = 0; c < width; c++)
                {
                    int gy = GridY(r);
                    switch (data.Cell(c, r))
                    {
                        case LevelData.Bumper:
                        {
                            var go = LevelKit.CreateBumper(new Vector3(c + 0.5f, gy + 0.5f, 0f));
                            go.name = $"Bumper_{c}";
                            go.transform.SetParent(Group("Bumpers"), false);
                            break;
                        }
                        case LevelData.Moving:
                        {
                            var def = data.movingPlatforms != null && movingIndex < data.movingPlatforms.Length
                                ? data.movingPlatforms[movingIndex] : null;
                            movingIndex++;
                            var go = LevelKit.CreateMovingPlatform(new Vector3(c + 0.5f, gy + 0.5f, 0f), def);
                            go.name = $"MovingPlatform_{c}";
                            go.transform.SetParent(Group("MovingPlatforms"), false);
                            break;
                        }
                        case LevelData.Hazard:
                        {
                            var go = LevelKit.CreateSpikes(new Vector3(c + 0.5f, gy + 0.28f, 0f)); // 基座贴地（地面顶 = gy）
                            go.name = $"Spikes_{c}";
                            go.transform.SetParent(Group("Spikes"), false);
                            break;
                        }
                        case LevelData.Checkpoint:
                        {
                            var go = LevelKit.CreateCheckpoint(new Vector3(c + 0.5f, gy + 0.63f, 0f));
                            go.name = $"Checkpoint_{c}";
                            go.transform.SetParent(Group("Checkpoints"), false);
                            break;
                        }
                        case LevelData.Cherry:
                        {
                            var go = LevelKit.CreateCherry(new Vector3(c + 0.5f, gy + 0.5f, 0f));
                            go.name = $"Cherry_{c}";
                            go.transform.SetParent(Group("Cherries"), false);
                            break;
                        }
                        case LevelData.Door:
                        {
                            var go = LevelKit.CreateDoor(new Vector3(c + 0.5f, gy + 1.03f, 0f)); // 基座贴地（地面顶 = gy）
                            go.name = $"Door_{c}";
                            go.transform.SetParent(Group("Exit"), false);
                            break;
                        }
                        case LevelData.Sign:
                        {
                            string message = data.signs != null && signIndex < data.signs.Length ? data.signs[signIndex] : "";
                            signIndex++;
                            var go = LevelKit.CreateSign(new Vector3(c + 0.5f, gy + 0.63f, 0f), message);
                            go.name = $"Sign_{c}";
                            go.transform.SetParent(Group("Signs"), false);
                            break;
                        }
                    }
                }
            }

            // 出生点标记（ADR-0009）：玩家常驻 00-Bootstrap，切关时 GameFlowController 按本标记重置玩家。
            // 位置同原玩家出生点：P 单元格上方 1.9m（新盒高 1.6，底距地面 0.05m，重生上抬后落下即站稳）。
            int pCol = -1;
            int pRow = -1;
            for (int r = 0; r < height && pCol < 0; r++)
            {
                var line = data.map[r];
                if (line == null) continue;
                for (int c = 0; c < line.Length; c++)
                {
                    if (line[c] == LevelData.Spawn)
                    {
                        pCol = c;
                        pRow = r;
                        break;
                    }
                }
            }
            var spawnGo = new GameObject("SpawnPoint");
            spawnGo.AddComponent<SpawnPoint>();
            spawnGo.transform.position = new Vector3(pCol + 0.5f, GridY(pRow) + 1.9f, 0f);

            LevelKit.CreateCameraBounds(width, height);

            LevelKit.CreateParallax();

            var configGo = new GameObject("LevelConfig");
            var config = configGo.AddComponent<LevelConfig>();
            var so = new SerializedObject(config);
            so.FindProperty("totalCherries").intValue = CountChar(data, LevelData.Cherry);
            so.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.SaveScene(scene, $"{ScenesFolder}/{data.scene}.unity");
        }

        /// <summary>tilemap 地形：草顶/纯岩自动选块 + TilemapCollider2D→Composite 碰撞。</summary>
        private static void CreateTerrain(LevelData data)
        {
            var tiles = new Dictionary<string, Tile>();
            foreach (var name in GrassTiles.Concat(new[] { RockTileName }))
            {
                var t = AssetDatabase.LoadAssetAtPath<Tile>($"{TilesFolder}/{name}.asset");
                if (t == null)
                    Debug.LogError($"LevelBuilder: 缺 Tile 资产 {name}.asset —— 先跑 Tools/Platformer/Build Tiles From Existing Slices");
                tiles[name] = t;
            }

            var gridGo = LevelKit.CreateTerrainRig();
            var tilemap = gridGo.GetComponentInChildren<Tilemap>();

            int height = data.Height;
            for (int r = 0; r < height; r++)
            {
                var line = data.map[r];
                if (line == null || line.Length != data.Width) continue;
                for (int c = 0; c < line.Length; c++)
                {
                    if (line[c] != LevelData.Solid) continue;
                    // 选块规则（设计文档 §2）：上方非实心 = 表面 → 草顶（按列轮换）；否则纯岩
                    bool exposed = data.Cell(c, r - 1) != LevelData.Solid;
                    string tileName = exposed ? GrassTiles[c % GrassTiles.Length] : RockTileName;
                    if (tiles[tileName] != null)
                        tilemap.SetTile(new Vector3Int(c, height - 1 - r, 0), tiles[tileName]);
                }
            }

            // Composite 碰撞几何强制生成（bug 修复）：
            // 程序化 SetTile 不触发编辑器端 collider 重建，保存场景时 Composite 几何为空（pathCount=0）
            // → 运行时玩家直接穿过地形掉落（实测复现）。ProcessTilemapChanges 让 TilemapCollider2D
            // 从 tile 数据生成形状，GenerateGeometry 再把形状合并进 Composite，随场景序列化固化。
            var tileCollider = gridGo.GetComponentInChildren<TilemapCollider2D>();
            var composite = gridGo.GetComponentInChildren<CompositeCollider2D>();
            tileCollider.ProcessTilemapChanges();
            composite.GenerateGeometry();
        }

        /// <summary>= 连续段合并为一个单向平台（每段一个 GameObject，避免逐格建件）。</summary>
        private static void BuildOneWayRuns(LevelData data, Func<string, Transform> group)
        {
            int height = data.Height;
            int width = data.Width;
            for (int r = 0; r < height; r++)
            {
                int c = 0;
                while (c < width)
                {
                    if (data.Cell(c, r) != LevelData.OneWay) { c++; continue; }
                    int start = c;
                    while (c < width && data.Cell(c, r) == LevelData.OneWay) c++;
                    int len = c - start;
                    int gy = height - 1 - r;
                    // 平台顶 = 单元格顶（保持 1 字符 = 1m 的度量语义）：厚 0.5m、顶对齐
                    var go = LevelKit.CreateOneWayPlatform(new Vector3(start + len * 0.5f, gy + 0.75f, 0f), len);
                    go.name = $"OneWay_{start}x{len}";
                    go.transform.SetParent(group("OneWayPlatforms"), false);
                }
            }
        }

        private static int CountChar(LevelData data, char target)
        {
            int n = 0;
            for (int r = 0; r < data.Height; r++)
            {
                var line = data.map[r];
                if (line == null) continue;
                foreach (var ch in line)
                    if (ch == target) n++;
            }
            return n;
        }
    }
}
