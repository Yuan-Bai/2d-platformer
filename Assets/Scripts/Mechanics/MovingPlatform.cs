using UnityEngine;
using Platformer.Player;

namespace Platformer.Mechanics
{
    /// <summary>
    /// 移动平台（ADR-0005，速度补偿方案）：
    /// FixedUpdate 沿本地路径点移动并记录本帧位移 delta；站在平台上的角色经
    /// PlayerBody.AddPlatformDelta 累加，由其 FixedUpdate 折算成速度与角色速度合并写入。
    /// 平台自身用 rb.MovePosition 驱动（kinematic 体规范做法）：直写 transform.position
    /// 依赖"仅在 FixedUpdate 写入 + 模拟步同步"的隐式约定，且不参与插值（高刷屏抖动）。
    /// 曾用 MovePosition 位置传送携带：实测会吞掉角色自身速度的运动
    /// （站在平台上无法移动/跳跃），且继承速度方案与 velocity 直写语义纠缠，故否决。
    /// </summary>
    [RequireComponent(typeof(Collider2D))]
    [RequireComponent(typeof(Rigidbody2D))]
    public sealed class MovingPlatform : MonoBehaviour
    {
        [SerializeField] public Vector2[] waypoints; // 相对初始位置的本地路径点（可留空 = 静止）
        [SerializeField] public float speed = 2f;

        private Rigidbody2D _rb;
        private Vector3 _startPos;
        private Vector3 _curPos;   // 显式跟踪当前位置：MovePosition 在下一模拟步才生效，不能回读 transform
        private Vector3 _prevPos;
        private Vector2 _delta;
        private int _target;

        private void Awake()
        {
            // 移动平台必须是 kinematic 刚体：static collider 被移动时，物理接触求解
            // 会把接触的玩家"夹"住（实测：顶到平台底部后卡住随行、永不下落）。
            _rb = GetComponent<Rigidbody2D>();
            _rb.bodyType = RigidbodyType2D.Kinematic;
            // MovePosition + Interpolate：kinematic 体官方推荐的平滑驱动组合，消除 50Hz 物理步在高刷屏的抖动
            _rb.interpolation = RigidbodyInterpolation2D.Interpolate;

            // 平台摩擦归零（ADR-0002 纪律）：默认摩擦 0.4 + velocityThreshold(1m/s)
            // 会钉住玩家相对平台的切向速度——"站在移动平台上走不动"的根因。
            GetComponent<Collider2D>().sharedMaterial = new PhysicsMaterial2D { friction = 0f };

            _startPos = transform.position;
            _curPos = _startPos;
            _prevPos = _startPos;
        }

        private void FixedUpdate()
        {
            _prevPos = _curPos;
            if (waypoints != null && waypoints.Length > 0)
            {
                Vector3 target = _startPos + (Vector3)waypoints[_target];
                _curPos = Vector3.MoveTowards(_curPos, target, speed * Time.fixedDeltaTime);
                if (Vector3.SqrMagnitude(_curPos - target) < 0.0001f)
                    _target = (_target + 1) % waypoints.Length;
            }
            _delta = _curPos - _prevPos;
            if (_delta != Vector2.zero)
                _rb.MovePosition(_curPos);
        }

        private void OnCollisionStay2D(Collision2D collision)
        {
            if (_delta == Vector2.zero) return;
            var player = collision.collider.GetComponent<PlayerBody>();
            if (player == null) return;

            // 仅当玩家中心在平台中心上方才携带（站在平台上）。
            // 用位置关系而非接触法线：法线方向在不同接触面语义易错，
            // 且角色顶到平台底部时会被误携带 → "卡住随行、永不下落"。
            if (player.transform.position.y > transform.position.y)
            {
                player.AddPlatformDelta(_delta);
            }
        }
    }
}
