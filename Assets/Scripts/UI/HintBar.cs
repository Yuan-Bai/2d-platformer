using UnityEngine;
using UnityEngine.UI;

namespace Platformer.UI
{
    /// <summary>
    /// 屏幕下方提示条（M3 教学提示）：Show → 快速淡入 → 停留 → 淡出。
    /// 全时间戳计时（ADR-0005 修订纪律），不用协程；Canvas/CanvasGroup/文本由生成器装配。
    /// 文本用 legacy Text（系统字体回退可渲染中文；TMP 默认字体不含 CJK 字形，M4 可换 TMP + CJK SDF 字体资产）。
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CanvasGroup))]
    public sealed class HintBar : MonoBehaviour
    {
        public static HintBar Instance { get; private set; }

        [SerializeField] private Text label;
        [SerializeField] private float holdSeconds = 4f;
        [SerializeField] private float fadeSeconds = 0.4f;

        private CanvasGroup _group;
        private float _showAt;
        private float _holdEnd;
        private bool _shown;

        private void Awake()
        {
            Instance = this;
            _group = GetComponent<CanvasGroup>();
            _group.alpha = 0f;
            _group.interactable = false;
            _group.blocksRaycasts = false;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        /// <summary>显示提示；duration 为空则用默认停留时长。重复调用重置计时。</summary>
        public void Show(string text, float? duration = null)
        {
            if (label != null) label.text = text;
            _showAt = Time.realtimeSinceStartup;
            _holdEnd = _showAt + (duration ?? holdSeconds);
            _shown = true;
        }

        private void Update()
        {
            if (!_shown || _group == null) return;
            float now = Time.realtimeSinceStartup;

            if (now < _holdEnd)
            {
                _group.alpha = Mathf.Min(1f, (now - _showAt) / fadeSeconds);
                return;
            }

            float t = (now - _holdEnd) / fadeSeconds;
            _group.alpha = Mathf.Max(0f, 1f - t);
            if (t >= 1f)
            {
                _shown = false;
                _group.alpha = 0f;
            }
        }
    }
}
