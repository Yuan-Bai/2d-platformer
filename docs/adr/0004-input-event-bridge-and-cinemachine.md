# ADR-0004：输入事件桥接 + 相机交给 Cinemachine 封装

M1 两个决定：(1) 输入事件在 Update 锁存、FixedUpdate 消费（事件桥接）——键盘按下事件的时序由跳跃缓冲天然承接，物理帧内只消费快照；(2) 相机不手写任何跟随逻辑，用 Cinemachine Follow + 死区/软区 + 阻尼，由 `PlayerCameraRig` 在 Awake 装配。

**Consequences**

- 输入锁存使「手柄/联机/.inputactions」的扩展点收窄在 `InputReader` 一处。
- M3 关卡相机边界（Confiner2D）沿用同一个封装组件装配，不需要新建相机体系。

> 补录于 M3 阶段 0：本决策在 M1 已生效于代码（`InputReader.cs`、`PlayerCameraRig.cs` 注释引用 ADR-0004），此前未落盘。
