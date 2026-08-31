using UnityEngine;
using Platformer.Player;

namespace Platformer.Levels
{
    /// <summary>
    /// 终点门（M3）：Trigger 接触玩家 → LevelManager.CompleteLevel。
    /// 视觉（door.png）与触发器由生成器装配；本组件只管触发语义，一次触发后由 LevelManager 的 _completing 兜底防重。
    /// </summary>
    [RequireComponent(typeof(Collider2D))]
    public sealed class LevelExit : MonoBehaviour
    {
        [SerializeField] private LevelManager levelManager;

        private void Awake()
        {
            if (levelManager == null) levelManager = FindObjectOfType<LevelManager>();
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (levelManager != null && other.TryGetComponent<PlayerBody>(out _))
                levelManager.CompleteLevel();
        }
    }
}
