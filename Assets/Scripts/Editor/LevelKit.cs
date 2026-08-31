using System;
using System.Collections.Generic;
using System.Linq;
using Cinemachine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Tilemaps;
using UnityEngine.UI;
using Platformer;
using Platformer.Cameras;
using Platformer.Levels;
using Platformer.Mechanics;
using Platformer.Player;
using Platformer.UI;

namespace Platformer.EditorTools
{
    /// <summary>
    /// 关卡乐高块（M3 混合分工支撑，单一事实源）：
    /// - 工厂方法：LevelBuilder（JSON 生成）与 Build Prefabs（预制体）共用同一套组件装配——改一处全项目生效。
    /// - Tools/Platformer/Build Prefabs：生成 Assets/Prefabs/ 下 9 个预制体，供手工搭关拖拽。
    /// - Tools/Platformer/New Level Scaffold：空场景脚手架（相机/VCam/边界/背景/HUD/空地形含碰撞/LevelManager）。
    ///   手工流程：Tile Palette 刷地形 → 拖预制体 → Inspector 配置 Player VCam 的 Follow Target 与 LevelManager → Build Settings。
    /// </summary>
    public static class LevelKit
    {
        // 素材路径（Sunny Land，PPU16）
        public const string SprBack = "Assets/Art/SunnyLand/environment/Background/back.png";
        public const string SprMiddle = "Assets/Art/SunnyLand/environment/Background/middle.png";
        public const string SprSign = "Assets/Art/SunnyLand/environment/Props/sign.png";
        public const string SprDoor = "Assets/Art/SunnyLand/environment/Props/door.png";
        public const string SprSpikes = "Assets/Art/SunnyLand/environment/Props/spikes-top.png";
        public const string SprPlatform = "Assets/Art/SunnyLand/environment/Props/platform-long.png";
        public const string SprBlock = "Assets/Art/SunnyLand/environment/Props/block.png";
        public const string SprCherry = "Assets/Art/SunnyLand/Misc/Sunnyland items/Sprites/cherry/cherry-1.png";

        /// <summary>预制体资产目录（Build Prefabs 产出、BuildAll 实例化消费）。</summary>
        private const string PrefabsFolder = "Assets/Prefabs";

        // ==================== 工厂方法 ====================

        /// <summary>
        /// 触发器机关统一归 Interactable 层（bug 修复）：地面检测的 BoxCast 会命中 Default 层的
        /// trigger（m_QueriesHitTriggers=1）→ 站在提示牌/樱桃旁被误判 grounded → 空中无限连跳。
        /// PlayerBody.CheckGrounded 排除本层后，trigger 不再参与地面判定；TriggerEnter2D 检测
        /// 基于碰撞矩阵（全碰撞）与组件（TryGetComponent），不受层影响。
        /// </summary>
        private static void SetInteractableLayer(GameObject go)
        {
            int layer = LayerMask.NameToLayer("Interactable");
            if (layer >= 0) go.layer = layer;
        }

        public static Camera CreateMainCamera()
        {
            var camGo = new GameObject("Main Camera");
            camGo.tag = "MainCamera";
            var cam = camGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.45f, 0.7f, 0.87f); // 天空色（截图后按 back.png 顶部色调微调）
            camGo.AddComponent<AudioListener>();
            camGo.AddComponent<CinemachineBrain>();
            camGo.transform.position = new Vector3(0f, 0f, -10f);
            return cam;
        }

        /// <summary>玩家（ADR-0008：新盒 0.5×1.6 + Foxy 帧动画）。position = 出生点中心。</summary>
        public static GameObject CreatePlayer(Vector3 position)
        {
            var go = new GameObject("Player");
            go.transform.position = position;
            // Player 专属层（ADR-0005）：生成期即设层（编辑器可见、无需等 Awake 纠正）；
            // PlayerBody.Awake 里的运行时设置保留为兜底（兼容旧场景/手工摆放）。
            int playerLayer = LayerMask.NameToLayer("Player");
            if (playerLayer >= 0) go.layer = playerLayer;
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sortingOrder = 20;
            var col = go.AddComponent<BoxCollider2D>();
            col.size = new Vector2(0.5f, 1.6f); // 视觉 2m 高，盒略小保宽容
            go.AddComponent<Rigidbody2D>();
            go.AddComponent<InputReader>();
            go.AddComponent<PlayerBody>();

            var visuals = go.AddComponent<PlayerVisuals>();
            var idleFrames = LoadFrames("idle", 4);
            var runFrames = LoadFrames("run", 6);
            var jumpFrames = LoadFrames("jump", 2);
            visuals.Configure(idleFrames, runFrames, jumpFrames);
            // 编辑模式可见性：PlayerVisuals 的 sprite 在运行时 Update 才赋值，
            // 编辑模式下 Scene 视图无精灵 → 无法目视对位 collider。生成期直接设首帧。
            if (idleFrames.Length > 0) sr.sprite = idleFrames[0];
            return go;
        }

