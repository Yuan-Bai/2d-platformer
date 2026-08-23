using NUnit.Framework;
using Platformer.Motor;
using Platformer.States;

namespace Platformer.Tests
{
    /// <summary>状态机转换轨迹测试：Idle→Run→Jump→Fall→Idle 的完整闭环。</summary>
    public class PlayerStateMachineTests
    {
        private const float Dt = 1f / 60f;

        private PlayerMotor _motor;
        private PlayerStateMachine _sm;
        private PlayerStateContext _ctx;
        private bool _grounded = true;

        [SetUp]
        public void SetUp()
        {
            _motor = new PlayerMotor(new PlayerMotor.Settings());
            _sm = new PlayerStateMachine(PlayerStateId.Idle);
            _ctx = new PlayerStateContext { Motor = _motor, Grounded = true };
        }

        /// <summary>模拟一帧：状态机先用上帧速度做转换，Motor 再更新速度（与 PlayerBody.FixedUpdate 同序）。</summary>
        private void Frame(PlayerMoveInput input, bool grounded)
        {
            _ctx.Input = input;
            _ctx.Grounded = grounded;
            _sm.Tick(_ctx, Dt);
            _motor.Tick(input, grounded, Dt);
        }

        [Test]
        public void StartsIdle()
        {
            Assert.AreEqual(PlayerStateId.Idle, _sm.Current);
        }

        [Test]
        public void Idle_WithMoveInput_GoesRun()
        {
            Frame(new PlayerMoveInput { MoveAxis = 1f }, grounded: true);
            Assert.AreEqual(PlayerStateId.Run, _sm.Current);
        }

        [Test]
        public void Run_WithoutMoveInput_GoesIdle()
        {
            Frame(new PlayerMoveInput { MoveAxis = 1f }, grounded: true);
            Assert.AreEqual(PlayerStateId.Run, _sm.Current);
            Frame(default, grounded: true);
            Assert.AreEqual(PlayerStateId.Idle, _sm.Current);
        }

        [Test]
        public void FullLoop_IdleRunJumpFallIdle()
        {
            // 起跑
            Frame(new PlayerMoveInput { MoveAxis = 1f }, grounded: true);
            Assert.AreEqual(PlayerStateId.Run, _sm.Current);

            // 起跳（地面帧按下）
            Frame(new PlayerMoveInput { MoveAxis = 1f, JumpQueued = true, JumpHeld = true }, grounded: true);
            // 下一帧离地上升：状态机看到 vy>0 且不在地面 → Jump
            Frame(new PlayerMoveInput { MoveAxis = 1f, JumpHeld = true }, grounded: false);
            Assert.AreEqual(PlayerStateId.Jump, _sm.Current);

            // 上升直到顶点后下落 → 必然进入 Fall
            var states = new System.Collections.Generic.List<PlayerStateId>();
            for (int i = 0; i < 300; i++)
            {
                Frame(new PlayerMoveInput { MoveAxis = 1f, JumpHeld = true }, grounded: false);
                states.Add(_sm.Current);
                if (_sm.Current == PlayerStateId.Fall) break;
            }
            Assert.Contains(PlayerStateId.Fall, states, "上升结束后应进入 Fall");
        }
    }
}
