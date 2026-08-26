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
        public bool DownHeld { get; private set; } // 下落穿过单向平台（S / ↓）

        private void OnEnable() => _kb = Keyboard.current;
        
        private void Update()
        {
            // 防御：设备被测试夹具移除后（对象销毁前）不再查询，
            // 避免 "Cached unprocessed value" 状态毒化。
            // 注意 InputSystem.Restore() 后孤儿设备对象的 m_DeviceIndex 不会复位（added 仍为 true），
            // 其缓冲区索引会别名到下一个测试的新设备——因此再校验设备 id 是否仍解析到自身
            //（Restore 后要么查无此 id 返回 null，要么 id 撞上新设备返回别的对象，两者都会拦住）。
            if (_kb == null || !_kb.added || InputSystem.GetDeviceById(_kb.deviceId) != _kb) return;

            MoveAxis = (_kb.dKey.isPressed || _kb.rightArrowKey.isPressed ? 1f : 0f)
                     - (_kb.aKey.isPressed || _kb.leftArrowKey.isPressed ? 1f : 0f);
            JumpHeld = _kb.spaceKey.isPressed;
            DownHeld = _kb.sKey.isPressed || _kb.downArrowKey.isPressed;
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

        /// <summary>清空锁存的跳跃事件（死亡重生时调用，防止重生瞬间幽灵起跳）。</summary>
        public void ClearPending() => _pendingJump = false;
    }
}
