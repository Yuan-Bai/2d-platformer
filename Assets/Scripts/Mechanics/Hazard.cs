using UnityEngine;
using Platformer.Player;

namespace Platformer.Mechanics
{
    /// <summary>
    /// 危险物（ADR-0005，一击死亡）：Trigger 接触即触发玩家死亡流程
    /// （冻结帧 → 传送重生点），不做血量。
    /// </summary>
    public sealed class Hazard : MonoBehaviour
    {
        private void OnTriggerEnter2D(Collider2D other)
        {
            if (other.TryGetComponent<PlayerBody>(out var player))
                player.Die();
        }
    }
}