        /// <summary>弹簧（触发器 = 根位置下半 0.5m；视觉 = 子物体，压缩动画只缩子物体）。</summary>
        public static GameObject CreateBumper(Vector3 position)
        {
            var go = new GameObject("Bumper");
            go.transform.position = position;
            SetInteractableLayer(go);
            var trigger = go.AddComponent<BoxCollider2D>();
            trigger.isTrigger = true;
            trigger.size = new Vector2(0.9f, 0.5f);
            trigger.offset = new Vector2(0f, -0.25f); // 脚踩即触发
            go.AddComponent<Bumper>();

            var visual = go.AddComponent<BumperVisual>();
            var so = new SerializedObject(visual);
            so.FindProperty("defaultSprite").objectReferenceValue = LoadSprite(SprBlock);
            so.ApplyModifiedPropertiesWithoutUndo();

            var vgo = new GameObject("Visual");
            vgo.transform.SetParent(go.transform, false);
            vgo.transform.localScale = new Vector3(0.9f, 0.9f, 1f);
            var sr = vgo.AddComponent<SpriteRenderer>();
            sr.sprite = LoadSprite(SprBlock);
            sr.color = new Color(1f, 0.85f, 0.3f);
            sr.sortingOrder = 20;
            return go;
        }

        /// <summary>移动平台（2×0.5m；waypoints 相对初始位置，null = 静止）。</summary>
        public static GameObject CreateMovingPlatform(Vector3 position, MovingPlatformDef def)
        {
            var go = new GameObject("MovingPlatform");
            go.transform.position = position;
            go.transform.localScale = new Vector3(1f, 0.5f, 1f);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = LoadSprite(SprPlatform);
            sr.sortingOrder = 20;
            var col2d = go.AddComponent<BoxCollider2D>();
            col2d.size = new Vector2(2f, 1f); // sprite 2×1m，碰撞随 scale(1,0.5) → 世界 (2,0.5)，与视觉一致
            var mover = go.AddComponent<MovingPlatform>(); // RequireComponent 自动补 Rigidbody2D
            mover.waypoints = def?.waypoints?.Select(w => new Vector2(w.x, w.y)).ToArray() ?? Array.Empty<Vector2>();
            mover.speed = def != null ? def.speed : 2f;
            return go;
        }

        /// <summary>尖刺（基座贴 position.y；触发器略小于视觉，宽容）。</summary>
        public static GameObject CreateSpikes(Vector3 position)
        {
            var go = new GameObject("Spikes");
            go.transform.position = position;
            SetInteractableLayer(go);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = LoadSprite(SprSpikes);
            sr.sortingOrder = 20;
            var trigger = go.AddComponent<BoxCollider2D>();
            trigger.isTrigger = true;
            trigger.size = new Vector2(0.7f, 0.35f);
            go.AddComponent<Hazard>();
            return go;
        }

        /// <summary>重生点（绿色木牌 + 1×2 触发器）。</summary>
        public static GameObject CreateCheckpoint(Vector3 position)
        {
            var go = new GameObject("Checkpoint");
            go.transform.position = position;
            SetInteractableLayer(go);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = LoadSprite(SprSign);
            sr.color = new Color(0.3f, 0.9f, 0.4f);
            sr.sortingOrder = 20;
            var trigger = go.AddComponent<BoxCollider2D>();
            trigger.isTrigger = true;
            trigger.size = new Vector2(1f, 2f);
            go.AddComponent<Checkpoint>();
            return go;
        }

