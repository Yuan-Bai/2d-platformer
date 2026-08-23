using NUnit.Framework;
using Platformer.Motor;

namespace Platformer.Tests
{
    public class TimersTests
    {
        private const float Dt = 1f / 60f;

        [Test]
        public void CoyoteTimer_ActiveWithinWindow_ThenExpires()
        {
            var timer = new CoyoteTimer(0.1f);
            Assert.IsFalse(timer.Active, "初始不应有效");

            timer.Refresh();
            Assert.IsTrue(timer.Active);

            for (int i = 0; i < 5; i++) timer.Tick(Dt); // 0.0833s < 0.1s
            Assert.IsTrue(timer.Active);

            for (int i = 0; i < 2; i++) timer.Tick(Dt); // 共 7 帧 0.1167s，远离浮点边界
            Assert.IsFalse(timer.Active, "超窗后失效");
        }

        [Test]
        public void JumpBuffer_QueuedConsumeOnce_ThenExpires()
        {
            var buffer = new JumpBuffer(0.1f);
            Assert.IsFalse(buffer.HasQueued);

            buffer.Queue();
            Assert.IsTrue(buffer.HasQueued);
            buffer.Consume();
            Assert.IsFalse(buffer.HasQueued, "消费后立即失效");

            buffer.Queue();
            for (int i = 0; i < 7; i++) buffer.Tick(Dt); // 0.1167s > 0.1s
            Assert.IsFalse(buffer.HasQueued, "超窗后失效");
        }
    }
}
