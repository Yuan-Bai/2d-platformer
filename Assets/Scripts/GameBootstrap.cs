using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace Platformer
{
    /// <summary>
    /// 应用级启动配置：固定帧率上限 + 确保 uGUI 交互基础设施存在。
    /// EventSystem 补全必须在运行时程序集：Platformer.Editor 未引用 Unity.InputSystem 包，
    /// 而本程序集（Platformer.Game）引用了它——InputSystemUIInputModule（新输入系统模块）在此可用。
    /// </summary>
    public sealed class GameBootstrap : MonoBehaviour
    {
        [SerializeField] private int targetFrameRate = 60;

        private void Awake()
        {
            Application.targetFrameRate = targetFrameRate;
            EnsureUiInfrastructure();
        }

        /// <summary>
        /// 确保 uGUI 交互基础设施存在（按钮/滑条点击依赖）：
        /// 1) EventSystem + InputSystemUIInputModule（新输入系统指针事件）
        /// 2) 每个 Canvas 都有 GraphicRaycaster（射线检测；缺失则 EventSystem 找不到任何 UI 元素）
        /// </summary>
        private static void EnsureUiInfrastructure()
        {
            if (FindObjectOfType<EventSystem>() == null)
            {
                var go = new GameObject("EventSystem");
                go.AddComponent<EventSystem>();
                go.AddComponent<InputSystemUIInputModule>();
            }

            foreach (var canvas in FindObjectsOfType<Canvas>())
                if (canvas.GetComponent<GraphicRaycaster>() == null)
                    canvas.gameObject.AddComponent<GraphicRaycaster>();
        }
    }
}