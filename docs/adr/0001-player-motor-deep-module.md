# ADR-0001：运动核心做成纯 C# 深模块（PlayerMotor）

M1 决定把全部运动计算收敛进不依赖 MonoBehaviour、不写 Rigidbody2D 的纯 C# 类 `PlayerMotor`：输入 + 地面事实 → 期望速度，内部消化双段重力、土狼时间、跳跃缓冲、跳切、终端速度与空中控制。接口即测试面，19 个 EditMode 测试全部经由 `Tick(input, grounded, dt)` 这一个入口。

**Considered Options**

- 逻辑散在 `PlayerBody` 的 MonoBehaviour 里：手感规则与物理写入纠缠，只能 PlayMode 验证，反馈回路慢（每轮几十秒物理帧），且规则不可复用——否决。
- 纯 C# 深模块 + 薄适配层：手感全部在无 Unity 依赖的类里，EditMode 毫秒级测试；`PlayerBody` 只负责探测地面事实、传入输入、写出速度——采纳。

> 补录于 M3 阶段 0：本决策在 M1（commit `84e764b`）已生效于代码，此前未落盘。编号沿用代码注释中的既有编号体系；ADR-0001 与 ADR-0003 在代码中无引用，按 M1 提交内容与 README 重建。
