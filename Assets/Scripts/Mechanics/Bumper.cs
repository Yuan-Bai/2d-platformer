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

        /// <summary>弹起已触发（供视觉层订阅：BumperVisual 的压缩动画）。纯表现事件，不改手感。</summary>
        public event System.Action Bounced;

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (other.TryGetComponent<PlayerBody>(out var player))
            {
                player.Bounce(bounceVelocity);
                AudioManager.Instance?.PlayBumper();
                Bounced?.Invoke();
            }
        }
    }
}
