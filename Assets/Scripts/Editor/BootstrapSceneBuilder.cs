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
        private const string BackgroundMiddle = "Assets/Art/SunnyLand/environment/Background/middle.png";
        private const string FoxyIdleFrame = "Assets/Art/SunnyLand/Characters/Foxy/idle/sprites/f-01.png";

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

            // 常驻 Canvas：HUD + 提示 + 主菜单 + 暂停 + 通关面板
            var canvas = LevelKit.CreateHudCanvas();
            LevelKit.CreateHud(canvas);
            CreateMenuPanel(canvas.transform);
            CreatePausePanel(canvas.transform);
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
        /// 主菜单面板（M4b 视觉版）：森林背景图 + 深绿遮罩、描边标题 + 副标题、狐狸立绘、
        /// 开始/退出按钮（hover 变色）、音量滑条、操作提示。
        /// 组件挂在常驻激活的 PanelRoot 上、控制子面板显隐——组件自身不能在初始 inactive 的对象上
        /// （SetActive(false) 的对象 Update 不执行，面板将永远无法自行显示）。
        /// </summary>
        private static void CreateMenuPanel(Transform canvas)
        {
            var rootGo = new GameObject("MainMenuPanelRoot", typeof(RectTransform));
            rootGo.transform.SetParent(canvas, false);
            StretchFull(rootGo.transform); // PanelRoot 必须全屏，否则子面板的 0..1 锚定缩到 100×100 中心块

            var panelGo = new GameObject("MainMenuPanel", typeof(RectTransform));
            panelGo.transform.SetParent(rootGo.transform, false);
            var rt = (RectTransform)panelGo.transform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;

            // 森林背景图（middle 层才是树林；back 层是海天，不用于菜单。全屏拉伸 + 深绿遮罩压暗保证文字可读）
            var bgImg = panelGo.AddComponent<Image>();
            bgImg.sprite = LevelKit.LoadSprite(BackgroundMiddle);
            bgImg.type = Image.Type.Simple;
            bgImg.color = new Color(0.75f, 0.85f, 0.8f, 1f); // 轻微提亮偏绿，贴合森林调
            var dim = CreateRect("Dim", panelGo.transform);
            var dimImg = dim.gameObject.AddComponent<Image>();
            dimImg.color = new Color(0.06f, 0.14f, 0.10f, 0.55f);

            // 标题 + 描边
            var titleGo = new GameObject("Title", typeof(RectTransform));
            titleGo.transform.SetParent(panelGo.transform, false);
            var titleRt = (RectTransform)titleGo.transform;
            titleRt.anchorMin = new Vector2(0.5f, 1f);
            titleRt.anchorMax = new Vector2(0.5f, 1f);
            titleRt.pivot = new Vector2(0.5f, 1f);
            titleRt.anchoredPosition = new Vector2(0f, -100f);
            titleRt.sizeDelta = new Vector2(900f, 130f);
            var title = titleGo.AddComponent<Text>();
            title.text = "狐跃林间";
            title.fontSize = 110;
            title.alignment = TextAnchor.MiddleCenter;
            title.color = new Color(1f, 0.92f, 0.55f);
            var outline = titleGo.AddComponent<Outline>();
            outline.effectColor = new Color(0.1f, 0.2f, 0.12f, 1f);
            outline.effectDistance = new Vector2(4f, -4f);

            // 副标题
            var subGo = new GameObject("Subtitle", typeof(RectTransform));
            subGo.transform.SetParent(panelGo.transform, false);
            var subRt = (RectTransform)subGo.transform;
            subRt.anchorMin = new Vector2(0.5f, 1f);
            subRt.anchorMax = new Vector2(0.5f, 1f);
            subRt.pivot = new Vector2(0.5f, 1f);
            subRt.anchoredPosition = new Vector2(0f, -235f);
            subRt.sizeDelta = new Vector2(700f, 50f);
            var sub = subGo.AddComponent<Text>();
            sub.text = "Foxy's Forest";
            sub.fontSize = 34;
            sub.alignment = TextAnchor.MiddleCenter;
            sub.color = new Color(1f, 1f, 1f, 0.75f);

            // 狐狸立绘（idle 第一帧放大）
            var foxGo = new GameObject("Fox", typeof(RectTransform));
            foxGo.transform.SetParent(panelGo.transform, false);
            var foxRt = (RectTransform)foxGo.transform;
            foxRt.anchorMin = new Vector2(0.5f, 1f);
            foxRt.anchorMax = new Vector2(0.5f, 1f);
            foxRt.pivot = new Vector2(0.5f, 1f);
            foxRt.anchoredPosition = new Vector2(-520f, -420f);
            foxRt.sizeDelta = new Vector2(320f, 360f);
            var fox = foxGo.AddComponent<Image>();
            fox.sprite = LevelKit.LoadSprite(FoxyIdleFrame);
            fox.color = Color.white;

            // 按钮 + 音量 + 提示
            var startBtn = CreateButton(panelGo.transform, "StartButton", "开始游戏", new Vector2(0f, -390f));
            var quitBtn = CreateButton(panelGo.transform, "QuitButton", "退出游戏", new Vector2(0f, -510f));
            CreateVolumeSlider(panelGo.transform, new Vector2(0f, -640f));
            CreateHintText(panelGo.transform, new Vector2(0f, 80f),
                "A / D 移动    空格 跳跃（长按跳更高）    S 下穿平台");

            var panel = rootGo.AddComponent<MainMenuPanel>();
            var so = new SerializedObject(panel);
            so.FindProperty("panelRoot").objectReferenceValue = panelGo;
            so.FindProperty("startButton").objectReferenceValue = startBtn;
            so.FindProperty("quitButton").objectReferenceValue = quitBtn;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>暂停面板（M4b）：深色遮罩 + 标题 + 继续/回主菜单按钮 + 音量滑条。组件挂 PanelRoot。</summary>
        private static void CreatePausePanel(Transform canvas)
        {
            var rootGo = new GameObject("PauseMenuRoot", typeof(RectTransform));
            rootGo.transform.SetParent(canvas, false);
            StretchFull(rootGo.transform); // 同上：PanelRoot 全屏

            var panelGo = new GameObject("PauseMenu", typeof(RectTransform));
            panelGo.transform.SetParent(rootGo.transform, false);
            var rt = (RectTransform)panelGo.transform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            var bg = panelGo.AddComponent<Image>();
            bg.color = new Color(0.04f, 0.08f, 0.06f, 0.82f); // 深色遮罩（时停画面压暗）
            panelGo.SetActive(false); // 暂停时才显示（组件在 Root 上，不受此影响）

            var titleGo = new GameObject("Title", typeof(RectTransform));
            titleGo.transform.SetParent(panelGo.transform, false);
            var titleRt = (RectTransform)titleGo.transform;
            titleRt.anchorMin = new Vector2(0.5f, 1f);
            titleRt.anchorMax = new Vector2(0.5f, 1f);
            titleRt.pivot = new Vector2(0.5f, 1f);
            titleRt.anchoredPosition = new Vector2(0f, -300f);
            titleRt.sizeDelta = new Vector2(600f, 110f);
            var title = titleGo.AddComponent<Text>();
            title.text = "暂停";
            title.fontSize = 84;
            title.alignment = TextAnchor.MiddleCenter;
            title.color = Color.white;

            var resumeBtn = CreateButton(panelGo.transform, "ResumeButton", "继续游戏", new Vector2(0f, -470f));
            var menuBtn = CreateButton(panelGo.transform, "MenuButton", "回到主菜单", new Vector2(0f, -590f));
            CreateVolumeSlider(panelGo.transform, new Vector2(0f, -730f));

            var panel = rootGo.AddComponent<PauseMenu>();
            var so = new SerializedObject(panel);
            so.FindProperty("panelRoot").objectReferenceValue = panelGo;
            so.FindProperty("resumeButton").objectReferenceValue = resumeBtn;
            so.FindProperty("menuButton").objectReferenceValue = menuBtn;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>音量滑条（M4b）：Slider + 百分比标签，值存 PlayerPrefs（M4c AudioManager 消费）。</summary>
        private static void CreateVolumeSlider(Transform parent, Vector2 anchoredPosition)
        {
            var root = CreateRect("Volume", parent);
            root.anchorMin = new Vector2(0.5f, 1f);
            root.anchorMax = new Vector2(0.5f, 1f);
            root.pivot = new Vector2(0.5f, 0.5f);
            root.anchoredPosition = anchoredPosition;
            root.sizeDelta = new Vector2(520f, 40f);

            var labelGo = new GameObject("Label", typeof(RectTransform));
            labelGo.transform.SetParent(root, false);
            var labelRt = (RectTransform)labelGo.transform;
            labelRt.anchorMin = new Vector2(0f, 0f);
            labelRt.anchorMax = new Vector2(0.32f, 1f);
            labelRt.offsetMin = labelRt.offsetMax = Vector2.zero;
            var label = labelGo.AddComponent<Text>();
            label.fontSize = 30;
            label.alignment = TextAnchor.MiddleLeft;
            label.color = Color.white;
            label.text = "音量 80%";

            // 滑条：背景 + 填充 + 手柄（Slider 标准结构）
            var sliderBg = CreateRect("Slider", root);
            sliderBg.anchorMin = new Vector2(0.34f, 0f);
            sliderBg.anchorMax = new Vector2(1f, 1f);
            sliderBg.offsetMin = sliderBg.offsetMax = Vector2.zero;
            var bgImg = sliderBg.gameObject.AddComponent<Image>();
            bgImg.color = new Color(0f, 0f, 0f, 0.5f);
            var slider = sliderBg.gameObject.AddComponent<Slider>();

            var fill = CreateRect("Fill", sliderBg);
            fill.anchorMin = Vector2.zero;
            fill.anchorMax = Vector2.one;
            fill.offsetMin = fill.offsetMax = Vector2.zero;
            var fillImg = fill.gameObject.AddComponent<Image>();
            fillImg.color = new Color(0.95f, 0.75f, 0.25f, 1f);

            var handle = CreateRect("Handle", sliderBg);
            handle.sizeDelta = new Vector2(24f, 56f);
            var handleImg = handle.gameObject.AddComponent<Image>();
            handleImg.color = Color.white;

            slider.fillRect = fill;
            slider.handleRect = handle;
            slider.targetGraphic = handleImg;

            var control = root.gameObject.AddComponent<VolumeSlider>();
            var so = new SerializedObject(control);
            so.FindProperty("slider").objectReferenceValue = slider;
            so.FindProperty("label").objectReferenceValue = label;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>底部操作提示小字。</summary>
        private static void CreateHintText(Transform parent, Vector2 anchoredPosition, string text)
        {
            var go = new GameObject("HintText", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = new Vector2(0.5f, 0f);
            rt.anchorMax = new Vector2(0.5f, 0f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = anchoredPosition;
            rt.sizeDelta = new Vector2(1100f, 40f);
            var label = go.AddComponent<Text>();
            label.text = text;
            label.fontSize = 26;
            label.alignment = TextAnchor.MiddleCenter;
            label.color = new Color(1f, 1f, 1f, 0.7f);
        }

        /// <summary>全屏拉伸 RectTransform（默认铺满父级）。</summary>
        private static RectTransform CreateRect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            return rt;
        }

        /// <summary>把已有对象拉伸至铺满父级（供 PanelRoot 使用）。</summary>
        private static void StretchFull(Transform t)
        {
            var rt = (RectTransform)t;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
        }

        /// <summary>通关面板：标题 + 樱桃累计 + 回到主菜单按钮（组件挂在 PanelRoot 上，理由同上）。</summary>
        private static void CreateGameClearPanel(Transform canvas)
        {
            var rootGo = new GameObject("GameClearPanelRoot", typeof(RectTransform));
            rootGo.transform.SetParent(canvas, false);
            StretchFull(rootGo.transform); // 同上：PanelRoot 全屏

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

        /// <summary>uGUI 按钮（Button + legacy Text，锚定中上，anchoredY 定位；hover/按下变色）。</summary>
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
            var colors = btn.colors;
            colors.normalColor = new Color(0.95f, 0.75f, 0.25f, 1f);
            colors.highlightedColor = new Color(1f, 0.88f, 0.45f, 1f);
            colors.pressedColor = new Color(0.72f, 0.55f, 0.15f, 1f);
            btn.colors = colors;

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
