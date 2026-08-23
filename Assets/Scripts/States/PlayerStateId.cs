namespace Platformer.States
{
    /// <summary>Locomotion 状态组的全部状态。后续扩展（WallSlide / Dash）在此追加。</summary>
    public enum PlayerStateId
    {
        Idle,
        Run,
        Jump,
        Fall,
    }
}
