using UnityEngine;
using Platformer.Motor;
using Platformer.States;

namespace Platformer.Player
{
    /// <summary>
    /// Unity 薄适配层：全项目唯一写 Rigidbody2D.velocity 的地方（ADR-0002）。
    /// FixedUpdate 管线：地面检测 → 输入快照 → 状态机 Tick（决策）→ Motor Tick（计算）→ 写速度。
    /// 运动逻辑全部在纯 C# 的 Motor 与状态机里，本类不承载任何手感决策。
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D))]
    [RequireComponent(typeof(BoxCollider2D))]
    [RequireComponent(typeof(SpriteRenderer))]
    public sealed class PlayerBody : MonoBehaviour
    {
        [Header("手感参数（M1 调校对象）")]
        [SerializeField] private PlayerMotor.Settings settings = new PlayerMotor.Settings();

        [Header("地面检测")]
        [SerializeField] private LayerMask groundLayers = ~0;
        [SerializeField] private float groundCheckDistance = 0.08f;
        [SerializeField] private float groundCheckInset = 0.02f;

        private Rigidbody2D _rb;
        private BoxCollider2D _col;
        private InputReader _input;
        private PlayerMotor _motor;
        private PlayerStateMachine _stateMachine;
        private PlayerStateContext _ctx;
        private bool _grounded;

        public PlayerStateId CurrentState => _stateMachine.Current;
        public bool Grounded => _grounded;

        private void Awake()
        {
            _rb = GetComponent<Rigidbody2D>();
            _col = GetComponent<BoxCollider2D>();
            _input = GetComponent<InputReader>() ?? gameObject.AddComponent<InputReader>();

            _motor = new PlayerMotor(settings);
            _stateMachine = new PlayerStateMachine(PlayerStateId.Idle);
            _ctx = new PlayerStateContext { Motor = _motor };

            // ADR-0002 纪律：重力归零（代码全权施加）、锁定旋转、插值平滑表现
            _rb.gravityScale = 0f;
            _rb.freezeRotation = true;
            _rb.interpolation = RigidbodyInterpolation2D.Interpolate;

            // 摩擦归零（ADR-0002 补充纪律）：接触求解只负责法向支撑，
            // 切向运动完全由代码掌控。默认摩擦 0.4 + velocityThreshold(1m/s)
            // 会压制地面水平速度——"地面无法移动"bug 的根因。
            _col.sharedMaterial = new PhysicsMaterial2D { friction = 0f };
        }

        private void FixedUpdate()
        {
            float dt = Time.fixedDeltaTime;

            _grounded = CheckGrounded();
            _ctx.Input = _input.BuildInput();
            _ctx.Grounded = _grounded;

            _stateMachine.Tick(_ctx, dt);
            MoveCommand cmd = _motor.Tick(_ctx.Input, _grounded, dt);
            _rb.velocity = cmd.Velocity;
        }

        /// <summary>
        /// 向下 BoxCast 检测地面。探测 box 完全位于角色碰撞体之外。
        /// ⚠️ gap 必须大于 Physics2D 的 Default Contact Offset（默认 0.01）：
        /// cast 起始 box 会按 contact offset 膨胀做重叠判定，gap 过小会导致
        /// 膨胀后与自身碰撞体重叠 → 每帧 hit 自己 → grounded 恒 true（"无限连跳"根因）。
        /// </summary>
        private bool CheckGrounded()
        {
            Bounds b = _col.bounds;
            const float boxHeight = 0.05f;
            const float gap = 0.02f; // > contact offset(0.01)，保证膨胀后仍不与自身重叠
            Vector2 size = new Vector2(b.size.x - groundCheckInset * 2f, boxHeight);
            Vector2 origin = new Vector2(b.center.x, b.min.y - boxHeight * 0.5f - gap);
            RaycastHit2D hit = Physics2D.BoxCast(origin, size, 0f, Vector2.down, groundCheckDistance, groundLayers);
            return hit.collider != null;
        }
    }
}