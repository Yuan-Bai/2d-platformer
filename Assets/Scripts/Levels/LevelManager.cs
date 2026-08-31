using UnityEngine;
using UnityEngine.SceneManagement;
using Platformer.Player;
using Platformer.UI;

namespace Platformer.Levels
{
    /// <summary>
    /// 关卡流程管理（ADR-0007，窄接口深行为）：
    /// 樱桃计数（RegisterCherry）→ HUD 同步；终点门（CompleteLevel）→ 冻结输入 → 过关统计提示 → 延时加载下一场景。
    /// 跨场景累计用 static 字段传递（通关画面读 TotalCollected）；M4 接主菜单时只需把场景 0 换成主菜单，语义不变。
    /// 计时一律时间戳（ADR-0005 修订纪律），不用协程，免疫打断冻结。
    /// </summary>
    public sealed class LevelManager : MonoBehaviour
    {
        [SerializeField] private PlayerBody player;
        [SerializeField] private int totalCherries;
        [SerializeField] private string nextSceneName;
        [SerializeField] private float exitDelaySeconds = 1.6f;
        [SerializeField] private string completeTextFormat = "本关樱桃 {0}/{1}";

        /// <summary>跨场景樱桃累计（通关画面显示）。</summary>
        public static int TotalCollected { get; private set; }

        public int Collected { get; private set; }
        public int TotalInLevel => totalCherries;

        private bool _completing;
        private float _loadDeadline;

        /// <summary>测试与重新开始入口：清空跨场景累计。</summary>
        public static void ResetProgress() => TotalCollected = 0;

        /// <summary>
        /// 生成器/测试装配入口：一次性配置全部字段。
        /// PlayMode 测试拿不到 SerializedObject（UnityEditor 命名空间），故走公开配置方法而非序列化字段直写。
        /// </summary>
        public void Configure(PlayerBody playerBody, int totalCherriesInLevel, string nextScene, float exitDelay = 1.6f)
        {
            player = playerBody;
            totalCherries = totalCherriesInLevel;
            nextSceneName = nextScene;
            exitDelaySeconds = exitDelay;
        }

        /// <summary>樱桃收集入口（Collectible 调用）：计数 + 累计 + HUD 同步。重复收集由 Collectible 自毁保证。</summary>
        public void RegisterCherry()
        {
            Collected++;
            TotalCollected++;
            CherryHud.Instance?.SetCollected(Collected, totalCherries);
        }

        /// <summary>过关入口（LevelExit 调用）：冻结输入 → 统计提示 → 延时加载下一场景。重复调用被忽略。</summary>
        public void CompleteLevel()
        {
            if (_completing) return;
            _completing = true;

            if (player != null)
            {
                var input = player.GetComponent<InputReader>();
                if (input != null) input.enabled = false;
            }

            HintBar.Instance?.Show(string.Format(completeTextFormat, Collected, totalCherries), 2.2f);
            _loadDeadline = Time.realtimeSinceStartup + exitDelaySeconds;
        }

        private void Update()
        {
            if (_completing && Time.realtimeSinceStartup >= _loadDeadline)
            {
                _completing = false;
                LoadNext();
            }
        }

        private void LoadNext()
        {
            if (!string.IsNullOrEmpty(nextSceneName))
            {
                SceneManager.LoadScene(nextSceneName);
                return;
            }

            // 回退：编辑器里场景未进 Build Settings 时 buildIndex 为 -1，只能提示不能跳转
            int current = SceneManager.GetActiveScene().buildIndex;
            if (current >= 0 && current + 1 < SceneManager.sceneCountInBuildSettings)
            {
                SceneManager.LoadScene(current + 1);
                return;
            }
            Debug.LogWarning("LevelManager: nextSceneName 未配置且无法按 buildIndex+1 跳转（编辑器直开单场景？）。", this);
        }
    }
}
