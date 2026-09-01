using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using Platformer.Levels;
using Platformer.Player;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;

namespace Platformer.Tests
{
    /// <summary>
    /// M3 关卡流程回归测试（ADR-0009 接缝迁移）：樱桃收集计数 / 终点门冻结输入 / 末关语义。
    /// 原 LevelManager → 常驻 GameFlowController：每测试新建实例（累计自动清零，不再有 static 状态）。
    /// CompleteLevel 系列走真实流程（夹具场景 TestLevelA 进 Playing 态）。
    /// 沿用 M2 教训：_spawned 跟踪 + [TearDown] 兜底清理（断言失败中止协程时测试末尾清理不执行）。
    /// </summary>
    public class M3LevelFlowTests : InputTestFixture
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

        /// <summary>
        /// 空列表流程实例：CompleteLevel 的 LoadNext 只会进入 GameClear 而非 LoadScene
        /// （RegisterCherry 等无状态守卫的测试用，不触发真实加载）。
        /// </summary>
        private GameFlowController CreateFlow()
        {
            var go = Track(new GameObject("GameFlowController"));
            var flow = go.AddComponent<GameFlowController>();
            flow.LevelSceneNames = new string[0];
            return flow;
        }

        /// <summary>真实流程实例：挂夹具关卡列表（Playing 态由 StartGame 到达）。</summary>
        private GameFlowController CreateFlowWithFixtureLevels()
        {
            var go = Track(new GameObject("GameFlowController"));
            var flow = go.AddComponent<GameFlowController>();
            flow.LevelSceneNames = new[] { FlowTestSupport.LevelA };
            flow.ExitDelaySeconds = 0.1f;
            return flow;
        }

        [UnityTest]
        public IEnumerator Collectible_RegistersCountAndDestroys()
        {
            Track(PlayerTestScene.CreateGround(new Vector3(0f, -3f, 0f), new Vector2(10f, 1f)));
            var playerGo = Track(PlayerTestScene.CreatePlayer(new Vector3(0f, -1.3f, 0f)));
            var flow = CreateFlow();

            var cherry = Track(new GameObject("Cherry"));
            cherry.transform.position = new Vector3(0f, -1.3f, 0f);
            var col = cherry.AddComponent<BoxCollider2D>();
            col.size = Vector2.one;
            col.isTrigger = true;
            cherry.AddComponent<Collectible>();

            for (int i = 0; i < 5; i++) yield return new WaitForFixedUpdate();

            Assert.AreEqual(1, flow.CollectedInLevel, "樱桃收集应计数 +1");
            Assert.AreEqual(1, flow.TotalCollected, "跨关累计应 +1");
            Assert.IsTrue(cherry == null, "樱桃应自毁（销毁后可判 == null）");
        }

        [UnityTest]
        public IEnumerator LevelExit_CompleteFreezesInputButNotPhysics()
        {
            // 真实流程：StartGame 异步加载夹具关卡 → Playing；玩家被重置到 SpawnPoint(10,-1.3)
            var playerGo = Track(PlayerTestScene.CreatePlayer(Vector3.zero));
            var input = playerGo.GetComponent<InputReader>();
            var flow = CreateFlowWithFixtureLevels();
            flow.StartGame();
            yield return FlowTestSupport.WaitUntilState(flow, FlowState.Playing);
            yield return FlowTestSupport.WaitPhysics(); // transform 同步在物理步

            Assert.Greater(playerGo.transform.position.x, 9f, "玩家应被重置到夹具关卡出生点");

            // 门放在玩家当前位置：玩家瞬移即触发（RespawnAt 上抬 0.1m，仍在触发区内）
            var door = Track(new GameObject("Door"));
            door.transform.position = playerGo.transform.position;
            var col = door.AddComponent<BoxCollider2D>();
            col.size = new Vector2(1f, 2f);
            col.isTrigger = true;
            door.AddComponent<LevelExit>();

            for (int i = 0; i < 5; i++) yield return new WaitForFixedUpdate();

            Assert.IsFalse(input.enabled, "过关后应冻结玩家输入");
            Assert.AreEqual(0f, input.MoveAxis, "冻结输入应为中性：InputReader 禁用时归零（防残留按键持续奔跑）");
            Assert.AreEqual(FlowState.LevelClear, flow.State, "接触门后应先进入过关过渡态");
        }

        /// <summary>末关语义（ADR-0009）：列表耗尽 → 延时后进入 GameClear（不 LoadScene 新关卡）。</summary>
        [UnityTest]
        public IEnumerator CompleteLevel_LastLevel_TransitionsToGameClear()
        {
            var playerGo = Track(PlayerTestScene.CreatePlayer(Vector3.zero));
            var flow = CreateFlowWithFixtureLevels(); // 单关卡列表：TestLevelA 即末关
            flow.StartGame();
            yield return FlowTestSupport.WaitUntilState(flow, FlowState.Playing);
            yield return FlowTestSupport.WaitPhysics(); // transform 同步在物理步

            var door = Track(new GameObject("Door"));
            door.transform.position = playerGo.transform.position;
            var col = door.AddComponent<BoxCollider2D>();
            col.size = new Vector2(1f, 2f);
            col.isTrigger = true;
            door.AddComponent<LevelExit>();

            for (int i = 0; i < 5; i++) yield return new WaitForFixedUpdate();

            Assert.AreEqual(FlowState.LevelClear, flow.State, "接触门后应先进入过关过渡态");
            yield return new WaitForSecondsRealtime(0.5f); // 跨过延时（时间戳计时走 realtime）

            Assert.AreEqual(FlowState.GameClear, flow.State, "末关过后延时到期应进入通关态（不 LoadScene）");
        }
    }
}
