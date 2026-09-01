using System;
using UnityEngine;
using UnityEngine.UI;

namespace Platformer.UI
{
    /// <summary>
    /// 音量滑条（M4b UI 部分）：绑定 uGUI Slider，值存 PlayerPrefs（键 MasterVolume）。
    /// 主菜单与暂停面板各挂一个实例——通过静态事件 <see cref="VolumeChanged"/> 跨实例同步：
    /// 任一实例拖动即广播，其余实例 SetValueWithoutNotify 跟随（不触发 onValueChanged，无循环）。
    /// 初始化在 OnEnable（而非 Start）：面板初始 inactive 时 Start 永不执行（PanelRoot 教训的延伸），
    /// 且每次面板显示都重读 PlayerPrefs——暂停面板调的音量，回主菜单时主菜单滑条自动同步。
    /// M4c 的 AudioManager 订阅同事件实现音量实时生效。
    /// </summary>
    public sealed class VolumeSlider : MonoBehaviour
    {
        public const string MasterVolumeKey = "MasterVolume";
        public const float DefaultVolume = 0.8f;

        /// <summary>音量变化广播（拖动的实例发起；所有实例与 AudioManager 消费）。</summary>
        public static event Action<float> VolumeChanged;

        [SerializeField] private Slider slider;
        [SerializeField] private Text label;

        private void OnEnable()
        {
            if (slider == null) return;
            slider.minValue = 0f;
            slider.maxValue = 1f;
            slider.SetValueWithoutNotify(PlayerPrefs.GetFloat(MasterVolumeKey, DefaultVolume));
            UpdateLabel();
            slider.onValueChanged.AddListener(OnValueChanged);
            VolumeChanged += OnVolumeChangedExternal;
        }

        private void OnDisable()
        {
            if (slider == null) return;
            slider.onValueChanged.RemoveListener(OnValueChanged);
            VolumeChanged -= OnVolumeChangedExternal;
        }

        private void OnValueChanged(float value)
        {
            PlayerPrefs.SetFloat(MasterVolumeKey, value);
            UpdateLabel();
            VolumeChanged?.Invoke(value);
        }

        /// <summary>其他实例（或代码）发起的音量变化：同步本滑条但不回发事件。</summary>
        private void OnVolumeChangedExternal(float value)
        {
            if (slider == null || Mathf.Approximately(slider.value, value)) return;
            slider.SetValueWithoutNotify(value);
            UpdateLabel();
        }

        private void UpdateLabel()
        {
            if (label != null)
                label.text = $"音量 {Mathf.RoundToInt(slider.value * 100f)}%";
        }
    }
}
