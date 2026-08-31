using NUnit.Framework;
using Platformer.Player;
using Platformer.States;

namespace Platformer.Tests
{
    /// <summary>
    /// 动画帧选择回归测试（"跳跃第一帧一闪而过" bug 的反馈循环）：
    /// 旧实现按时间播放跳跃两帧（10fps → 0.1s 后切到下降帧），上升段大部分时间显示第二帧。
    /// 新语义：Jump 恒返回上升帧(0)、Fall 恒返回下降帧(1)——按运动阶段选帧，与跳跃时长无关。
    /// </summary>
    public class PlayerAnimSelectorTests
    {
        private PlayerAnimSelector _selector;
        private const float Dt = 1f / 60f;

        [SetUp]
        public void SetUp()
        {
            _selector = new PlayerAnimSelector(10f);
        }

        [Test]
        public void Jump_ReturnsRiseFrame_ForWholeRiseDuration()
        {
            // 上升段持续约 0.46s（11/24），远超旧动画 0.2s——断言全程都是上升帧
            for (int i = 0; i < 30; i++)
                Assert.AreEqual(0, _selector.Tick(PlayerStateId.Jump, Dt, 2),
                    $"Jump 第 {i} 帧应保持上升帧（旧实现在 0.2s 后切到下降帧）");
        }

        [Test]
        public void Fall_ReturnsFallFrame()
        {
            for (int i = 0; i < 10; i++)
                Assert.AreEqual(1, _selector.Tick(PlayerStateId.Fall, Dt, 1));
        }

        [Test]
        public void JumpThenFall_SwitchesFramesByPhase_NotByTime()
        {
            _selector.Tick(PlayerStateId.Jump, Dt, 2);
            _selector.Tick(PlayerStateId.Jump, Dt, 2);
            // 状态切换（进入下降段）立即换下降帧，并重置相位
            Assert.AreEqual(1, _selector.Tick(PlayerStateId.Fall, Dt, 1));
        }

        [Test]
        public void Idle_CyclesThroughFrames()
        {
            // 10fps、4 帧：0.1s 步进正好每步一帧，循环 1→2→3→0→1
            Assert.AreEqual(1, _selector.Tick(PlayerStateId.Idle, 0.1f, 4));
            Assert.AreEqual(2, _selector.Tick(PlayerStateId.Idle, 0.1f, 4));
            Assert.AreEqual(3, _selector.Tick(PlayerStateId.Idle, 0.1f, 4));
            Assert.AreEqual(0, _selector.Tick(PlayerStateId.Idle, 0.1f, 4));
            Assert.AreEqual(1, _selector.Tick(PlayerStateId.Idle, 0.1f, 4));
        }

        [Test]
        public void StateSwitch_ResetsLoopPhase()
        {
            _selector.Tick(PlayerStateId.Idle, 0.25f, 4); // 已到 index 2
            // 切到 Run 后相位归零
            Assert.AreEqual(0, _selector.Tick(PlayerStateId.Run, 0.01f, 6));
            // 切回 Idle 再次归零
            Assert.AreEqual(0, _selector.Tick(PlayerStateId.Idle, 0.01f, 4));
        }
    }
}
