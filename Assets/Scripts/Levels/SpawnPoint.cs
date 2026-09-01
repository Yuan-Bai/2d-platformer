using UnityEngine;

namespace Platformer.Levels
{
    /// <summary>
    /// 关卡出生点标记（ADR-0009）：切关时 GameFlowController 玩家重置锚点。
    /// 玩家常驻于 00-Bootstrap 后不再存在于关卡场景，出生位置由本标记承载
    /// （LevelBuilder 在 JSON 'P' 单元格处创建）。
    /// </summary>
    public sealed class SpawnPoint : MonoBehaviour { }
}
