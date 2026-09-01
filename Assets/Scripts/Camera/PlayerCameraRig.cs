using Cinemachine;
using UnityEngine;

namespace Platformer.Cameras
{
    /// <summary>
    /// Cinemachine 相机兜底封装（ADR-0004）：生成管线（LevelKit.CreateCameraRig）已在
    /// 生成期装配 Follow + FramingTransposer（Body）与全部参数，编辑模式 Inspector 可直接调整。
    /// 本组件只负责手工搭关场景的兜底：Follow 未设时从 followTarget 取、Body 缺失时补默认 Transposer。
    /// 相机手感参数调整入口 = VCam 的 Body 面板（FramingTransposer 序列化参数）。
    /// </summary>
    public sealed class PlayerCameraRig : MonoBehaviour
    {
        [SerializeField] private Transform followTarget;

        private void Awake()
        {
            var vcam = GetComponent<CinemachineVirtualCamera>();
            if (vcam == null)
            {
                Debug.LogWarning("PlayerCameraRig: 需要 CinemachineVirtualCamera", this);
                enabled = false;
                return;
            }

            // 兜底 1：Follow 未设（手工搭关/旧场景）时从 followTarget 补
            if (vcam.Follow == null && followTarget != null)
                vcam.Follow = followTarget;

            // 兜底 2：Body 未配（手工搭关/旧场景）时补默认 FramingTransposer
            if (vcam.GetCinemachineComponent<CinemachineFramingTransposer>() == null)
            {
                var framing = vcam.AddCinemachineComponent<CinemachineFramingTransposer>();
                framing.m_DeadZoneWidth = 0.3f;
                framing.m_DeadZoneHeight = 0.5f;
                framing.m_SoftZoneWidth = 0.6f;
                framing.m_SoftZoneHeight = 0.6f;
                framing.m_XDamping = 0.35f;
                framing.m_YDamping = 0.35f;
                framing.m_LookaheadTime = 0.35f;
                framing.m_LookaheadSmoothing = 10f;
                framing.m_LookaheadIgnoreY = true;
            }
        }

        /// <summary>
        /// 切关重绑（ADR-0009，GameFlowController 调用）：
        /// Follow 指向新关玩家 + Confiner 边界换新关 CameraBounds。
        /// 换边界后必须 InvalidateCache，否则 Confiner 继续用旧关卡几何缓存（相机飞出边界）。
        /// bounds 允许 null（如菜单态无边界时解绑）。
        /// </summary>
        public void Bind(Transform follow, PolygonCollider2D bounds)
        {
            followTarget = follow;
            var vcam = GetComponent<CinemachineVirtualCamera>();
            if (vcam == null) return;
            vcam.Follow = follow;
            var conf = vcam.GetComponent<CinemachineConfiner2D>();
            if (conf == null) return;
            conf.m_BoundingShape2D = bounds;
            conf.InvalidateCache();
        }
    }
}
