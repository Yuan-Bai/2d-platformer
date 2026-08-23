using System.IO;
using Cinemachine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Platformer.Cameras;
using Platformer.Player;

namespace Platformer.EditorTools
{
    /// <summary>
    /// 一键生成 M1 手感测试房。菜单：Tools/Platformer/Build Test Room。
    /// 场景：地面 + 台阶 + 浮空平台 + 玩家（方块占位）+ Cinemachine VCam，保存到 Assets/Scenes/TestRoom.unity。
    /// 角色正式像素动画在 M2/M3 接入（Sunny Land 的 Foxy spritesheet）。
    /// </summary>
    public static class TestRoomBuilder
    {
        [MenuItem("Tools/Platformer/Build Test Room")]
        public static void Build()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // 主相机 + Cinemachine Brain
            var camGo = new GameObject("Main Camera");
            camGo.tag = "MainCamera";
            camGo.AddComponent<Camera>();
            camGo.AddComponent<AudioListener>();
            camGo.AddComponent<CinemachineBrain>();

            // 地面与平台（占位方块，1x1 单位 sprite 按 scale 拉伸）
            Sprite placeholder = CreateOrLoadPlaceholderSprite();
            CreatePlatform("Ground", new Vector3(0f, -3f, 0f), new Vector2(28f, 2f), placeholder);
            CreatePlatform("Step1", new Vector3(-5f, -1.6f, 0f), new Vector2(4f, 0.6f), placeholder);
            CreatePlatform("Step2", new Vector3(-1f, -0.4f, 0f), new Vector2(4f, 0.6f), placeholder);
            CreatePlatform("Step3", new Vector3(3f, 0.8f, 0f), new Vector2(4f, 0.6f), placeholder);
            CreatePlatform("FloatPlatform", new Vector3(7.5f, 2.8f, 0f), new Vector2(3f, 0.6f), placeholder);
            CreatePlatform("HighPlatform", new Vector3(11f, 4.5f, 0f), new Vector2(3f, 0.6f), placeholder);

            // 玩家：1x1 方块占位 + 物理 + 输入 + 身体
            var player = new GameObject("Player");
            player.transform.position = new Vector3(-7f, -1.2f, 0f);
            var sr = player.AddComponent<SpriteRenderer>();
            sr.sprite = placeholder;
            sr.color = new Color(1f, 0.62f, 0.25f);
            player.AddComponent<BoxCollider2D>();
            player.AddComponent<Rigidbody2D>();
            player.AddComponent<InputReader>();
            player.AddComponent<PlayerBody>();

            // VCam（Follow 目标经序列化字段注入）
            var vcamGo = new GameObject("Player VCam");
            vcamGo.AddComponent<CinemachineVirtualCamera>();
            var rig = vcamGo.AddComponent<PlayerCameraRig>();
            var so = new SerializedObject(rig);
            so.FindProperty("followTarget").objectReferenceValue = player.transform;
            so.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.SaveScene(scene, "Assets/Scenes/TestRoom.unity");
            Debug.Log("Test Room 已生成：Assets/Scenes/TestRoom.unity —— 打开后点 Play，A/D 或方向键移动、空格跳跃。");
        }

        private static void CreatePlatform(string name, Vector3 pos, Vector2 size, Sprite sprite)
        {
            var go = new GameObject(name);
            go.transform.position = pos;
            go.transform.localScale = new Vector3(size.x, size.y, 1f);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.color = new Color(0.36f, 0.52f, 0.36f);
            var col = go.AddComponent<BoxCollider2D>();
            col.size = Vector2.one; // 随 scale 缩放为 size
        }

        /// <summary>生成或加载 1x1 单位的白色占位方块（4x4px，PPU=4，Point 过滤）。</summary>
        private static Sprite CreateOrLoadPlaceholderSprite()
        {
            const string path = "Assets/Art/Placeholder.png";
            var loaded = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (loaded != null) return loaded;

            var tex = new Texture2D(4, 4, TextureFormat.RGBA32, false);
            var pixels = new Color[16];
            for (int i = 0; i < pixels.Length; i++) pixels[i] = Color.white;
            tex.SetPixels(pixels);
            tex.Apply();

            Directory.CreateDirectory("Assets/Art");
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);

            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Sprite;
            importer.spritePixelsPerUnit = 4f;
            importer.filterMode = FilterMode.Point;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }
    }
}
