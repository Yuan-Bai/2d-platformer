using UnityEngine;
using UnityEngine.UI;

namespace Platformer.UI
{
    /// <summary>
    /// 樱桃 HUD（M3 极简方案）：左上角计数「x/y」。由 GameFlowController 在收集/切关时推送。
    /// M4b 起跟随流程状态显隐：Menu 态隐藏（菜单界面不显示 0/0），其余状态显示。
    /// 显隐由 GameFlowController 驱动（SetVisible）——本组件挂在 HUD 根自身，
    /// 不能自行 SetActive(false)（会停掉自身 Update，无法自恢复）。
    /// 文本用 legacy Text（系统字体回退可渲染中文；TMP 默认字体不含 CJK 字形，M4 可换 TMP + CJK SDF 字体资产）。
    /// 场景内至多一个实例；测试场景里可为空（GameFlowController 对 null 兜底）。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CherryHud : MonoBehaviour
    {
        public static CherryHud Instance { get; private set; }

        [SerializeField] private Text label;
        [SerializeField] private string format = "{0}/{1}";

        private void Awake() => Instance = this;

        private void Start()
        {
            // 初始菜单态（Bootstrap 首帧）隐藏 HUD；进入关卡由 GameFlowController.FinishLevelLoad 恢复
            var flow = GameFlowController.Instance;
            if (flow != null && flow.State == FlowState.Menu) SetVisible(false);
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        public void SetVisible(bool visible) => gameObject.SetActive(visible);

        public void SetCollected(int collected, int total)
        {
            if (label != null) label.text = string.Format(format, collected, total);
        }
    }
}
