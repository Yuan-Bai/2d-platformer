namespace Platformer.States
{
    /// <summary>
    /// 单个行为状态的接口。状态只做两件事：声明转换，以及在进出时发信号
    /// （M2 起用于动画参数 / 音效）。运动计算一律交给 Motor —— 状态不写物理。
    /// </summary>
    public interface IPlayerState
    {
        void Enter(PlayerStateContext ctx);

        /// <summary>返回下一状态；返回自身 id 表示保持不变。</summary>
        PlayerStateId Tick(PlayerStateContext ctx, float dt);

        void Exit(PlayerStateContext ctx);
    }
}
