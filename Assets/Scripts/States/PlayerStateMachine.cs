using System.Collections.Generic;

namespace Platformer.States
{
    /// <summary>
    /// 分层状态机的 Locomotion 组运行时。纯 C#，不依赖 MonoBehaviour。
    /// 决策逻辑全部内聚在各状态小类中；状态机只负责持有实例与执行转换。
    /// </summary>
    public sealed class PlayerStateMachine
    {
        private readonly IReadOnlyDictionary<PlayerStateId, IPlayerState> _states;
        private IPlayerState _current;

        public PlayerStateId Current { get; private set; }

        public PlayerStateMachine(PlayerStateId start)
        {
            _states = new Dictionary<PlayerStateId, IPlayerState>
            {
                { PlayerStateId.Idle, new IdleState() },
                { PlayerStateId.Run, new RunState() },
                { PlayerStateId.Jump, new JumpState() },
                { PlayerStateId.Fall, new FallState() },
            };
            _current = _states[start];
            Current = start;
        }

        public void Tick(PlayerStateContext ctx, float dt)
        {
            PlayerStateId next = _current.Tick(ctx, dt);
            if (next == Current) return;

            _current.Exit(ctx);
            _current = _states[next];
            Current = next;
            _current.Enter(ctx);
        }
    }
}
