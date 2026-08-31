using UnityEngine;
using Platformer.States;

namespace Platformer.Player
{
    /// <summary>
    /// 玩家表现层（ADR-0008）：只读状态机的 <see cref="PlayerStateId"/> 与输入轴，驱动精灵帧循环与翻转。
    /// 不回写任何手感数据；帧循环在代码内完成（不依赖 Animator 资产），状态→动画映射即本组件的序列化字段。
    /// fall 无独立素材：默认复用 jump 第 2 帧（Awake 自动回退）。
    /// </summary>
    [RequireComponent(typeof(SpriteRenderer))]
    public sealed class PlayerVisuals : MonoBehaviour
    {
        [SerializeField] private PlayerBody body;
        [SerializeField] private InputReader input;
        [SerializeField] private float fps = 10f;
        [SerializeField] private Sprite[] idleFrames;
        [SerializeField] private Sprite[] runFrames;
        [SerializeField] private Sprite[] jumpFrames;
        [SerializeField] private Sprite[] fallFrames;

        private SpriteRenderer _sr;
        private PlayerAnimSelector _selector;
        private float _facing = 1f;

        /// <summary>生成器装配入口：注入 Foxy 帧序列。fall 不注入（Awake 自动回退 jump 末帧，ADR-0008）。</summary>
        public void Configure(Sprite[] idle, Sprite[] run, Sprite[] jump)
        {
            idleFrames = idle;
            runFrames = run;
            jumpFrames = jump;
            fallFrames = null;
        }

        private void Awake()
        {
            _sr = GetComponent<SpriteRenderer>();
            if (body == null) body = GetComponent<PlayerBody>();
            if (input == null) input = GetComponent<InputReader>();
            _selector = new PlayerAnimSelector(fps);

            // fall 无独立素材（ADR-0008）：空时回退到 jump 末帧
            if (fallFrames == null || fallFrames.Length == 0)
                fallFrames = jumpFrames != null && jumpFrames.Length > 1 ? new[] { jumpFrames[1] } : jumpFrames;
        }

        private void Update()
        {
            if (body == null || _sr == null) return;

            // 朝向：以输入轴为准（不靠速度符号——空中急停不会甩脸）；无输入保持最后朝向
            if (input != null)
            {
                float axis = input.MoveAxis;
                if (axis > 0.01f) _facing = 1f;
                else if (axis < -0.01f) _facing = -1f;
            }
            _sr.flipX = _facing < 0f;

            // 帧选择：Jump=上升帧、Fall=下降帧（按运动阶段），Idle/Run 时间循环。
            // 帧索引选择逻辑在纯 C# 的 PlayerAnimSelector 中（可 EditMode 单测）。
            Sprite[] frames = SelectFrames(body.CurrentState);
            if (frames == null || frames.Length == 0) return;

            int index = _selector.Tick(body.CurrentState, Time.deltaTime, frames.Length);
            _sr.sprite = frames[Mathf.Min(index, frames.Length - 1)];
        }

        private Sprite[] SelectFrames(PlayerStateId state)
        {
            switch (state)
            {
                case PlayerStateId.Idle: return idleFrames;
                case PlayerStateId.Run: return runFrames;
                case PlayerStateId.Jump: return jumpFrames;
                case PlayerStateId.Fall: return fallFrames;
                default: return null;
            }
        }
    }
}
