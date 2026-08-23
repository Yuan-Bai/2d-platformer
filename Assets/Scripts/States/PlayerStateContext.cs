using Platformer.Motor;

namespace Platformer.States
{
    /// <summary>
    /// 状态机每帧可见的事实集合。由 Unity 适配层（PlayerBody）在 FixedUpdate 里填充，
    /// 状态据此做转换决策。纯 C#，可 EditMode 测试。
    /// </summary>
    public sealed class PlayerStateContext
    {
        public PlayerMotor Motor;
        public PlayerMoveInput Input;
        public bool Grounded;
    }
}
