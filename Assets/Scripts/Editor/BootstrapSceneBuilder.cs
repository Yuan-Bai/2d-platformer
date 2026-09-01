using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using Platformer;
using Platformer.UI;

namespace Platformer.EditorTools
{
    /// <summary>
    /// Bootstrap 常驻场景构建器（ADR-0009）：
    /// 生成 00-Bootstrap 场景（相机/CameraRig/Player/常驻 Canvas 四面板/GameFlowController/GameBootstrap），
    /// 并提供「Play From Bootstrap」一键进 Play（方案 A 的编辑器工作流：关卡场景直开无玩家，一律从此启动）。
    /// </summary>
    public static class BootstrapSceneBuilder
    {
        private const string BootstrapScenePath = "Assets/Scenes/00-Bootstrap.unity";

        [MenuItem("Tools/Platformer/Create Bootstrap Scene")]
        public static void CreateBootstrapScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // 常驻相机（天空色 + AudioListener + CinemachineBrain）
            LevelKit.CreateMainCamera();

            // 常驻相机封装（prefab 核心；Follow/边界由 GameFlowController 切关重绑）
            LevelKit.InstantiateCameraRigCore();

            // 常驻玩家（prefab；切关时 RespawnAt 重置位置）
            LevelKit.InstantiatePlayer(Vector3.zero);

            // 常驻 Canvas：HUD + 提示 + 主菜单 + 通关面板
            var canvas = LevelKit.CreateHudCanvas();
            LevelKit.CreateHud(canvas);
            CreateMenuPanel(canvas.transform);
            CreateGameClearPanel(canvas.transform);

            new GameObject("GameFlowController").AddComponent<GameFlowController>();
            new GameObject("GameBootstrap").AddComponent<GameBootstrap>();

            if (!AssetDatabase.IsValidFolder("Assets/Scenes"))
                AssetDatabase.CreateFolder("Assets", "Scenes");
            EditorSceneManager.SaveScene(scene, BootstrapScenePath);
            Debug.Log($"Bootstrap 常驻场景已生成 → {BootstrapScenePath}。游玩请用 Tools/Platformer/Play From Bootstrap。");
        }

        /// <summary>一键从 00-Bootstrap 进 Play（关卡场景无玩家，直开无效——统一入口）。</summary>
        [MenuItem("Tools/Platformer/Play From Bootstrap")]
        public static void PlayFromBootstrap()
        {
            if (!File.Exists(BootstrapScenePath))
            {
                Debug.LogError("Play From Bootstrap: 缺 00-Bootstrap 场景 —— 先跑 Tools/Platformer/Create Bootstrap Scene");
                return;
            }
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;
            EditorSceneManager.OpenScene(BootstrapScenePath);
            EditorApplication.isPlaying = true;
        }

        // ==================== 常驻 UI 面板装配 ====================