        /// <summary>樱桃收集物（0.6 缩放 + 0.5×0.5 触发器）。</summary>
        public static GameObject CreateCherry(Vector3 position)
        {
            var go = new GameObject("Cherry");
            go.transform.position = position;
            SetInteractableLayer(go);
            go.transform.localScale = new Vector3(0.6f, 0.6f, 1f);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = LoadSprite(SprCherry);
            sr.sortingOrder = 20;
            var trigger = go.AddComponent<BoxCollider2D>();
            trigger.isTrigger = true;
            trigger.size = new Vector2(0.5f, 0.5f);
            go.AddComponent<Collectible>();
            return go;
        }

        /// <summary>终点门（基座贴 position.y；触发器覆盖 [base, base+1.8]）。</summary>
        public static GameObject CreateDoor(Vector3 position)
        {
            var go = new GameObject("Door");
            go.transform.position = position;
            SetInteractableLayer(go);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = LoadSprite(SprDoor);
            sr.sortingOrder = 20;
            var trigger = go.AddComponent<BoxCollider2D>();
            trigger.isTrigger = true;
            trigger.size = new Vector2(1f, 1.8f);
            trigger.offset = new Vector2(0f, -0.13f);
            go.AddComponent<LevelExit>();
            return go;
        }

        /// <summary>教学路牌（message 在 Inspector 可改；3×2.5 触发区围绕路牌）。</summary>
        public static GameObject CreateSign(Vector3 position, string message)
        {
            var go = new GameObject("Sign");
            go.transform.position = position;
            SetInteractableLayer(go);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = LoadSprite(SprSign);
            sr.sortingOrder = 20;
            var trigger = go.AddComponent<BoxCollider2D>();
            trigger.isTrigger = true;
            trigger.size = new Vector3(3f, 2.5f);
            trigger.offset = new Vector2(0f, 0.62f);
            var tutorial = go.AddComponent<TutorialTrigger>();
            var so = new SerializedObject(tutorial);
            so.FindProperty("message").stringValue = message;
            so.ApplyModifiedPropertiesWithoutUndo();
            return go;
        }

        /// <summary>单向平台（长 length 米、厚 0.5m；position = 平台中心；OneWayPlatform 组件自动配置层与 effector）。</summary>
        public static GameObject CreateOneWayPlatform(Vector3 position, float length)
        {
            var go = new GameObject("OneWayPlatform");
            go.transform.position = position;
            // sprite 原始 2×1m：scale.x = length/2 才得到"长 length 米"的平台
            //（此前 scale.x=length 导致平台比设计长一倍；col.size 同理必须匹配 sprite 原尺寸）
            go.transform.localScale = new Vector3(length * 0.5f, 0.5f, 1f);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = LoadSprite(SprPlatform);
            sr.color = new Color(0.45f, 0.85f, 0.95f, 0.8f);
            sr.sortingOrder = 20;
            var col = go.AddComponent<BoxCollider2D>();
            col.size = new Vector2(2f, 1f); // 随 scale 缩放为世界 (length, 0.5)，与视觉一致
            go.AddComponent<OneWayPlatform>(); // Reset() 自动配置层与 PlatformEffector2D
            return go;
        }

        /// <summary>空地形脚手架：Grid + Tilemap + TilemapCollider2D→Composite（静态），供 Tile Palette 直接刷图。</summary>
        public static GameObject CreateTerrainRig()
        {
            var gridGo = new GameObject("Grid");
            gridGo.AddComponent<Grid>();
            var groundGo = new GameObject("Ground");
            groundGo.transform.SetParent(gridGo.transform, false);
            groundGo.AddComponent<Tilemap>();
            var renderer = groundGo.AddComponent<TilemapRenderer>();
            renderer.sortingOrder = 10;
            var rb = groundGo.AddComponent<Rigidbody2D>();
            rb.bodyType = RigidbodyType2D.Static;
            var tileCollider = groundGo.AddComponent<TilemapCollider2D>();
            tileCollider.usedByComposite = true;
            var composite = groundGo.AddComponent<CompositeCollider2D>();
            composite.geometryType = CompositeCollider2D.GeometryType.Polygons;
            return gridGo;
        }

        /// <summary>
        /// VCam + 跟随封装 + 相机边界（地图范围 + 2m 边距的矩形 PolygonCollider2D，设计文档 §4）。
        /// 不用 tilemap Composite 当 confiner 边界：场景加载瞬间复合几何可能未生成 → NaN 相机（实测）。
        /// follow 允许 null（脚手架场景由用户在 Inspector 补拖 Player）。
        /// </summary>
        public static void CreateCameraRig(Transform follow, float width, float height)
        {
            ConfigureCameraRig(CreateCameraRigCore(), follow, width, height);
        }

