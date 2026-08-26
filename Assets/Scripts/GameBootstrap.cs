using UnityEngine;

namespace Platformer
{
    /// <summary>应用级启动配置：固定帧率上限，统一编辑器与构建的手感基准。</summary>
    public sealed class GameBootstrap : MonoBehaviour
    {
        [SerializeField] private int targetFrameRate = 60;

        private void Awake() => Application.targetFrameRate = targetFrameRate;
    }
}