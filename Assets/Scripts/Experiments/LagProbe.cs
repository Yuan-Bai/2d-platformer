using Cinemachine;
using UnityEngine;
using Platformer.Player;

namespace Platformer.Experiments
{
    /// <summary>
    /// 稳态滞后公式验证探针：匀速跑动时，实测"角色越过死区边缘的世界距离"，
    /// 并与理论值（连续版 & 离散版）并排显示。挂到 Main Camera 上，Play 后按住 D 跑。
    /// </summary>
    public sealed class LagProbe : MonoBehaviour
    {
        [SerializeField] private Transform player;
        [SerializeField] private Camera cam;
        [SerializeField] private CinemachineFramingTransposer framing;

        private Rigidbody2D _playerRb;
        private GUIStyle _style;

        private void Start()
        {
            if (cam == null) cam = Camera.main;
            if (player == null) player = FindObjectOfType<PlayerBody>().transform;
            _playerRb = player.GetComponent<Rigidbody2D>();
            if (framing == null)
                framing = FindObjectOfType<CinemachineVirtualCamera>()
                    .GetCinemachineComponent<CinemachineFramingTransposer>();
        }

        private void OnGUI()
        {
            if (player == null || cam == null || framing == null) return;
            if (_style == null) _style = new GUIStyle(GUI.skin.label) { fontSize = 16 };

            // 1) 屏幕可见总宽（世界单位）——和你读过的 ScreenToOrtho 同一套换算
            float depth = Mathf.Abs(cam.transform.position.z - player.position.z);
            float halfHeight = Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad) * depth;
            float screenWidth = halfHeight * 2f * cam.aspect;

            // 2) 实测：角色越过死区右边缘的世界距离（在死区内时为负值）
            float deadHalfWorld = framing.m_DeadZoneWidth * 0.5f * screenWidth;
            float measuredGap = Mathf.Abs(player.position.x - cam.transform.position.x) - deadHalfWorld;

            // 3) 理论：连续版 L=v/k 与离散版 L=v·dt/(1-e^(-k·dt))
            float speed = Mathf.Abs(_playerRb.velocity.x);
            float k = 4.605170186f / Mathf.Max(framing.m_XDamping, 0.0001f);
            float predictedContinuous = speed / k;
            float predictedDiscrete =
                speed * Time.fixedDeltaTime / (1f - Mathf.Exp(-k * Time.fixedDeltaTime));

            float viewportX = cam.WorldToViewportPoint(player.position).x;

            GUI.Label(new Rect(10, 10, 900, 24),
                $"视口x={viewportX:F3}  |  实测出界量={measuredGap:F3} 世界单位  |  速度={_playerRb.velocity.x:F2}",
                _style);
            GUI.Label(new Rect(10, 40, 900, 24),
                $"理论(连续)={predictedContinuous:F3}  |  理论(离散)={predictedDiscrete:F3}  |  k={k:F2}",
                _style);
        }
    }
}