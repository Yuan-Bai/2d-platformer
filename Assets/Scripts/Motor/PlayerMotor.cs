using UnityEngine;

namespace Platformer.Motor
{
    /// <summary>
    /// 运动计算核心（深模块）：输入 + 地面事实 → 期望速度。
    /// 内部消化全部手感机制：双段重力、土狼时间、跳跃缓冲、可变跳高、跳切、
    /// 终端下落速度、空中控制。不依赖 MonoBehaviour、不直接写 Rigidbody2D ——
    /// 接口即测试面，全部行为可在 EditMode 单测。
    /// </summary>
    public sealed class PlayerMotor
    {
        /// <summary>手感参数（M1 调校对象，见设计文档"手感参数清单"）。</summary>
        public sealed class Settings
        {
            public float MaxSpeed = 6f;
            public float GroundAcceleration = 45f;
            public float GroundDeceleration = 45f;
            public float AirAcceleration = 28f;
            public float AirDeceleration = 14f;
            public float JumpVelocity = 11f;
            public float RiseGravity = 24f;
            public float FallGravity = 34f;
            public float JumpCutMultiplier = 0.5f;
            public float MaxFallSpeed = 16f;
            public float CoyoteWindow = 0.1f;
            public float JumpBufferWindow = 0.1f;
        }

        private readonly Settings _s;
        private readonly CoyoteTimer _coyote;
        private readonly JumpBuffer _jumpBuffer;
        private Vector2 _velocity;
        private bool _jumpHeld;
        private bool _rising; // 处于跳跃上升段（跳切与上升重力的管辖范围）

        public PlayerMotor(Settings settings)
        {
            _s = settings;
            _coyote = new CoyoteTimer(settings.CoyoteWindow);
            _jumpBuffer = new JumpBuffer(settings.JumpBufferWindow);
        }

        /// <summary>当前竖直速度（状态机据此判定 Jump/Fall）。</summary>
        public float VerticalSpeed => _velocity.y;

        /// <summary>当前水平速度。</summary>
        public float HorizontalSpeed => _velocity.x;

        public MoveCommand Tick(PlayerMoveInput input, bool grounded, float dt)
        {
            // 1. 计时器推进；落地刷新土狼窗口
            _coyote.Tick(dt);
            _jumpBuffer.Tick(dt);
            if (grounded) _coyote.Refresh();

            _jumpHeld = input.JumpHeld;
            if (input.JumpQueued) _jumpBuffer.Queue();

            // 2. 起跳判定：缓冲事件 && （土狼窗口内 || 落地）
            if (_jumpBuffer.HasQueued && (_coyote.Active || grounded))
            {
                _jumpBuffer.Consume();
                _velocity.y = _s.JumpVelocity;
                _rising = true;
            }

            // 3. 跳切：上升段松开跳跃键 → 上升速度一次性打折（可变跳高的实现）
            if (_rising && !_jumpHeld && _velocity.y > 0f)
            {
                _velocity.y *= _s.JumpCutMultiplier;
                _rising = false;
            }

            // 4. 重力（双段）：上升段轻、下落段重；封顶终端下落速度
            float gravity = _rising && _velocity.y > 0f ? _s.RiseGravity : _s.FallGravity;
            _velocity.y = Mathf.Max(_velocity.y - gravity * dt, -_s.MaxFallSpeed);
            if (_velocity.y <= 0f) _rising = false;

            // 5. 水平：有输入则向目标速度移动（同向加速、反向先刹车），无输入则刹车；
            //    空中加速与刹车均弱于地面（空中控制的实现）
            float targetX = input.MoveAxis * _s.MaxSpeed;
            if (Mathf.Abs(input.MoveAxis) > 0.01f)
            {
                bool accelerating = Mathf.Sign(input.MoveAxis) == Mathf.Sign(_velocity.x)
                                    || Mathf.Abs(_velocity.x) < 0.01f;
                float rate = grounded
                    ? (accelerating ? _s.GroundAcceleration : _s.GroundDeceleration)
                    : (accelerating ? _s.AirAcceleration : _s.AirDeceleration);
                _velocity.x = Mathf.MoveTowards(_velocity.x, targetX, rate * dt);
            }
            else
            {
                float brake = grounded ? _s.GroundDeceleration : _s.AirDeceleration;
                _velocity.x = Mathf.MoveTowards(_velocity.x, 0f, brake * dt);
            }

            return new MoveCommand { Velocity = _velocity };
        }
    }
}
