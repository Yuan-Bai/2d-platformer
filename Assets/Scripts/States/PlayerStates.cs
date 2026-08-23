using UnityEngine;

namespace Platformer.States
{
    /// <summary>转换判定共用的两个事实：是否落地、竖直速度方向。</summary>
    internal static class TransitionFacts
    {
        public static bool HasMoveInput(PlayerStateContext ctx) => Mathf.Abs(ctx.Input.MoveAxis) > 0.01f;

        public static PlayerStateId GroundedState(PlayerStateContext ctx) =>
            HasMoveInput(ctx) ? PlayerStateId.Run : PlayerStateId.Idle;

        public static PlayerStateId AirState(PlayerStateContext ctx) =>
            ctx.Motor.VerticalSpeed > 0f ? PlayerStateId.Jump : PlayerStateId.Fall;
    }

    public sealed class IdleState : IPlayerState
    {
        public void Enter(PlayerStateContext ctx) { }

        public PlayerStateId Tick(PlayerStateContext ctx, float dt)
        {
            if (!ctx.Grounded) return TransitionFacts.AirState(ctx);
            if (TransitionFacts.HasMoveInput(ctx)) return PlayerStateId.Run;
            return PlayerStateId.Idle;
        }

        public void Exit(PlayerStateContext ctx) { }
    }

    public sealed class RunState : IPlayerState
    {
        public void Enter(PlayerStateContext ctx) { }

        public PlayerStateId Tick(PlayerStateContext ctx, float dt)
        {
            if (!ctx.Grounded) return TransitionFacts.AirState(ctx);
            if (!TransitionFacts.HasMoveInput(ctx)) return PlayerStateId.Idle;
            return PlayerStateId.Run;
        }

        public void Exit(PlayerStateContext ctx) { }
    }

    public sealed class JumpState : IPlayerState
    {
        public void Enter(PlayerStateContext ctx) { }

        public PlayerStateId Tick(PlayerStateContext ctx, float dt)
        {
            if (ctx.Grounded) return TransitionFacts.GroundedState(ctx);
            if (ctx.Motor.VerticalSpeed <= 0f) return PlayerStateId.Fall;
            return PlayerStateId.Jump;
        }

        public void Exit(PlayerStateContext ctx) { }
    }

    public sealed class FallState : IPlayerState
    {
        public void Enter(PlayerStateContext ctx) { }

        public PlayerStateId Tick(PlayerStateContext ctx, float dt)
        {
            if (ctx.Grounded) return TransitionFacts.GroundedState(ctx);
            if (ctx.Motor.VerticalSpeed > 0f) return PlayerStateId.Jump; // 土狼窗口内起跳
            return PlayerStateId.Fall;
        }

        public void Exit(PlayerStateContext ctx) { }
    }
}
