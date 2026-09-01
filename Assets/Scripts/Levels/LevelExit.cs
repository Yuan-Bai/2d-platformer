using UnityEngine;
using Platformer.Player;

namespace Platformer.Levels
{
    /// <summary>
    /// 终点门（M3）：Trigger 接触玩家 → GameFlowController.CompleteLevel（ADR-0009 接缝）。
    /// 视觉（door.png）与触发器由生成器装配；本组件只管触发语义，防重由 GameFlowController 的 _completing 兜底。
    /// </summary>
    [RequireComponent(typeof(Collider2D))]
    public sealed class LevelExit : MonoBehaviour
    {
        private void OnTriggerEnter2D(Collider2D other)
        {
            if (other.TryGetComponent<PlayerBody>(out _))
                GameFlowController.Instance?.CompleteLevel();
        }
    }
}