        /// <summary>
        /// CameraRig 核心（预制体内容）：VCam + Body 全参数 + PlayerCameraRig + Confiner。
        /// Follow 与 BoundingShape2D 是场景级引用（指向场景 Player / 场景 CameraBounds），
        /// 预制体里留空，由 <see cref="ConfigureCameraRig"/> 在实例化后补。
        /// </summary>
        public static GameObject CreateCameraRigCore()
        {
            var vcamGo = new GameObject("Player VCam");
            var vcam = vcamGo.AddComponent<CinemachineVirtualCamera>();

            // Body pipeline 生成期装配（编辑模式即可见可调，此前运行时 AddComponent 导致
            // Inspector 里 VCam 是空壳：无 Body、无参数面板，调相机必须改代码）。
            var framing = vcam.AddCinemachineComponent<CinemachineFramingTransposer>();
            framing.m_DeadZoneWidth = 0.3f;
            framing.m_DeadZoneHeight = 0.5f;
            framing.m_SoftZoneWidth = 0.6f;
            framing.m_SoftZoneHeight = 0.6f;
            framing.m_XDamping = 0.35f;
            framing.m_YDamping = 0.35f;
            // LookAhead（视野前瞻）：相机沿玩家移动方向提前偏移、停下后回中。
            // 只做水平前瞻（IgnoreY），竖直前瞻会干扰跳跃落地视线。
            framing.m_LookaheadTime = 0.35f;
            framing.m_LookaheadSmoothing = 10f;
            framing.m_LookaheadIgnoreY = true;

            // PlayerCameraRig 保留为手工搭关的兜底封装（followTarget 补设 + 无 body 时补默认）
            vcamGo.AddComponent<PlayerCameraRig>();

            vcamGo.AddComponent<CinemachineConfiner2D>();
            return vcamGo;
        }

        /// <summary>
        /// 配置 CameraRig 实例：Follow + 场景级 CameraBounds + Confiner 引用。
        /// CameraBounds 不进预制体（尺寸随关卡 width/height 变化）。
        /// </summary>
        public static void ConfigureCameraRig(GameObject vcamGo, Transform follow, float width, float height)
        {
            var vcam = vcamGo.GetComponent<CinemachineVirtualCamera>();
            vcam.Follow = follow; // 生成期即设 Follow，Inspector 直接可见可调（此前运行时 Awake 才设）

            var rig = vcamGo.GetComponent<PlayerCameraRig>();
            var so = new SerializedObject(rig);
            so.FindProperty("followTarget").objectReferenceValue = follow;
            so.ApplyModifiedPropertiesWithoutUndo();

            var conf = vcamGo.GetComponent<CinemachineConfiner2D>();
            var boundsGo = new GameObject("CameraBounds");
            var poly = boundsGo.AddComponent<PolygonCollider2D>();
            // confiner 只读几何、不做物理（bug 修复）：
            // 非 trigger 的实心多边形会把出生在地图内的玩家刚体推出场景（"启动被弹出"根因）。
            // → isTrigger（无推挤）+ Ignore Raycast 层（地面探测排除、语义"纯几何"）。
            poly.isTrigger = true;
            int ignoreRaycast = LayerMask.NameToLayer("Ignore Raycast");
            if (ignoreRaycast >= 0) boundsGo.layer = ignoreRaycast;
            const float margin = 2f;
            poly.SetPath(0, new[]
            {
                new Vector2(-margin, -margin),
                new Vector2(width + margin, -margin),
                new Vector2(width + margin, height + margin),
                new Vector2(-margin, height + margin),
            });
            conf.m_BoundingShape2D = poly;
        }

