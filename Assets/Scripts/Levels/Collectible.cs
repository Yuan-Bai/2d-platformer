using UnityEngine;
using Platformer.Player;

namespace Platformer.Levels
{
    /// <summary>
    /// 樱桃收集物（M3）：Trigger 接触玩家 → GameFlowController.RegisterCherry + 自毁（ADR-0009 接缝）。
    /// 无通关门槛（锚定决策 Q3）；视觉（cherry 帧）与触发器由生成器装配。
    /// </summary>
    [RequireComponent(typeof(Collider2D))]
    public sealed class Collectible : MonoBehaviour
    {
        private void OnTriggerEnter2D(Collider2D other)
        {
            if (other.TryGetComponent<PlayerBody>(out _) == false) return;
            GameFlowController.Instance?.RegisterCherry();
            Destroy(gameObject);
        }
    }
}
