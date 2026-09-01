using UnityEngine;
using UnityEngine.UI;

namespace Platformer.UI
{
    /// <summary>
    /// 音量滑条（M4b UI 部分）：绑定 uGUI Slider，值存 PlayerPrefs（键 MasterVolume）。
    /// M4c 的 AudioManager 启动时读取同键并应用、播放中按滑条实时生效。
    /// 主菜单与暂停面板各挂一个实例（同键共享，天然同步）。
    /// </summary>
    public sealed class VolumeSlider : MonoBehaviour
    {
        public const string MasterVolumeKey = "MasterVolume";
        public const float DefaultVolume = 0.8f;

        [SerializeField] private Slider slider;
        [SerializeField] private Text label;

        private void Start()
        {
            if (slider == null) return;
            slider.minValue = 0f;
            slider.maxValue = 1f;
            slider.value = PlayerPrefs.GetFloat(MasterVolumeKey, DefaultVolume);
            UpdateLabel();
            slider.onValueChanged.AddListener(OnValueChanged);
        }

        private void OnValueChanged(float value)
        {
            PlayerPrefs.SetFloat(MasterVolumeKey, value);
            UpdateLabel();
        }

        private void UpdateLabel()
        {
            if (label != null)
                label.text = $"音量 {Mathf.RoundToInt(slider.value * 100f)}%";
        }
    }
}
