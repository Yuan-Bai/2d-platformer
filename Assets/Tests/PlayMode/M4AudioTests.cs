using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using Platformer.Player;
using Platformer.UI;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Platformer.Tests
{
    /// <summary>
    /// M4c 音频测试：BGM 随 FlowState 自动切曲（Menu→菜单曲、Playing→关卡曲、GameClear→通关曲、
    /// LevelClear 保持）+ 音量滑条事件实时生效到 AudioListener.volume。
    /// 测试自建 AudioManager 与 clip（运行时 AudioClip.Create），不依赖 00-Bootstrap 场景。
    /// </summary>
    public class M4AudioTests : InputTestFixture
    {
        private Keyboard _kb;
        private readonly List<GameObject> _spawned = new List<GameObject>();

        [SetUp]
        public override void Setup()
        {
            base.Setup();
            _kb = InputSystem.AddDevice<Keyboard>();
            _spawned.Clear();
            AudioListener.volume = 1f;
            PlayerPrefs.DeleteKey(VolumeSlider.MasterVolumeKey);

            // PlayMode 测试跑在当前打开的 00-Bootstrap 场景里，其常驻 AudioManager 已占用静态 Instance：
            // 测试自建的实例会在 Awake 被防御性 Destroy（无 AudioSource → 后续断言越界）。
            // 先接管：销毁场景实例（OnDestroy 复位 Instance），测试再自建独立实例。
            var existing = Object.FindObjectOfType<AudioManager>();
            if (existing != null) Object.DestroyImmediate(existing.gameObject);
        }

        [TearDown]
        public override void TearDown()
        {
            Time.timeScale = 1f;
            AudioListener.volume = 1f;
            PlayerPrefs.DeleteKey(VolumeSlider.MasterVolumeKey);
            foreach (var go in _spawned)
                if (go != null) Object.DestroyImmediate(go);
            _spawned.Clear();
            if (_kb != null && _kb.added) InputSystem.RemoveDevice(_kb);
            FlowTestSupport.UnloadFixtureLevels();
            base.TearDown();
        }

        private GameObject Track(GameObject go)
        {
            _spawned.Add(go);
            return go;
        }

        private static AudioClip MakeClip(string name) => AudioClip.Create(name, 44100, 1, 44100, false);

        /// <summary>
        /// BGM 切曲：Menu→菜单曲；StartGame→Playing→关卡曲；空关卡列表 StartGame 直通 GameClear→通关曲。
        /// AudioManager 内部两个 AudioSource：索引 0 是音乐源（AddComponent 顺序保证）。
        /// </summary>
        [UnityTest]
        public IEnumerator Music_SwitchesWithFlowState()
        {
            Track(PlayerTestScene.CreatePlayer(Vector3.zero));
            var flowGo = Track(new GameObject("GameFlowController"));
            var flow = flowGo.AddComponent<GameFlowController>();
            flow.LevelSceneNames = new[] { FlowTestSupport.LevelA };
            flow.ExitDelaySeconds = 0.1f;

            var audioGo = Track(new GameObject("AudioManager"));
            var audio = audioGo.AddComponent<AudioManager>();
            var menuClip = MakeClip("menu");
            var levelClip = MakeClip("level");
            var clearClip = MakeClip("clear");
            // 私有序列化字段用反射注入（测试程序集不可用 SerializedObject，且避免生产 API 污染）
            var type = typeof(AudioManager);
            type.GetField("menuMusic", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).SetValue(audio, menuClip);
            type.GetField("levelMusic", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).SetValue(audio, levelClip);
            type.GetField("gameClearMusic", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).SetValue(audio, clearClip);

            yield return null; // 一帧让 Update 应用 Menu 曲
            var musicSource = audioGo.GetComponents<AudioSource>()[0];
            Assert.AreSame(menuClip, musicSource.clip, "Menu 态应播菜单曲");

            flow.StartGame();
            yield return FlowTestSupport.WaitUntilState(flow, FlowState.Playing);
            yield return null;
            Assert.AreSame(levelClip, musicSource.clip, "Playing 态应播关卡曲");

            // LevelClear：过关过渡期保持关卡曲（不切、不重播）
            var played = musicSource.clip;
            flow.CompleteLevel();
            Assert.AreEqual(FlowState.LevelClear, flow.State);
            yield return null;
            Assert.AreSame(played, musicSource.clip, "LevelClear 应保持当前曲");

            // 空关卡列表：StartGame 直通 GameClear → 通关曲
            flow.ReturnToMenu();
            yield return null;
            flow.LevelSceneNames = new string[0];
            flow.StartGame();
            yield return FlowTestSupport.WaitUntilState(flow, FlowState.GameClear);
            yield return null;
            Assert.AreSame(clearClip, musicSource.clip, "GameClear 态应播通关曲");
        }

        /// <summary>
        /// 音量事件流：VolumeSlider 拖动（onValueChanged）→ 静态事件广播 → AudioManager → AudioListener.volume。
        /// 覆盖用户报告的「暂停面板调音量回主菜单同步」的底层链路（AudioListener 全局唯一，跨面板天然一致）。
        /// </summary>
        [UnityTest]
        public IEnumerator VolumeEvent_AppliesToAudioListener()
        {
            Track(new GameObject("AudioManager")).AddComponent<AudioManager>();

            // 真实 VolumeSlider + Slider（面板初始 inactive 场景的 OnEnable 时序）
            var vsGo = Track(new GameObject("VolumeSlider"));
            vsGo.SetActive(false);
            var vs = vsGo.AddComponent<VolumeSlider>();
            var slider = Track(new GameObject("Slider")).AddComponent<Slider>();
            typeof(VolumeSlider)
                .GetField("slider", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                .SetValue(vs, slider);
            vsGo.SetActive(true); // OnEnable：订阅静态事件 + 读 PlayerPrefs 设值

            slider.onValueChanged.Invoke(0.3f); // 模拟用户拖动
            Assert.AreEqual(0.3f, AudioListener.volume, 0.001f, "拖动应实时生效到全局音量");
            Assert.AreEqual(0.3f, PlayerPrefs.GetFloat(VolumeSlider.MasterVolumeKey), "拖动应持久化");

            slider.onValueChanged.Invoke(0.65f);
            Assert.AreEqual(0.65f, AudioListener.volume, 0.001f, "再次拖动应更新音量");

            yield return null;
        }
    }
}
