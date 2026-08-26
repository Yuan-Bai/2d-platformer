using UnityEngine;

namespace Platformer.Mechanics
{
    /// <summary>
    /// 单向平台（ADR-0005）：PlatformEffector2D 只挡上方来客 + OneWayPlatform 层。
    /// 玩家按住"下"时由 PlayerBody 切 IgnoreLayerCollision 实现主动下穿。
    /// 编辑器 AddComponent（Reset）与运行时 AddComponent（Awake）均自动配置。
    /// </summary>
    [RequireComponent(typeof(Collider2D))]
    public sealed class OneWayPlatform : MonoBehaviour
    {
        private void Reset() => Configure();

        private void Awake() => Configure();

        private void Configure()
        {
            int layer = LayerMask.NameToLayer("OneWayPlatform");
            if (layer >= 0) gameObject.layer = layer;

            var col = GetComponent<Collider2D>();
            col.usedByEffector = true; // PlatformEffector2D 生效的必要条件

            var effector = GetComponent<PlatformEffector2D>();
            if (effector == null) effector = gameObject.AddComponent<PlatformEffector2D>();
            effector.useOneWay = true;
            effector.useSideBounce = false;
            effector.useSideFriction = false;
        }
    }
}
