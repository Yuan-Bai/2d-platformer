using UnityEngine;

namespace Platformer.Levels
{
    /// <summary>
    /// 关卡数据组件（ADR-0009）：承载关卡级静态数据（樱桃总数等）。
    /// 兼作编辑器防护：直开关卡场景进 Play 时（无常驻 GameFlowController → 无玩家/相机/HUD）
    /// 打警告提示从 00-Bootstrap 启动。
    /// 取代 M3 LevelManager 的序列化配置职责（流程编排已并入常驻 GameFlowController）。
    /// </summary>
    public sealed class LevelConfig : MonoBehaviour
    {
        [SerializeField] private int totalCherries;

        public int TotalCherries => totalCherries;

        /// <summary>生成器/测试装配入口。</summary>
        public void Configure(int cherries) => totalCherries = cherries;

        private void Awake()
        {
            if (GameFlowController.Instance == null)
                Debug.LogWarning(
                    "LevelConfig: 当前无常驻 GameFlowController（玩家/相机/HUD 缺失）。" +
                    "编辑器调试请用 Tools/Platformer/Play From Bootstrap 启动。", this);
        }
    }
}
