using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using Platformer.Mechanics;
using Platformer.Player;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;

namespace Platformer.Tests
{
    /// <summary>
    /// M2 关卡机制的 PlayMode 物理回归测试（ADR-0005 的验收）：
    /// 单向平台下穿 / 弹簧冲量 / 移动平台携带 / 危险物死亡重生。
    /// </summary>
    public class M2MechanicsTests : InputTestFixture
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
            // 兜底清理：断言失败或未处理日志会中止测试协程，测试末尾的 Cleanup() 不会执行。
            // 泄漏的角色/机关会污染后续测试（物理碰撞 + 孤儿键盘缓冲区索引别名——
            // "Cached unprocessed value" 雪球的根因），因此这里同步销毁并移除测试键盘。
            foreach (var go in _spawned)
                if (go != null) Object.DestroyImmediate(go);
            _spawned.Clear();
            if (_kb != null && _kb.added)
                InputSystem.RemoveDevice(_kb);
            base.TearDown();
        }

        private IEnumerator Cleanup()
        {
            foreach (var go in _spawned) Object.Destroy(go);
            _spawned.Clear();
            yield return null;
            if (_kb != null && _kb.added) InputSystem.RemoveDevice(_kb);
        }

        private GameObject Track(GameObject go)
        {
            _spawned.Add(go);
            return go;
        }

        [UnityTest]
        public IEnumerator OneWayPlatform_HoldingDown_DropsThrough()
        {
            Track(PlayerTestScene.CreateGround(new Vector3(0f, -3f, 0f), new Vector2(20f, 1f)));

            var plat = Track(new GameObject("OneWayPlatform"));
            plat.transform.position = new Vector3(0f, -1f, 0f);
            plat.transform.localScale = new Vector3(8f, 0.5f, 1f);
            plat.AddComponent<SpriteRenderer>();
            var pcol = plat.AddComponent<BoxCollider2D>();
            pcol.size = Vector2.one;
            plat.AddComponent<OneWayPlatform>();

            var player = Track(PlayerTestScene.CreatePlayer(new Vector3(0f, -0.2f, 0f)));
            var body = player.GetComponent<PlayerBody>();

            // 等 10 帧让玩家落到平台上并稳定接触（平台顶 -0.75，站上后玩家中心 ≈ -0.25）。
            // 此前版本只等 1 帧，接触未建立，测的是"空中下落穿过"而非"站上后断开接触下穿"。
            // 且旧断言 y < -0.5 几何上写反：站在平台上中心 y ≈ -0.25，永远不可能 < -0.5。
            for (int i = 0; i < 10; i++) yield return new WaitForFixedUpdate();

            float yOnPlatform = player.transform.position.y;
            Assert.Greater(yOnPlatform, -0.5f, $"前置条件：玩家应站在单向平台上（未穿透到地面），实际 y={yOnPlatform}");
            Assert.Less(yOnPlatform, 0f, $"前置条件：玩家不应停留在出生高度，实际 y={yOnPlatform}");
            Assert.IsTrue(body.Grounded, "前置条件：玩家应处于落地状态");

            InputSystem.QueueStateEvent(_kb, new KeyboardState(Key.S));
            yield return null;
            for (int i = 0; i < 40; i++) yield return new WaitForFixedUpdate();
            InputSystem.QueueStateEvent(_kb, new KeyboardState());
            yield return null;
            yield return new WaitForFixedUpdate(); // 恢复碰撞忽略

            Assert.Less(player.transform.position.y, -1.5f, $"按住 S 应穿过单向平台下落，实际 y={player.transform.position.y}");
            yield return Cleanup();
        }

        [UnityTest]
        public IEnumerator Bumper_LaunchesPlayerUpward()
        {
            Track(PlayerTestScene.CreateGround(new Vector3(0f, -3f, 0f), new Vector2(20f, 1f)));

            var bumper = Track(new GameObject("Bumper"));
            bumper.transform.position = new Vector3(0f, -1.5f, 0f);
            var bcol = bumper.AddComponent<BoxCollider2D>();
            bcol.isTrigger = true;
            bumper.AddComponent<Bumper>();

            // 出生在触发器上方（底 -0.8 > 触发器顶 -1.0）：先落到弹簧上，再被弹起。
            // 此前版本出生点与触发器重叠、第 0 帧即弹起，且断言第 30 帧瞬时 vy——
            // 弹起(14)后 0.58s 已回落（14 - 34×0.58 ≈ -5.7），断言必然失败。
            // 改为记录等待期间的峰值竖直速度，与弹起时机解耦。
            var player = Track(PlayerTestScene.CreatePlayer(new Vector3(0f, -0.3f, 0f)));
            var rb = player.GetComponent<Rigidbody2D>();

            float maxVy = float.MinValue;
            for (int i = 0; i < 30; i++)
            {
                yield return new WaitForFixedUpdate();
                maxVy = Mathf.Max(maxVy, rb.velocity.y);
            }

            Assert.Greater(maxVy, 5f, $"弹簧应把玩家弹起（峰值 vy 应明显大于 0），实际峰值 vy={maxVy}");
            yield return Cleanup();
        }

        [UnityTest]
        public IEnumerator MovingPlatform_CarriesPlayer()
        {
            var platform = Track(new GameObject("MovingPlatform"));
            platform.transform.position = new Vector3(0f, -2f, 0f);
            platform.transform.localScale = new Vector3(4f, 0.5f, 1f);
            platform.AddComponent<SpriteRenderer>();
            var pcol = platform.AddComponent<BoxCollider2D>();
            pcol.size = Vector2.one;
            var mover = platform.AddComponent<MovingPlatform>();
            mover.waypoints = new[] { new Vector2(4f, 0f) };
            mover.speed = 2f;

            var player = Track(PlayerTestScene.CreatePlayer(new Vector3(0f, -1.25f, 0f)));

            for (int i = 0; i < 60; i++) yield return new WaitForFixedUpdate();

            Assert.Greater(player.transform.position.x, 0.5f,
                $"站上移动平台后玩家应被位移补偿携带，实际 x={player.transform.position.x}");
            yield return Cleanup();
        }

        [UnityTest]
        public IEnumerator MovingPlatform_PlayerCanWalkAndJumpOnIt()
        {
            // 地面兜底（防止玩家滑出平台后坠落无底）
            Track(PlayerTestScene.CreateGround(new Vector3(0f, -4f, 0f), new Vector2(40f, 1f)));

            var platform = Track(new GameObject("MovingPlatform"));
            platform.transform.position = new Vector3(0f, -2f, 0f);
            platform.transform.localScale = new Vector3(8f, 0.5f, 1f);
            platform.AddComponent<SpriteRenderer>();
            var pcol = platform.AddComponent<BoxCollider2D>();
            pcol.size = Vector2.one;
            var mover = platform.AddComponent<MovingPlatform>();
            mover.waypoints = new[] { new Vector2(4f, 0f) }; // 只向右，避免端点折返干扰
            mover.speed = 2f;

            var player = Track(PlayerTestScene.CreatePlayer(new Vector3(0f, -1.25f, 0f)));
            var rb = player.GetComponent<Rigidbody2D>();
            var body = player.GetComponent<PlayerBody>();

            // 落到平台上并稳定接触
            for (int i = 0; i < 20; i++) yield return new WaitForFixedUpdate();

            Assert.IsTrue(body.Grounded, "前置：玩家应已站在移动平台上（落地检测失败）");

            // 按住 D 向右走：断言相对平台的位移（排除平台自身携带的位移）
            InputSystem.QueueStateEvent(_kb, new KeyboardState(Key.D));
            yield return null;
            float relX0 = player.transform.position.x - platform.transform.position.x;
            for (int i = 0; i < 40; i++) yield return new WaitForFixedUpdate();
            float relX1 = player.transform.position.x - platform.transform.position.x;
            InputSystem.QueueStateEvent(_kb, new KeyboardState());
            yield return null;

            Assert.Greater(relX1 - relX0, 0.5f,
                $"站在移动平台上按 D 应能相对平台向右移动，实际相对位移 {relX1 - relX0}");

            // 按空格起跳：断言峰值竖直速度
            InputSystem.QueueStateEvent(_kb, new KeyboardState(Key.Space));
            yield return null;
            float maxVy = float.MinValue;
            for (int i = 0; i < 10; i++)
            {
                yield return new WaitForFixedUpdate();
                maxVy = Mathf.Max(maxVy, rb.velocity.y);
            }
            InputSystem.QueueStateEvent(_kb, new KeyboardState());
            yield return null;

            Assert.Greater(maxVy, 5f, $"站在移动平台上按空格应能起跳，实际峰值 vy={maxVy}");
            yield return Cleanup();
        }

        [UnityTest]
        public IEnumerator KinematicPlatform_Stationary_PlayerCanWalk()
        {
            // A/B 对照：kinematic 平台但静止（waypoints 留空 → _delta=0 → 不调用 MovePosition 携带）。
            // 若此测试绿而移动平台红 → 毒药是"移动 + MovePosition 携带"组合；
            // 若此测试也红 → 毒药是 kinematic 接触本身。
            var platform = Track(new GameObject("KinematicPlatform"));
            platform.transform.position = new Vector3(0f, -2f, 0f);
            platform.transform.localScale = new Vector3(8f, 0.5f, 1f);
            platform.AddComponent<SpriteRenderer>();
            var pcol = platform.AddComponent<BoxCollider2D>();
            pcol.size = Vector2.one;
            platform.AddComponent<MovingPlatform>(); // 不设 waypoints：静止 kinematic

            var player = Track(PlayerTestScene.CreatePlayer(new Vector3(0f, -1.25f, 0f)));
            var rb = player.GetComponent<Rigidbody2D>();

            for (int i = 0; i < 10; i++) yield return new WaitForFixedUpdate();

            InputSystem.QueueStateEvent(_kb, new KeyboardState(Key.D));
            yield return null;
            float x0 = rb.position.x;
            for (int i = 0; i < 40; i++) yield return new WaitForFixedUpdate();
            float moved = rb.position.x - x0;
            InputSystem.QueueStateEvent(_kb, new KeyboardState());
            yield return null;

            Assert.Greater(moved, 1f, $"静止 kinematic 平台上按 D 应能移动，实际 rb.position 位移 {moved}");
            yield return Cleanup();
        }

        [UnityTest]
        public IEnumerator Hazard_RespawnsAtCheckpoint()
        {
            Track(PlayerTestScene.CreateGround(new Vector3(0f, -3f, 0f), new Vector2(40f, 1f)));

            var checkpoint = Track(new GameObject("Checkpoint"));
            checkpoint.transform.position = new Vector3(2f, -2f, 0f);
            var ccol = checkpoint.AddComponent<BoxCollider2D>();
            ccol.size = new Vector2(1f, 2f);
            ccol.isTrigger = true;
            checkpoint.AddComponent<Checkpoint>();

            var hazard = Track(new GameObject("Hazard"));
            hazard.transform.position = new Vector3(5f, -2f, 0f);
            var hcol = hazard.AddComponent<BoxCollider2D>();
            hcol.size = new Vector2(1f, 2f);
            hcol.isTrigger = true;
            hazard.AddComponent<Hazard>();

            var player = Track(PlayerTestScene.CreatePlayer(new Vector3(0f, -1.3f, 0f)));

            // 向右跑：经过 Checkpoint(x=2) → 撞上 Hazard(x=5) 死亡
            InputSystem.QueueStateEvent(_kb, new KeyboardState(Key.D));
            yield return null;
            for (int i = 0; i < 60; i++) yield return new WaitForFixedUpdate();
            InputSystem.QueueStateEvent(_kb, new KeyboardState());

            // 等死亡冻结(0.35s) + 重生 + 落定
            for (int i = 0; i < 60; i++) yield return new WaitForFixedUpdate();

            float x = player.transform.position.x;
            Assert.Greater(x, 1.5f, $"应重生在 Checkpoint(x=2) 附近，实际 x={x}");
            Assert.Less(x, 2.8f, $"重生后无输入不应跑远，实际 x={x}");
            yield return Cleanup();
        }
    }
}
