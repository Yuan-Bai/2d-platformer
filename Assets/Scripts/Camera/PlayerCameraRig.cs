using Cinemachine;
using UnityEngine;

namespace Platformer.Cameras
{
    /// <summary>
    /// Cinemachine 相机封装（ADR-0004）：Follow + 死区/软区 + 阻尼，零手写相机逻辑。
    /// 挂在场景中的 VCam 上；由 TestRoomBuilder 自动装配，或手动挂载时拖入 followTarget。
    /// </summary>
    public sealed class PlayerCameraRig : MonoBehaviour
    {
        [SerializeField] private Transform followTarget;
        [SerializeField] private Vector2 deadZone = new Vector2(0.3f, 0.5f);

        private void Awake()
        {
            var vcam = GetComponent<CinemachineVirtualCamera>();
            if (vcam == null || followTarget == null)
            {
                Debug.LogWarning("PlayerCameraRig: 需要 CinemachineVirtualCamera 与 followTarget", this);
                enabled = false;
                return;
            }

            vcam.Follow = followTarget;

            // 死区：角色在屏幕死区内移动不追，出了死区才平滑跟上（平台跳跃手感关键）
            var framing = vcam.AddCinemachineComponent<CinemachineFramingTransposer>();
            framing.m_DeadZoneWidth = deadZone.x;
            framing.m_DeadZoneHeight = deadZone.y;
            framing.m_SoftZoneWidth = 0.6f;
            framing.m_SoftZoneHeight = 0.6f;
            framing.m_XDamping = 0.35f;
            framing.m_YDamping = 0.35f;
        }
    }
}