        /// <summary>
        /// 从 Player.prefab 实例化（预制体化：改 prefab 资产 → 全场景同步）。
        /// prefab 缺失时退回代码装配并告警（新环境未跑 Build Prefabs 的保底）。
        /// </summary>
        public static GameObject InstantiatePlayer(Vector3 position)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{PrefabsFolder}/Player.prefab");
            if (prefab == null)
            {
                Debug.LogWarning("LevelKit: 缺 Player.prefab —— 先跑 Tools/Platformer/Build Prefabs；本次退回代码装配");
                return CreatePlayer(position);
            }
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            go.transform.position = position;
            return go;
        }

        /// <summary>
        /// 从 CameraRig.prefab 实例化 + 配置场景级引用（Follow / CameraBounds / Confiner）。
        /// prefab 缺失时退回代码装配并告警。
        /// </summary>
        public static GameObject InstantiateCameraRig(Transform follow, float width, float height)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{PrefabsFolder}/CameraRig.prefab");
            GameObject vcamGo;
            if (prefab == null)
            {
                Debug.LogWarning("LevelKit: 缺 CameraRig.prefab —— 先跑 Tools/Platformer/Build Prefabs；本次退回代码装配");
                vcamGo = CreateCameraRigCore();
            }
            else
            {
                vcamGo = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            }
            ConfigureCameraRig(vcamGo, follow, width, height);
            return vcamGo;
        }

        public static void CreateParallax(Camera cam)
        {
            var go = new GameObject("Parallax");
            var para = go.AddComponent<ParallaxBackground>();
            para.Configure(cam, new[]
            {
                new ParallaxBackground.Layer
                {
                    sprite = LoadSprite(SprBack), scrollFactor = 0.15f, y = 1f, sortingOrder = 0,
                },
                new ParallaxBackground.Layer
                {
                    sprite = LoadSprite(SprMiddle), scrollFactor = 0.4f, y = -1f, sortingOrder = 5,
                },
            });
        }

        public static void CreateHud()
        {
            var canvasGo = new GameObject("Canvas");
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            // --- CherryHud（左上角） ---
            var hudGo = new GameObject("CherryHud", typeof(RectTransform));
            hudGo.transform.SetParent(canvasGo.transform, false);
            var hudRt = (RectTransform)hudGo.transform;
            hudRt.anchorMin = hudRt.anchorMax = new Vector2(0f, 1f);
            hudRt.pivot = new Vector2(0f, 1f);
            hudRt.anchoredPosition = new Vector2(16f, -16f);
            hudRt.sizeDelta = new Vector2(220f, 44f);
            var hud = hudGo.AddComponent<CherryHud>();

            var iconGo = new GameObject("Icon", typeof(RectTransform));
            iconGo.transform.SetParent(hudGo.transform, false);
            var iconRt = (RectTransform)iconGo.transform;
            iconRt.anchorMin = iconRt.anchorMax = new Vector2(0f, 0.5f);
            iconRt.pivot = new Vector2(0f, 0.5f);
            iconRt.sizeDelta = new Vector2(40f, 40f);
            var icon = iconGo.AddComponent<Image>();
            icon.sprite = LoadSprite(SprCherry);

            var labelGo = new GameObject("Label", typeof(RectTransform));
            labelGo.transform.SetParent(hudGo.transform, false);
            var labelRt = (RectTransform)labelGo.transform;
            labelRt.anchorMin = Vector2.zero;
            labelRt.anchorMax = Vector2.one;
            labelRt.offsetMin = new Vector2(46f, 0f);
            labelRt.offsetMax = Vector2.zero;
            var hudLabel = labelGo.AddComponent<Text>();
            hudLabel.fontSize = 28;
            hudLabel.alignment = TextAnchor.MiddleLeft;
            hudLabel.color = Color.white;
            hudLabel.text = "0/0";
            var hudSo = new SerializedObject(hud);
            hudSo.FindProperty("label").objectReferenceValue = hudLabel;
            hudSo.ApplyModifiedPropertiesWithoutUndo();

            // --- HintBar（底部居中） ---
            var hintGo = new GameObject("HintBar", typeof(RectTransform));
            hintGo.transform.SetParent(canvasGo.transform, false);
            var hintRt = (RectTransform)hintGo.transform;
            hintRt.anchorMin = hintRt.anchorMax = new Vector2(0.5f, 0f);
            hintRt.pivot = new Vector2(0.5f, 0f);
            hintRt.anchoredPosition = new Vector2(0f, 70f);
            hintRt.sizeDelta = new Vector2(900f, 64f);
            var group = hintGo.AddComponent<CanvasGroup>();
            group.alpha = 0f;
            var bg = hintGo.AddComponent<Image>();
            bg.color = new Color(0f, 0f, 0f, 0.55f);
            var hint = hintGo.AddComponent<HintBar>();

            var hintLabelGo = new GameObject("Label", typeof(RectTransform));
            hintLabelGo.transform.SetParent(hintGo.transform, false);
            var hintLabelRt = (RectTransform)hintLabelGo.transform;
            hintLabelRt.anchorMin = Vector2.zero;
            hintLabelRt.anchorMax = Vector2.one;
            var hintLabel = hintLabelGo.AddComponent<Text>();
            hintLabel.fontSize = 28;
            hintLabel.alignment = TextAnchor.MiddleCenter;
            hintLabel.color = Color.white;
            var hintSo = new SerializedObject(hint);
            hintSo.FindProperty("label").objectReferenceValue = hintLabel;
            hintSo.ApplyModifiedPropertiesWithoutUndo();
        }

        public static Sprite LoadSprite(string path) =>
            AssetDatabase.LoadAssetAtPath<Sprite>(path);

        public static Sprite[] LoadFrames(string state, int count)
        {
            var list = new List<Sprite>(count);
            for (int i = 1; i <= count; i++)
            {
                var s = LoadSprite($"Assets/Art/SunnyLand/Characters/Foxy/{state}/sprites/f-0{i}.png");
                if (s != null) list.Add(s);
            }
            return list.ToArray();
        }

        // ==================== 菜单：预制体 ====================

        [MenuItem("Tools/Platformer/Build Prefabs")]
        public static void BuildPrefabs()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            if (!AssetDatabase.IsValidFolder("Assets/Prefabs"))
                AssetDatabase.CreateFolder("Assets", "Prefabs");

            SavePrefab("Player", CreatePlayer(Vector3.zero));
            SavePrefab("CameraRig", CreateCameraRigCore());
            SavePrefab("Bumper", CreateBumper(Vector3.zero));
            SavePrefab("MovingPlatform", CreateMovingPlatform(Vector3.zero, null));
            SavePrefab("Spikes", CreateSpikes(Vector3.zero));
            SavePrefab("Checkpoint", CreateCheckpoint(Vector3.zero));
            SavePrefab("Cherry", CreateCherry(Vector3.zero));
            SavePrefab("Door", CreateDoor(Vector3.zero));
            SavePrefab("Sign", CreateSign(Vector3.zero, ""));
            SavePrefab("OneWayPlatform", CreateOneWayPlatform(Vector3.zero, 4f));

            AssetDatabase.SaveAssets();
            Debug.Log("预制体已生成 → Assets/Prefabs/（Player/CameraRig/Bumper/MovingPlatform/Spikes/Checkpoint/Cherry/Door/Sign/OneWayPlatform）");
        }

        private static void SavePrefab(string name, GameObject go)
        {
            PrefabUtility.SaveAsPrefabAsset(go, $"Assets/Prefabs/{name}.prefab");
            UnityEngine.Object.DestroyImmediate(go);
        }

        // ==================== 菜单：手工搭关脚手架 ====================

        [MenuItem("Tools/Platformer/New Level Scaffold")]
        public static void NewLevelScaffold()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var camera = CreateMainCamera();
            CreateTerrainRig();
            InstantiateCameraRig(null, 60f, 20f); // Follow Target 由用户在 Inspector 拖入 Player
            CreateParallax(camera);
            CreateHud();
            new GameObject("LevelManager").AddComponent<LevelManager>(); // Inspector 配置 player/totalCherries/nextSceneName
            new GameObject("GameBootstrap").AddComponent<GameBootstrap>();

            if (!AssetDatabase.IsValidFolder("Assets/Scenes/Levels"))
                AssetDatabase.CreateFolder("Assets/Scenes", "Levels");
            EditorSceneManager.SaveScene(scene, "Assets/Scenes/Levels/Custom.unity");

            Debug.Log("脚手架已生成 Assets/Scenes/Levels/Custom.unity。手工流程：\n" +
                      " 1) Window > 2D > Tile Palette：新建调色板，把 Assets/Tiles 的 tileset_0~3 拖入；选中 Ground(Tilemap) 刷地形\n" +
                      " 2) 从 Assets/Prefabs 拖机关/樱桃/门/玩家进场景（玩家摆出生点）\n" +
                      " 3) Inspector：Player VCam 的 Follow Target 拖入 Player；LevelManager 填 player/totalCherries/nextSceneName\n" +
                      " 4) File > Build Settings 把本场景加入列表");
        }
    }
}
