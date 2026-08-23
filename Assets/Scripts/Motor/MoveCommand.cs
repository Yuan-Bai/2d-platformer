using UnityEngine;

namespace Platformer.Motor
{
    /// <summary>Motor 一帧计算的输出：期望速度。由 Unity 薄适配层写入 Rigidbody2D。</summary>
    public struct MoveCommand
    {
        public Vector2 Velocity;
    }
}
