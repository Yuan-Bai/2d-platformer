using UnityEngine;
using UnityEngine.UI;

namespace Platformer.UI
{
    /// <summary>
    /// 暂停菜单面板（M4b，常驻 00-Bootstrap 的 Canvas）：Playing 且 IsPaused 时显示。
    /// 纯视图：暂停切换由 GameFlowController 消费 InputReader.PausePressed（流程权威），
    /// 本组件只做显隐与按钮委托（继续 → ResumeGame；回主菜单 → ResumeGame + ReturnToMenu）。
    /// 组件挂在常驻激活的 PanelRoot 上（初始 inactive 对象上的组件 Update 不执行——PanelRoot 模式）。
    /// </summary>
    public sealed class PauseMenu : MonoBehaviour
    {
        [SerializeField] private GameObject panelRoot;
        [SerializeField] private Button resumeButton;
        [SerializeField] private Button menuButton;

        private void Start()
        {
            if (resumeButton != null) resumeButton.onClick.AddListener(OnResumeClicked);
            if (menuButton != null) menuButton.onClick.AddListener(OnMenuClicked);
        }

        private void OnResumeClicked()
        {
            AudioManager.Instance?.PlayClick();
            GameFlowController.Instance?.ResumeGame();
        }

        private void OnMenuClicked()
        {
            AudioManager.Instance?.PlayClick();
            var flow = GameFlowController.Instance;
            if (flow == null) return;
            flow.ResumeGame();
            flow.ReturnToMenu();
        }

        private void Update()
        {
            var flow = GameFlowController.Instance;
            bool show = flow != null && flow.State == FlowState.Playing && flow.IsPaused;
            if (panelRoot != null) panelRoot.SetActive(show);
        }
    }
}
