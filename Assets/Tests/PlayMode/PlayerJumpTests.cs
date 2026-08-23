using System.Collections;
using NUnit.Framework;
using Platformer.Player;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;

namespace Platformer.Tests
{
    /// <summary>
    /// PlayMode 物理回归测试：空中跳跃限制。
    /// 这是「无限连跳」bug 的反馈循环：起跳后在空中等到土狼窗口(0.1s)远超过期，
    /// 再次按跳，断言竖直速度不因新起跳而变正。bug 存在时此测试为红。
    /// </summary>
    public class PlayerJumpTests : InputTestFixture
    {
        private Keyboard _kb;

        [SetUp]
        public override void Setup()
        {
            base.Setup();
            _kb = InputSystem.AddDevice<Keyboard>();
        }

        [TearDown]
        public override void TearDown()
        {
            base.TearDown();
        }

        [UnityTest]
        public IEnumerator JumpInAir_AfterCoyoteWindow_DoesNotJumpAgain()
        {
            // 最小场景：地面 + 玩家
            var ground = new GameObject("Ground");
            ground.transform.position = new Vector3(0f, -2f, 0f);
            var gCol = ground.AddComponent<BoxCollider2D>();
            gCol.size = new Vector2(40f, 1f);

            var player = new GameObject("Player");
            player.transform.position = new Vector3(0f, -1.1f, 0f);
            player.AddComponent<SpriteRenderer>();
            var pCol = player.AddComponent<BoxCollider2D>();
            pCol.size = Vector2.one;
            player.AddComponent<Rigidbody2D>();
            player.AddComponent<InputReader>();
            player.AddComponent<PlayerBody>();

            var rb = player.GetComponent<Rigidbody2D>();

            yield return new WaitForFixedUpdate(); // 落定

            // 起跳：按住空格 3 帧（全程按住 → 满跳高约 2.5 单位，空中约 50 帧）
            // 注意：PlayMode 下 InputSystem 自动更新，不手动调用 Update（避免双更新重置帧事件）
            Press(_kb.spaceKey);
            yield return null;
            for (int i = 0; i < 3; i++)
            {
                yield return new WaitForFixedUpdate();
            }

            // 继续上升并开始回落；30 帧时已过顶点（约 27 帧）但在空中（高约 2.4 单位）
            for (int i = 0; i < 30; i++)
            {
                yield return new WaitForFixedUpdate();
            }

            float vyBefore = rb.velocity.y;
            Assert.Less(vyBefore, 0f, $"前置条件不成立：30 帧后应已开始下落（vy={vyBefore}）");

            // 松开再按下：制造一次新的"跳跃按下"事件（此时土狼窗口 0.1s 已远超过期）
            Release(_kb.spaceKey);
            yield return null;
            Press(_kb.spaceKey);
            yield return null;
            yield return new WaitForFixedUpdate();

            float vyAfter = rb.velocity.y;
            Release(_kb.spaceKey);

            Assert.Less(vyAfter, 0f, $"空中土狼过期后按跳不应起跳（vyBefore={vyBefore}, vyAfter={vyAfter}）");

            // 清理顺序：先销毁对象并等一帧，再移除测试键盘
            Object.Destroy(player);
            Object.Destroy(ground);
            yield return null;
            InputSystem.RemoveDevice(_kb);
        }
    }
}
