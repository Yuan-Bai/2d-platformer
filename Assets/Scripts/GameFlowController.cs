using UnityEngine;
using UnityEngine.SceneManagement;
using Platformer.Cameras;
using Platformer.Levels;
using Platformer.Player;
using Platformer.UI;

namespace Platformer
{
    /// <summary>应用流程状态（GameFlowController 对外暴露）。</summary>
    public enum FlowState
    {
        Menu,       // 主菜单（Bootstrap 启动态）
        Playing,    // 关卡游玩
        LevelClear, // 过关过渡（输入冻结 + 提示 → 切关）
        GameClear,  // 全关卡通关（通关面板）
    }

    /// <summary>
    /// 游戏流程编排深模块（ADR-0009，窄接口大行为）：
    /// 对外只有 5 个入口（StartGame/ReturnToMenu/QuitGame/RegisterCherry/CompleteLevel）+ 状态与计数查询；
    /// 内部承载关卡有序列表、Additive 加载/卸载编排、玩家与相机重置、樱桃统计、切关时序。
    /// 取代 M3 的 LevelManager（场景链 + static 跨场景传递）：常驻于 00-Bootstrap，切关全程不销毁，
    /// 玩家对象与音乐连续（验收标准「玩家不重建、音乐不中断」的载体）。
    /// 计时一律时间戳（ADR-0005 纪律），不用协程。
    /// </summary>
    public sealed class GameFlowController : MonoBehaviour
    {
        public static GameFlowController Instance { get; private set; }

        /// <summary>关卡有序列表（Build Settings 中 01..04 的场景名；末项通关 → GameClear）。</summary>
        [SerializeField] private string[] levelSceneNames =
        {
            "01-Tutorial", "02-ForestPath", "03-SkyBridge", "04-ThornForest",
        };

        [SerializeField] private float exitDelaySeconds = 1.6f;
        [SerializeField] private string completeTextFormat = "本关樱桃 {0}/{1}";

        /// <summary>
        /// 关卡有序列表（Inspector 可配；代码/测试亦可注入——手工关卡扩展入口）。
        /// 空列表合法：CompleteLevel 后直接进入 GameClear（测试用，避免触发 LoadScene 打断测试运行器）。
        /// </summary>
        public string[] LevelSceneNames
        {
            get => levelSceneNames;
            set => levelSceneNames = value;
        }

        /// <summary>过关提示到切关的延时（测试可拉短）。</summary>
        public float ExitDelaySeconds
        {
            get => exitDelaySeconds;
            set => exitDelaySeconds = value;
        }

        public FlowState State { get; private set; } = FlowState.Menu;

        /// <summary>本关已收集樱桃（HUD 同步）。</summary>
        public int CollectedInLevel { get; private set; }

        /// <summary>跨关樱桃累计（原 LevelManager.TotalCollected static → 常驻实例字段，不再需要 static）。</summary>
        public int TotalCollected { get; private set; }

        /// <summary>本关樱桃总数（LevelConfig 提供，HUD 分母）。</summary>
        public int TotalInLevel => _totalInLevel;

        /// <summary>全游戏樱桃总数（通关面板分母；StartGame 起随关卡加载累加）。</summary>
        public int TotalInGame { get; private set; }

        /// <summary>是否暂停（Playing 态按 Esc 冻结 Time.timeScale；Time.timeScale 由本类独占管理）。</summary>
        public bool IsPaused { get; private set; }

        private int _levelIndex = -1; // 当前关卡在 levelSceneNames 中的索引（-1 = 未加载关卡）
        private int _totalInLevel;
        private bool _completing;
        private float _loadDeadline;
        private string _activeLevelScene;
        private AsyncOperation _loading;      // 进行中的 Additive 加载
        private int _pendingLevelIndex = -1;  // 加载完成后要进入的关卡索引
        private string _pendingLevelName;
        private InputReader _input;           // 常驻玩家输入（Start 缓存；暂停键消费）

        private void Awake()
        {
            if (Instance != null)
            {
                Destroy(gameObject); // 防御：重复实例（重复加载 Bootstrap 场景）
                return;
            }
            Instance = this;
        }