        /// <summary>
        /// 主菜单面板：标题 + 开始/退出按钮（视觉打磨在 M4b）。
        /// 组件挂在常驻激活的 PanelRoot 上、控制子面板显隐——组件自身不能在初始 inactive 的对象上
        /// （SetActive(false) 的对象 Update 不执行，面板将永远无法自行显示）。
        /// </summary>
        private static void CreateMenuPanel(Transform canvas)
        {
            var rootGo = new GameObject("MainMenuPanelRoot", typeof(RectTransform));
            rootGo.transform.SetParent(canvas, false);

            var panelGo = new GameObject("MainMenuPanel", typeof(RectTransform));
            panelGo.transform.SetParent(rootGo.transform, false);
            var rt = (RectTransform)panelGo.transform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            var bg = panelGo.AddComponent<Image>();
            bg.color = new Color(0.16f, 0.28f, 0.22f, 0.92f); // 森林深绿半透明

            // 标题
            var titleGo = new GameObject("Title", typeof(RectTransform));
            titleGo.transform.SetParent(panelGo.transform, false);
            var titleRt = (RectTransform)titleGo.transform;
            titleRt.anchorMin = new Vector2(0.5f, 1f);
            titleRt.anchorMax = new Vector2(0.5f, 1f);
            titleRt.pivot = new Vector2(0.5f, 1f);
            titleRt.anchoredPosition = new Vector2(0f, -140f);
            titleRt.sizeDelta = new Vector2(900f, 120f);
            var title = titleGo.AddComponent<Text>();
            title.text = "狐跃林间";
            title.fontSize = 96;
            title.alignment = TextAnchor.MiddleCenter;
            title.color = Color.white;

            // 开始按钮
            var startBtn = CreateButton(panelGo.transform, "StartButton", "开始游戏", new Vector2(0f, -320f));
            // 退出按钮
            var quitBtn = CreateButton(panelGo.transform, "QuitButton", "退出", new Vector2(0f, -460f));

            var panel = rootGo.AddComponent<MainMenuPanel>();
            var so = new SerializedObject(panel);
            so.FindProperty("panelRoot").objectReferenceValue = panelGo;
            so.FindProperty("startButton").objectReferenceValue = startBtn;
            so.FindProperty("quitButton").objectReferenceValue = quitBtn;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>通关面板：标题 + 樱桃累计 + 回到主菜单按钮（组件挂在 PanelRoot 上，理由同上）。</summary>
        private static void CreateGameClearPanel(Transform canvas)
        {
            var rootGo = new GameObject("GameClearPanelRoot", typeof(RectTransform));
            rootGo.transform.SetParent(canvas, false);

            var panelGo = new GameObject("GameClearPanel", typeof(RectTransform));
            panelGo.transform.SetParent(rootGo.transform, false);
            var rt = (RectTransform)panelGo.transform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            var bg = panelGo.AddComponent<Image>();
            bg.color = new Color(0.16f, 0.28f, 0.22f, 0.92f);
            panelGo.SetActive(false); // GameClear 状态才显示

            var titleGo = new GameObject("Title", typeof(RectTransform));
            titleGo.transform.SetParent(panelGo.transform, false);
            var titleRt = (RectTransform)titleGo.transform;
            titleRt.anchorMin = new Vector2(0.5f, 1f);
            titleRt.anchorMax = new Vector2(0.5f, 1f);
            titleRt.pivot = new Vector2(0.5f, 1f);
            titleRt.anchoredPosition = new Vector2(0f, -200f);
            titleRt.sizeDelta = new Vector2(900f, 110f);
            var title = titleGo.AddComponent<Text>();
            title.text = "恭喜通关！";
            title.fontSize = 80;
            title.alignment = TextAnchor.MiddleCenter;
            title.color = Color.white;

            var labelGo = new GameObject("Summary", typeof(RectTransform));
            labelGo.transform.SetParent(panelGo.transform, false);
            var labelRt = (RectTransform)labelGo.transform;
            labelRt.anchorMin = new Vector2(0.5f, 1f);
            labelRt.anchorMax = new Vector2(0.5f, 1f);
            labelRt.pivot = new Vector2(0.5f, 1f);
            labelRt.anchoredPosition = new Vector2(0f, -340f);
            labelRt.sizeDelta = new Vector2(900f, 60f);
            var label = labelGo.AddComponent<Text>();
            label.fontSize = 40;
            label.alignment = TextAnchor.MiddleCenter;
            label.color = Color.white;

            var menuBtn = CreateButton(panelGo.transform, "MenuButton", "回到主菜单", new Vector2(0f, -460f));

            var panel = rootGo.AddComponent<GameClearPanel>();
            var so = new SerializedObject(panel);
            so.FindProperty("panelRoot").objectReferenceValue = panelGo;
            so.FindProperty("label").objectReferenceValue = label;
            so.FindProperty("menuButton").objectReferenceValue = menuBtn;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>uGUI 按钮（Button + legacy Text，锚定中上，anchoredY 定位）。</summary>
        private static Button CreateButton(Transform parent, string name, string text, Vector2 anchoredPosition)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = new Vector2(0.5f, 1f);
            rt.anchorMax = new Vector2(0.5f, 1f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = anchoredPosition;
            rt.sizeDelta = new Vector2(360f, 80f);
            var img = go.AddComponent<Image>();
            img.color = new Color(0.95f, 0.75f, 0.25f, 1f); // 暖黄按钮
            var btn = go.AddComponent<Button>();

            var labelGo = new GameObject("Label", typeof(RectTransform));
            labelGo.transform.SetParent(go.transform, false);
            var labelRt = (RectTransform)labelGo.transform;
            labelRt.anchorMin = Vector2.zero;
            labelRt.anchorMax = Vector2.one;
            labelRt.offsetMin = labelRt.offsetMax = Vector2.zero;
            var label = labelGo.AddComponent<Text>();
            label.text = text;
            label.fontSize = 40;
            label.alignment = TextAnchor.MiddleCenter;
            label.color = new Color(0.2f, 0.15f, 0.05f, 1f);
            return btn;
        }
    }
}
