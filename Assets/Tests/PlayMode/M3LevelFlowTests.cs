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
    /// M3 关卡流程回归测试（ADR-0007）：樱桃收集计数 / 终点门冻结输入。
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
            LevelManager.ResetProgress();
        }

        [TearDown]
        public override void TearDown()
        {
            foreach (var go in _spawned)
                if (go != null) Object.DestroyImmediate(go);
            _spawned.Clear();
            if (_kb != null && _kb.added) InputSystem.RemoveDevice(_kb);
            LevelManager.ResetProgress();
            base.TearDown();
        }

        private GameObject Track(GameObject go)
        {
            _spawned.Add(go);
            return go;
        }

        /// <summary>exitDelay 拉满：测试内绝不触发场景加载（LoadScene 会打断测试运行器）。</summary>
        private LevelManager CreateManager(PlayerBody player)
        {
            var go = Track(new GameObject("LevelManager"));
            var manager = go.AddComponent<LevelManager>();
            manager.Configure(player, totalCherriesInLevel: 1, nextScene: null, exitDelay: 60f);
            return manager;
        }

        [UnityTest]
        public IEnumerator Collectible_RegistersCountAndDestroys()
        {
            Track(PlayerTestScene.CreateGround(new Vector3(0f, -3f, 0f), new Vector2(10f, 1f)));
            var playerGo = Track(PlayerTestScene.CreatePlayer(new Vector3(0f, -1.3f, 0f)));
            var manager = CreateManager(playerGo.GetComponent<PlayerBody>());

            var cherry = Track(new GameObject("Cherry"));
            cherry.transform.position = new Vector3(0f, -1.3f, 0f);
            var col = cherry.AddComponent<BoxCollider2D>();
            col.size = Vector2.one;
            col.isTrigger = true;
            cherry.AddComponent<Collectible>();

            for (int i = 0; i < 5; i++) yield return new WaitForFixedUpdate();

            Assert.AreEqual(1, manager.Collected, "樱桃收集应计数 +1");
            Assert.AreEqual(1, LevelManager.TotalCollected, "跨场景累计应 +1");
            Assert.IsTrue(cherry == null, "樱桃应自毁（销毁后可判 == null）");
        }

        [UnityTest]
        public IEnumerator LevelExit_CompleteFreezesInputButNotPhysics()
        {
            Track(PlayerTestScene.CreateGround(new Vector3(0f, -3f, 0f), new Vector2(10f, 1f)));
            var playerGo = Track(PlayerTestScene.CreatePlayer(new Vector3(0f, -1.3f, 0f)));
            var body = playerGo.GetComponent<PlayerBody>();
            var input = playerGo.GetComponent<InputReader>();
            CreateManager(body);

            var door = Track(new GameObject("Door"));
            door.transform.position = new Vector3(0f, -1.3f, 0f);
            var col = door.AddComponent<BoxCollider2D>();
            col.size = new Vector2(1f, 2f);
            col.isTrigger = true;
            door.AddComponent<LevelExit>();

            // 等 20 帧：玩家从出生点下落（0.7m ≈ 0.2s ≈ 10 帧）→ 穿过门触发区 → 落地稳定
            for (int i = 0; i < 20; i++) yield return new WaitForFixedUpdate();

            Assert.IsFalse(input.enabled, "过关后应冻结玩家输入");
            Assert.AreEqual(0f, input.MoveAxis, "冻结输入应为中性：InputReader 禁用时归零（防残留按键持续奔跑）");
            Assert.IsTrue(body.Grounded, "冻结输入 ≠ 冻结物理：玩家应仍受重力落在地面（与死亡冻结区分）");
        }
    }
}