        private void Start()
        {
            _input = FindObjectOfType<InputReader>();
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void Update()
        {
            // 暂停键消费（Playing 态才可暂停；输入读取细节封装在 InputReader，本类只消费语义）
            if (State == FlowState.Playing && _input != null && _input.PausePressed)
            {
                if (IsPaused) ResumeGame();
                else PauseGame();
            }

            if (_completing && Time.realtimeSinceStartup >= _loadDeadline)
            {
                _completing = false;
                LoadNext();
            }

            // Additive 加载完成轮询（LoadScene 在 Play 中异步激活，场景对象 isDone 后才可用）
            if (_loading != null && _loading.isDone)
            {
                var op = _loading;
                _loading = null;
                FinishLevelLoad(_pendingLevelIndex, _pendingLevelName);
            }
        }

        // ==================== 玩家/菜单入口 ====================

        /// <summary>主菜单「开始游戏」：清空累计 → 加载第一关。</summary>
        public void StartGame()
        {
            ResumeGame(); // 防御：暂停中退出到菜单再开始时，timeScale 必须复位
            TotalCollected = 0;
            TotalInGame = 0;
            CollectedInLevel = 0;
            LoadLevel(0);
        }

        /// <summary>回到主菜单：卸载当前关卡场景（若已加载）→ Menu 状态。</summary>
        public void ReturnToMenu()
        {
            ResumeGame();
            UnloadLevel();
            CollectedInLevel = 0;
            CherryHud.Instance?.SetVisible(false); // M4b：菜单态隐藏 HUD
            State = FlowState.Menu;
        }

        public void QuitGame()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        // ==================== 暂停（M4b：Esc 暂停菜单） ====================

        /// <summary>暂停游戏（仅 Playing 态有效）：冻结时间刻度。菜单/过关态不可暂停。</summary>
        public void PauseGame()
        {
            if (State != FlowState.Playing || IsPaused) return;
            IsPaused = true;
            Time.timeScale = 0f;
        }

        /// <summary>恢复游戏：还原时间刻度。任何入口流转前必须先恢复（timeScale 独占管理不变量）。</summary>
        public void ResumeGame()
        {
            if (!IsPaused) return;
            IsPaused = false;
            Time.timeScale = 1f;
        }

        // ==================== 关卡内事件（机关层调用） ====================

        /// <summary>樱桃收集入口（Collectible 调用）：计数 + 累计 + HUD 同步。重复收集由 Collectible 自毁保证。</summary>
        public void RegisterCherry()
        {
            CollectedInLevel++;
            TotalCollected++;
            CherryHud.Instance?.SetCollected(CollectedInLevel, _totalInLevel);
        }

        /// <summary>过关入口（LevelExit 调用）：冻结输入 → 统计提示 → 延时切关。重复调用被忽略。</summary>
        public void CompleteLevel()
        {
            if (_completing || State != FlowState.Playing) return;
            ResumeGame(); // 防御：切关时序依赖真实时间推进（timeScale=0 会卡死延时）
            _completing = true;
            State = FlowState.LevelClear;
            SetInputEnabled(false);
            HintBar.Instance?.Show(string.Format(completeTextFormat, CollectedInLevel, _totalInLevel), 2.2f);
            _loadDeadline = Time.realtimeSinceStartup + exitDelaySeconds;
        }

        // ==================== 内部：切关编排 ====================

        private void LoadNext()
        {
            int next = _levelIndex + 1;
            if (next < levelSceneNames.Length)
                LoadLevel(next);
            else
                State = FlowState.GameClear; // 末关之后：通关面板（GameClearPanel 监听 State 显示）
        }

        /// <summary>
        /// 发起切关：卸载旧关（异步）+ 发起新关 Additive 异步加载。
        /// 加载完成由 Update 轮询（项目禁用协程纪律），完成后 FinishLevelLoad 收尾。
        /// </summary>
        private void LoadLevel(int index)
        {
            UnloadLevel();

            // 空列表/越界守卫（注释承诺的空列表合法）：直通通关面板——末关之后的语义
            if (index >= levelSceneNames.Length)
            {
                State = FlowState.GameClear;
                return;
            }

            string name = levelSceneNames[index];
            _pendingLevelIndex = index;
            _pendingLevelName = name;
            _loading = SceneManager.LoadSceneAsync(name, LoadSceneMode.Additive);
            if (_loading == null)
            {
                Debug.LogError(
                    $"GameFlowController: 场景 '{name}' 无法发起加载（不在 Build Settings？" +
                    "请先跑 Tools/Platformer/Build All Levels）", this);
                State = FlowState.Menu;
            }
        }

        /// <summary>Additive 加载完成后收尾：校验 → 重置玩家/相机 → 解冻输入 → Playing。</summary>
        private void FinishLevelLoad(int index, string name)
        {
            Scene scene = SceneManager.GetSceneByName(name);
            if (!scene.IsValid() || !scene.isLoaded)
            {
                Debug.LogError($"GameFlowController: 场景 '{name}' 加载失败", this);
                State = FlowState.Menu;
                return;
            }

            _levelIndex = index;
            _activeLevelScene = name;
            SceneManager.SetActiveScene(scene);

            // 关卡内对象按场景作用域查找（FindInScene）：异步卸载的旧关残留对象不干扰
            var spawn = FindInScene<SpawnPoint>(scene);
            var player = FindObjectOfType<PlayerBody>();
            if (player == null || spawn == null)
            {
                Debug.LogError(
                    "GameFlowController: 缺 Player 或 SpawnPoint（Player 应常驻于 00-Bootstrap）", this);
                State = FlowState.Menu;
                return;
            }

            var config = FindInScene<LevelConfig>(scene);
            _totalInLevel = config != null ? config.TotalCherries : 0;
            TotalInGame += _totalInLevel;
            CollectedInLevel = 0;
            CherryHud.Instance?.SetVisible(true); // M4b：进入关卡恢复 HUD（菜单态曾隐藏）
            CherryHud.Instance?.SetCollected(0, _totalInLevel);

            // 玩家重置到新关出生点：位置/速度/重生点/输入缓冲全清（复用死亡重生同一条重置路径）
            player.RespawnAt(spawn.transform.position);

            // 相机重绑：Follow 新关玩家 + Confiner 边界换新关 CameraBounds（换边界必须 InvalidateCache）
            var rig = FindObjectOfType<PlayerCameraRig>();
            var anchor = FindInScene<CameraBoundsAnchor>(scene);
            rig?.Bind(player.transform, anchor != null ? anchor.Bounds : null);

            SetInputEnabled(true);
            ResumeGame(); // 防御：暂停中不应触发切关，但保险起见进入新关前 timeScale 必为 1
            State = FlowState.Playing;
        }

        /// <summary>
        /// 卸载当前关卡场景（异步，API 非过时且物理回调外安全；调用点恒在 Update/公开入口，不在 trigger 内）。
        /// 旧关对象下一帧才销毁；关卡对象查找已按场景作用域隔离，不受残留影响。
        /// </summary>
        private void UnloadLevel()
        {
            if (string.IsNullOrEmpty(_activeLevelScene)) return;
            SceneManager.UnloadSceneAsync(_activeLevelScene);
            _activeLevelScene = null;
            _levelIndex = -1;
        }

        /// <summary>按场景作用域找组件（根对象递归）：不受其他场景残留对象干扰。</summary>
        private static T FindInScene<T>(Scene scene) where T : Component
        {
            foreach (var root in scene.GetRootGameObjects())
            {
                var c = root.GetComponentInChildren<T>();
                if (c != null) return c;
            }
            return null;
        }

        private void SetInputEnabled(bool enabled)
        {
            var player = FindObjectOfType<PlayerBody>();
            if (player == null) return;
            var input = player.GetComponent<InputReader>();
            if (input != null) input.enabled = enabled; // InputReader.OnDisable 归零输入（中性输入不变量）
        }
    }
}
