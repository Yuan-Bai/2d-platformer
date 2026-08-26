using UnityEngine;
using Platformer.Player;

namespace Platformer.Mechanics
{
    /// <summary>
    /// 弹簧（ADR-0005）：Trigger 接触 → PlayerMotor.Bounce 显式冲量。
    /// 冲量不经过跳跃判定：不消耗跳跃缓冲、不受跳切管辖、不依赖地面。
    /// </summary>
    public sealed class Bumper : MonoBehaviour
    {
        [SerializeField] private float bounceVelocity = 14f;

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (other.TryGetComponent<PlayerBody>(out var player))
                player.Bounce(bounceVelocity);
        }
    }
}
