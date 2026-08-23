using NUnit.Framework;
using Platformer.Motor;
using UnityEngine;

namespace Platformer.Tests
{
    /// <summary>
    /// PlayerMotor 手感机制测试：覆盖 M1 验收参数表（起跳、土狼、缓冲、可变跳高、跳切、水平、终端速度）。
    /// </summary>
    public class PlayerMotorTests
    {
        private PlayerMotor.Settings _s;
        private PlayerMotor _motor;
        private const float Dt = 1f / 60f;
        private const float Eps = 1e-3f;

        [SetUp]
        public void SetUp()
        {
            _s = new PlayerMotor.Settings();
            _motor = new PlayerMotor(_s);
        }

        private static PlayerMoveInput Input(float axis = 0f, bool queued = false, bool held = false) =>
            new PlayerMoveInput { MoveAxis = axis, JumpQueued = queued, JumpHeld = held };

        private MoveCommand Step(PlayerMoveInput input, bool grounded, int frames = 1)
        {
            MoveCommand cmd = default;
            for (int i = 0; i < frames; i++) cmd = _motor.Tick(input, grounded, Dt);
            return cmd;
        }

        [Test]
        public void Jump_FromGround_SetsUpwardVelocity()
        {
            MoveCommand cmd = Step(Input(queued: true, held: true), grounded: true);
            // 起跳帧：JumpVelocity 赋完立即被上升重力扣一帧
            Assert.AreEqual(_s.JumpVelocity - _s.RiseGravity * Dt, cmd.Velocity.y, Eps);
        }

        [Test]
        public void JumpBuffer_PressInAir_ThenLand_AutoJumps()
        {
            // 空中按下（无地面，无土狼），一帧后落地
            Step(Input(queued: true, held: true), grounded: false);
            MoveCommand cmd = Step(Input(held: true), grounded: true);
            Assert.Greater(cmd.Velocity.y, 0f, "落地帧应自动起跳（跳跃缓冲）");
        }

        [Test]
        public void JumpBuffer_Expired_DoesNotJump()
        {
            Step(Input(queued: true, held: true), grounded: false); // 空中按下
            Step(Input(held: true), grounded: false, frames: 10);   // 空中继续 10 帧，缓冲已超窗（0.1s = 6 帧）
            MoveCommand cmd = Step(Input(held: true), grounded: true); // 之后才落地
            Assert.LessOrEqual(cmd.Velocity.y, 0f, "超窗后落地不应起跳");
        }

        [Test]
        public void CoyoteTime_JustLeftGround_CanStillJump()
        {
            Step(Input(), grounded: true); // 刷新土狼
            Step(Input(), grounded: false); // 离地 1 帧（1/60 < 0.1s 窗口）
            MoveCommand cmd = Step(Input(queued: true, held: true), grounded: false);
            Assert.Greater(cmd.Velocity.y, 0f, "土狼窗口内应能起跳");
        }

        [Test]
        public void CoyoteTime_Expired_DoesNotJump()
        {
            Step(Input(), grounded: true);
            Step(Input(), grounded: false, frames: 7); // 0.1167s > 0.1s 窗口
            MoveCommand cmd = Step(Input(queued: true, held: true), grounded: false);
            Assert.LessOrEqual(cmd.Velocity.y, 0f, "土狼超窗后不应起跳");
        }

        [Test]
        public void VariableJumpHeight_HoldHigherThanTap()
        {
            // 按住：完整上升
            var held = new PlayerMotor(_s);
            float heldPeak = SimulateJumpPeak(held, holdJump: true);

            // 点按：第 2 帧松开触发跳切
            var tapped = new PlayerMotor(_s);
            float tapPeak = SimulateJumpPeak(tapped, holdJump: false);

            Assert.Greater(heldPeak, tapPeak, "按住跳跃应明显高于点按（可变跳高）");
            Assert.Greater(heldPeak - tapPeak, 0.5f, "高度差应肉眼可感知");
        }

        /// <summary>模拟一次起跳直到回到起跳高度以下，返回最高点相对起跳点的高度。</summary>
        private float SimulateJumpPeak(PlayerMotor motor, bool holdJump)
        {
            float y = 0f, peak = 0f;
            bool grounded = true;
            bool jumped = false;

            for (int i = 0; i < 240 && !(jumped && grounded); i++)
            {
                bool queued = !jumped;
                bool held = holdJump || (!jumped); // 点按：起跳帧按住，之后松开
                var input = Input(queued: queued, held: held);
                var cmd = motor.Tick(input, grounded, Dt);
                y += cmd.Velocity.y * Dt;
                peak = Mathf.Max(peak, y);
                if (cmd.Velocity.y > 0f) jumped = true;

                // 简化地面模拟：y <= 0 视为落地
                grounded = y <= 0f && cmd.Velocity.y <= 0f;
            }
            return peak;
        }

        [Test]
        public void JumpCut_ReleaseTrimsRiseVelocity()
        {
            Step(Input(queued: true, held: true), grounded: true);
            MoveCommand cmd = Step(Input(held: false), grounded: false);
            // 第 2 帧：上升段松键 → 速度 × 0.5，再被重力扣一帧
            float expected = (_s.JumpVelocity - _s.RiseGravity * Dt) * _s.JumpCutMultiplier - _s.FallGravity * Dt;
            Assert.AreEqual(expected, cmd.Velocity.y, Eps);
        }

        [Test]
        public void Horizontal_AcceleratesToMaxSpeed_ThenCaps()
        {
            var cmd = Step(Input(axis: 1f), grounded: true, frames: 120);
            Assert.AreEqual(_s.MaxSpeed, cmd.Velocity.x, Eps, "水平速度应封顶于 MaxSpeed");
        }

        [Test]
        public void Horizontal_NoInput_BrakesToZero()
        {
            Step(Input(axis: 1f), grounded: true, frames: 60);
            var cmd = Step(Input(), grounded: true, frames: 60);
            Assert.AreEqual(0f, cmd.Velocity.x, Eps);
        }

        [Test]
        public void Fall_CapsAtTerminalVelocity()
        {
            var cmd = Step(Input(), grounded: false, frames: 300);
            Assert.AreEqual(-_s.MaxFallSpeed, cmd.Velocity.y, Eps, "下落速度应封顶于 MaxFallSpeed");
        }

        [Test]
        public void AirControl_AccelerationWeakerThanGround()
        {
            var ground = new PlayerMotor(_s);
            var air = new PlayerMotor(_s);

            var g1 = ground.Tick(Input(axis: 1f), grounded: true, Dt);
            var g2 = ground.Tick(Input(axis: 1f), grounded: true, Dt);
            float groundDelta = g2.Velocity.x - g1.Velocity.x;

            var a1 = air.Tick(Input(axis: 1f), grounded: false, Dt);
            var a2 = air.Tick(Input(axis: 1f), grounded: false, Dt);
            float airDelta = a2.Velocity.x - a1.Velocity.x;

            Assert.Less(airDelta, groundDelta, "空中加速度应弱于地面（空中控制）");
        }
    }
}
