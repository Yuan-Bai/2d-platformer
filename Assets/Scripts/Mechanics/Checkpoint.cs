using UnityEngine;
using Platformer.Player;

namespace Platformer.Mechanics
{
    /// <summary>
    /// 重生点：玩家经过（Trigger）即更新其重生位置。
    /// 默认重生位置 = 玩家出生点（PlayerBody.Awake 自动记录）。
    /// </summary>
    public sealed class Checkpoint : MonoBehaviour
    {
        private void OnTriggerEnter2D(Collider2D other)
        {
            if (other.TryGetComponent<PlayerBody>(out var player))
                player.RespawnPosition = transform.position;
        }
    }
}
