using UnityEngine;
using UnityEngine.UI;

namespace Platformer.UI
{
    /// <summary>
    /// 主菜单面板（ADR-0009，常驻 00-Bootstrap 的 Canvas）：FlowState.Menu 时显示，其他状态隐藏。
    /// 按钮行为委托 GameFlowController（StartGame / QuitGame），面板不持有任何流程逻辑。
    /// </summary>
    public sealed class MainMenuPanel : MonoBehaviour
    {
        [SerializeField] private GameObject panelRoot;
        [SerializeField] private Button startButton;
        [SerializeField] private Button quitButton;

        private void Start()
        {
            if (startButton != null) startButton.onClick.AddListener(OnStartClicked);
            if (quitButton != null) quitButton.onClick.AddListener(OnQuitClicked);
        }

        private void OnStartClicked()
        {
            AudioManager.Instance?.PlayClick();
            GameFlowController.Instance?.StartGame();
        }

        private void OnQuitClicked()
        {
            AudioManager.Instance?.PlayClick();
            GameFlowController.Instance?.QuitGame();
        }

        private void Update()
        {
            var flow = GameFlowController.Instance;
            bool show = flow != null && flow.State == FlowState.Menu;
            if (panelRoot != null) panelRoot.SetActive(show);
        }
    }
}
