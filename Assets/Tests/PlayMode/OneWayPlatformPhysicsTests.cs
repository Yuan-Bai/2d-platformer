using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using Platformer.Mechanics;
using Platformer.Player;
using Platformer.States;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;

namespace Platformer.Tests
{
    /// <summary>
    /// 单向平台下落/穿出回归测试（两个 bug 的反馈循环）：
    /// Bug A（"按住 S 落到单向平台保持跳跃末帧"）：玩家从上方落向单向平台时按住下穿键，
    ///   落地瞬间 ground mask 排除了平台层 → grounded 恒 false → Fall 状态卡死。
    /// Bug B（"跳上头顶的单向平台时多一次小跳"）：上升穿出平台顶面时脚底 BoxCast
    ///   命中平台 → grounded 误判 1 帧 → 跳跃缓冲被意外消费，二次起跳。
    /// </summary>
    public class OneWayPlatformPhysicsTests : InputTestFixture
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
            if (_kb != null && _kb.added)
                InputSystem.RemoveDevice(_kb);
            base.TearDown();
        }

        private GameObject CreateOneWayPlatform(Vector3 pos, Vector2 size)
        {
            var go = new GameObject("OneWay");
            _spawned.Add(go);
            go.transform.position = pos;
            var col = go.AddComponent<BoxCollider2D>();
            col.size = size;
            go.AddComponent<OneWayPlatform>(); // Awake 自动配置层 + PlatformEffector2D
            return go;
        }

        [UnityTest]
        public IEnumerator LandOnOneWay_WhileHoldingDown_BecomesGrounded()
        {
            // 平台顶面 = 0.5；玩家从 2.5m 高处下落并全程按住 S
            CreateOneWayPlatform(new Vector3(0f, 0f, 0f), new Vector2(4f, 1f));

            var player = PlayerTestScene.CreatePlayer(new Vector3(0f, 2.5f, 0f));
            _spawned.Add(player);
            var body = player.GetComponent<PlayerBody>();

            InputSystem.QueueStateEvent(_kb, new KeyboardState(Key.S));

            // 等待下落 + 落定（1.5m 下落约 0.3s，留足余量）
            for (int i = 0; i < 90; i++)
                yield return new WaitForFixedUpdate();

            InputSystem.QueueStateEvent(_kb, new KeyboardState());

            Assert.IsTrue(body.Grounded,
                "Bug A：按住 S 落到单向平台顶面后应被判定接地（旧实现 mask 排除平台层 → grounded 恒 false）");
            Assert.AreEqual(PlayerStateId.Idle, body.CurrentState,
                "Bug A：落地后应回到 Idle，而非保持 Fall（跳跃末帧）");
        }

        [UnityTest]
        public IEnumerator JumpUpThroughOneWay_NoGroundedBlip_WhileRising()
        {
            // 地面顶 -0.5，玩家站其上；单向平台顶 1.7，玩家满跳高 2.52 可越过
            var ground = PlayerTestScene.CreateGround(new Vector3(0f, -1f, 0f), new Vector2(20f, 1f));
            _spawned.Add(ground);
            CreateOneWayPlatform(new Vector3(0f, 1.2f, 0f), new Vector2(4f, 1f));

            var player = PlayerTestScene.CreatePlayer(new Vector3(0f, 0f, 0f));
            _spawned.Add(player);
            var body = player.GetComponent<PlayerBody>();
            var rb = player.GetComponent<Rigidbody2D>();

            // 起跳（按住空格）
            InputSystem.QueueStateEvent(_kb, new KeyboardState(Key.Space));
            yield return null;

            // 上升采样：跳过起跳前 2 帧（第 1 帧还在地面 grounded=true 属正常）
            bool anyGrounded = false;
            bool reachedApex = false;
            for (int i = 0; i < 50; i++)
            {
                yield return new WaitForFixedUpdate();
                if (i >= 2 && body.Grounded) anyGrounded = true;
                if (i > 10 && rb.velocity.y <= 0f) { reachedApex = true; break; }
            }

            InputSystem.QueueStateEvent(_kb, new KeyboardState());

            Assert.IsTrue(reachedApex, "前置条件不成立：起跳应到达顶点（跳跃参数变化时调整平台高度）");
            Assert.IsFalse(anyGrounded,
                "Bug B：上升穿出单向平台顶面期间不应出现 grounded 误判（旧实现脚底 BoxCast 命中平台 → 缓冲二次起跳）");
        }
    }
}
