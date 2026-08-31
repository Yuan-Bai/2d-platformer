using UnityEngine;
using Platformer.Player;
using Platformer.UI;

namespace Platformer.Levels
{
    /// <summary>
    /// 教学触发区（M3）：玩家进入即向 HintBar 显示提示文案（淡入 → 停留 → 淡出）。
    /// 每关首见机制各配一个触发区；路牌视觉（sign.png）与触发器由生成器装配。
    /// </summary>
    [RequireComponent(typeof(Collider2D))]
    public sealed class TutorialTrigger : MonoBehaviour
    {
        [SerializeField] private string message = "";

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (string.IsNullOrEmpty(message)) return;
            if (other.TryGetComponent<PlayerBody>(out _))
                HintBar.Instance?.Show(message);
        }
    }
}
