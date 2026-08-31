using UnityEngine;
using UnityEngine.UI;

namespace Platformer.UI
{
    /// <summary>
    /// 樱桃 HUD（M3 极简方案）：左上角计数「x/y」。由 LevelManager 在收集时推送。
    /// 文本用 legacy Text（系统字体回退可渲染中文；TMP 默认字体不含 CJK 字形，M4 可换 TMP + CJK SDF 字体资产）。
    /// 场景内至多一个实例；测试场景里可为空（LevelManager 对 null 兜底）。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CherryHud : MonoBehaviour
    {
        public static CherryHud Instance { get; private set; }

        [SerializeField] private Text label;
        [SerializeField] private string format = "{0}/{1}";

        private void Awake() => Instance = this;

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        public void SetCollected(int collected, int total)
        {
            if (label != null) label.text = string.Format(format, collected, total);
        }
    }
}
