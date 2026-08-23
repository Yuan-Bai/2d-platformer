namespace Platformer.Motor
{
    /// <summary>一帧（固定时间步）的规范化玩家输入。由输入适配层填充，Motor 只认这个结构。</summary>
    public struct PlayerMoveInput
    {
        /// <summary>水平移动轴：-1（左）.. 0 .. +1（右）。</summary>
        public float MoveAxis;

        /// <summary>本帧是否有一个"跳跃按下"事件（与缓冲配套，可来自数帧前的按下）。</summary>
        public bool JumpQueued;

        /// <summary>跳跃键当前是否按住（用于可变跳高 / 跳切）。</summary>
        public bool JumpHeld;
    }
}
