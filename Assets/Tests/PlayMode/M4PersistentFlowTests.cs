using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using Platformer.Player;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Platformer.Tests
{
    /// <summary>
    /// M4 常驻流程测试（ADR-0009）：Additive 切关 / 玩家与累计不重建 / 回主菜单。
    /// 走真实场景加载（夹具场景 TestLevelA/B 永久在 Build Settings，见 FlowTestSupport）。
    /// 玩家由测试场景装配（模拟常驻层对象），GameFlowController 加载关卡后按场景作用域重置它。
    /// </summary>
    public class M4PersistentFlowTests : InputTestFixture
    {
        private Keyboard _kb;
        private readonly List<GameObject> _spawned = new List<GameObject>();

        [SetUp]
        public override void Setup()
        {
            base.Setup();
            _kb = InputSystem.AddDevice<Keyboard>();
            _spawned.Clear();
        }

        [TearDown]
        public override void TearDown()
        {
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

        private GameFlowController CreateFlow(string[] levels)
        {
            var go = Track(new GameObject("GameFlowController"));
            var flow = go.AddComponent<GameFlowController>();
            flow.LevelSceneNames = levels;
            flow.ExitDelaySeconds = 0.1f;
            return flow;
        }

        /// <summary>StartGame：加载首关 → Playing；玩家重置到首关 SpawnPoint；本关樱桃总数与累计正确。</summary>
        [UnityTest]
        public IEnumerator StartGame_LoadsFirstLevel_ResetsPlayerAndCounters()
        {
            var playerGo = Track(PlayerTestScene.CreatePlayer(Vector3.zero));
            var flow = CreateFlow(new[] { FlowTestSupport.LevelA, FlowTestSupport.LevelB });

            flow.StartGame();
            yield return FlowTestSupport.WaitUntilState(flow, FlowState.Playing);
            yield return FlowTestSupport.WaitPhysics(); // transform 同步在物理步

            Assert.AreEqual(FlowState.Playing, flow.State);
            Assert.AreEqual(3, flow.TotalInLevel, "本关樱桃总数来自 LevelConfig(A)=3");
            Assert.AreEqual(3, flow.TotalInGame, "全游戏累计应累加首关 3");
            Assert.Greater(playerGo.transform.position.x, 9f, "玩家应重置到 A 关 SpawnPoint(10,-1.3)");
            Assert.IsTrue(SceneManager.GetSceneByName(FlowTestSupport.LevelA).isLoaded, "A 关场景应已 Additive 加载");
        }

        /// <summary>切关：CompleteLevel → 延时 → 卸载旧关、加载新关、玩家重置到新关、输入解冻、累计叠加。</summary>
        [UnityTest]
        public IEnumerator CompleteLevel_SwitchesToNextLevel_PlayerPersists()
        {
            var playerGo = Track(PlayerTestScene.CreatePlayer(Vector3.zero));
            var input = playerGo.GetComponent<InputReader>();
            var flow = CreateFlow(new[] { FlowTestSupport.LevelA, FlowTestSupport.LevelB });
            flow.StartGame();
            yield return FlowTestSupport.WaitUntilState(flow, FlowState.Playing);

            // 直接调用过关入口（门触发已由 M3LevelFlowTests 端到端覆盖）
            flow.CompleteLevel();
            Assert.AreEqual(FlowState.LevelClear, flow.State);
            Assert.IsFalse(input.enabled, "过关过渡期输入应冻结");

            yield return FlowTestSupport.WaitUntilState(flow, FlowState.Playing); // 延时到期 → 异步加载新关 → Playing
            yield return FlowTestSupport.WaitPhysics(); // transform 同步在物理步

            Assert.AreEqual(FlowState.Playing, flow.State, "切关完成后回到 Playing");
            Assert.IsTrue(input.enabled, "切关完成后输入应解冻");
            Assert.Greater(playerGo.transform.position.x, 19f, "玩家应重置到 B 关 SpawnPoint(20,-1.3)");
            Assert.AreEqual(5, flow.TotalInLevel, "新关樱桃总数来自 LevelConfig(B)=5");
            Assert.AreEqual(8, flow.TotalInGame, "全游戏累计应叠加 3+5");

            // 旧关卸载是异步的：多等几帧后断言已卸载、新关已加载
            for (int i = 0; i < 5; i++) yield return null;
            Assert.IsFalse(SceneManager.GetSceneByName(FlowTestSupport.LevelA).isLoaded, "旧关 A 应已卸载");
            Assert.IsTrue(SceneManager.GetSceneByName(FlowTestSupport.LevelB).isLoaded, "新关 B 应已加载");
        }

        /// <summary>ReturnToMenu：卸载当前关卡 → Menu 态（玩家对象本身不销毁，是常驻层）。</summary>
        [UnityTest]
        public IEnumerator ReturnToMenu_UnloadsLevel()
        {
            var playerGo = Track(PlayerTestScene.CreatePlayer(Vector3.zero));
            var flow = CreateFlow(new[] { FlowTestSupport.LevelA });
            flow.StartGame();
            yield return FlowTestSupport.WaitUntilState(flow, FlowState.Playing);
            Assert.AreEqual(FlowState.Playing, flow.State);

            flow.ReturnToMenu();

            Assert.AreEqual(FlowState.Menu, flow.State);
            Assert.IsTrue(playerGo != null, "玩家是常驻层对象，回菜单不销毁");
            for (int i = 0; i < 5; i++) yield return null; // 异步卸载完成
            Assert.IsFalse(SceneManager.GetSceneByName(FlowTestSupport.LevelA).isLoaded, "关卡场景应已卸载");
        }
    }
}
