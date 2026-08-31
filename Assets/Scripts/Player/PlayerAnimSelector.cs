using UnityEngine;
using Platformer.States;

namespace Platformer.Player
{
    /// <summary>
    /// 玩家动画帧选择（纯 C# 深模块）：状态 + 时间 → 帧索引。
    /// - Jump：恒返回 0（上升帧）——上升全程第一帧，下降由 Fall 帧接管；
    ///   按运动阶段选帧而非按时间播放（跳跃高度可变，动画时长无需匹配）。
    /// - Fall：恒返回 1（下降帧 = jump 末帧）。
    /// - Idle/Run：按 fps 时间循环（loopFrameCount 由调用方给出）。
    /// 状态切换自动重置相位。
    /// </summary>
    public sealed class PlayerAnimSelector
    {
        private readonly float _fps;
        private PlayerStateId _state;
        private float _time;

        public PlayerAnimSelector(float fps)
        {
            _fps = fps;
        }

        /// <summary>推进一帧。返回调用方帧数组中的索引（调用方负责越界 clamp）。</summary>
        public int Tick(PlayerStateId state, float dt, int loopFrameCount)
        {
            if (state != _state)
            {
                _state = state;
                _time = 0f;
            }
            _time += dt;

            switch (state)
            {
                case PlayerStateId.Jump: return 0; // 上升帧
                case PlayerStateId.Fall: return 1; // 下降帧（fall 回退 jump 末帧，单元素数组由调用方 clamp）
                default: return (int)(_time * _fps) % Mathf.Max(loopFrameCount, 1);
            }
        }
    }
}
