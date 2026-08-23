namespace Platformer.Motor
{
    /// <summary>
    /// 土狼时间（Coyote Time）：离开平台边缘后仍可起跳的宽限窗口。
    /// 纯 C#，无 UnityEngine 依赖，可 EditMode 单测。
    /// 实现采用「到期时刻」模型（now &lt; expiresAt），避免逐帧减法的浮点残渣。
    /// </summary>
    public sealed class CoyoteTimer
    {
        private readonly float _window;
        private float _now;
        private float _expiresAt;

        public CoyoteTimer(float window) => _window = window;

        /// <summary>落地（或站在地面上）时刷新窗口。</summary>
        public void Refresh() => _expiresAt = _now + _window;

        /// <summary>窗口是否仍然有效。</summary>
        public bool Active => _now < _expiresAt;

        public void Tick(float dt) => _now += dt;
    }

    /// <summary>
    /// 跳跃缓冲（Jump Buffer）：按下跳跃键的事件被记住一个短窗口，
    /// 落地后自动起跳。纯 C#，可 EditMode 单测。到期时刻模型，无浮点残渣。
    /// </summary>
    public sealed class JumpBuffer
    {
        private readonly float _window;
        private float _now;
        private float _expiresAt;

        public JumpBuffer(float window) => _window = window;

        /// <summary>记录一次"跳跃按下"事件。</summary>
        public void Queue() => _expiresAt = _now + _window;

        /// <summary>是否有一个待消费的跳跃事件。</summary>
        public bool HasQueued => _now < _expiresAt;

        /// <summary>消费跳跃事件（起跳时调用一次），立即到期。</summary>
        public void Consume() => _expiresAt = 0f;

        public void Tick(float dt) => _now += dt;
    }
}
