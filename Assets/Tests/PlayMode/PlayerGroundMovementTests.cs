using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using Platformer.Player;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;

namespace Platformer.Tests
{
    /// <summary>
    /// PlayMode 物理回归测试：地面水平移动。
    /// 这是「地面无法左右移动」bug 的反馈循环：在真实 2D 物理环境下模拟按住 D 键
    /// 60 个物理帧，断言玩家 x 位移显著大于 0。bug 存在时此测试为红。
    /// </summary>
    public class PlayerGroundMovementTests : InputTestFixture
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
            // 兜底清理：测试中断（断言失败/未处理日志）时，场景残留会污染后续测试
            //（泄漏的 InputReader 会别名到下一测试的键盘缓冲区，触发 paranoid 缓存错误）
            foreach (var go in _spawned)
                if (go != null) Object.DestroyImmediate(go);
            _spawned.Clear();
            if (_kb != null && _kb.added)
                InputSystem.RemoveDevice(_kb);
            base.TearDown();
        }

        [UnityTest]
        public IEnumerator HoldingRight_OnGround_MovesPlayer()
        {
            // 最小场景：地面 + 玩家（不依赖 TestRoom 场景资产，隔离复现）
            var ground = new GameObject("Ground");
            _spawned.Add(ground);
            ground.transform.position = new Vector3(0f, -2f, 0f);
            var gCol = ground.AddComponent<BoxCollider2D>();
            gCol.size = new Vector2(40f, 1f);

            var player = new GameObject("Player");
            _spawned.Add(player);
            player.transform.position = new Vector3(0f, -1.1f, 0f); // 贴地，下落 1~2 帧落定
            player.AddComponent<SpriteRenderer>();
            var pCol = player.AddComponent<BoxCollider2D>();
            pCol.size = Vector2.one;
            player.AddComponent<Rigidbody2D>();
            player.AddComponent<InputReader>();
            player.AddComponent<PlayerBody>();

            yield return new WaitForFixedUpdate(); // 让角色落定

            InputSystem.QueueStateEvent(_kb, new KeyboardState(Key.D));
            yield return null; // 让 InputSystem 自动更新处理按下事件（不手动 Update，避免双更新重置 wasPressedThisFrame）
            float startX = player.transform.position.x;

            for (int i = 0; i < 60; i++)
            {
                yield return new WaitForFixedUpdate();
            }

            float moved = player.transform.position.x - startX;
            InputSystem.QueueStateEvent(_kb, new KeyboardState());

            // 修复后 60 帧位移约 5+ 单位（加速期 + 满速期）；bug 状态下被摩擦压制到龟速
            Assert.Greater(moved, 1f, $"按住 D 后 60 物理帧内应移动 >1 单位，实际 {moved}");

            // 场景销毁与键盘移除统一由 TearDown 兜底执行（测试中断时也不会泄漏）
        }
    }
}
