using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace Platformer.EditorTools
{
    /// <summary>
    /// Sunny Land 美术导入规范化（M3 阶段 1）：
    /// 所有 SunnyLand 的 PNG 统一为 Sprite / PPU 16 / Point 过滤（tileset.png 除外——
    /// 其多格切片归用户所有（Sprite Editor 手工切片：25×23 网格、透明格排除、pivot center），
    /// 本工具不重切，只做 PPU/过滤的兜底与 Tile 资产生成）。
    /// 已导入的资产需跑一次 Tools/Platformer/Reimport Sunny Land Sprites 才会应用新设置。
    /// </summary>
    public sealed class SunnyLandArtPostprocessor : AssetPostprocessor
    {
        private const string ArtRoot = "Assets/Art/SunnyLand";
        private const string TilesetPath = "Assets/Art/SunnyLand/environment/tileset.png";

        private void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith(ArtRoot)) return;
            if (!assetPath.EndsWith(".png")) return;
            if (assetPath == TilesetPath) return;

            var importer = (TextureImporter)assetImporter;
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = 16f;
            importer.filterMode = FilterMode.Point;
            importer.mipmapEnabled = false;
        }
    }

    public static class TilesetTools
    {
        private const string TilesetPath = "Assets/Art/SunnyLand/environment/tileset.png";
        private const string TilesFolder = "Assets/Tiles";

        /// <summary>
        /// 基于用户已在 Sprite Editor 切好的 tileset_N 精灵（128 个，16px 网格，PPU16）生成 Tile 资产。
        /// 不重新切片；顺带兜底 PPU16/Point（若导入设置被覆盖则纠正），并删除旧版错误切片
        /// （曾按错误尺寸 144×112 切出的左下角 9×7，精灵引用已失效）生成的 tileset_r*.asset。
        /// 地形选块（用户目视确认）：tileset_0~2 = 草顶岩石（表面块），tileset_3 = 纯岩石（内部块）。
        /// </summary>
        [MenuItem("Tools/Platformer/Build Tiles From Existing Slices")]
        public static void BuildTilesFromSlices()
        {
            var importer = (TextureImporter)AssetImporter.GetAtPath(TilesetPath);
            if (importer == null)
            {
                Debug.LogError($"TilesetTools: 找不到 {TilesetPath}");
                return;
            }

            // 兜底导入设置（用户切片存在 meta 里，不会被覆盖）
            bool changed = false;
            if (importer.spritePixelsPerUnit != 16f) { importer.spritePixelsPerUnit = 16f; changed = true; }
            if (importer.filterMode != FilterMode.Point) { importer.filterMode = FilterMode.Point; changed = true; }
            if (importer.spriteImportMode != SpriteImportMode.Multiple) { importer.spriteImportMode = SpriteImportMode.Multiple; changed = true; }
            if (changed) importer.SaveAndReimport();

            if (!AssetDatabase.IsValidFolder(TilesFolder))
                AssetDatabase.CreateFolder("Assets", "Tiles");

            // 清理旧版错误切片生成的 Tile（tileset_r*，其精灵引用已随用户重切失效）
            int removed = 0;
            foreach (var guid in AssetDatabase.FindAssets("tileset_r", new[] { TilesFolder }))
            {
                if (AssetDatabase.DeleteAsset(AssetDatabase.GUIDToAssetPath(guid)))
                    removed++;
            }

            // 从用户切片生成 Tile 资产
            var sprites = AssetDatabase.LoadAllAssetsAtPath(TilesetPath).OfType<Sprite>().ToList();
            int created = 0, updated = 0;
            foreach (var sprite in sprites)
            {
                string path = $"{TilesFolder}/{sprite.name}.asset";
                var existing = AssetDatabase.LoadAssetAtPath<Tile>(path);
                var tile = existing != null ? existing : ScriptableObject.CreateInstance<Tile>();
                tile.sprite = sprite;
                tile.colliderType = Tile.ColliderType.Sprite; // 实心地块：碰撞盒 = 整格
                if (existing == null)
                {
                    AssetDatabase.CreateAsset(tile, path);
                    created++;
                }
                else
                {
                    EditorUtility.SetDirty(tile);
                    updated++;
                }
            }
            AssetDatabase.SaveAssets();
            Debug.Log($"Tile 资产就绪：新建 {created}、更新 {updated}、清理旧切片 {removed}（共 {sprites.Count} 个用户切片）→ {TilesFolder}");
        }

        [MenuItem("Tools/Platformer/Reimport Sunny Land Sprites")]
        public static void ReimportAll()
        {
            AssetDatabase.ImportAsset("Assets/Art/SunnyLand",
                ImportAssetOptions.ImportRecursive | ImportAssetOptions.ForceUpdate);
            Debug.Log("Sunny Land 精灵已按 PPU16/Point 重新导入。");
        }
    }
}
