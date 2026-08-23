using UnityEngine;
using UnityEngine.InputSystem;
using Platformer.Motor;

namespace Platformer.Player
{
    /// <summary>
    /// 输入适配层：把新 Input System 的键盘状态规范化为 Motor 能消费的 <see cref="PlayerMoveInput"/>。
    /// 事件桥接（ADR-0004）：按下事件在 Update 锁存，FixedUpdate 消费——跳跃缓冲天然承担两者间的时序桥。
    /// M1 只支持键盘；手柄与 .inputactions 资产在需要时（如联机）再加。
    /// </summary>
    public sealed class InputReader : MonoBehaviour
    {
        private Keyboard _kb;
        private bool _pendingJump; // Update 锁存的"跳跃按下"，FixedUpdate 取走

        public float MoveAxis { get; private set; }
        public bool JumpHeld { get; private set; }

        private void OnEnable() => _kb = Keyboard.current;

        private void Update()
        {
            if (_kb == null) return;

            MoveAxis = (_kb.dKey.isPressed || _kb.rightArrowKey.isPressed ? 1f : 0f)
                     - (_kb.aKey.isPressed || _kb.leftArrowKey.isPressed ? 1f : 0f);
            JumpHeld = _kb.spaceKey.isPressed;
            if (_kb.spaceKey.wasPressedThisFrame) _pendingJump = true;
        }

        /// <summary>构建本固定步的输入快照；取走锁存的跳跃事件。</summary>
        public PlayerMoveInput BuildInput()
        {
            bool queued = _pendingJump;
            _pendingJump = false;
            return new PlayerMoveInput
            {
                MoveAxis = MoveAxis,
                JumpQueued = queued,
                JumpHeld = JumpHeld,
            };
        }
    }
}
