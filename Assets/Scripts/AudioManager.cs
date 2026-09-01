using UnityEngine;
using Platformer.UI;

namespace Platformer
{
    /// <summary>
    /// 音频管理器（M4c，常驻 00-Bootstrap 场景）：
    /// 全项目音频的单一事实源——所有音乐/音效 clip 集中在此，机关与表现层只调语义方法（零配置）。
    /// - BGM：按 <see cref="GameFlowController.State"/> 自动切曲（Menu→菜单曲、Playing→关卡曲、
    ///   GameClear→通关曲；LevelClear 保持当前曲不打断）。循环播放。
    /// - 音量：订阅 <see cref="VolumeSlider.VolumeChanged"/> → AudioListener.volume（全局混音系数，
    ///   音乐与音效一起缩放）；初始值读 PlayerPrefs（VolumeSlider.MasterVolumeKey）。
    /// - SFX：单一 AudioSource + PlayOneShot（支持同帧叠音）。
    /// - 切场景不中断：对象与两个 AudioSource 都在常驻场景，不随关卡 Additive 卸载。
    /// 音频不受 Time.timeScale 影响：暂停（timeScale=0）时音乐继续——主流暂停行为。
    /// </summary>
    public sealed class AudioManager : MonoBehaviour
    {
        public static AudioManager Instance { get; private set; }

        [Header("音乐（3 曲：菜单/关卡/通关）")]
        [SerializeField] private AudioClip menuMusic;
        [SerializeField] private AudioClip levelMusic;
        [SerializeField] private AudioClip gameClearMusic;

        [Header("音效（6 个语义槽）")]
        [SerializeField] private AudioClip jumpSfx;
        [SerializeField] private float jumpVolume = 0.7f; // 试听反馈：跳跃偏响，降至 0.7
        [SerializeField] private AudioClip cherrySfx;
        [SerializeField] private AudioClip bumperSfx;
        [SerializeField] private AudioClip deathSfx;
        [SerializeField] private AudioClip doorSfx;
        [SerializeField] private AudioClip clickSfx;

        private AudioSource _musicSource;
        private AudioSource _sfxSource;
        private FlowState _musicState;
        private bool _musicStateKnown;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject); // 防御重复（场景重建或误加第二实例）
                return;
            }
            Instance = this;

            _musicSource = gameObject.AddComponent<AudioSource>();
            _musicSource.playOnAwake = false;
            _musicSource.loop = true;

            _sfxSource = gameObject.AddComponent<AudioSource>();
            _sfxSource.playOnAwake = false;

            // 初始音量：直接读 PlayerPrefs（滑条可能尚未 OnEnable，主动对齐一次）
            AudioListener.volume = PlayerPrefs.GetFloat(VolumeSlider.MasterVolumeKey, VolumeSlider.DefaultVolume);
        }

        private void OnEnable() => VolumeSlider.VolumeChanged += OnVolumeChanged;
        private void OnDisable() => VolumeSlider.VolumeChanged -= OnVolumeChanged;

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void OnVolumeChanged(float value) => AudioListener.volume = value;

        private void Update()
        {
            var flow = GameFlowController.Instance;
            if (flow == null) return;

            var state = flow.State;
            if (_musicStateKnown && state == _musicState) return;
            _musicStateKnown = true;
            _musicState = state;

            // LevelClear：保持当前曲（终点门音效已提示过关，音乐不打断）
            if (state == FlowState.LevelClear) return;

            var clip = state == FlowState.Menu ? menuMusic
                     : state == FlowState.Playing ? levelMusic
                     : gameClearMusic;
            if (clip == null || clip == _musicSource.clip) return;
            _musicSource.clip = clip;
            _musicSource.Play();
        }

        // ==================== SFX 语义入口（调用方零配置，不持 clip 引用） ====================

        /// <summary>跳跃（PlayerVisuals 检测到进入 Jump 状态时触发）。音量用 jumpVolume（试听降 0.7）。</summary>
        public void PlayJump() => PlaySfx(jumpSfx, jumpVolume);

        /// <summary>樱桃收集（Collectible 触发）。</summary>
        public void PlayCherry() => PlaySfx(cherrySfx);

        /// <summary>弹簧弹射（Bumper 触发）。</summary>
        public void PlayBumper() => PlaySfx(bumperSfx);

        /// <summary>死亡（Hazard 触发）。</summary>
        public void PlayDeath() => PlaySfx(deathSfx);

        /// <summary>终点门过关（LevelExit 触发）。</summary>
        public void PlayDoor() => PlaySfx(doorSfx);

        /// <summary>UI 按钮点击（主菜单/暂停面板按钮）。</summary>
        public void PlayClick() => PlaySfx(clickSfx);

        private void PlaySfx(AudioClip clip, float volumeScale = 1f)
        {
            if (clip != null) _sfxSource.PlayOneShot(clip, volumeScale);
        }
    }
}
