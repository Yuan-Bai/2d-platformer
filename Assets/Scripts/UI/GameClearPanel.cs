using UnityEngine;
using UnityEngine.UI;

namespace Platformer.UI
{
    /// <summary>
    /// 通关面板（ADR-0009，常驻 00-Bootstrap 的 Canvas，取代 M3 的 05 场景 GameClearScreen）：
    /// FlowState.GameClear 时显示全流程樱桃累计，「回到主菜单」委托 GameFlowController.ReturnToMenu。
    /// 文本用 legacy Text（系统字体回退可渲染中文）。
    /// </summary>
    public sealed class GameClearPanel : MonoBehaviour
    {
        [SerializeField] private GameObject panelRoot;
        [SerializeField] private Text label;
        [SerializeField] private Button menuButton;
        [SerializeField] private string format = "樱桃累计 {0}/{1}";

        private void Start()
        {
            if (menuButton != null) menuButton.onClick.AddListener(OnMenuClicked);
        }

        private void OnMenuClicked() => GameFlowController.Instance?.ReturnToMenu();

        private void Update()
        {
            var flow = GameFlowController.Instance;
            bool show = flow != null && flow.State == FlowState.GameClear;
            if (panelRoot != null) panelRoot.SetActive(show);
            if (show && label != null)
                label.text = string.Format(format, flow.TotalCollected, flow.TotalInGame);
        }
    }
}
