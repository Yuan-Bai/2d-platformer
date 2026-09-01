using UnityEngine;

namespace Platformer.Levels
{
    /// <summary>
    /// 相机边界标记（ADR-0009，切关重绑锚点）：挂在关卡场景的 CameraBounds 对象上，
    /// GameFlowController 加载关卡后经此取 PolygonCollider2D，重绑常驻相机的 Confiner。
    /// CameraBounds 对象本身无组件类型（纯几何），本标记提供类型化接缝。
    /// </summary>
    [RequireComponent(typeof(PolygonCollider2D))]
    public sealed class CameraBoundsAnchor : MonoBehaviour
    {
        public PolygonCollider2D Bounds => GetComponent<PolygonCollider2D>();
    }
}
