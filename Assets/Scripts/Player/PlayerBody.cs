using System.Collections;
using UnityEngine;
using Platformer.Motor;
using Platformer.States;

namespace Platformer.Player
{
    /// <summary>
    /// Unity 薄适配层：全项目唯一写 Rigidbody2D.velocity 的地方（ADR-0002）。
    /// FixedUpdate 管线：平台位移应用 → 地面检测 → 输入快照 → 状态机 Tick（决策）→ Motor Tick（计算）→ 写速度。
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

        [Header("死亡重生")]
        [SerializeField] private float deathFreezeSeconds = 0.35f;
        [SerializeField] private float respawnLift = 0.1f; // 重生时上抬，防止卡进碰撞体

        private Rigidbody2D _rb;
        private BoxCollider2D _col;
        private InputReader _input;
        private PlayerMotor _motor;
        private PlayerStateMachine _stateMachine;
        private PlayerStateContext _ctx;
        private bool _grounded;
        private bool _dead;
        private Vector2 _pendingPlatformDelta;
        private bool _downHeld;
        private int _playerLayer;
        private int _oneWayLayer;
        private Collider2D _ignoredOneWay; // 下穿期间被忽略的单向平台 collider

        public PlayerStateId CurrentState => _stateMachine.Current;
        public bool Grounded => _grounded;

        /// <summary>当前重生位置（出生点自动记录，Checkpoint 经过时更新）。</summary>
        public Vector2 RespawnPosition { get; set; }

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

            // 玩家独立层（ADR-0005）：单向平台下穿切换的基础 + 地面探测排除自身的第二道保险
            _playerLayer = LayerMask.NameToLayer("Player");
            _oneWayLayer = LayerMask.NameToLayer("OneWayPlatform");
            if (_playerLayer >= 0) gameObject.layer = _playerLayer;

            RespawnPosition = transform.position;
        }

        private void FixedUpdate()
        {
            // 移动平台速度补偿（ADR-0005）：把平台本帧位移折算成速度，与角色自身速度合并写入。
            // 此前用 MovePosition 位置传送携带：实测（M2 回归测试复现）传送与速度积分叠加时，
            // 角色自身速度的运动被完全吞掉——"站在移动平台上走不动/跳不起来"的根因。
            // 改为速度叠加后，携带与角色运动共用同一条速度积分路径，互不覆盖。
            Vector2 carryVelocity = Vector2.zero;
            if (_pendingPlatformDelta != Vector2.zero)
            {
                carryVelocity = _pendingPlatformDelta / Time.fixedDeltaTime;
                _pendingPlatformDelta = Vector2.zero;
            }

            // 死亡冻结帧：暂停一切运动写（保持随平台移动），等重生
            if (_dead)
            {
                _rb.velocity = carryVelocity;
                return;
            }

            float dt = Time.fixedDeltaTime;

            // 单向平台下穿（ADR-0005）：按住"下"时忽略与脚下单向平台的碰撞。
            // 用 per-collider 忽略（Physics2D.IgnoreCollision）而非层矩阵：
            // 实测 IgnoreLayerCollision 对 PlatformEffector2D 的既有接触不生效（接触不断、角色被托住）。
            bool down = _input.DownHeld;
            if (down != _downHeld && _oneWayLayer >= 0)
            {
                if (down)
                {
                    _ignoredOneWay = FindOneWayPlatformBelow();
                    if (_ignoredOneWay != null) Physics2D.IgnoreCollision(_col, _ignoredOneWay, true);
                }
                else if (_ignoredOneWay != null)
                {
                    Physics2D.IgnoreCollision(_col, _ignoredOneWay, false);
                    _ignoredOneWay = null;
                }
                _downHeld = down;
            }

            _grounded = CheckGrounded();
            _ctx.Input = _input.BuildInput();
            _ctx.Grounded = _grounded;

            _stateMachine.Tick(_ctx, dt);
            MoveCommand cmd = _motor.Tick(_ctx.Input, _grounded, dt);
            _rb.velocity = cmd.Velocity + carryVelocity;
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

            LayerMask mask = groundLayers;
            if (_playerLayer >= 0) mask &= ~(1 << _playerLayer);          // 双保险：不探测自己
            if (_downHeld && _oneWayLayer >= 0) mask &= ~(1 << _oneWayLayer); // 下穿时单向平台不算地面

            RaycastHit2D hit = Physics2D.BoxCast(origin, size, 0f, Vector2.down, groundCheckDistance, mask);
            return hit.collider != null;
        }

        /// <summary>弹簧冲量入口（Bumper 调用），转发给 Motor 的显式冲量接口。</summary>
        public void Bounce(float verticalVelocity) => _motor.Bounce(verticalVelocity);

        /// <summary>移动平台速度补偿入口（MovingPlatform 调用）：本帧位移在 FixedUpdate 折算成速度并入角色速度。</summary>
        public void AddPlatformDelta(Vector2 delta) => _pendingPlatformDelta += delta;

        /// <summary>死亡入口（Hazard 调用）：冻结帧 → 传送重生点。重复调用被忽略。</summary>
        public void Die()
        {
            if (_dead) return;
            _dead = true;
            StartCoroutine(DeathSequence());
        }

        /// <summary>查找脚下的单向平台 collider（下穿忽略用）。</summary>
        private Collider2D FindOneWayPlatformBelow()
        {
            Bounds b = _col.bounds;
            const float boxHeight = 0.05f;
            const float gap = 0.02f;
            Vector2 size = new Vector2(b.size.x - groundCheckInset * 2f, boxHeight);
            Vector2 origin = new Vector2(b.center.x, b.min.y - boxHeight * 0.5f - gap);
            RaycastHit2D hit = Physics2D.BoxCast(origin, size, 0f, Vector2.down, groundCheckDistance, 1 << _oneWayLayer);
            return hit.collider;
        }

        private IEnumerator DeathSequence()
        {
            yield return new WaitForSecondsRealtime(deathFreezeSeconds);
            _rb.MovePosition(new Vector2(RespawnPosition.x, RespawnPosition.y + respawnLift));
            _rb.velocity = Vector2.zero;
            _motor.Reset();
            _input.ClearPending(); // 丢弃冻结期间锁存的跳跃事件，防止重生瞬间幽灵起跳
            _dead = false;
        }
    }
}
